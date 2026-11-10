using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Jedyny właściciel <see cref="Time.timeScale"/>. Składa trzy warstwy:
    /// <list type="number">
    /// <item><b>Pauza</b> – twarde 0.</item>
    /// <item><b>Hit stop</b> – mikro-pauza przy trafieniu (2–7 klatek), skala 0.02, z budżetem łańcucha.</item>
    /// <item><b>Time dilation</b> – stos żądań spowolnienia z krzywą wejścia/wyjścia; wygrywa najwolniejsze.</item>
    /// </list>
    /// Wynik = pauza ? 0 : min(hitStop, dilation). <see cref="Time.fixedDeltaTime"/> skaluje się razem
    /// z czasem, więc fizyka w slow-motion jest płynna (tyle samo kroków na sekundę rzeczywistą).
    /// Wszystko liczone w czasie rzeczywistym (unscaled).
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class TimeDirector : MonoBehaviour
    {
        // ---------------- strojenie ----------------
        /// <summary>Krok fizyki przy skali 1. 1/100 s: kolizje 6 ziemniaków i zrywów bez "przelatywania".</summary>
        public static float BaseFixedDelta = 0.01f;
        /// <summary>Skala czasu w hit-stopie. Nie 0: cząsteczki i fizyka pełzną, obraz nie wygląda na zawieszony.</summary>
        public static float HitStopScale = 0.02f;
        /// <summary>Okno łańcucha: kolejne trafienia w tym czasie dostają 0.65^n czasu (chaos ≠ pokaz slajdów).</summary>
        public static float ChainWindow = 0.5f;
        public static float ChainFalloff = 0.65f;
        /// <summary>Twardy limit łącznego hit-stopu na sekundę rzeczywistą.</summary>
        public static float MaxStopPerSecond = 0.22f;

        static TimeDirector instance;

        bool paused;
        float stopLeft;
        float lastStopAt = -10f;
        int chain;
        float budget; // zużyty hit-stop w bieżącym oknie 1 s
        float budgetWindow;

        struct Dilation
        {
            public int Id;
            public float Scale, EaseIn, Hold, EaseOut, Age;
            public bool Manual; // trwa aż do Release
        }

        readonly List<Dilation> dilations = new List<Dilation>(8);
        int nextId = 1;
        float applied = 1f;

        /// <summary>Aktualna skala świata (bez pauzy).</summary>
        public static float WorldScale { get { return instance != null ? instance.applied : 1f; } }
        public static bool InHitStop { get { return instance != null && instance.stopLeft > 0f; } }
        public static bool Paused { get { return instance != null && instance.paused; } }

        public static TimeDirector Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("TimeDirector");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<TimeDirector>();
            return instance;
        }

        // ---------------- API ----------------

        public static void SetPaused(bool on)
        {
            Ensure().paused = on;
            instance.Apply();
        }

        /// <summary>Kasuje hit-stop i wszystkie spowolnienia (np. koniec meczu, wyjście do menu).</summary>
        public static void ResetAll()
        {
            var t = Ensure();
            t.paused = false;
            t.stopLeft = 0f;
            t.dilations.Clear();
            t.Apply();
        }

        /// <summary>
        /// Mikro-pauza. seconds – czas rzeczywisty (0.05 s ≈ 3 klatki przy 60 FPS – próg, od którego
        /// mózg "czuje" kontakt; 0.1 s = ciężki cios). Zwraca faktycznie przyznany czas.
        /// </summary>
        public static float HitStop(float seconds)
        {
            var t = Ensure();
            if (seconds <= 0f || t.paused) return 0f;
            float now = Time.unscaledTime;
            t.chain = now - t.lastStopAt < ChainWindow ? t.chain + 1 : 0;
            t.lastStopAt = now;
            seconds *= Mathf.Pow(ChainFalloff, t.chain);
            seconds = Mathf.Min(seconds, Mathf.Max(0f, MaxStopPerSecond - t.budget));
            if (seconds < 0.012f) return 0f;
            t.budget += seconds;
            t.stopLeft = Mathf.Max(t.stopLeft, seconds);
            t.Apply();
            return seconds;
        }

        /// <summary>
        /// Spowolnienie: wejście (easeIn), utrzymanie (hold), powrót (easeOut) – sekundy rzeczywiste.
        /// Przykłady: ostatni nokaut 0.25× / 0.06 / 0.5 / 0.45; bliski unik 0.55× / 0.03 / 0.12 / 0.25.
        /// </summary>
        public static int Dilate(float scale, float easeIn, float hold, float easeOut)
        {
            var t = Ensure();
            var d = new Dilation { Id = t.nextId++, Scale = Mathf.Clamp(scale, 0.05f, 1f), EaseIn = Mathf.Max(0.001f, easeIn), Hold = hold, EaseOut = Mathf.Max(0.001f, easeOut) };
            t.dilations.Add(d);
            return d.Id;
        }

        /// <summary>Spowolnienie trwające do <see cref="Release"/> (np. "bullet time" podczas ujęcia finałowego).</summary>
        public static int DilateUntilReleased(float scale, float easeIn)
        {
            var t = Ensure();
            var d = new Dilation { Id = t.nextId++, Scale = Mathf.Clamp(scale, 0.05f, 1f), EaseIn = Mathf.Max(0.001f, easeIn), Hold = float.MaxValue, EaseOut = 0.35f, Manual = true };
            t.dilations.Add(d);
            return d.Id;
        }

        public static void Release(int id, float easeOut = 0.35f)
        {
            if (instance == null) return;
            var list = instance.dilations;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id != id) continue;
                var d = list[i];
                d.Manual = false;
                d.EaseOut = Mathf.Max(0.001f, easeOut);
                d.Hold = Mathf.Max(0f, d.Age - d.EaseIn); // od teraz zaczyna się powrót
                list[i] = d;
                return;
            }
        }

        // ---------------- pętla ----------------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.1f) dt = 0.1f;

            budgetWindow += dt;
            if (budgetWindow >= 1f) { budgetWindow = 0f; budget = 0f; }

            if (!paused)
            {
                if (stopLeft > 0f) stopLeft = Mathf.Max(0f, stopLeft - dt);
                for (int i = dilations.Count - 1; i >= 0; i--)
                {
                    var d = dilations[i];
                    d.Age += dt;
                    if (!d.Manual && d.Age > d.EaseIn + d.Hold + d.EaseOut) { dilations.RemoveAt(i); continue; }
                    dilations[i] = d;
                }
            }
            Apply();
        }

        static float Evaluate(Dilation d)
        {
            float a = d.Age;
            float w;
            if (a < d.EaseIn) w = Smooth(a / d.EaseIn);
            else if (d.Manual || a < d.EaseIn + d.Hold) w = 1f;
            else w = 1f - Smooth((a - d.EaseIn - d.Hold) / d.EaseOut);
            return Mathf.Lerp(1f, d.Scale, w);
        }

        // ease-out cubic na wejściu, ease-in-out przy powrocie: "zassanie" czasu jest szybkie, powrót miękki
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        void Apply()
        {
            float s = 1f;
            for (int i = 0; i < dilations.Count; i++) s = Mathf.Min(s, Evaluate(dilations[i]));
            if (stopLeft > 0f) s = Mathf.Min(s, HitStopScale);
            applied = s;
            float final = paused ? 0f : s;
            if (!Mathf.Approximately(Time.timeScale, final)) Time.timeScale = final;
            // fizyka: tyle samo kroków na sekundę rzeczywistą (płynny slow-motion), min. 1e-4 (limit Unity)
            float fixedDt = BaseFixedDelta * Mathf.Max(s, 0.01f);
            if (!Mathf.Approximately(Time.fixedDeltaTime, fixedDt)) Time.fixedDeltaTime = fixedDt;
            if (AudioManager.Instance != null) AudioManager.Instance.SetTimeWarp(paused ? 1f : s);
        }

        void OnDestroy()
        {
            if (instance != this) return;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = BaseFixedDelta;
            instance = null;
        }
    }
}
