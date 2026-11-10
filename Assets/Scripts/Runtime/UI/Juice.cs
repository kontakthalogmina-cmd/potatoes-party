using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotatoParty
{
    /// <summary>Krzywe wygładzania (easing) dla UI. Wszystkie przyjmują t ∈ [0,1].</summary>
    public static class Ease
    {
        public static float OutBack(float t, float s = 1.70158f) { t -= 1f; return 1f + (s + 1f) * t * t * t + s * t * t; }
        public static float InBack(float t, float s = 1.70158f) { return (s + 1f) * t * t * t - s * t * t; }
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float InOutCubic(float t) { return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f; }
        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }
    }

    /// <summary>
    /// "Soczysty" przycisk: sprężyna skali i obrotu zamiast liniowego lerpa.
    /// <code>
    /// stan          skala celu   obrót   dźwięk               cząsteczki
    /// najechanie    1.10         ±2.5°   Tick (wyżej z każdym) 6 iskier z krawędzi
    /// wciśnięcie    0.90 / 1.06  0       –                     –
    /// klik          kopnięcie +0.35 (sprężyna 7 Hz, ζ 0.32)  Click + Coin   18 strzępków + pierścień
    /// </code>
    /// Działa z myszą, padem i klawiaturą (Select/Deselect), w czasie nieskalowanym (pauza, hit stop).
    /// </summary>
    public class JuicyButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IPointerDownHandler, IPointerUpHandler, ISubmitHandler, IPointerClickHandler
    {
        public static float HoverScale = 1.1f, PressScaleX = 0.9f, PressScaleY = 1.06f, ClickKick = 0.35f;
        public static float Frequency = 7f, Damping = 0.32f;
        static int hoverCounter;

        public Color Accent = Color.white;
        bool pointer, selected, pressed;
        Spring sx, sy, rot;
        Graphic shine;
        float shineT = -1f;
        float tiltSign = 1f;

        void Awake()
        {
            sx.Set(1f); sy.Set(1f); rot.Set(0f);
            // połysk przelatujący po przycisku przy najechaniu (przycięty do kształtu przycisku)
            if (GetComponent<RectMask2D>() == null) gameObject.AddComponent<RectMask2D>();
            var go = new GameObject("Polysk", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(46f, 0f);
            rt.localRotation = Quaternion.Euler(0, 0, -18f);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;
            shine = img;
        }

        bool Interactable { get { var s = GetComponent<Selectable>(); return s == null || s.interactable; } }

        public void OnPointerEnter(PointerEventData e) { if (!pointer) Hover(); pointer = true; }
        public void OnPointerExit(PointerEventData e) { pointer = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) { pressed = true; }
        public void OnPointerUp(PointerEventData e) { pressed = false; }
        public void OnSelect(BaseEventData e) { if (!selected && !pointer) Hover(); selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; }
        public void OnSubmit(BaseEventData e) { Click(); }
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Click(); }

        void Hover()
        {
            if (!Interactable || !isActiveAndEnabled) return;
            tiltSign = -tiltSign;
            rot.Kick(tiltSign * 220f);
            sx.Kick(1.6f); sy.Kick(1.2f);
            shineT = 0f;
            // dźwięk: krótki "tick", co najechanie trochę wyżej (gama pentatoniczna – zawsze ładnie brzmi)
            float[] steps = { 1f, 1.122f, 1.26f, 1.498f, 1.682f };
            AudioManager.PlayPitched(Sfx.Tick, 0.35f, steps[hoverCounter++ % steps.Length]);
            UISparks.Burst((RectTransform)transform, Accent, 6, 0.6f);
        }

        void Click()
        {
            if (!Interactable || !isActiveAndEnabled) return;
            sx.Kick(ClickKick * 19f); sy.Kick(-ClickKick * 9f); // pop w szerz, przysiad w pionie
            rot.Kick(-tiltSign * 360f);
            AudioManager.PlayPitched(Sfx.Coin, 0.4f, 1.25f);
            UISparks.Burst((RectTransform)transform, Accent, 18, 1.2f);
            UISparks.Ring((RectTransform)transform, Accent);
        }

        void OnDisable()
        {
            pointer = selected = pressed = false;
            sx.Set(1f); sy.Set(1f); rot.Set(0f);
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            bool on = Interactable;
            float tx = 1f, ty = 1f;
            if (on && pressed) { tx = PressScaleX; ty = PressScaleY; }
            else if (on && (pointer || selected)) { tx = ty = HoverScale; }
            sx.Step(tx, Frequency, Damping, dt);
            sy.Step(ty, Frequency * 1.12f, Damping, dt);
            rot.Step(0f, Frequency * 0.8f, 0.28f, dt);
            transform.localScale = new Vector3(sx.Value, sy.Value, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, rot.Value);

            if (shineT >= 0f && shine != null)
            {
                shineT += dt / 0.38f;
                var rt = (RectTransform)shine.transform;
                float w = ((RectTransform)transform).rect.width;
                rt.anchoredPosition = new Vector2(Mathf.Lerp(-40f, w + 40f, Ease.InOutCubic(Mathf.Clamp01(shineT))), 0f);
                shine.color = new Color(1f, 1f, 1f, 0.35f * Mathf.Sin(Mathf.Clamp01(shineT) * Mathf.PI));
                if (shineT >= 1f) { shineT = -1f; shine.color = new Color(1f, 1f, 1f, 0f); }
            }
        }
    }

    /// <summary>
    /// Cząsteczki UI bez kamery i bez ParticleSystem: pula obrazków na nakładce ekranu,
    /// animowanych w czasie nieskalowanym (prędkość, grawitacja, obrót, zanik). Rysują się nad całym UI.
    /// Kształty: "strzępki ziemniaka" (prostokąciki), iskry (kropki) i pierścień fali uderzeniowej.
    /// </summary>
    public class UISparks : MonoBehaviour
    {
        const int PoolSize = 160;
        static UISparks instance;

        struct P { public RectTransform Rt; public Image Img; public Vector2 V; public float Life, Max, Spin, Size; public bool Ring; public Color C; }
        readonly P[] pool = new P[PoolSize];
        int next;
        RectTransform layer;

        static UISparks Get()
        {
            if (instance != null) return instance;
            var canvas = UIKit.CreateCanvas("UI Iskry", 90, null);
            Object.DontDestroyOnLoad(canvas.gameObject);
            var gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr != null) gr.enabled = false;
            instance = canvas.gameObject.AddComponent<UISparks>();
            instance.layer = UIKit.Fill(UIKit.Rect("Warstwa", canvas.transform));
            for (int i = 0; i < PoolSize; i++)
            {
                var rt = UIKit.Rect("Iskra", instance.layer);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                var img = rt.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                rt.gameObject.SetActive(false);
                instance.pool[i] = new P { Rt = rt, Img = img };
            }
            return instance;
        }

        /// <summary>Wyrzut iskier/strzępków z krawędzi elementu UI.</summary>
        public static void Burst(RectTransform from, Color accent, int count, float power)
        {
            if (from == null) return;
            var s = Get();
            Vector2 center, half;
            s.Locate(from, out center, out half);
            for (int i = 0; i < count; i++)
            {
                // punkt startu na obwodzie prostokąta, kierunek na zewnątrz
                float a = Random.value * Mathf.PI * 2f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var start = center + new Vector2(d.x * half.x, d.y * half.y) * Random.Range(0.85f, 1.05f);
                bool shred = Random.value < 0.6f;
                var c = shred ? Color.Lerp(new Color(1f, 0.86f, 0.36f), new Color(1f, 0.95f, 0.65f), Random.value) : Color.Lerp(accent, Color.white, 0.5f);
                float size = shred ? Random.Range(9f, 18f) : Random.Range(6f, 11f);
                s.Spawn(start, d * Random.Range(260f, 620f) * power + Vector2.up * Random.Range(80f, 240f) * power,
                    Random.Range(0.45f, 0.85f), size, c, shred ? Random.Range(-720f, 720f) : 0f, shred, false);
            }
        }

        /// <summary>Fala uderzeniowa: rozszerzający się pierścień wokół elementu.</summary>
        public static void Ring(RectTransform from, Color accent)
        {
            if (from == null) return;
            var s = Get();
            Vector2 center, half;
            s.Locate(from, out center, out half);
            s.Spawn(center, Vector2.zero, 0.42f, Mathf.Max(half.x, half.y) * 2f, Color.Lerp(accent, Color.white, 0.6f), 0f, false, true);
        }

        void Locate(RectTransform from, out Vector2 center, out Vector2 half)
        {
            var corners = new Vector3[4];
            from.GetWorldCorners(corners);
            var cam = (Camera)null;
            var c = from.GetComponentInParent<Canvas>();
            if (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) cam = c.worldCamera;
            Vector2 a, b;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, RectTransformUtility.WorldToScreenPoint(cam, corners[0]), null, out a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, RectTransformUtility.WorldToScreenPoint(cam, corners[2]), null, out b);
            center = (a + b) * 0.5f;
            half = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) * 0.5f;
        }

        void Spawn(Vector2 pos, Vector2 v, float life, float size, Color c, float spin, bool shred, bool ring)
        {
            var p = pool[next];
            next = (next + 1) % PoolSize;
            p.Rt.gameObject.SetActive(true);
            p.Rt.anchoredPosition = pos;
            p.Rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            p.Img.sprite = ring ? RingSprite() : (shred ? null : UIKit.Circle);
            p.Img.color = c;
            p.Rt.sizeDelta = shred ? new Vector2(size, size * Random.Range(0.45f, 0.75f)) : new Vector2(size, size);
            p.V = v; p.Life = 0f; p.Max = life; p.Spin = spin; p.Size = size; p.Ring = ring; p.C = c;
            pool[(next + PoolSize - 1) % PoolSize] = p;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = 0; i < PoolSize; i++)
            {
                var p = pool[i];
                if (!p.Rt.gameObject.activeSelf) continue;
                p.Life += dt;
                float k = p.Life / p.Max;
                if (k >= 1f) { p.Rt.gameObject.SetActive(false); pool[i] = p; continue; }
                if (p.Ring)
                {
                    float s = Mathf.Lerp(1f, 1.6f, Ease.OutCubic(k));
                    p.Rt.localScale = new Vector3(s, s, 1f);
                    var c = p.C; c.a = 0.8f * (1f - k); p.Img.color = c;
                }
                else
                {
                    p.V += Vector2.down * 1500f * dt;          // grawitacja (px/s²)
                    p.V *= Mathf.Exp(-1.8f * dt);              // opór
                    p.Rt.anchoredPosition += p.V * dt;
                    p.Rt.localRotation = Quaternion.Euler(0, 0, p.Rt.localEulerAngles.z + p.Spin * dt);
                    float s = k < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.7f) / 0.3f);
                    p.Rt.localScale = new Vector3(s, s, 1f);
                }
                pool[i] = p;
            }
        }

        static Sprite ringSprite;
        static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Pierscien UI" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.9f) / 0.07f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return ringSprite;
        }
    }

    /// <summary>
    /// Wjazd i zjazd elementów ekranu "kaskadą": każdy element wjeżdża z boku z opóźnieniem 60 ms,
    /// z przeskokiem (ease-out-back). Zjazd: ease-in-back w przeciwną stronę.
    /// Rejestrujemy elementy WEWNĄTRZ wierszy layoutu (np. przycisk w wierszu), nie same wiersze –
    /// layout nie nadpisuje wtedy pozycji, a skalę/obrót zostawiamy dla <see cref="JuicyButton"/>.
    /// </summary>
    public class UIStagger : MonoBehaviour
    {
        public float Offset = -520f, Stagger = 0.06f, InTime = 0.5f, OutTime = 0.22f;
        readonly List<RectTransform> items = new List<RectTransform>();
        readonly List<Vector2> home = new List<Vector2>();
        float t;
        bool outgoing;
        System.Action onOut;

        public void Register(RectTransform rt)
        {
            items.Add(rt);
            home.Add(rt.anchoredPosition);
        }

        public void PlayIn()
        {
            outgoing = false;
            onOut = null;
            t = 0f;
            Apply();
            AudioManager.PlayPitched(Sfx.Whoosh, 0.25f, 1.15f);
        }

        public void PlayOut(System.Action done)
        {
            if (!gameObject.activeInHierarchy) { if (done != null) done(); return; }
            outgoing = true;
            onOut = done;
            t = 0f;
        }

        void OnEnable() { PlayIn(); }

        void Update()
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Apply();
            if (outgoing && t >= OutTime + Stagger * items.Count)
            {
                outgoing = false;
                var cb = onOut;
                onOut = null;
                if (cb != null) cb();
            }
        }

        void Apply()
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                float local = t - i * Stagger;
                float x;
                if (!outgoing) x = Mathf.LerpUnclamped(Offset, 0f, Ease.OutBack(Mathf.Clamp01(local / InTime), 2.2f));
                else x = Mathf.LerpUnclamped(0f, Offset, Ease.InBack(Mathf.Clamp01(local / OutTime), 1.6f));
                items[i].anchoredPosition = home[i] + new Vector2(x, 0f);
            }
        }
    }

    /// <summary>
    /// Tytuł z osobnymi literami na sprężynach: przy wejściu litery spadają kolejno z góry i odbijają się,
    /// potem "oddychają" falą. Klik w tytuł podrzuca wszystkie litery.
    /// </summary>
    public class BounceTitle : MonoBehaviour, IPointerClickHandler
    {
        readonly List<RectTransform> letters = new List<RectTransform>();
        Spring[] ys = new Spring[0], rs = new Spring[0];
        float t;

        public static BounceTitle Build(RectTransform parent, string text, int size, Color fill, Color outline, float spacing)
        {
            var host = UIKit.Rect("Tytul " + text, parent);
            UIKit.Fill(host);
            var bt = host.gameObject.AddComponent<BounceTitle>();
            var img = host.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // tylko do kliknięcia
            float total = 0f;
            var widths = new float[text.Length];
            for (int i = 0; i < text.Length; i++) { widths[i] = text[i] == ' ' ? size * 0.35f : size * 0.68f * spacing; total += widths[i]; }
            float x = -total * 0.5f;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != ' ')
                {
                    var l = UIKit.Label(host, text[i].ToString(), size, fill, TextAnchor.MiddleCenter, FontStyle.Bold);
                    var rt = l.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(size * 1.1f, size * 1.4f);
                    rt.anchoredPosition = new Vector2(x + widths[i] * 0.5f, 0f);
                    UIKit.Outline(l, outline, size * 0.05f);
                    UIKit.Shadow(l, size * 0.08f, 0.35f);
                    l.raycastTarget = false;
                    bt.letters.Add(rt);
                }
                x += widths[i];
            }
            bt.ys = new Spring[bt.letters.Count];
            bt.rs = new Spring[bt.letters.Count];
            return bt;
        }

        void OnEnable()
        {
            t = 0f;
            for (int i = 0; i < ys.Length; i++) { ys[i].Set(600f); rs[i].Set(Random.Range(-25f, 25f)); }
        }

        public void OnPointerClick(PointerEventData e)
        {
            for (int i = 0; i < ys.Length; i++) { ys[i].Kick(Random.Range(900f, 1500f)); rs[i].Kick(Random.Range(-400f, 400f)); }
            AudioManager.PlayPitched(Sfx.Jump, 0.5f, 1.3f);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            t += dt;
            for (int i = 0; i < letters.Count; i++)
            {
                bool released = t > 0.15f + i * 0.055f;
                float wave = Mathf.Sin(t * 2.4f - i * 0.55f) * 7f;
                ys[i].Step(released ? wave : 600f, 4.2f, 0.38f, dt);
                rs[i].Step(Mathf.Sin(t * 1.7f - i * 0.8f) * 2.5f, 3.5f, 0.3f, dt);
                var p = letters[i].anchoredPosition;
                letters[i].anchoredPosition = new Vector2(p.x, ys[i].Value);
                letters[i].localRotation = Quaternion.Euler(0, 0, rs[i].Value);
                // przy uderzeniu o "podłogę" litera się spłaszcza
                float squash = Mathf.Clamp(ys[i].Velocity * -0.0004f, -0.12f, 0.18f);
                letters[i].localScale = new Vector3(1f + squash, 1f - squash, 1f);
            }
        }
    }
}
