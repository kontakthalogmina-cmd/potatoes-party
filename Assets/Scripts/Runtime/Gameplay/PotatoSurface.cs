using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Stan powierzchni ziemniaka (błoto, szron, zwęglenie, siniaki, wgniecenie, błysk trafienia) przekazywany
    /// do shadera PotatoParty/PotatoHero. Każdy ziemniak dostaje WŁASNE kopie swoich materiałów (ok. 5 sztuk)
    /// zamiast MaterialPropertyBlock: renderer z MPB wypada z SRP Batchera, a osobne materiały na tym samym
    /// wariancie shadera są batchowane (SRP Batcher grupuje po wariancie, nie po materiale).
    /// Maski są spójne na całej postaci (liczone w przestrzeni korzenia <see cref="Root"/>).
    /// </summary>
    public class PotatoSurface : MonoBehaviour
    {
        static readonly int MudAmountId = Shader.PropertyToID("_MudAmount");
        static readonly int MudDirId = Shader.PropertyToID("_MudDir");
        static readonly int MudColorId = Shader.PropertyToID("_MudColor");
        static readonly int FrostAmountId = Shader.PropertyToID("_FrostAmount");
        static readonly int BurnAmountId = Shader.PropertyToID("_BurnAmount");
        static readonly int BurnHeatId = Shader.PropertyToID("_BurnHeat");
        static readonly int BurnDirId = Shader.PropertyToID("_BurnDir");
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int Root0Id = Shader.PropertyToID("_PP_Root0");
        static readonly int Root1Id = Shader.PropertyToID("_PP_Root1");
        static readonly int Root2Id = Shader.PropertyToID("_PP_Root2");
        static readonly int UseRootId = Shader.PropertyToID("_PP_UseRoot");
        static readonly int ImpactCenterId = Shader.PropertyToID("_ImpactCenter");
        static readonly int ImpactDirId = Shader.PropertyToID("_ImpactDir");
        static readonly int BruiseAmountId = Shader.PropertyToID("_BruiseAmount");
        static readonly int BruiseDirId = Shader.PropertyToID("_BruiseDir");

        /// <summary>Przestrzeń postaci: zwykle Potato.Visual (obraca się z fikołkami i zgniataniem).</summary>
        public Transform Root;

        [Range(0, 1)] public float Mud;
        [Range(0, 1)] public float Frost;
        [Range(0, 1)] public float Burn;
        [Range(0, 1)] public float Heat;
        [Range(0, 1)] public float Flash;
        /// <summary>Siniaki po ciosach: interaktywna maska zniszczeń, znika powoli (kumuluje się w długiej minigrze).</summary>
        [Range(0, 1)] public float Bruise;

        /// <summary>Poziom szronu, do którego postać dąży sama (np. 0.3 na lodowej mapie).</summary>
        [Range(0, 1)] public float FrostTarget;

        [Header("Zanikanie (na sekundę)")]
        public float MudDryDelay = 4f;
        public float MudFade = 0.05f;
        public float FrostRate = 0.25f;
        public float BurnFade = 0.03f;
        public float HeatFade = 1.2f;
        public float FlashFade = 7f;
        public float BruiseFade = 0.035f;

        [Header("Wgniecenie przy ciosie (sprężyna)")]
        public float DentFrequency = 6.5f;
        public float DentDamping = 0.3f;
        /// <summary>Promień "ciała" w metrach świata (tułów 0.66 × skala postaci 1.3).</summary>
        public float BodyRadius = 0.88f;

        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<Material> materials = new List<Material>();
        Vector4 mudDir = new Vector4(0, -1, 0, 0);
        Vector4 burnDir = new Vector4(0, 0, 1, 0);
        Vector4 bruiseDir = new Vector4(0, 0, 1, 0);
        Vector3 dentDirLocal = Vector3.forward;
        Spring dent;
        float mudAge;

        // ostatnio wysłane wartości masek: SetFloat/SetColor tylko przy zmianie
        float sentMud = -1f, sentFrost = -1f, sentBurn = -1f, sentHeat = -1f, sentFlash = -1f, sentBruise = -1f;
        Color sentMudColor = new Color(-1, -1, -1, -1);
        Vector4 sentMudDir, sentBurnDir, sentBruiseDir;
        bool dentWasActive = true;

        /// <summary>Domyślny kolor błota (taki jak w shaderze).</summary>
        public static readonly Color DefaultMud = new Color(0.27f, 0.18f, 0.11f, 1f);
        /// <summary>Kolor "błota": ziemia, piasek, farba, zupa, mąka – ta sama maska, inny kolor.</summary>
        public Color MudColor = DefaultMud;

        public void Init(Transform root, IEnumerable<Renderer> parts)
        {
            Root = root;
            renderers.Clear();
            renderers.AddRange(parts);
            // własne kopie materiałów (wspólne z pamięci podręcznej Shapes.HeroMat mają też manekiny w garderobie)
            var map = new Dictionary<Material, Material>();
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var src = r.sharedMaterial;
                if (src == null) continue;
                Material inst;
                if (!map.TryGetValue(src, out inst))
                {
                    inst = new Material(src) { name = src.name + " (" + name + ")" };
                    inst.SetFloat(UseRootId, 1f);
                    map[src] = inst;
                    materials.Add(inst);
                }
                r.sharedMaterial = inst;
            }
            Push();
        }

        void OnDestroy()
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null) Destroy(materials[i]);
            materials.Clear();
        }

        // ---------------- API dla minigier ----------------

        /// <summary>Rozbryzg błota z punktu w świecie (np. wybuch fałszywego złota, kałuża).</summary>
        public void SplashMud(Vector3 fromWorld, float amount)
        {
            SplashMud(fromWorld, amount, DefaultMud);
        }

        /// <summary>
        /// Rozbryzg w dowolnym kolorze (farba, zupa, piasek, mąka). Świeża plama przejmuje kolor
        /// proporcjonalnie do swojej siły, więc kolejne trafienia różnymi kolorami mieszają się.
        /// </summary>
        public void SplashMud(Vector3 fromWorld, float amount, Color color)
        {
            float before = Mud;
            Mud = Mathf.Clamp01(Mud + amount);
            float k = before < 0.05f ? 1f : Mathf.Clamp01(amount / Mathf.Max(0.01f, Mud));
            MudColor = Color.Lerp(MudColor, color, k);
            mudDir = ToRootDir(fromWorld, 0.8f);
            mudAge = 0f;
        }

        /// <summary>Kąpiel: woda zmywa błoto i trochę sadzy.</summary>
        public void Wash(float amount = 1f)
        {
            Mud = Mathf.MoveTowards(Mud, 0f, amount);
            Burn = Mathf.MoveTowards(Burn, 0f, amount * 0.5f);
            Heat = 0f;
        }

        /// <summary>Zwęglenie od wybuchu w punkcie świata. Żar na krawędzi gaśnie szybko, sadza zostaje dłużej.</summary>
        public void Scorch(Vector3 explosionWorld, float amount)
        {
            Scorch(explosionWorld, amount, 1f);
        }

        /// <summary>Zwęglenie z kontrolą żaru (0 = sama sadza, 1 = świecąca krawędź).</summary>
        public void Scorch(Vector3 explosionWorld, float amount, float heat)
        {
            Burn = Mathf.Clamp01(Burn + amount);
            Heat = Mathf.Max(Heat, heat);
            burnDir = ToRootDir(explosionWorld, 0.85f);
        }

        /// <summary>Natychmiastowe zamrożenie (np. trafienie śnieżką). Później topnieje do <see cref="FrostTarget"/>.</summary>
        public void Freeze(float amount) { Frost = Mathf.Clamp01(Frost + amount); }

        /// <summary>Krótki biały błysk po trafieniu.</summary>
        public void HitFlash(float amount = 1f) { Flash = Mathf.Max(Flash, Mathf.Clamp01(amount)); }

        /// <summary>
        /// Cios: miękkie wgniecenie w miejscu trafienia (wierzchołki w shaderze, sprężyna z odbiciem)
        /// i siniak, który zostaje na dłużej. pushDirWorld = kierunek, w którym cios pcha ofiarę.
        /// </summary>
        public void Impact(Vector3 pushDirWorld, float strength)
        {
            if (pushDirWorld.sqrMagnitude < 1e-4f) return;
            strength = Mathf.Clamp01(strength);
            var t = Root != null ? Root : transform;
            dentDirLocal = t.InverseTransformDirection(pushDirWorld.normalized);
            // wartość od razu (widoczna w klatce hit-stopu), potem sprężyna wraca z przestrzałem (odbicie = wybrzuszenie)
            dent.Value = Mathf.Max(dent.Value, strength);
            dent.Velocity = 0f;
            Bruise = Mathf.Clamp01(Bruise + 0.22f * strength);
            // siniak po stronie, z której przyszedł cios (przeciwnie do kierunku pchnięcia)
            var d = -dentDirLocal;
            bruiseDir = new Vector4(d.x, d.y, d.z, 0.9f);
        }

        public void Clear() { Mud = Frost = Burn = Heat = Flash = Bruise = 0f; FrostTarget = 0f; MudColor = DefaultMud; dent.Reset(); }

        // Kierunek w przestrzeni postaci zapamiętany w chwili trafienia: plama "przykleja się" do ciała
        // i obraca razem z ziemniakiem, zamiast zawsze patrzeć na miejsce wybuchu.
        Vector4 ToRootDir(Vector3 fromWorld, float strength)
        {
            var t = Root != null ? Root : transform;
            var d = fromWorld - t.position;
            if (d.sqrMagnitude < 1e-4f) return new Vector4(0, -1, 0, 0);
            var local = t.InverseTransformDirection(d.normalized);
            return new Vector4(local.x, local.y, local.z, strength);
        }

        // ---------------- aktualizacja ----------------

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            mudAge += dt;
            if (mudAge > MudDryDelay) Mud = Mathf.MoveTowards(Mud, 0f, MudFade * dt);
            Frost = Mathf.MoveTowards(Frost, FrostTarget, FrostRate * dt);
            Burn = Mathf.MoveTowards(Burn, 0f, BurnFade * dt);
            Heat = Mathf.MoveTowards(Heat, 0f, HeatFade * dt);
            Flash = Mathf.MoveTowards(Flash, 0f, FlashFade * dt);
            Bruise = Mathf.MoveTowards(Bruise, 0f, BruiseFade * dt);
            dent.Step(0f, DentFrequency, DentDamping, dt);
            Push();
        }

        static bool Changed(ref float sent, float v)
        {
            if (Mathf.Abs(sent - v) < 0.0005f) return false;
            sent = v;
            return true;
        }

        void Push()
        {
            int n = materials.Count;
            if (n == 0) return;
            var t = Root != null ? Root : transform;

            // przestrzeń postaci zmienia się co klatkę (ruch, fikołki): zawsze
            var m = t.worldToLocalMatrix;
            Vector4 r0 = m.GetRow(0), r1 = m.GetRow(1), r2 = m.GetRow(2);
            bool dentActive = Mathf.Abs(dent.Value) > 0.002f || Mathf.Abs(dent.Velocity) > 0.02f;
            Vector4 center = Vector4.zero, dentDir = Vector4.zero;
            if (dentActive)
            {
                var p = t.position;
                var w = t.TransformDirection(dentDirLocal);
                center = new Vector4(p.x, p.y, p.z, BodyRadius);
                dentDir = new Vector4(w.x, w.y, w.z, Mathf.Clamp(dent.Value, -0.6f, 1f));
            }

            bool mud = Changed(ref sentMud, Mud) | sentMudDir != mudDir | sentMudColor != MudColor;
            bool frost = Changed(ref sentFrost, Frost);
            float heatOut = Heat * Mathf.Clamp01(Burn * 2f);
            bool burn = Changed(ref sentBurn, Burn) | Changed(ref sentHeat, heatOut) | sentBurnDir != burnDir;
            bool flash = Changed(ref sentFlash, Flash);
            bool bruise = Changed(ref sentBruise, Bruise) | sentBruiseDir != bruiseDir;
            sentMudDir = mudDir; sentMudColor = MudColor; sentBurnDir = burnDir; sentBruiseDir = bruiseDir;

            for (int i = 0; i < n; i++)
            {
                var mat = materials[i];
                if (mat == null) continue;
                mat.SetVector(Root0Id, r0);
                mat.SetVector(Root1Id, r1);
                mat.SetVector(Root2Id, r2);
                if (dentActive || dentWasActive)
                {
                    mat.SetVector(ImpactCenterId, center);
                    mat.SetVector(ImpactDirId, dentDir);
                }
                if (mud)
                {
                    mat.SetFloat(MudAmountId, Mud);
                    mat.SetVector(MudDirId, mudDir);
                    mat.SetColor(MudColorId, MudColor);
                }
                if (frost) mat.SetFloat(FrostAmountId, Frost);
                if (burn)
                {
                    mat.SetFloat(BurnAmountId, Burn);
                    mat.SetFloat(BurnHeatId, heatOut);
                    mat.SetVector(BurnDirId, burnDir);
                }
                if (flash) mat.SetFloat(FlashAmountId, Flash);
                if (bruise)
                {
                    mat.SetFloat(BruiseAmountId, Bruise);
                    mat.SetVector(BruiseDirId, bruiseDir);
                }
            }
            dentWasActive = dentActive;
        }
    }
}
