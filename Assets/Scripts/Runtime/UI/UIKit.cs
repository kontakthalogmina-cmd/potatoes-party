using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotatoParty
{
    /// <summary>Pomocnicze funkcje budujące interfejs (uGUI) w kodzie.</summary>
    public static class UIKit
    {
        // Spójna, pogodna paleta interfejsu: białe karty, granatowy tekst, soczyste akcenty
        public static readonly Color Cream = Shapes.Hex("#FFFFFF");
        public static readonly Color Dark = Shapes.Hex("#2E2A5A");
        public static readonly Color Orange = Shapes.Hex("#FF8A3D");
        public static readonly Color Yellow = Shapes.Hex("#FFC93C");
        public static readonly Color Teal = Shapes.Hex("#1FC2B2");
        public static readonly Color Pink = Shapes.Hex("#FF5FA2");
        public static readonly Color Purple = Shapes.Hex("#7B61FF");
        public static readonly Color Red = Shapes.Hex("#FF5A5F");
        public static readonly Color Green = Shapes.Hex("#2FC872");
        public static readonly Color Soft = Shapes.Hex("#8C89B0");
        public static readonly Color Shade = new Color(0.10f, 0.11f, 0.30f, 0.45f);

        static Font font, titleFont;
        static Sprite rounded, circle;

        /// <summary>Zaokrąglona czcionka do zwykłego tekstu (Varela Round).</summary>
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("PotatoParty/Fonts/VarelaRound-Regular");
                if (font == null) { Debug.LogWarning("[Potatoes Party] Brak czcionki VarelaRound w Resources, używam domyślnej."); font = BuiltinFont(); }
                return font;
            }
        }

        /// <summary>Gruba, "imprezowa" czcionka do tytułów i przycisków (Titan One).</summary>
        public static Font TitleFont
        {
            get
            {
                if (titleFont == null) titleFont = Resources.Load<Font>("PotatoParty/Fonts/TitanOne-Regular");
                if (titleFont == null) { Debug.LogWarning("[Potatoes Party] Brak czcionki TitanOne w Resources, używam zapasowej."); titleFont = Font; }
                return titleFont;
            }
        }

        static Font BuiltinFont()
        {
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        public static Sprite Rounded
        {
            get
            {
                if (rounded == null) rounded = MakeRounded(64, 22);
                return rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null) circle = MakeRounded(128, 64);
                return circle;
            }
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            int b = Mathf.Min(radius, size / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static Canvas CreateCanvas(string name, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Rozciąga element między kotwicami z marginesami (w pikselach referencyjnych).</summary>
        public static RectTransform Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Fill(RectTransform rt)
        {
            return Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color c, bool roundedCorners = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            if (roundedCorners)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Tekst", parent);
            var t = rt.gameObject.AddComponent<Text>();
            // Duże i pogrubione napisy (tytuły, przyciski, wyniki): gruba, imprezowa Titan One.
            // Reszta: prosta, zaokrąglona Varela Round, żeby tekst był lekki i czytelny.
            bool title = size >= 44 || (style == FontStyle.Bold && size >= 30);
            t.font = title ? TitleFont : Font;
            t.text = text;
            int fitted = t.font != Font ? Mathf.RoundToInt(size * 0.88f) : size;
            t.fontSize = fitted;
            t.color = c;
            t.alignment = align;
            t.fontStyle = t.font != Font ? FontStyle.Normal : style;
            t.lineSpacing = 0.95f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            // wielkie tytuły mają własne, duże ramki — automatyczne zmniejszanie tylko by je psuło
            if (size < 90) rt.gameObject.AddComponent<FitText>().MaxSize = fitted;
            return t;
        }

        public static void Shadow(Graphic g, float dist = 3f, float alpha = 0.35f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, alpha);
            s.effectDistance = new Vector2(dist, -dist);
        }

        public static void Outline(Graphic g, Color c, float dist = 3f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(dist, -dist);
        }

        public static Button Button(Transform parent, string text, Color c, Action onClick, int fontSize = 34)
        {
            var img = Panel(parent, "Przycisk " + text, c);
            // "wypukły" przycisk: ciemniejszy spód w tym samym kolorze zamiast szarego cienia
            var lip = img.gameObject.AddComponent<Shadow>();
            lip.effectColor = Color.Lerp(c, Color.black, 0.28f);
            lip.effectDistance = new Vector2(0, -7);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.93f, 0.75f);
            colors.selectedColor = new Color(1f, 0.93f, 0.75f);
            colors.pressedColor = new Color(0.85f, 0.8f, 0.7f);
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.6f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.onClick.AddListener(() =>
            {
                AudioManager.Play(Sfx.Click, 0.8f);
                if (onClick != null) onClick();
            });
            var label = Label(img.transform, text, Mathf.Max(fontSize, 30), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 4), new Vector2(-14, -4));
            var ls = label.gameObject.AddComponent<Shadow>();
            ls.effectColor = new Color(0, 0, 0, 0.22f);
            ls.effectDistance = new Vector2(0, -3);
            img.gameObject.AddComponent<JuicyButton>().Accent = c;
            return btn;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static InputField Input(Transform parent, string placeholder, int limit, int fontSize = 30)
        {
            var img = Panel(parent, "Pole tekstowe", Color.white);
            var field = img.gameObject.AddComponent<InputField>();
            var text = Label(img.transform, "", fontSize, Dark, TextAnchor.MiddleLeft);
            text.font = Font; // przy wpisywaniu czytelniejsza jest lżejsza czcionka
            UnityEngine.Object.Destroy(text.GetComponent<FitText>());
            text.resizeTextForBestFit = false;
            text.fontSize = fontSize;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 4), new Vector2(-18, -4));
            var ph = Label(img.transform, placeholder, fontSize, new Color(0.45f, 0.38f, 0.32f, 0.7f), TextAnchor.MiddleLeft, FontStyle.Italic);
            Stretch(ph.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 4), new Vector2(-18, -4));
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = limit;
            return field;
        }

        public static GameObject Row(Transform parent, string name, float height)
        {
            var rt = Rect(name, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return rt.gameObject;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, int padding = 0)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return v;
        }
    }

    /// <summary>Wybór jednej z opcji strzałkami: &lt; wartość &gt;.</summary>
    public class Stepper : MonoBehaviour
    {
        public string[] Options;
        public int Index;
        public Action<int> Changed;
        Text valueText;
        Button left, right;

        public static Stepper Create(Transform parent, string title, string[] options, int index, Action<int> changed, float height = 74f)
        {
            var row = UIKit.Row(parent, "Wybor " + title, height);
            var st = row.AddComponent<Stepper>();
            st.Options = options;
            st.Index = Mathf.Clamp(index, 0, options.Length - 1);
            st.Changed = changed;

            var label = UIKit.Label(row.transform, title, 28, UIKit.Dark, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Stretch(label.rectTransform, new Vector2(0, 0), new Vector2(0.38f, 1), Vector2.zero, Vector2.zero);

            var box = UIKit.Panel(row.transform, "Wartosc", Shapes.Hex("#EEF0FA"));
            UIKit.Stretch(box.rectTransform, new Vector2(0.38f, 0.08f), new Vector2(1, 0.92f), Vector2.zero, Vector2.zero);

            st.left = UIKit.Button(box.transform, "<", UIKit.Orange, () => st.Step(-1), 34);
            UIKit.Stretch((RectTransform)st.left.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 4), new Vector2(68, -4));
            st.right = UIKit.Button(box.transform, ">", UIKit.Orange, () => st.Step(1), 34);
            UIKit.Stretch((RectTransform)st.right.transform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-68, 4), new Vector2(-4, -4));

            st.valueText = UIKit.Label(box.transform, "", 28, UIKit.Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(st.valueText.rectTransform, Vector2.zero, Vector2.one, new Vector2(72, 0), new Vector2(-72, 0));
            st.Refresh();
            return st;
        }

        public void Step(int dir)
        {
            Index = (Index + dir + Options.Length) % Options.Length;
            Refresh();
            if (Changed != null) Changed(Index);
        }

        public void Set(int index)
        {
            Index = Mathf.Clamp(index, 0, Options.Length - 1);
            Refresh();
        }

        public void SetInteractable(bool on)
        {
            left.interactable = on;
            right.interactable = on;
            valueText.color = on ? UIKit.Dark : new Color(UIKit.Dark.r, UIKit.Dark.g, UIKit.Dark.b, 0.45f);
        }

        void Refresh() { valueText.text = Options[Index]; }
    }

    /// <summary>Delikatne powiększenie przycisku po najechaniu/zaznaczeniu.</summary>
    public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        float target = 1f;
        bool pointer, selected, pressed;

        public void OnPointerEnter(PointerEventData e) { pointer = true; UpdateTarget(); }
        public void OnPointerExit(PointerEventData e) { pointer = false; pressed = false; UpdateTarget(); }
        public void OnPointerDown(PointerEventData e) { pressed = true; UpdateTarget(); }
        public void OnPointerUp(PointerEventData e) { pressed = false; UpdateTarget(); }
        public void OnSelect(BaseEventData e) { selected = true; UpdateTarget(); }
        public void OnDeselect(BaseEventData e) { selected = false; UpdateTarget(); }

        void UpdateTarget()
        {
            var sel = GetComponent<Selectable>();
            bool on = sel == null || sel.interactable;
            target = pressed && on ? 0.95f : (pointer || selected) && on ? 1.06f : 1f;
        }

        void OnDisable()
        {
            pointer = selected = pressed = false;
            target = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, target, Time.unscaledDeltaTime * 14f);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Sprężyste pojawienie się okienka (powiększenie z lekkim "odbiciem").</summary>
    public class UIPop : MonoBehaviour
    {
        public float Delay;
        float t;

        void OnEnable()
        {
            t = -Delay;
            transform.localScale = Vector3.one * 0.85f;
            Update();
        }

        void Update()
        {
            if (t >= 0.45f) return;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.45f);
            // ease-out-back: szybkie powiększenie z małym przeskokiem
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
            float sc = Mathf.LerpUnclamped(0.85f, 1f, e);
            transform.localScale = new Vector3(sc, sc, 1f);
        }
    }

    /// <summary>Bujający się tytuł w menu.</summary>
    public class UIWobble : MonoBehaviour
    {
        public float Angle = 3f;
        public float Speed = 1.6f;
        public float Pulse = 0.03f;

        void Update()
        {
            float t = Time.unscaledTime * Speed;
            transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t) * Angle);
            float s = 1f + Mathf.Sin(t * 2f) * Pulse;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>
    /// Zmniejsza napis tylko wtedy, gdy naprawdę nie mieści się w swojej ramce (za szeroki albo za wysoki).
    /// Wbudowany "Best Fit" liczy wysokie odstępy grubej czcionki i niepotrzebnie zmniejszał tytuły.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class FitText : MonoBehaviour
    {
        public int MaxSize = 30;
        Text text;
        string last;
        Vector2 lastSize;

        void OnEnable() { last = null; Apply(); }
        void OnRectTransformDimensionsChange() { last = null; }
        void LateUpdate() { Apply(); }

        void Apply()
        {
            if (text == null) text = GetComponent<Text>();
            if (text == null) return;
            var r = ((RectTransform)transform).rect;
            if (text.text == last && r.size == lastSize) return;
            last = text.text;
            lastSize = r.size;
            text.resizeTextForBestFit = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (r.width < 8f || r.height < 8f || string.IsNullOrEmpty(text.text)) { text.fontSize = MaxSize; return; }

            int min = Mathf.Max(10, Mathf.RoundToInt(MaxSize * 0.45f));
            var gen = text.cachedTextGeneratorForLayout;
            for (int size = MaxSize; size >= min; size -= 2)
            {
                text.fontSize = size;
                var st = text.GetGenerationSettings(new Vector2(r.width, 0f));
                st.horizontalOverflow = HorizontalWrapMode.Overflow;
                float w = gen.GetPreferredWidth(text.text, st) / text.pixelsPerUnit;
                // napis w jednej linii: wystarczy, że się zmieści na szerokość
                if (w <= r.width + 1f && text.text.IndexOf('\n') < 0 && size * 1.05f <= r.height + size * 0.35f) return;
                // dłuższy tekst: zawijamy i sprawdzamy wysokość (bez ogromnych odstępów czcionki)
                st.horizontalOverflow = HorizontalWrapMode.Wrap;
                float lines = gen.GetPreferredHeight(text.text, st) / text.pixelsPerUnit / Mathf.Max(1f, text.font.lineHeight * size / text.font.fontSize);
                if (lines * size * 1.1f <= r.height + 2f) return;
            }
            text.fontSize = min;
        }
    }
}
