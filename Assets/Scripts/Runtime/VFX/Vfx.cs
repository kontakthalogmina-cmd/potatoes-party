using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace PotatoParty
{
    /// <summary>
    /// Efekty cząsteczkowe na <b>VFX Graph</b> (GPU). Każdy efekt to JEDEN trwały <see cref="VisualEffect"/>
    /// w przestrzeni świata, do którego wysyłamy zdarzenie <c>Burst</c> z atrybutami źródła
    /// (position, direction, color, size, spawnCount). Setki trafień = jeden draw call na typ efektu, zero alokacji.
    /// <para>Grafy leżą w <c>Assets/Resources/PotatoParty/VFX/</c> (nazwy niżej). Schemat każdego grafu,
    /// węzeł po węźle, jest w <c>docs/Rework.md</c> → "VFX Graph". Dopóki grafu nie ma, ten sam efekt robi
    /// zapasowy system na ParticleSystem (<see cref="Fx"/>), więc gra wygląda dobrze od pierwszego Play.</para>
    /// </summary>
    public static class Vfx
    {
        public const string HitGraph = "PP_Hit";
        public const string DustGraph = "PP_Dust";
        public const string TrailGraph = "PP_DashTrail";
        public const string ExplosionGraph = "PP_Explosion";
        public const string SplashGraph = "PP_Splash";

        static readonly int BurstEvent = Shader.PropertyToID("Burst");
        static readonly int PositionId = Shader.PropertyToID("position");
        static readonly int DirectionId = Shader.PropertyToID("direction");
        static readonly int ColorId = Shader.PropertyToID("color");
        static readonly int SizeId = Shader.PropertyToID("size");
        static readonly int SpawnCountId = Shader.PropertyToID("spawnCount");

        sealed class Slot
        {
            public VisualEffect Effect;
            public VFXEventAttribute Attr;
        }

        static readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>();
        static readonly HashSet<string> missing = new HashSet<string>();
        static Transform root;

        /// <summary>Czy dany graf jest w projekcie (wtedy zapasowy ParticleSystem się nie odpala).</summary>
        public static bool Has(string graph) { return Get(graph) != null; }

        static Slot Get(string graph)
        {
            Slot s;
            if (slots.TryGetValue(graph, out s))
            {
                if (s.Effect != null) return s;
                slots.Remove(graph);
            }
            if (missing.Contains(graph)) return null;
            var asset = Resources.Load<VisualEffectAsset>("PotatoParty/VFX/" + graph);
            if (asset == null || !SystemInfo.supportsComputeShaders) { missing.Add(graph); return null; }
            if (root == null)
            {
                var go = new GameObject("VFX Graph");
                Object.DontDestroyOnLoad(go);
                root = go.transform;
            }
            var holder = new GameObject(graph);
            holder.transform.SetParent(root, false);
            var fx = holder.AddComponent<VisualEffect>();
            fx.visualEffectAsset = asset;
            s = new Slot { Effect = fx, Attr = fx.CreateVFXEventAttribute() };
            slots[graph] = s;
            return s;
        }

        static bool Burst(string graph, Vector3 pos, Vector3 dir, Color color, float size, int count)
        {
            var s = Get(graph);
            if (s == null) return false;
            var a = s.Attr;
            a.SetVector3(PositionId, pos);
            a.SetVector3(DirectionId, dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up);
            a.SetVector3(ColorId, new Vector3(color.r, color.g, color.b));
            a.SetFloat(SizeId, size);
            a.SetFloat(SpawnCountId, count);
            s.Effect.SendEvent(BurstEvent, a);
            return true;
        }

        // ---------------- efekty rozgrywki ----------------

        /// <summary>Trafienie zrywem: gwiazda HDR, snop iskier wzdłuż ciosu, fala, kurz, błysk światła.</summary>
        public static void Hit(Transform parent, Vector3 pos, Vector3 dir, Color c, float power01)
        {
            dir.y = 0f;
            if (Burst(HitGraph, pos, dir, Color.Lerp(c, Color.white, 0.45f), 1f + power01, Mathf.RoundToInt(24 + 30 * power01)))
            {
                Fx.FlashLight(parent, pos, Color.Lerp(c, Color.white, 0.5f), 3.5f + 2f * power01, 6f + 8f * power01, 0.14f);
                return;
            }
            Fx.Impact(parent, pos, c, dir, power01);
        }

        /// <summary>Ciężkie lądowanie: pierścień kurzu przy ziemi + drobne grudki.</summary>
        public static void HeavyLanding(Transform parent, Vector3 feet, float k01)
        {
            if (Burst(DustGraph, feet, Vector3.up, SkinFx.GroundDustColor, 0.8f + 0.8f * k01, Mathf.RoundToInt(10 + 14 * k01))) return;
            Fx.Dust(parent, feet, 0.6f + 0.8f * k01, 7);
        }

        public static void JumpPuff(Transform parent, Vector3 feet)
        {
            if (Burst(DustGraph, feet, Vector3.up, SkinFx.GroundDustColor, 0.55f, 6)) return;
            Effects.Puff(parent, feet, 0.6f);
        }

        public static void Skid(Transform parent, Vector3 feet, Vector3 back)
        {
            if (Burst(DustGraph, feet, back + Vector3.up * 0.4f, SkinFx.GroundDustColor, 0.45f, 3)) return;
            Fx.Dust(parent, feet, 0.4f, 2);
        }

        /// <summary>Smuga zrywu: wstęga w kolorze gracza + kurz spod stóp.</summary>
        public static void DashTrail(Transform parent, Vector3 feet, Vector3 dir, Color c)
        {
            if (Burst(TrailGraph, feet + Vector3.up * 0.6f, -dir, c, 1f, 4)) return;
            Fx.Dust(parent, feet, 0.55f, 3);
        }

        /// <summary>Wybuch (zastępuje ParticleSystem, gdy graf istnieje). Zwraca true, jeśli zagrał VFX Graph.</summary>
        public static bool Explosion(Vector3 pos, float size)
        {
            return Burst(ExplosionGraph, pos, Vector3.up, new Color(1f, 0.55f, 0.2f), size, Mathf.RoundToInt(60 * Mathf.Clamp(size, 0.5f, 2f)));
        }

        /// <summary>Rozbryzg wody / błota. Zwraca true, jeśli zagrał VFX Graph.</summary>
        public static bool Splash(Vector3 pos, Color c, float size)
        {
            return Burst(SplashGraph, pos, Vector3.up, c, size, Mathf.RoundToInt(40 * Mathf.Clamp(size, 0.5f, 2f)));
        }

        /// <summary>Zmiana mapy: grafy zostają (są globalne), czyścimy tylko żywe cząsteczki.</summary>
        public static void ClearAll()
        {
            foreach (var kv in slots)
                if (kv.Value.Effect != null) kv.Value.Effect.Reinit();
        }
    }
}
