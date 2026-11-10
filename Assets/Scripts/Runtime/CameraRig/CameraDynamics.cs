using Unity.Cinemachine;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Rozszerzenie potoku Cinemachine: kamera "czuje" prędkość i ciosy.
    /// Działa w etapie <c>Finalize</c>, po kompozytorze i po listenerze impulsów:
    /// <list type="bullet">
    /// <item><b>FOV prędkości</b>: +0…+6° gdy grupa biegnie 4→14 m/s (efekt "pędu").</item>
    /// <item><b>Dutch</b>: przechył do ±2.5° proporcjonalny do bocznego ruchu kamery (sprężyna 0.9 Hz, ζ 0.7).</item>
    /// <item><b>Oddech z ręki</b>: szum Perlina 0.3 Hz, 0.12° w spoczynku → 0.3° w pędzie.</item>
    /// <item><b>Odrzut</b> (Kick) i <b>puls FOV</b> (Punch): sprężyny z przestrzałem, czytelny kierunek ciosu.</item>
    /// </list>
    /// </summary>
    public class CameraDynamics : CinemachineExtension
    {
        [Header("Prędkość → obiektyw")]
        public float SpeedForMin = 4f;        // m/s: poniżej brak efektu
        public float SpeedForMax = 14f;       // m/s: pełny efekt
        public float SpeedFov = 6f;           // ° dodatkowego FOV przy pełnej prędkości
        public float SpeedResponse = 2.5f;    // 1/s wygładzania

        [Header("Dutch (przechył)")]
        public float DutchPerSpeed = 0.22f;   // ° na m/s bocznego ruchu kamery
        public float DutchMax = 2.5f;
        public float DutchFrequency = 0.9f;
        public float DutchDamping = 0.7f;

        [Header("Oddech z ręki")]
        public float HandheldAmplitude = 0.12f; // °
        public float HandheldSpeedBoost = 0.18f;
        public float HandheldFrequency = 0.3f;  // Hz

        [Header("Odrzut i puls FOV")]
        public float KickFrequency = 6.5f;
        public float KickDamping = 0.45f;
        public float PunchFrequency = 5f;
        public float PunchDamping = 0.5f;

        [System.NonSerialized] public CinematicCamera Owner;
        [System.NonSerialized] public float BaseFov = 38f;

        /// <summary>0..1 – jak szybko porusza się grupa (do dystansu, motion bluru i FOV).</summary>
        public float SpeedFactor { get; private set; }

        Spring dutch, fovPunch;
        Spring3 kick;
        Vector3 lastFocus;
        float noiseT;
        bool hasLast;

        public void Kick(Vector3 worldDir, float strength)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 1e-4f) return;
            worldDir.Normalize();
            float v = strength * 6.2831853f * KickFrequency; // amplituda ≈ v / (2πf)
            kick.Kick(new Vector3(worldDir.x * v, -0.35f * v, worldDir.z * v));
        }

        public void Punch(float degrees) { fovPunch.Kick(degrees * 6.2831853f * PunchFrequency); }

        public void ResetState()
        {
            dutch.Reset();
            fovPunch.Reset();
            kick.Reset();
            hasLast = false;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;
            float dt = Application.isPlaying ? Mathf.Min(Time.unscaledDeltaTime, 0.1f) : 0f;

            // --- prędkość grupy → 0..1
            float speed = Owner != null ? Owner.GroupSpeed : 0f;
            float target = Mathf.Clamp01(Mathf.InverseLerp(SpeedForMin, SpeedForMax, speed));
            SpeedFactor = Mathf.Lerp(SpeedFactor, target, 1f - Mathf.Exp(-dt * SpeedResponse));

            // --- boczny ruch kadru w przestrzeni kamery → dutch
            Vector3 focus = Owner != null ? Owner.FocusPoint : state.RawPosition;
            float lateral = 0f;
            if (hasLast && dt > 0f)
            {
                Vector3 right = state.RawOrientation * Vector3.right;
                lateral = Vector3.Dot(focus - lastFocus, right) / dt;
            }
            lastFocus = focus;
            hasLast = true;
            dutch.Step(Mathf.Clamp(-lateral * DutchPerSpeed, -DutchMax, DutchMax), DutchFrequency, DutchDamping, dt);
            kick.Step(Vector3.zero, KickFrequency, KickDamping, dt);
            fovPunch.Step(0f, PunchFrequency, PunchDamping, dt);

            // --- oddech z ręki (dwa niezależne kanały Perlina)
            noiseT += dt * HandheldFrequency;
            if (noiseT > 1000f) noiseT -= 1000f;
            float amp = HandheldAmplitude + HandheldSpeedBoost * SpeedFactor;
            float nPitch = (Mathf.PerlinNoise(noiseT, 0.31f) * 2f - 1f) * amp;
            float nYaw = (Mathf.PerlinNoise(noiseT, 5.77f) * 2f - 1f) * amp;

            var lens = state.Lens;
            lens.FieldOfView = Mathf.Clamp(BaseFov + SpeedFov * SpeedFactor + fovPunch.Value, 10f, 90f);
            lens.Dutch = dutch.Value;
            state.Lens = lens;
            state.PositionCorrection += kick.Value;
            state.OrientationCorrection = state.OrientationCorrection * Quaternion.Euler(nPitch, nYaw, 0f);
        }
    }
}
