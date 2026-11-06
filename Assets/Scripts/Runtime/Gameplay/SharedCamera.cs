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
        float shake;

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
            return sc;
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

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
            var pot = t.GetComponent<Potato>();
            if (pot == null) return true; // np. piłka: zawsze w kadrze
            if (pot.Eliminated) return false;
            return !humansOnly || (pot.Slot != null && !pot.Slot.IsBot);
        }

        void Compute(out Vector3 f, out float d)
        {
            bool anyHuman = false;
            foreach (var t in Targets) if (Usable(t, true) && t.GetComponent<Potato>() != null) { anyHuman = true; break; }

            Bounds b = new Bounds(ArenaCenter, Vector3.one);
            int count = 0;
            foreach (var t in Targets)
            {
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
            if (shake > 0f) pos += Random.insideUnitSphere * shake;
            transform.SetPositionAndRotation(pos, rot);
        }

        void LateUpdate()
        {
            Vector3 f;
            float d;
            Compute(out f, out d);
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f);
            focus = Vector3.Lerp(focus, f, k);
            distance = Mathf.Lerp(distance, d, k);
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 2.5f);
            Apply();
        }
    }
}
