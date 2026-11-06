using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PotatoParty
{
    /// <summary>
    /// Spokojny ekran ładowania przed każdą minigrą: numer i nazwa minigry, mapa, pasek postępu,
    /// a na dolnym panelu jedna krótka wskazówka do tej minigry. Pod spodem budujemy arenę.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        CanvasGroup group;
        RectTransform barFill;
        Text round, title, map, tip;
        public bool Busy { get; private set; }

        public static LoadingScreen Create(Transform parent)
        {
            var go = new GameObject("Ladowanie");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<LoadingScreen>();
            l.Build();
            return l;
        }

        void Build()
        {
            var canvas = UIKit.CreateCanvas("Ekran ladowania", 60, transform);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            var root = UIKit.Fill(UIKit.Rect("Korzen", canvas.transform));

            var bg = UIKit.Panel(root, "Tlo", Shapes.Hex("#4FB6FF"), false);
            UIKit.Fill(bg.rectTransform);

            round = UIKit.Label(root, "", 34, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(round.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1400, 60));

            title = UIKit.Label(root, "", 110, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1600, 150));
            var titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0.1f, 0.2f, 0.5f, 0.45f);
            titleShadow.effectDistance = new Vector2(0, -7);

            map = UIKit.Label(root, "", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(map.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(1400, 56));

            // pasek postępu
            var bar = UIKit.Panel(root, "Pasek", new Color(1f, 1f, 1f, 0.3f));
            UIKit.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(900, 40));
            barFill = UIKit.Panel(bar.transform, "Postep", Color.white).rectTransform;
            UIKit.Stretch(barFill, Vector2.zero, new Vector2(0f, 1f), new Vector2(5, 5), new Vector2(-5, -5));

            // dolny panel ze wskazówką
            var panel = UIKit.Panel(root, "Panel wskazowki", new Color(0.12f, 0.2f, 0.45f, 0.55f), false);
            UIKit.Stretch(panel.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0, 130));
            tip = UIKit.Label(panel.transform, "", 30, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(tip.rectTransform, Vector2.zero, Vector2.one, new Vector2(80, 14), new Vector2(-80, -14));

            canvas.gameObject.SetActive(false);
        }

        void SetProgress(float p)
        {
            barFill.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
        }

        /// <summary>
        /// Pokazuje ekran, w połowie paska wywołuje <paramref name="build"/> (budowa areny), potem znika.
        /// </summary>
        public IEnumerator Run(int roundNo, int total, Minigame mg, MapTheme theme, Action build)
        {
            Busy = true;
            var canvasGo = group.gameObject;
            canvasGo.SetActive(true);
            round.text = "Minigra " + roundNo + " z " + total + (roundNo == total && total > 1 ? "  ·  FINAŁ" : "");
            title.text = mg.Title;
            map.text = theme.Name;
            tip.text = "<color=#FFD23F>Wskazówka:</color> " + mg.Tip;
            SetProgress(0f);

            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / 0.25f;
                yield return null;
            }
            group.alpha = 1f;

            float shown = 0f;
            float target = UnityEngine.Random.Range(0.35f, 0.5f);
            while (shown < target)
            {
                shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 0.9f);
                SetProgress(shown);
                yield return null;
            }

            // budowa świata pod zasłoną
            yield return null;
            build();
            yield return null;

            // dajemy chwilę na przeczytanie wskazówki
            float minEnd = Time.unscaledTime + 1.6f;
            while (shown < 1f || Time.unscaledTime < minEnd)
            {
                shown = Mathf.MoveTowards(shown, 1f, Time.unscaledDeltaTime * 0.7f);
                SetProgress(shown);
                yield return null;
            }

            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / 0.3f;
                yield return null;
            }
            group.alpha = 0f;
            canvasGo.SetActive(false);
            Busy = false;
        }

        public void Hide()
        {
            StopAllCoroutines();
            group.gameObject.SetActive(false);
            Busy = false;
        }
    }
}
