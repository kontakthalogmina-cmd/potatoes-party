using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Wspólna kamera dla wszystkich graczy: patrzy z góry pod kątem, sama się przesuwa
    /// i oddala tak, żeby było widać każdego ziemniaka i kawałek otoczenia wokół.
    /// </summary>
    public class SharedCamera : MonoBehaviour
    {
        public Camera Cam;
        public readonly List<Transform> Targets = new List<Transform>();
        public Vector3 ArenaCenter;
        public float ArenaRadius = 10f;
        public float Pitch = 52f;
        public float MinDistance = 12f;
        public float MaxDistance = 34f;
        public float ExtraMargin = 5f;
        public Vector3 ScreenOffset = Vector3.zero;

        Vector3 focus;
        float distance = 20f;

        // ---------------- wstrząs: trauma (Eiserloh, GDC 2016) ----------------
        // Trauma 0..1 rośnie przy trafieniach i liniowo opada; siła wstrząsu = trauma², więc małe
        // stuknięcia są subtelne, a wybuch naprawdę "rzuca" kamerą. Ruch z szumu Perlina (gładki,
        // ~18 Hz), a nie z Random.insideUnitSphere co klatkę (biały szum = drganie obrazu, nie wstrząs).
        [Header("Wstrząs (trauma)")]
        public float TraumaDecay = 1.5f;          // na sekundę
        public float ShakeTranslation = 0.38f;    // m przy traumie 1
        public float ShakeRoll = 2.4f;            // stopnie przy traumie 1
        public float ShakeYawPitch = 0.9f;        // stopnie przy traumie 1
        public float ShakeFrequency = 18f;        // Hz
        [Header("Odrzut i puls FOV")]
        public float KickFrequency = 7.5f;
        public float KickDamping = 0.42f;
        public float PunchFrequency = 5.5f;
        public float PunchDamping = 0.5f;

        float trauma;
        float noiseTime;
        Spring kickX, kickY, kickZ, fovPunch;
        float baseFov = 45f;
        readonly System.Collections.Generic.Dictionary<Transform, Potato> potatoCache = new System.Collections.Generic.Dictionary<Transform, Potato>();

        public static SharedCamera Create()
        {
            var go = new GameObject("Kamera wspolna");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            go.AddComponent<AudioListener>();
            go.tag = "MainCamera";
            var sc = go.AddComponent<SharedCamera>();
            sc.Cam = cam;
            sc.baseFov = cam.fieldOfView;
            return sc;
        }

        /// <summary>Stare API (metry wstrząsu 0.1–0.6) przeliczone na traumę: 0.12 → 0.38, 0.6 → 0.85.</summary>
        public void Shake(float amount) { AddTrauma(Mathf.Sqrt(Mathf.Max(0f, amount)) * 1.1f); }

        /// <summary>Dodaje traumę (0..1). Sumuje się, ale nie przekracza 1.</summary>
        public void AddTrauma(float amount) { trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount)); }

        /// <summary>
        /// Kierunkowy odrzut kamery wzdłuż kierunku trafienia (sprężyna z przestrzałem): oko "czyta",
        /// w którą stronę poleciał cios, czego czysty losowy wstrząs nie przekazuje.
        /// </summary>
        public void Kick(Vector3 worldDir, float strength)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 1e-4f) return;
            worldDir.Normalize();
            // prędkość początkowa sprężyny: amplituda ≈ v / (2πf)
            float v = strength * 6.2831853f * KickFrequency;
            kickX.Kick(worldDir.x * v);
            kickZ.Kick(worldDir.z * v);
            kickY.Kick(-0.35f * v);
        }

        /// <summary>Puls pola widzenia w stopniach (ujemny = szybkie przybliżenie przy trafieniu).</summary>
        public void Punch(float degrees) { fovPunch.Kick(degrees * 6.2831853f * PunchFrequency); }

        public void SnapNow()
        {
            Compute(out focus, out distance);
            Apply();
        }

        /// <summary>
        /// Kamera podąża za graczami-ludźmi (jeśli są na planszy), a gdy ich nie ma, za wszystkimi.
        /// Przybliża się, gdy gracze są blisko siebie, i oddala, gdy się rozbiegną.
        /// </summary>
        public float FollowArenaPull = 0.1f;
        public float MinSpread = 7f;

        bool Usable(Transform t, bool humansOnly)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return false;
            if (t.position.y < ArenaCenter.y - 6f) return false; // spadający gracze nie ciągną kamery w dół
            var pot = PotatoOf(t);
            if (pot == null) return true; // np. piłka: zawsze w kadrze
            if (pot.Eliminated) return false;
            return !humansOnly || (pot.Slot != null && !pot.Slot.IsBot);
        }

        // GetComponent co klatkę dla każdego celu (2x) to zbędny koszt natywnego wywołania: cache po Transformie.
        Potato PotatoOf(Transform t)
        {
            Potato p;
            if (potatoCache.TryGetValue(t, out p)) return p;
            if (potatoCache.Count > Targets.Count + 32) potatoCache.Clear();
            p = t.GetComponent<Potato>();
            potatoCache[t] = p;
            return p;
        }

        void Compute(out Vector3 f, out float d)
        {
            bool anyHuman = false;
            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (Usable(t, true) && PotatoOf(t) != null) { anyHuman = true; break; }
            }

            Bounds b = new Bounds(ArenaCenter, Vector3.one);
            int count = 0;
            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (!Usable(t, anyHuman)) continue;
                if (count == 0) b = new Bounds(t.position, Vector3.one);
                else b.Encapsulate(t.position);
                count++;
            }
            if (count == 0) b = new Bounds(ArenaCenter, Vector3.one * ArenaRadius);

            f = Vector3.Lerp(b.center, ArenaCenter, FollowArenaPull);
            f.y = ArenaCenter.y;
            float spread = Mathf.Max(b.size.x, b.size.z * 1.4f, MinSpread);
            float halfFov = Cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float aspectFix = Mathf.Max(1f, 1.6f / Mathf.Max(0.5f, Cam.aspect));
            d = (spread * 0.5f + ExtraMargin) / Mathf.Tan(halfFov) * aspectFix;
            d = Mathf.Clamp(d, MinDistance, MaxDistance);
        }

        void Apply()
        {
            Quaternion rot = Quaternion.Euler(Pitch, 0, 0);
            Vector3 pos = focus + ScreenOffset - rot * Vector3.forward * distance;
            pos += new Vector3(kickX.Value, kickY.Value, kickZ.Value);

            float s = trauma * trauma;
            if (s > 0.0001f)
            {
                // trzy niezależne "kanały" szumu (różne wiersze Perlina) – brak korelacji osi
                float t = noiseTime;
                float nx = Mathf.PerlinNoise(t, 0.37f) * 2f - 1f;
                float ny = Mathf.PerlinNoise(t, 7.91f) * 2f - 1f;
                float nr = Mathf.PerlinNoise(t, 13.13f) * 2f - 1f;
                float np = Mathf.PerlinNoise(t, 21.7f) * 2f - 1f;
                pos += (rot * new Vector3(nx, ny, 0f)) * (ShakeTranslation * s);
                rot = rot * Quaternion.Euler(np * ShakeYawPitch * s, nx * ShakeYawPitch * s, nr * ShakeRoll * s);
            }
            transform.SetPositionAndRotation(pos, rot);
            if (Cam != null) Cam.fieldOfView = baseFov + fovPunch.Value;
        }

        void LateUpdate()
        {
            Vector3 f;
            float d;
            Compute(out f, out d);
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f);
            focus = Vector3.Lerp(focus, f, k);
            distance = Mathf.Lerp(distance, d, k);
            // wszystko w czasie rzeczywistym: wstrząs i odrzut grają także w trakcie hit-stopu
            float dt = Time.unscaledDeltaTime;
            trauma = Mathf.MoveTowards(trauma, 0f, dt * TraumaDecay);
            noiseTime += dt * ShakeFrequency;
            if (noiseTime > 10000f) noiseTime -= 10000f;
            kickX.Step(0f, KickFrequency, KickDamping, dt);
            kickY.Step(0f, KickFrequency, KickDamping, dt);
            kickZ.Step(0f, KickFrequency, KickDamping, dt);
            fovPunch.Step(0f, PunchFrequency, PunchDamping, dt);
            Apply();
        }
    }
}
