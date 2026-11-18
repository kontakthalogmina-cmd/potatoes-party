using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PotatoParty
{
    /// <summary>Interfejs w trakcie gry: tytuł, czas, karty graczy, podpisy nad głowami, komunikaty.</summary>
    public class Hud : MonoBehaviour
    {
        Canvas hudCanvas, fxCanvas, labelCanvas;
        RectTransform top, chipsRow, toastRoot, labelRoot;
        Text title, info, countdown, timerText;
        Image timerRing;
        RectTransform timer;
        float countdownTime;
        readonly Dictionary<PlayerSlot, Chip> chips = new Dictionary<PlayerSlot, Chip>();
        readonly Dictionary<Potato, Text> labels = new Dictionary<Potato, Text>();
        readonly List<ToastItem> toasts = new List<ToastItem>();
        readonly Dictionary<Potato, Meter> meters = new Dictionary<Potato, Meter>();

        /// <summary>Pasek nad głową gracza (np. Sizzle Meter w "Wielkiej Patelni Babci").</summary>
        class Meter
        {
            public RectTransform Root, Fill;
            public Image FillImage, Frame;
            public Text Value;
            public float Target, Shown, Pulse;
            public string Caption;
        }

        class Chip { public Image Bg, Portrait, Inner; public Color SkinColor; public Text Name, Points, Stat; }
        class ToastItem { public Text Text; public float Age; }

        public static Hud Create(Transform parent)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Hud>();
            h.Build();
            return h;
        }

        void Build()
        {
            labelCanvas = UIKit.CreateCanvas("Podpisy", 5, transform);
            labelRoot = UIKit.Fill(UIKit.Rect("Podpisy", labelCanvas.transform));

            hudCanvas = UIKit.CreateCanvas("HUD", 10, transform);
            var root = UIKit.Fill(UIKit.Rect("Korzen", hudCanvas.transform));

            // biała "pigułka" u góry: nazwa minigry i jedna linijka informacji
            var topPanel = UIKit.Panel(root, "Gora", new Color(1f, 1f, 1f, 0.94f));
            top = UIKit.Place(topPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(760, 84));
            Soft(topPanel);
            title = UIKit.Label(top, "", 34, UIKit.Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(title.rectTransform, new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(10, 0), new Vector2(-10, -4));
            info = UIKit.Label(top, "", 24, UIKit.Soft, TextAnchor.MiddleCenter);
            UIKit.Stretch(info.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(10, 6), new Vector2(-10, 0));

            chipsRow = UIKit.Rect("Gracze", root);
            UIKit.Stretch(chipsRow, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-900, 16), new Vector2(900, 100));
            var layout = chipsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // okrągły licznik czasu w prawym górnym rogu
            var timerBg = UIKit.Panel(root, "Czas", Color.white, false);
            timerBg.sprite = UIKit.Circle;
            timerBg.type = Image.Type.Simple;
            timer = UIKit.Place(timerBg.rectTransform, new Vector2(1f, 1f), new Vector2(-78, -78), new Vector2(112, 112));
            timerRing = UIKit.Panel(timer, "Pierscien", Shapes.Hex("#7FE3FF"), false);
            timerRing.sprite = UIKit.Circle;
            timerRing.type = Image.Type.Filled;
            timerRing.fillMethod = Image.FillMethod.Radial360;
            timerRing.fillOrigin = (int)Image.Origin360.Top;
            timerRing.fillClockwise = false;
            UIKit.Fill(timerRing.rectTransform);
            var hole = UIKit.Panel(timer, "Srodek", Color.white, false);
            hole.sprite = UIKit.Circle;
            hole.type = Image.Type.Simple;
            UIKit.Stretch(hole.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
            timerText = UIKit.Label(timer, "", 42, UIKit.Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
            Soft(timerBg);
            UIKit.Stretch(timerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -16));

            fxCanvas = UIKit.CreateCanvas("Komunikaty", 30, transform);
            var fxRoot = UIKit.Fill(UIKit.Rect("Korzen", fxCanvas.transform));
            fxRoot.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            countdown = UIKit.Label(fxRoot, "", 190, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(countdown.rectTransform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(1400, 300));
            UIKit.Outline(countdown, UIKit.Dark, 6f);
            toastRoot = UIKit.Rect("Toasty", fxRoot);
            UIKit.Stretch(toastRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-700, -420), new Vector2(700, -130));
            var v = toastRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter;
            v.spacing = 6;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;

            Hide();
        }

        public void Show(Minigame mg)
        {
            hudCanvas.gameObject.SetActive(true);
            labelCanvas.gameObject.SetActive(true);
            title.text = mg.Title;
            info.text = mg.Goal;
            foreach (Transform c in chipsRow) Destroy(c.gameObject);
            chips.Clear();
            foreach (var s in GameManager.Instance.Slots) chips[s] = MakeChip(s);
        }

        Chip MakeChip(PlayerSlot s)
        {
            // karta gracza: biała, z kolorowym portretem ziemniaka, imieniem i wynikiem
            var bg = UIKit.Panel(chipsRow, "Karta " + s.Name, new Color(1f, 1f, 1f, 0.94f));
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 250;
            Soft(bg);
            var chip = new Chip { Bg = bg };

            var portrait = UIKit.Panel(bg.transform, "Portret", s.Color);
            UIKit.Stretch(portrait.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(6, 6), new Vector2(76, -6));
            chip.Portrait = portrait;
            // środek portretu w kolorze skórki, ramka w kolorze gracza
            chip.SkinColor = s.SkinData.BodyColor(s.Color);
            var inner = UIKit.Panel(portrait.transform, "Skorka", chip.SkinColor);
            UIKit.Stretch(inner.rectTransform, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
            chip.Inner = inner;
            for (int e = -1; e <= 1; e += 2)
            {
                var eye = UIKit.Panel(inner.transform, "Oko", Color.white, false);
                eye.sprite = UIKit.Circle;
                UIKit.Place(eye.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(e * 13f, 0), new Vector2(20, 24));
                var pupil = UIKit.Panel(eye.transform, "Zrenica", Shapes.Hex("#1E1430"), false);
                pupil.sprite = UIKit.Circle;
                UIKit.Place(pupil.rectTransform, new Vector2(0.5f, 0.4f), Vector2.zero, new Vector2(11, 13));
            }

            chip.Name = UIKit.Label(bg.transform, s.Name, 22, UIKit.Dark, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Stretch(chip.Name.rectTransform, Vector2.zero, Vector2.one, new Vector2(86, 6), new Vector2(-10, -8));
            chip.Points = UIKit.Label(bg.transform, "", 36, Color.Lerp(s.Color, Color.black, 0.12f), TextAnchor.LowerRight, FontStyle.Bold);
            UIKit.Stretch(chip.Points.rectTransform, Vector2.zero, Vector2.one, new Vector2(86, 4), new Vector2(-12, -6));
            chip.Stat = UIKit.Label(bg.transform, "", 20, UIKit.Soft, TextAnchor.LowerLeft);
            UIKit.Stretch(chip.Stat.rectTransform, Vector2.zero, Vector2.one, new Vector2(86, 8), new Vector2(-70, -6));
            return chip;
        }

        static void Soft(Graphic g)
        {
            var sh = g.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.1f, 0.1f, 0.3f, 0.22f);
            sh.effectDistance = new Vector2(0, -6);
        }

        public void Hide()
        {
            hudCanvas.gameObject.SetActive(false);
        }

        public void UpdateHud(Minigame mg, List<Potato> potatoes)
        {
            if (hudCanvas.gameObject.activeSelf)
            {
                string extra = mg.Running ? mg.HudInfo().Trim() : "";
                if (extra.StartsWith("·")) extra = extra.Substring(1).Trim();
                info.text = extra.Length > 0 ? extra : mg.Goal;
                foreach (var p in potatoes)
                {
                    Chip c;
                    if (p == null || !chips.TryGetValue(p.Slot, out c)) continue;
                    c.Points.text = p.Slot.Points.ToString();
                    c.Stat.text = mg.StatFor(p);
                    var col = p.Slot.Color;
                    c.Portrait.color = p.Eliminated ? Color.Lerp(col, Color.gray, 0.75f) : col;
                    if (c.Inner != null) c.Inner.color = p.Eliminated ? Color.Lerp(c.SkinColor, Color.gray, 0.75f) : c.SkinColor;
                }
                bool timed = mg.TimeLeft < 999f && mg.TimeLimit < 999f;
                timer.gameObject.SetActive(timed);
                if (timed)
                {
                    timerText.text = Mathf.CeilToInt(mg.TimeLeft).ToString();
                    timerRing.fillAmount = Mathf.Clamp01(mg.TimeLeft / Mathf.Max(1f, mg.TimeLimit));
                    timerRing.color = mg.TimeLeft <= 10f ? Shapes.Hex("#FF5A4F") : Shapes.Hex("#7FE3FF");
                }
            }
            UpdateLabels(potatoes);
        }

        public void UpdateLabels(List<Potato> potatoes)
        {
            labelCanvas.gameObject.SetActive(true);
            var cam = GameManager.Instance.Camera.Cam;
            // usuń podpisy znikniętych ziemniaków
            var dead = new List<Potato>();
            foreach (var kv in labels) if (kv.Key == null || !potatoes.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var d in dead)
            {
                if (labels[d] != null) Destroy(labels[d].gameObject);
                labels.Remove(d);
            }
            if (meters.Count > 0)
            {
                dead.Clear();
                foreach (var kv in meters) if (kv.Key == null || !potatoes.Contains(kv.Key)) dead.Add(kv.Key);
                foreach (var d in dead) RemoveMeter(d);
            }
            foreach (var p in potatoes)
            {
                if (p == null) continue;
                Text t;
                if (!labels.TryGetValue(p, out t))
                {
                    t = UIKit.Label(labelRoot, p.Slot.Name, 26, p.Slot.Color, TextAnchor.MiddleCenter, FontStyle.Bold);
                    t.rectTransform.sizeDelta = new Vector2(320, 40);
                    UIKit.Outline(t, new Color(1, 1, 1, 0.95f), 2.5f);
                    labels[p] = t;
                }
                Vector3 sp = cam.WorldToScreenPoint(p.HeadAnchor.position + Vector3.up * 0.7f);
                bool visible = !p.Eliminated && sp.z > 0;
                t.gameObject.SetActive(visible);
                Meter m;
                bool hasMeter = meters.TryGetValue(p, out m);
                if (hasMeter) m.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(labelRoot, sp, null, out local);
                t.rectTransform.anchoredPosition = local;
                if (hasMeter) PlaceMeter(m, local);
            }
        }

        // ---------------- paski nad głowami ----------------

        /// <summary>
        /// Ustawia pasek nad graczem (0..1). Tworzy go przy pierwszym wywołaniu. Kolor wypełnienia przechodzi
        /// zieleń → żółć → czerwień; powyżej 75% pasek drży i pulsuje (presja!).
        /// </summary>
        public void SetMeter(Potato p, float value01, string caption = null)
        {
            if (p == null) return;
            Meter m;
            if (!meters.TryGetValue(p, out m))
            {
                m = MakeMeter(p);
                meters[p] = m;
            }
            float v = Mathf.Clamp01(value01);
            if (v > m.Target + 0.004f) m.Pulse = Mathf.Min(1f, m.Pulse + 0.25f);
            m.Target = v;
            if (caption != null) m.Caption = caption;
        }

        public void ClearMeters()
        {
            foreach (var kv in meters) if (kv.Value != null && kv.Value.Root != null) Destroy(kv.Value.Root.gameObject);
            meters.Clear();
        }

        void RemoveMeter(Potato p)
        {
            Meter m;
            if (!meters.TryGetValue(p, out m)) return;
            if (m.Root != null) Destroy(m.Root.gameObject);
            meters.Remove(p);
        }

        Meter MakeMeter(Potato p)
        {
            var m = new Meter();
            m.Root = UIKit.Rect("Pasek " + p.Slot.Name, labelRoot);
            m.Root.sizeDelta = new Vector2(132, 20);
            m.Frame = UIKit.Panel(m.Root, "Rama", new Color(0.12f, 0.08f, 0.16f, 0.88f));
            UIKit.Fill(m.Frame.rectTransform);
            var inner = UIKit.Rect("Wnetrze", m.Root);
            UIKit.Stretch(inner, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
            m.FillImage = UIKit.Panel(inner, "Wypelnienie", Shapes.Hex("#6BD66B"));
            m.Fill = m.FillImage.rectTransform;
            UIKit.Stretch(m.Fill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            m.Value = UIKit.Label(m.Root, "", 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Fill(m.Value.rectTransform);
            UIKit.Outline(m.Value, new Color(0.1f, 0.05f, 0.1f, 0.9f), 1.5f);
            return m;
        }

        static readonly Color MeterCool = new Color(0.42f, 0.84f, 0.42f), MeterWarm = new Color(1f, 0.82f, 0.25f), MeterHot = new Color(1f, 0.33f, 0.2f);

        void PlaceMeter(Meter m, Vector2 labelPos)
        {
            float dt = Time.unscaledDeltaTime;
            // szybko w górę (czytelny skok zagrożenia), wolniej w dół
            m.Shown = Mathf.MoveTowards(m.Shown, m.Target, dt * (m.Target > m.Shown ? 3f : 1.2f));
            m.Pulse = Mathf.MoveTowards(m.Pulse, 0f, dt * 2.5f);
            float v = m.Shown;
            m.Fill.anchorMax = new Vector2(v, 1f);
            m.FillImage.color = v < 0.5f ? Color.Lerp(MeterCool, MeterWarm, v * 2f) : Color.Lerp(MeterWarm, MeterHot, (v - 0.5f) * 2f);
            float danger = Mathf.Clamp01((v - 0.75f) / 0.25f);
            Vector2 jitter = danger > 0f
                ? new Vector2(Mathf.Sin(Time.unscaledTime * 61f), Mathf.Cos(Time.unscaledTime * 47f)) * (1.5f + 2.5f * danger)
                : Vector2.zero;
            float s = 1f + 0.12f * m.Pulse + 0.06f * danger * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 14f));
            m.Root.localScale = new Vector3(s, s, 1f);
            m.Root.anchoredPosition = labelPos + new Vector2(0f, -30f) + jitter;
            m.Frame.color = Color.Lerp(new Color(0.12f, 0.08f, 0.16f, 0.88f), new Color(0.55f, 0.08f, 0.05f, 0.95f), danger);
            m.Value.text = (string.IsNullOrEmpty(m.Caption) ? "" : m.Caption + " ") + Mathf.RoundToInt(v * 100f) + "%";
        }

        public void HideLabels()
        {
            labelCanvas.gameObject.SetActive(false);
        }

        public void ClearLabels()
        {
            foreach (var kv in labels) if (kv.Value != null) Destroy(kv.Value.gameObject);
            labels.Clear();
            ClearMeters();
        }

        public void Countdown(string text)
        {
            countdown.text = text;
            countdownTime = 0f;
        }

        public void Toast(string text, Color color)
        {
            var t = UIKit.Label(toastRoot, text, 40, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Outline(t, Color.white, 3f);
            var ts = t.gameObject.AddComponent<Shadow>();
            ts.effectColor = new Color(0.1f, 0.1f, 0.3f, 0.3f);
            ts.effectDistance = new Vector2(0, -4);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
            toasts.Add(new ToastItem { Text = t });
            while (toasts.Count > 4)
            {
                Destroy(toasts[0].Text.gameObject);
                toasts.RemoveAt(0);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (!string.IsNullOrEmpty(countdown.text))
            {
                countdownTime += dt;
                float s = 1f + Mathf.Max(0f, 0.6f - countdownTime * 3f);
                countdown.transform.localScale = new Vector3(s, s, 1);
                var c = countdown.color;
                c.a = Mathf.Clamp01(1.4f - countdownTime * 1.6f);
                countdown.color = c;
                if (countdownTime > 1f) countdown.text = "";
            }
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                t.Age += dt;
                var c = t.Text.color;
                c.a = Mathf.Clamp01(3f - t.Age);
                t.Text.color = c;
                if (t.Age > 3f)
                {
                    Destroy(t.Text.gameObject);
                    toasts.RemoveAt(i);
                }
            }
        }
    }
}
