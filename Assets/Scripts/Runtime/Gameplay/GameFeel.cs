using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Tłumiona sprężyna (oscylator harmoniczny) niezależna od liczby klatek. Struktura, zero alokacji.
    /// Podstawa całego "soczystego" ruchu: zgniatanie, wgniecenia, odrzut kamery, puls FOV.
    /// frequency = ile drgań na sekundę, damping: 1 = bez przestrzału, 0.3 = galaretka.
    /// </summary>
    public struct Spring
    {
        public float Value;
        public float Velocity;

        // Krok semi-implicit Euler z podziałem na pod-kroki ≤ 1/120 s: stabilny przy 30 i 240 FPS,
        // a przy nagłym skoku dt (ładowanie mapy) nie "wybucha".
        public void Step(float target, float frequency, float damping, float dt)
        {
            if (dt <= 0f) return;
            if (dt > 0.1f) dt = 0.1f;
            float omega = 6.2831853f * frequency;
            float k = omega * omega;
            float c = 2f * damping * omega;
            int n = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 12);
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                Velocity += (-c * Velocity - k * (Value - target)) * h;
                Value += Velocity * h;
            }
        }

        public void Kick(float velocity) { Velocity += velocity; }
        public void Reset() { Value = 0f; Velocity = 0f; }
    }

    /// <summary>
    /// Sprzężenie zwrotne trafień: hit-stop, kamera (trauma + odrzut + puls FOV), puls post-processu.
    /// Jeden punkt wejścia, żeby wszystkie minigry "czuły się" tak samo.
    /// </summary>
    public static class GameFeel
    {
        // ---------------- strojenie (wartości sprawdzone na 60 FPS, wszystko w czasie rzeczywistym) ----------------
        /// <summary>Hit-stop za zryw w przeciwnika: 0.055 s ≈ 3,3 klatki – próg, od którego mózg "czuje" kontakt.</summary>
        public static float DashHitStop = 0.055f;
        /// <summary>Dodatkowy hit-stop za pełną moc trafienia (power01 = 1).</summary>
        public static float DashHitStopPerPower = 0.045f;
        /// <summary>Skala czasu w trakcie hit-stopu. Nie 0: cząsteczki i fizyka "pełzną", obraz nie wygląda na zawieszony.</summary>
        public static float HitStopTimeScale = 0.03f;
        /// <summary>Budżet hit-stopu: w ciągu tylu sekund po trafieniu kolejne dostają coraz mniej (chaos 6 graczy ≠ pokaz slajdów).</summary>
        public static float HitStopBudgetWindow = 0.6f;

        static Runner runner;
        static float stopLeft;
        static float lastStopTime = -10f;
        static int chain;

        /// <summary>Czy gra jest właśnie w hit-stopie (np. ofiara drży, a reszta świata stoi).</summary>
        public static bool InHitStop { get { return stopLeft > 0f; } }

        /// <summary>
        /// Krótkie zatrzymanie czasu. Nie nadpisuje pauzy (timeScale 0) ani cudzego slow-motion.
        /// Kolejne trafienia w krótkim oknie dostają malejący czas (0.6^n): chaos się nie "zacina".
        /// </summary>
        public static void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            Ensure();
            float now = Time.unscaledTime;
            chain = now - lastStopTime < HitStopBudgetWindow ? chain + 1 : 0;
            lastStopTime = now;
            seconds *= Mathf.Pow(0.6f, chain);
            if (seconds < 0.016f) return;

            if (stopLeft <= 0f)
            {
                // pauza albo inny efekt czasu ma pierwszeństwo
                if (Time.timeScale < 0.999f) return;
                Time.timeScale = HitStopTimeScale;
            }
            stopLeft = Mathf.Max(stopLeft, seconds);
        }

        /// <summary>
        /// Trafienie zrywem: hit-stop, kamera, puls obrazu i efekt cząsteczkowy w punkcie kontaktu.
        /// power01: 0 = muśnięcie, 1 = pełny zryw w biegu.
        /// </summary>
        public static void DashHit(Potato attacker, Potato victim, Vector3 dir, float power01)
        {
            power01 = Mathf.Clamp01(power01);
            HitStop(DashHitStop + DashHitStopPerPower * power01);
            var cam = Fx.Camera;
            if (cam != null)
            {
                cam.AddTrauma(0.3f + 0.25f * power01);
                cam.Kick(dir, 0.35f + 0.35f * power01);
                cam.Punch(-1.2f - 1.6f * power01);
            }
            Look.Punch(0.35f + 0.4f * power01);
            var contact = Vector3.Lerp(attacker.transform.position, victim.transform.position, 0.55f) + Vector3.up * 0.35f;
            Fx.Impact(victim.transform.parent, contact, attacker.Color, dir, power01);
        }

        /// <summary>Uderzenie z otoczenia (belka, wahadło, garnek, wybuch). Bez efektu cząsteczkowego – ma go minigra.</summary>
        public static void EnvironmentHit(Vector3 dir, float power01)
        {
            power01 = Mathf.Clamp01(power01);
            if (power01 < 0.35f) return;
            HitStop(0.03f + 0.04f * power01);
            if (Fx.Camera != null) Fx.Camera.Kick(dir, 0.25f * power01);
            Look.Punch(0.25f * power01);
        }

        internal static void Ensure()
        {
            if (runner != null) return;
            var go = new GameObject("GameFeel");
            go.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }

        /// <summary>Zegar hit-stopu i pulsu obrazu w czasie rzeczywistym (działa, gdy timeScale ≈ 0).</summary>
        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                float dt = Time.unscaledDeltaTime;
                if (stopLeft > 0f)
                {
                    // ktoś inny przejął czas (pauza w trakcie hit-stopu) – oddajemy kontrolę bez przywracania
                    if (Mathf.Abs(Time.timeScale - HitStopTimeScale) > 0.0001f) stopLeft = 0f;
                    else
                    {
                        stopLeft -= dt;
                        if (stopLeft <= 0f) { stopLeft = 0f; Time.timeScale = 1f; }
                    }
                }
                Look.TickPunch(dt);
            }

            void OnDisable()
            {
                if (stopLeft > 0f && Mathf.Abs(Time.timeScale - HitStopTimeScale) < 0.0001f) Time.timeScale = 1f;
                stopLeft = 0f;
            }
        }
    }
}
