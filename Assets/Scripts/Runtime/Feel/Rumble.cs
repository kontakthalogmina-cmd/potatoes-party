using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PotatoParty
{
    /// <summary>
    /// Wibracje padów (Input System). Każdy pad ma dwa silniki: <b>niski</b> (ciężki, "dudnienie" – lądowanie,
    /// wybuch) i <b>wysoki</b> (ostry, "brzęczenie" – trafienie, zryw). Impulsy sumują się i wygasają
    /// wykładniczo w czasie rzeczywistym, więc grają także w hit-stopie.
    /// Bez Input System klasa kompiluje się do pustych metod.
    /// </summary>
    public sealed class Rumble : MonoBehaviour
    {
        /// <summary>Globalny mnożnik (ustawienia gracza). 0 = wyłączone.</summary>
        public static float Strength = 1f;
        /// <summary>Szybkość wygaszania na sekundę (e^-k·t): 9 ≈ 75 ms do połowy.</summary>
        public static float DecayLow = 7f, DecayHigh = 12f;

        static Rumble instance;

        sealed class Motor { public float Low, High; }
        readonly Dictionary<int, Motor> motors = new Dictionary<int, Motor>();
        readonly List<int> dead = new List<int>();

        static Rumble Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("Rumble");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Rumble>();
            return instance;
        }

        /// <summary>Impuls na padzie gracza (boty i klawiatura są pomijane).</summary>
        public static void Pulse(PlayerSlot slot, float low, float high)
        {
            if (slot == null || slot.Kind != ControlKind.Gamepad || Strength <= 0f) return;
            var r = Ensure();
            Motor m;
            if (!r.motors.TryGetValue(slot.DeviceId, out m)) { m = new Motor(); r.motors[slot.DeviceId] = m; }
            m.Low = Mathf.Clamp01(m.Low + low * Strength);
            m.High = Mathf.Clamp01(m.High + high * Strength);
        }

        /// <summary>Impuls na wszystkich padach (wybuch, gol), osłabiany z odległością od źródła.</summary>
        public static void PulseAll(IList<Potato> potatoes, Vector3 source, float low, float high, float radius)
        {
            if (potatoes == null) return;
            for (int i = 0; i < potatoes.Count; i++)
            {
                var p = potatoes[i];
                if (p == null || p.Slot == null) continue;
                float k = radius > 0f ? Mathf.Clamp01(1f - Vector3.Distance(p.transform.position, source) / radius) : 1f;
                if (k > 0.02f) Pulse(p.Slot, low * k, high * k);
            }
        }

        public static void StopAll()
        {
            if (instance == null) return;
            instance.motors.Clear();
#if ENABLE_INPUT_SYSTEM
            InputSystem.ResetHaptics();
#endif
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool paused = TimeDirector.Paused;
            dead.Clear();
            foreach (var kv in motors)
            {
                var m = kv.Value;
                m.Low *= Mathf.Exp(-DecayLow * dt);
                m.High *= Mathf.Exp(-DecayHigh * dt);
                if (m.Low < 0.01f && m.High < 0.01f) { m.Low = m.High = 0f; dead.Add(kv.Key); }
                Send(kv.Key, paused ? 0f : m.Low, paused ? 0f : m.High);
            }
            for (int i = 0; i < dead.Count; i++) motors.Remove(dead[i]);
        }

        static void Send(int deviceId, float low, float high)
        {
#if ENABLE_INPUT_SYSTEM
            foreach (var pad in Gamepad.all)
            {
                if (pad.deviceId != deviceId) continue;
                pad.SetMotorSpeeds(low, high);
                return;
            }
#endif
        }

        void OnDisable() { StopAll(); }
        void OnApplicationFocus(bool focus) { if (!focus) StopAll(); }
    }
}
