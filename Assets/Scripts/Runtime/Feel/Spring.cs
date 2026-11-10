using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Tłumiona sprężyna (oscylator harmoniczny) niezależna od FPS. Struktura, zero alokacji.
    /// Fundament całego proceduralnego ruchu: squash &amp; stretch, przechył, wgniecenia, odrzut kamery.
    /// <para>frequency – ile pełnych drgań na sekundę (Hz); damping – współczynnik tłumienia ζ
    /// (1 = krytyczne, bez przestrzału; 0.3 = galaretka z 2–3 odbiciami).</para>
    /// </summary>
    public struct Spring
    {
        public float Value;
        public float Velocity;

        /// <summary>
        /// Krok semi-implicit Euler z pod-krokami ≤ 1/240 s. Stabilny od 20 do 360 FPS
        /// i przy skoku dt (ładowanie mapy), bo dt jest obcinane do 0.1 s.
        /// </summary>
        public void Step(float target, float frequency, float damping, float dt)
        {
            if (dt <= 0f) return;
            if (dt > 0.1f) dt = 0.1f;
            float omega = 6.2831853f * frequency;
            float k = omega * omega;
            float c = 2f * damping * omega;
            int n = Mathf.Clamp(Mathf.CeilToInt(dt * 240f), 1, 24);
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                Velocity += (-c * Velocity - k * (Value - target)) * h;
                Value += Velocity * h;
            }
        }

        /// <summary>Uderzenie prędkością. Amplituda wychylenia ≈ v / (2π·f).</summary>
        public void Kick(float velocity) { Velocity += velocity; }

        /// <summary>Natychmiastowe wychylenie (widoczne już w klatce hit-stopu), bez prędkości.</summary>
        public void Set(float value) { Value = value; Velocity = 0f; }

        public bool Resting(float eps = 0.0005f) { return Mathf.Abs(Value) < eps && Mathf.Abs(Velocity) < eps * 20f; }
        public void Reset() { Value = 0f; Velocity = 0f; }
    }

    /// <summary>Sprężyna wektorowa (3 niezależne osie o tych samych parametrach).</summary>
    public struct Spring3
    {
        public Vector3 Value;
        public Vector3 Velocity;

        public void Step(Vector3 target, float frequency, float damping, float dt)
        {
            if (dt <= 0f) return;
            if (dt > 0.1f) dt = 0.1f;
            float omega = 6.2831853f * frequency;
            float k = omega * omega;
            float c = 2f * damping * omega;
            int n = Mathf.Clamp(Mathf.CeilToInt(dt * 240f), 1, 24);
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                Velocity += (-c * Velocity - k * (Value - target)) * h;
                Value += Velocity * h;
            }
        }

        public void Kick(Vector3 velocity) { Velocity += velocity; }
        public void Reset() { Value = Vector3.zero; Velocity = Vector3.zero; }
    }
}
