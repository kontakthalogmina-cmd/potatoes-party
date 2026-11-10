using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PotatoParty
{
    /// <summary>Koło fortuny losujące mapę przed rundą.</summary>
    public class FortuneWheel : MonoBehaviour
    {
        RectTransform wheel;
        Text header, result;
        GameObject shade;

        public static FortuneWheel Create(RectTransform parent)
        {
            var rt = UIKit.Fill(UIKit.Rect("Kolo fortuny", parent));
            var w = rt.gameObject.AddComponent<FortuneWheel>();
            w.Build(rt);
            w.Hide();
            return w;
        }

        void Build(RectTransform rt)
        {
            var sh = UIKit.Panel(rt, "Przyciemnienie", UIKit.Shade, false);
            UIKit.Fill(sh.rectTransform);
            shade = sh.gameObject;

            header = UIKit.Label(rt, "", 54, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1600, 140));
            UIKit.Outline(header, UIKit.Dark, 4f);

            var rim = UIKit.Panel(rt, "Obrecz", UIKit.Dark);
            rim.sprite = UIKit.Circle;
            UIKit.Place(rim.rectTransform, new Vector2(0.5f, 0.47f), Vector2.zero, new Vector2(700, 700));
            UIKit.Shadow(rim, 12f, 0.35f);

            var go = new GameObject("Kolo", typeof(RectTransform));
            go.transform.SetParent(rim.transform, false);
            wheel = (RectTransform)go.transform;
            UIKit.Place(wheel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660, 660));
            var img = go.AddComponent<RawImage>();
            img.texture = MakeWheelTexture(512);

            int n = MapTheme.AllIds.Length;
            for (int i = 0; i < n; i++)
            {
                float c = (i + 0.5f) * 360f / n;
                float rad = c * Mathf.Deg2Rad;
                var t = UIKit.Label(wheel, MapTheme.Get(MapTheme.AllIds[i]).Name.Replace(" ", "\n"), 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.rectTransform.sizeDelta = new Vector2(300, 120);
                t.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 190f;
                t.rectTransform.localRotation = Quaternion.Euler(0, 0, -c);
                UIKit.Outline(t, UIKit.Dark, 3f);
            }

            var hub = UIKit.Panel(rim.transform, "Srodek", UIKit.Cream);
            hub.sprite = UIKit.Circle;
            UIKit.Place(hub.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
            var hubT = UIKit.Label(hub.transform, "?", 60, UIKit.Orange, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Fill(hubT.rectTransform);

            // Wskaźnik u góry (obrócony kwadrat)
            var pointer = UIKit.Panel(rim.transform, "Wskaznik", UIKit.Red);
            UIKit.Place(pointer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 6), new Vector2(64, 64));
            pointer.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            UIKit.Outline(pointer, Color.white, 4f);

            result = UIKit.Label(rt, "", 56, UIKit.Yellow, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(result.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(1600, 120));
            UIKit.Outline(result, UIKit.Dark, 4f);
        }

        static Texture2D MakeWheelTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            int n = MapTheme.AllIds.Length;
            float seg = 360f / n;
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d);
                    // kąt mierzony zgodnie z ruchem wskazówek zegara od góry
                    float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                    if (ang < 0) ang += 360f;
                    int i = Mathf.Min(n - 1, Mathf.FloorToInt(ang / seg));
                    Color c = MapTheme.Get(MapTheme.AllIds[i]).WheelColor;
                    // jaśniejsze paski w środku segmentu i białe linie podziału
                    float local = ang - i * seg;
                    float edgeDeg = Mathf.Min(local, seg - local) * Mathf.Deg2Rad * d;
                    if (edgeDeg < 4f) c = Color.white;
                    else if ((Mathf.FloorToInt(d / 22f) % 2) == 0) c = Color.Lerp(c, Color.white, 0.12f);
                    if (d > r - 10f) c = Color.white;
                    c.a = a;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public void Hide() { gameObject.SetActive(false); }

        public IEnumerator Spin(int resultIndex, int round, int total, string minigame)
        {
            gameObject.SetActive(true);
            shade.SetActive(true);
            header.text = "Runda " + round + " z " + total + ": " + minigame + "\n<size=36>Koło fortuny wybiera mapę…</size>";
            result.text = "";
            int n = MapTheme.AllIds.Length;
            float seg = 360f / n;
            float start = wheel.localEulerAngles.z % 360f;
            float target = 360f * 5f + (resultIndex + 0.5f) * seg + Random.Range(-seg * 0.35f, seg * 0.35f);
            float duration = 4.2f;
            float t = 0f;
            int lastTick = 0;
            yield return new WaitForSeconds(0.4f);
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                float angle = Mathf.Lerp(start, target, eased);
                wheel.localRotation = Quaternion.Euler(0, 0, angle);
                int tick = Mathf.FloorToInt(angle / seg);
                if (tick != lastTick) { lastTick = tick; AudioManager.Play(Sfx.Tick, 0.9f, 0.1f); }
                yield return null;
            }
            var map = MapTheme.Get(MapTheme.AllIds[resultIndex]);
            result.text = "Wylosowano: " + map.Name + "!";
            AudioManager.Play(Sfx.Fanfare, 0.8f, 0f);
            yield return new WaitForSeconds(2f);
            Hide();
        }
    }

    /// <summary>Gracz (z najmniejszą liczbą punktów) wybiera mapę na następną rundę.</summary>
    public class MapPicker : MonoBehaviour
    {
        Text header, hint;
        readonly Image[] cards = new Image[3];
        readonly Text[] badges = new Text[3];
        int highlight;
        int chosen = -1;
        float stickCooldown;

        public static MapPicker Create(RectTransform parent)
        {
            var rt = UIKit.Fill(UIKit.Rect("Wybor mapy", parent));
            var m = rt.gameObject.AddComponent<MapPicker>();
            m.Build(rt);
            m.Hide();
            return m;
        }

        void Build(RectTransform rt)
        {
            var sh = UIKit.Panel(rt, "Przyciemnienie", UIKit.Shade, false);
            UIKit.Fill(sh.rectTransform);
            header = UIKit.Label(rt, "", 52, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(1700, 200));
            UIKit.Outline(header, UIKit.Dark, 4f);

            for (int i = 0; i < 3; i++)
            {
                var theme = MapTheme.Get(MapTheme.AllIds[i]);
                int idx = i;
                var b = UIKit.Button(rt, "", theme.WheelColor, () => chosen = idx, 30);
                var brt = (RectTransform)b.transform;
                UIKit.Place(brt, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 560f, -20f), new Vector2(500, 560));
                cards[i] = b.GetComponent<Image>();
                Destroy(b.GetComponent<JuicyButton>()); // skalowaniem kart steruje wybór
                var name = UIKit.Label(brt, theme.Name, 48, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
                UIKit.Stretch(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 24), new Vector2(-24, -40));
                UIKit.Outline(name, UIKit.Dark, 3f);
                var tag = UIKit.Label(brt, theme.Tagline + (theme.Grip < 0.6f ? "\n\nUwaga: bardzo ślisko!" : ""), 32, UIKit.Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(tag.rectTransform, Vector2.zero, Vector2.one, new Vector2(30, 60), new Vector2(-30, -150));
                badges[i] = UIKit.Label(brt, "", 28, Color.white, TextAnchor.LowerCenter, FontStyle.Bold);
                UIKit.Stretch(badges[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 30), new Vector2(-20, -20));
                UIKit.Outline(badges[i], UIKit.Dark, 2f);
            }

            hint = UIKit.Label(rt, "", 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 100), new Vector2(1600, 100));
            UIKit.Outline(hint, UIKit.Dark, 3f);
        }

        public void Hide() { gameObject.SetActive(false); }

        public IEnumerator Pick(PlayerSlot chooser, int round, int total, Minigame mg, System.Action<int> done)
        {
            gameObject.SetActive(true);
            chosen = -1;
            int preferred = System.Array.IndexOf(MapTheme.AllIds, mg.PreferredMap);
            highlight = Mathf.Max(0, preferred);
            string col = ColorUtility.ToHtmlStringRGB(chooser.Color);
            header.text = "Runda " + round + " z " + total + ": " + mg.Title + "\nMapę wybiera <color=#" + col + ">" + chooser.Name + "</color>";
            for (int i = 0; i < 3; i++) badges[i].text = i == preferred ? "★ Polecana do tej minigry" : "";
            hint.text = chooser.IsBot
                ? "Bot się zastanawia…"
                : "Ruch w lewo/prawo, aby wybrać · skok lub akcja, aby zatwierdzić (albo kliknij myszką)";

            float t = 0f;
            float botDecide = Random.Range(1.4f, 2.4f);
            int botChoice = Random.value < 0.6f ? highlight : Random.Range(0, 3);
            while (chosen < 0)
            {
                t += Time.deltaTime;
                if (chooser.IsBot)
                {
                    if (t > botDecide * 0.5f) highlight = botChoice;
                    if (t > botDecide) chosen = botChoice;
                }
                else
                {
                    stickCooldown -= Time.deltaTime;
                    float x = chooser.ReadMove().x;
                    if (Mathf.Abs(x) > 0.5f && stickCooldown <= 0f)
                    {
                        highlight = (highlight + (x > 0 ? 1 : -1) + 3) % 3;
                        stickCooldown = 0.25f;
                        AudioManager.Play(Sfx.Click, 0.6f);
                    }
                    if (Mathf.Abs(x) < 0.2f) stickCooldown = 0f;
                    if (t > 0.4f && (chooser.ReadJump() || chooser.ReadAction())) chosen = highlight;
                    if (t > 20f) chosen = highlight;
                }
                for (int i = 0; i < 3; i++)
                {
                    float s = i == highlight ? 1.08f + Mathf.Sin(Time.time * 6f) * 0.015f : 0.94f;
                    cards[i].transform.localScale = Vector3.Lerp(cards[i].transform.localScale, new Vector3(s, s, 1), Time.deltaTime * 12f);
                }
                yield return null;
            }
            highlight = chosen;
            AudioManager.Play(Sfx.Join, 1f);
            hint.text = "Wybrano: " + MapTheme.Get(MapTheme.AllIds[chosen]).Name + "!";
            for (int k = 0; k < 30; k++)
            {
                for (int i = 0; i < 3; i++)
                {
                    float s = i == chosen ? 1.12f : 0.85f;
                    cards[i].transform.localScale = Vector3.Lerp(cards[i].transform.localScale, new Vector3(s, s, 1), 0.2f);
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            done(chosen);
            Hide();
        }
    }
}
