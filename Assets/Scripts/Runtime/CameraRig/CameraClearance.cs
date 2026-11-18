using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PotatoParty
{
    /// <summary>
    /// "Kamera w ścianie" – jedna z przyczyn sekundowego ekranu w jednym kolorze.
    /// <para>
    /// Kamera stoi nisko i 13–34 m za punktem kadru. Gdy gracze podbiegają do bliskiej krawędzi areny,
    /// kamera (razem z oddechem, odrzutem i tłumieniem Cinemachine) potrafi wjechać w dekorację za areną:
    /// koronę drzewa, dach stodoły, garnek, trybunę. Płaszczyzna bliska (0.2 m) przecina wtedy bryłę i cały
    /// kadr wypełnia jej wnętrze/ściana – jednolity kolor, dopóki gracze nie odejdą od krawędzi.
    /// </para>
    /// <para>
    /// Rozwiązanie: tuż przed renderem kamery (beginCameraRendering – poza wszystkimi LateUpdate, czyli po
    /// Cinemachine) sprawdzamy, które bryły świata zawierają kulę wokół obiektywu (dokładnie, w przestrzeni
    /// siatki – nie po AABB), i rysujemy je w trybie "tylko cień". Cienie zostają, więc nic nie "mruga" na
    /// ziemi; bryła wraca, gdy kamera odjedzie (histereza 0.35 m). Ziemniaki są pomijane.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraClearance : MonoBehaviour
    {
        public static float Radius = 0.9f;       // m wokół obiektywu (near 0.2 + zapas na kąt widzenia)
        public static float Hysteresis = 0.35f;  // m: bryła wraca dopiero, gdy kamera odsunie się o tyle więcej
        public static float RescanEvery = 2f;    // s: świat dobudowuje obiekty w trakcie gry (rzadko i nisko)

        public Camera Cam;

        Transform root;
        float rescanAt;

        struct Entry
        {
            public Renderer R;
            public Transform T;
            public Bounds Local;
            public ShadowCastingMode Original;
            public bool Hidden;
        }

        readonly List<Entry> entries = new List<Entry>(1024);
        readonly List<MeshRenderer> scratch = new List<MeshRenderer>(1024);
        readonly Dictionary<Renderer, ShadowCastingMode> hiddenBefore = new Dictionary<Renderer, ShadowCastingMode>();

        /// <summary>Korzeń świata (mapa + minigra). Ustawia GameManager przy każdej przebudowie.</summary>
        public Transform Root
        {
            get { return root; }
            set
            {
                if (root == value) return;
                RestoreAll();
                root = value;
                entries.Clear();
                rescanAt = 0f;
            }
        }

        public int HiddenCount { get; private set; }

        void OnEnable() { RenderPipelineManager.beginCameraRendering += OnBeginCamera; }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RestoreAll();
        }

        void Rescan()
        {
            rescanAt = Time.unscaledTime + RescanEvery;
            // zachowaj stan ukrytych (bez mrugnięcia przy przebudowie listy)
            hiddenBefore.Clear();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Hidden && entries[i].R != null) hiddenBefore[entries[i].R] = entries[i].Original;
            entries.Clear();
            if (root == null) { foreach (var kv in hiddenBefore) kv.Key.shadowCastingMode = kv.Value; hiddenBefore.Clear(); return; }
            root.GetComponentsInChildren(false, scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                var r = scratch[i];
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if (r.GetComponentInParent<Potato>() != null) continue;
                // małe obiekty (kwiatki, kamyki) nie zasłonią kadru – szkoda na nie czasu
                var b = r.bounds;
                if (b.size.x < 0.6f && b.size.y < 0.6f && b.size.z < 0.6f) continue;
                ShadowCastingMode original;
                bool wasHidden = hiddenBefore.TryGetValue(r, out original);
                entries.Add(new Entry { R = r, T = r.transform, Local = mf.sharedMesh.bounds, Original = wasHidden ? original : r.shadowCastingMode, Hidden = wasHidden });
                if (wasHidden) hiddenBefore.Remove(r);
            }
            scratch.Clear();
            // ukryte, które wypadły z listy (np. zmalały) – przywróć
            foreach (var kv in hiddenBefore) if (kv.Key != null) kv.Key.shadowCastingMode = kv.Value;
            hiddenBefore.Clear();
        }

        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != Cam || Cam == null) return;
            if (root == null) { if (HiddenCount > 0) RestoreAll(); return; }
            if (Time.unscaledTime >= rescanAt) Rescan();

            Vector3 eye = cam.transform.position;
            float rIn = Radius + cam.nearClipPlane, rOut = rIn + Hysteresis;
            var probe = new Bounds(eye, Vector3.one * (rOut * 2f));
            int hidden = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.R == null) continue;
                bool inside = false;
                // szybkie odrzucenie po AABB świata, dopiero potem dokładny test w przestrzeni siatki
                if (e.R.enabled && e.R.gameObject.activeInHierarchy && e.R.bounds.Intersects(probe))
                    inside = Contains(e, eye, e.Hidden ? rOut : rIn);
                if (inside != e.Hidden)
                {
                    e.Hidden = inside;
                    e.R.shadowCastingMode = inside ? ShadowCastingMode.ShadowsOnly : e.Original;
                    entries[i] = e;
                    if (inside) ScreenGlitchProbe.Note("kamera w bryle: " + e.R.name + " (ukryta)");
                }
                if (e.Hidden) hidden++;
            }
            HiddenCount = hidden;
        }

        /// <summary>Czy kula (świat) przecina lokalny prostopadłościan siatki (OBB – skala i obrót z transformu).</summary>
        static bool Contains(Entry e, Vector3 eye, float radius)
        {
            var local = e.T.InverseTransformPoint(eye);
            var s = e.T.lossyScale;
            var ext = e.Local.extents;
            var c = e.Local.center;
            // promień kuli w jednostkach lokalnych każdej osi
            float rx = radius / Mathf.Max(1e-4f, Mathf.Abs(s.x));
            float ry = radius / Mathf.Max(1e-4f, Mathf.Abs(s.y));
            float rz = radius / Mathf.Max(1e-4f, Mathf.Abs(s.z));
            return Mathf.Abs(local.x - c.x) < ext.x + rx
                && Mathf.Abs(local.y - c.y) < ext.y + ry
                && Mathf.Abs(local.z - c.z) < ext.z + rz;
        }

        void RestoreAll()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!e.Hidden) continue;
                if (e.R != null) e.R.shadowCastingMode = e.Original;
                e.Hidden = false;
                entries[i] = e;
            }
            HiddenCount = 0;
        }
    }
}
