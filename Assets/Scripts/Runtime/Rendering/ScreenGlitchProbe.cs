using System.Collections;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PotatoParty
{
    /// <summary>
    /// Czujnik "jednolitego ekranu". Co 0.25 s (czas rzeczywisty) czyta gotową klatkę (z UI) po WaitForEndOfFrame
    /// i próbkuje ją siatką 16×9. Gdy rozrzut kolorów
    /// we wszystkich 144 próbkach &lt; 2% (ekran jednolity) w trakcie gry, zapisuje do Editor.log / Player.log
    /// pełną migawkę: stan gry, czas, kamerę, efekty obrazu i 10 ostatnich zdarzeń "feel", plus zrzut PNG
    /// (maks. 5 na sesję) w <see cref="Application.persistentDataPath"/>.
    /// <para>Klawisze bisekcji na żywo: F6 bloom, F7 głębia ostrości, F8 motion blur, F9 cały post-processing.</para>
    /// </summary>
    public class ScreenGlitchProbe : MonoBehaviour
    {
        public static float Interval = 0.25f, UniformThreshold = 0.02f;
        const int W = 16, H = 9, MaxShots = 5;

        static ScreenGlitchProbe instance;
        static readonly string[] events = new string[10];
        static int eventHead;

        Texture2D frame;
        int shots;
        float lastReport = -10f;
        bool inGlitch;
        float glitchStart;

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("Czujnik glitcha ekranu");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenGlitchProbe>();
        }

        /// <summary>Zdarzenie do historii (wybuch, nokaut, błysk, najazd kamery...).</summary>
        public static void Note(string what)
        {
            events[eventHead] = Time.unscaledTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " " + what;
            eventHead = (eventHead + 1) % events.Length;
        }

        void Start()
        {
            StartCoroutine(Loop());
        }

        IEnumerator Loop()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return new WaitForSecondsRealtime(Interval);
                yield return eof;
                if (Screen.width < 16 || Screen.height < 9) continue;
                if (frame == null || frame.width != Screen.width || frame.height != Screen.height)
                {
                    if (frame != null) Destroy(frame);
                    frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                }
                // gotowy backbuffer (z UI) po WaitForEndOfFrame; moduł ScreenCapture nie jest w projekcie
                frame.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0, false);
                Analyze();
            }
        }

        void Analyze()
        {
            float mr = 0, mg = 0, mb = 0;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = frame.GetPixel((int)((x + 0.5f) / W * frame.width), (int)((y + 0.5f) / H * frame.height));
                    px[y * W + x] = c;
                    mr += c.r; mg += c.g; mb += c.b;
                }
            float n = px.Length;
            mr /= n; mg /= n; mb /= n;
            float spread = 0f;
            for (int i = 0; i < px.Length; i++)
            {
                spread = Mathf.Max(spread, Mathf.Abs(px[i].r - mr));
                spread = Mathf.Max(spread, Mathf.Abs(px[i].g - mg));
                spread = Mathf.Max(spread, Mathf.Abs(px[i].b - mb));
            }

            var gm = GameManager.Instance;
            bool playing = gm != null && (gm.State == GameState.Playing || gm.State == GameState.Intro || gm.State == GameState.Lobby || gm.State == GameState.MainMenu);
            bool uniform = playing && spread < UniformThreshold;
            if (uniform && !inGlitch) { inGlitch = true; glitchStart = Time.unscaledTime; }
            if (!uniform && inGlitch)
            {
                inGlitch = false;
                Debug.LogWarning("[Potatoes Party] GLITCH koniec po " + (Time.unscaledTime - glitchStart).ToString("F2") + " s");
            }
            if (!uniform || Time.unscaledTime - lastReport < 1f) return;
            lastReport = Time.unscaledTime;
            Report(gm, new Color(mr, mg, mb), spread);
        }

        void Report(GameManager gm, Color mean, float spread)
        {
            var sb = new StringBuilder(1024);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            sb.Append("[Potatoes Party] GLITCH: jednolity ekran RGB(").Append(mean.r.ToString("F2", ci)).Append(", ")
              .Append(mean.g.ToString("F2", ci)).Append(", ").Append(mean.b.ToString("F2", ci)).Append(") rozrzut ").Append(spread.ToString("F3", ci)).Append('\n');
            sb.Append("stan=").Append(gm.State).Append(" gra=").Append(gm.Current != null ? gm.Current.Title : "-")
              .Append(" timeScale=").Append(Time.timeScale.ToString("F3", ci)).Append(" hitStop=").Append(TimeDirector.InHitStop)
              .Append(" fixedDt=").Append(Time.fixedDeltaTime.ToString("F4", ci)).Append('\n');
            var rig = gm.Camera;
            if (rig != null && rig.Cam != null)
            {
                var t = rig.Cam.transform;
                sb.Append("kamera pos=").Append(t.position.ToString("F2")).Append(" rot=").Append(t.eulerAngles.ToString("F1"))
                  .Append(" fov=").Append(rig.Cam.fieldOfView.ToString("F1", ci)).Append(" near=").Append(rig.Cam.nearClipPlane.ToString("F2", ci))
                  .Append(" clear=").Append(rig.Cam.clearFlags).Append(" speed=").Append(rig.CameraSpeed.ToString("F1", ci))
                  .Append(" focus=").Append(rig.FocusPoint.ToString("F2"));
                if (rig.Brain != null)
                {
                    var live = rig.Brain.ActiveVirtualCamera;
                    sb.Append(" vcam=").Append(live != null ? live.Name : "brak").Append(" blend=").Append(rig.Brain.IsBlending);
                }
                // czy kamera siedzi w geometrii (najczęstsza przyczyna jednolitego koloru)
                var inside = Physics.OverlapSphere(t.position, 0.25f, ~0, QueryTriggerInteraction.Ignore);
                sb.Append(" wGeometrii=").Append(inside.Length > 0 ? inside[0].name : "nie");
                RaycastHit hit;
                if (Physics.Raycast(t.position, t.forward, out hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                    sb.Append(" przedObiektywem=").Append(hit.collider.name).Append('@').Append(hit.distance.ToString("F2", ci));
                sb.Append('\n');
            }
            sb.Append(PostFx.DebugState()).Append('\n');
            sb.Append("ostatnie zdarzenia:");
            for (int i = 0; i < events.Length; i++)
            {
                var e = events[(eventHead + i) % events.Length];
                if (e != null) sb.Append("\n  ").Append(e);
            }
            sb.Append("\nteraz=").Append(Time.unscaledTime.ToString("F2", ci));
            if (shots < MaxShots)
            {
                shots++;
                var path = System.IO.Path.Combine(Application.persistentDataPath, "glitch_" + System.DateTime.Now.ToString("HHmmss") + "_" + shots + ".png");
                frame.Apply(false);
                System.IO.File.WriteAllBytes(path, frame.EncodeToPNG());
                sb.Append("\nzrzut: ").Append(path);
            }
            Debug.LogWarning(sb.ToString());
        }

        void Update()
        {
            int which = -1;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            which = kb.f6Key.wasPressedThisFrame ? 0 : kb.f7Key.wasPressedThisFrame ? 1 : kb.f8Key.wasPressedThisFrame ? 2 : kb.f9Key.wasPressedThisFrame ? 3 : -1;
#else
            which = Input.GetKeyDown(KeyCode.F6) ? 0 : Input.GetKeyDown(KeyCode.F7) ? 1 : Input.GetKeyDown(KeyCode.F8) ? 2 : Input.GetKeyDown(KeyCode.F9) ? 3 : -1;
#endif
            if (which < 0) return;
            var msg = PostFx.DebugToggle(which);
            Debug.Log("[Potatoes Party] " + msg);
            var gm = GameManager.Instance;
            if (gm != null && gm.Hud != null) gm.Hud.Toast(msg, Color.white);
        }

        void OnDestroy()
        {
            if (frame != null) Destroy(frame);
        }
    }
}
