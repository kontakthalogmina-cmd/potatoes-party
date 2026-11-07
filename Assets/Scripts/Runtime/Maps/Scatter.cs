using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PotatoParty
{
    /// <summary>
    /// Gęste detale terenu (trawa, kwiatki, kamyki, muszelki, zaspy) łączone w kilka dużych siatek –
    /// setki drobiazgów kosztują tyle draw calli, ile jest kolorów, a nie obiektów.
    /// </summary>
    public class ScatterBatch
    {
        readonly Dictionary<Color32, List<CombineInstance>> parts = new Dictionary<Color32, List<CombineInstance>>();
        readonly Dictionary<Color32, float> gloss = new Dictionary<Color32, float>();
        static Mesh sphere, cylinder, cube;

        static Mesh Builtin(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return m;
        }

        public static Mesh Sphere { get { if (sphere == null) sphere = Builtin(PrimitiveType.Sphere); return sphere; } }
        public static Mesh Cylinder { get { if (cylinder == null) cylinder = Builtin(PrimitiveType.Cylinder); return cylinder; } }
        public static Mesh Cube { get { if (cube == null) cube = Builtin(PrimitiveType.Cube); return cube; } }

        public void Add(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, Color c, float smoothness = 0.35f)
        {
            Color32 k = c;
            List<CombineInstance> list;
            if (!parts.TryGetValue(k, out list)) { list = new List<CombineInstance>(); parts[k] = list; gloss[k] = smoothness; }
            list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, scale) });
        }

        public void Build(Transform parent, string name, bool castShadows = true)
        {
            foreach (var kv in parts)
            {
                var list = kv.Value;
                // siatki do 65k wierzchołków (UInt32 włączamy, gdy trzeba)
                const int chunk = 400;
                for (int start = 0; start < list.Count; start += chunk)
                {
                    int n = Mathf.Min(chunk, list.Count - start);
                    var mesh = new Mesh { name = name };
                    mesh.indexFormat = IndexFormat.UInt32;
                    mesh.CombineMeshes(list.GetRange(start, n).ToArray(), true, true);
                    mesh.RecalculateBounds();
                    var go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = Shapes.Mat(kv.Key, gloss[kv.Key]);
                    r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                }
            }
            parts.Clear();
        }
    }

    public static partial class MapBuilder
    {
        /// <summary>Trawa, kwiatki i kamyki na lądzie wokół areny + daleki pierścień wzgórz na horyzoncie.</summary>
        static void GroundDetail(Transform env, MapTheme t, float inner, float depth, float y, System.Random rng)
        {
            var b = new ScatterBatch();
            bool grassy = t.Id == MapId.Farm || t.Id == MapId.Orchard || t.Id == MapId.Forest || t.Id == MapId.Garden
                          || t.Id == MapId.Playground || t.Id == MapId.Cornfield;
            var diamond = Shapes.Diamond();
            var grass = Color.Lerp(t.Land, Shapes.Hex("#3E9A3A"), 0.35f);
            var grassHi = Color.Lerp(t.Land, Shapes.Hex("#B8F06A"), 0.35f);
            Color[] petals = { Shapes.Hex("#FFFFFF"), Shapes.Hex("#FFD23F"), Shapes.Hex("#FF6F91"), Shapes.Hex("#B57BFF"), Shapes.Hex("#7FD3FF") };

            int tufts = grassy ? 900 : 0;
            for (int i = 0; i < tufts; i++)
            {
                var p = RingPoint(rng, inner, y, 0.6f, depth - 2f);
                float s = R(rng, 0.6f, 1.2f);
                var col = rng.NextDouble() < 0.5 ? grass : grassHi;
                // kępka: 3 wąskie, wysokie listki rozchylone na boki
                for (int k = 0; k < 3; k++)
                {
                    var rot = Quaternion.Euler(R(rng, -22f, 22f), k * 60f + R(rng, 0, 40f), R(rng, -22f, 22f));
                    b.Add(diamond, p + Vector3.up * 0.22f * s, rot, new Vector3(0.12f, 0.5f, 0.05f) * s, col, 0.3f);
                }
            }
            int flowers = grassy ? 260 : 0;
            for (int i = 0; i < flowers; i++)
            {
                var p = RingPoint(rng, inner, y, 0.6f, depth * 0.7f);
                float s = R(rng, 0.7f, 1.2f);
                b.Add(ScatterBatch.Cylinder, p + Vector3.up * 0.22f * s, Quaternion.identity, new Vector3(0.04f, 0.22f, 0.04f) * s, grass, 0.3f);
                b.Add(ScatterBatch.Sphere, p + Vector3.up * 0.48f * s, Quaternion.Euler(R(rng, -15, 15), 0, R(rng, -15, 15)), new Vector3(0.26f, 0.12f, 0.26f) * s, petals[rng.Next(petals.Length)], 0.5f);
                b.Add(ScatterBatch.Sphere, p + Vector3.up * 0.53f * s, Quaternion.identity, Vector3.one * 0.09f * s, Shapes.Hex("#FFB000"), 0.5f);
            }

            // kamyki: wszędzie, w kolorze terenu
            var rockCol = t.Id == MapId.Volcano ? Shapes.Hex("#2E2624") : t.Id == MapId.Ice ? Shapes.Hex("#CFE6F7") : Color.Lerp(t.LandAlt, Shapes.Hex("#9A9590"), 0.55f);
            int rocks = t.Id == MapId.Kitchen || t.Id == MapId.Canteen || t.Id == MapId.Stadium ? 0 : 160;
            for (int i = 0; i < rocks; i++)
            {
                var p = RingPoint(rng, inner, y, 0.5f, depth - 1f);
                float s = R(rng, 0.25f, 0.9f) * (rng.NextDouble() < 0.1 ? 2.5f : 1f);
                b.Add(Shapes.Rock(rng.Next(6)), p + Vector3.up * 0.1f * s, Quaternion.Euler(0, R(rng, 0, 360), 0), new Vector3(s * 1.2f, s * 0.8f, s), Color.Lerp(rockCol, Color.white, R(rng, 0f, 0.15f)), 0.25f);
            }

            if (t.Id == MapId.Ice)
            {
                // zaspy śniegu
                for (int i = 0; i < 120; i++)
                {
                    var p = RingPoint(rng, inner, y, 0.5f, depth - 1f);
                    float s = R(rng, 0.8f, 2.6f);
                    b.Add(ScatterBatch.Sphere, p, Quaternion.Euler(0, R(rng, 0, 360), 0), new Vector3(s * 1.6f, s * 0.5f, s), Color.white, 0.55f);
                }
            }
            if (t.Id == MapId.Beach)
            {
                Color[] shells = { Shapes.Hex("#FFD6E0"), Shapes.Hex("#FFF1D6"), Shapes.Hex("#FFB59E") };
                for (int i = 0; i < 90; i++)
                {
                    var p = RingPoint(rng, inner, y, 0.5f, depth * 0.6f);
                    b.Add(ScatterBatch.Sphere, p + Vector3.up * 0.04f, Quaternion.Euler(0, R(rng, 0, 360), 0), new Vector3(0.3f, 0.1f, 0.24f), shells[rng.Next(shells.Length)], 0.7f);
                }
            }
            if (t.Id == MapId.Volcano)
            {
                // żarzące się szczeliny w czarnej skale
                for (int i = 0; i < 40; i++)
                {
                    var p = RingPoint(rng, inner, y, 1f, depth - 4f);
                    var crack = Shapes.Prim(PrimitiveType.Cube, env, p + Vector3.up * 0.02f, new Vector3(R(rng, 1.5f, 4f), 0.04f, R(rng, 0.12f, 0.3f)), Shapes.Hex("#FF6A1F"));
                    crack.transform.localRotation = Quaternion.Euler(0, R(rng, 0, 180), 0);
                    crack.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FF6A1F"), 0.3f, 2.5f);
                    crack.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            b.Build(env, "Detale terenu");

            Horizon(env, t, inner + depth, y, rng);
        }

        /// <summary>Daleki pierścień wzgórz/gór we mgle: plansza wydaje się ogromna, a nie zawieszona w pustce.</summary>
        static void Horizon(Transform env, MapTheme t, float r, float y, System.Random rng)
        {
            if (t.Id == MapId.Kitchen || t.Id == MapId.Canteen) return; // wnętrza mają własne ściany
            var b = new ScatterBatch();
            bool peaks = t.Id == MapId.Ice || t.Id == MapId.Volcano;
            var c1 = peaks ? (t.Id == MapId.Ice ? Shapes.Hex("#DCEBFA") : Shapes.Hex("#3A2E2C")) : Color.Lerp(t.Land, t.Sky, 0.25f);
            var c2 = Color.Lerp(c1, t.Sky, 0.35f);
            int n = 34;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n + R(rng, -0.05f, 0.05f);
                float rr = r + R(rng, 4f, 26f);
                var p = new Vector3(Mathf.Cos(a) * rr, y - 2f, Mathf.Sin(a) * rr);
                if (peaks)
                {
                    float h = R(rng, 14f, 32f);
                    b.Add(Shapes.Rock(rng.Next(6)), p + Vector3.up * h * 0.3f, Quaternion.Euler(0, R(rng, 0, 360), 0), new Vector3(R(rng, 16f, 28f), h, R(rng, 16f, 28f)), i % 2 == 0 ? c1 : c2, 0.2f);
                }
                else
                {
                    float w = R(rng, 18f, 34f);
                    b.Add(ScatterBatch.Sphere, p, Quaternion.Euler(0, R(rng, 0, 360), 0), new Vector3(w, R(rng, 7f, 16f), w * 0.8f), i % 2 == 0 ? c1 : c2, 0.15f);
                }
            }
            b.Build(env, "Horyzont", false);
            if (t.Id == MapId.Volcano)
            {
                // dymiący wielki wulkan w tle
                var v = Shapes.Empty("Wielki wulkan", env, new Vector3(-30f, y - 2f, r + 30f)).transform;
                var cone = Shapes.Prim(PrimitiveType.Sphere, v, new Vector3(0, 12f, 0), Vector3.one, Shapes.Hex("#2E2422"));
                cone.GetComponent<MeshFilter>().sharedMesh = Shapes.Rock(3);
                cone.transform.localScale = new Vector3(52f, 34f, 52f);
                var glow = Shapes.Prim(PrimitiveType.Sphere, v, new Vector3(0, 27f, 0), new Vector3(10f, 2f, 10f), Shapes.Hex("#FF6A1F"));
                glow.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FF6A1F"), 0.3f, 3f);
                var smoke = Fx.Make("Dym wulkanu", v, v.position + Vector3.up * 28f, Fx.Tex.Smoke, false, true);
                var m = smoke.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
                m.startSize = new ParticleSystem.MinMaxCurve(8f, 14f);
                m.startColor = new Color(0.25f, 0.22f, 0.22f, 0.6f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.prewarm = true;
                var sh = smoke.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 3f;
                smoke.transform.rotation = Quaternion.Euler(-90f, 0, 0);
                var em = smoke.emission; em.rateOverTime = 2.5f;
                var sol = smoke.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2f));
                var col = smoke.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
                col.color = new ParticleSystem.MinMaxGradient(g);
                smoke.Play();
            }
        }
    }
}
