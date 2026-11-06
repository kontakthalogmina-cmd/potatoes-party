using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Nocna oprawa (obecnie nieużywana, zostawiona na przyszłą mapę-jaskinię): skalne ściany kanionu,
    /// pochodnie z migającym ogniem, świecące kryształy, drewniane skrzynie i unoszące się iskierki.
    /// </summary>
    public static partial class MapBuilder
    {
        internal static void Dungeon(Transform env, MapTheme t, float arenaRadius, float inner, float y, System.Random rng)
        {
            Look.KeyLight(env, arenaRadius, t);
            Cliffs(env, t, inner, y, rng);
            Torches(env, inner, y);
            Crystals(env, t, inner, y, rng);
            Crates(env, inner, y, rng);
            Motes(env, t, arenaRadius);
        }

        static Color RockColor(MapTheme t)
        {
            return Color.Lerp(Shapes.Hex("#5A606A"), t.Land, 0.35f);
        }

        internal static GameObject Rock(Transform parent, Vector3 pos, Vector3 scale, Color c, int variant, float yaw)
        {
            var go = new GameObject("Skala");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Shapes.Rock(variant);
            go.AddComponent<MeshRenderer>().sharedMaterial = Shapes.Mat(c, 0.2f);
            return go;
        }

        /// <summary>Kanciaste skały dookoła, jakby arena leżała na dnie kanionu.</summary>
        static void Cliffs(Transform env, MapTheme t, float inner, float y, System.Random rng)
        {
            var root = Shapes.Empty("Skaly", env, Vector3.zero).transform;
            var rock = RockColor(t);
            var rockDark = Color.Lerp(rock, Color.black, 0.3f);
            const int n = 34;
            for (int layer = 0; layer < 2; layer++)
            {
                for (int i = 0; i < n; i++)
                {
                    float a = (i + (layer * 0.5f) + R(rng, -0.2f, 0.2f)) * Mathf.PI * 2f / n;
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    // od strony kamery (dół ekranu) skały są niższe, żeby nie zasłaniały areny
                    float near = Mathf.Clamp01(-dir.z * 1.6f);
                    float r = inner + (layer == 0 ? R(rng, 9f, 16f) : R(rng, 20f, 30f));
                    float h = (layer == 0 ? R(rng, 6f, 13f) : R(rng, 14f, 24f)) * Mathf.Lerp(1f, 0.35f, near);
                    float w = layer == 0 ? R(rng, 7f, 12f) : R(rng, 11f, 17f);
                    Rock(root, dir * r + Vector3.up * (y + h * 0.15f), new Vector3(w, h, w * R(rng, 0.8f, 1.2f)),
                        (i + layer) % 3 == 0 ? rockDark : rock, rng.Next(6), R(rng, 0, 360));
                }
            }
            // drobne kamienie bliżej areny
            for (int i = 0; i < 26; i++)
            {
                var p = RingPoint(rng, inner, y, 0.5f, 9f);
                float s = R(rng, 0.8f, 2.4f);
                Rock(root, p + Vector3.up * s * 0.1f, new Vector3(s * R(rng, 1f, 1.6f), s, s), i % 2 == 0 ? rock : rockDark, rng.Next(6), R(rng, 0, 360));
            }
        }

        /// <summary>Pochodnie na brzegu lądu: ciepłe, migające światło jak w Pummel Party.</summary>
        static void Torches(Transform env, float inner, float y)
        {
            var root = Shapes.Empty("Pochodnie", env, Vector3.zero).transform;
            float r = inner + 1.2f;
            int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * r / 9f), 8, 22);
            var wood = Shapes.Hex("#4A3020");
            var iron = Shapes.Hex("#33343A");
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / n;
                var pos = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                var torch = Shapes.Empty("Pochodnia", root, pos).transform;
                Shapes.Prim(PrimitiveType.Cylinder, torch, new Vector3(0, 1.1f, 0), new Vector3(0.22f, 1.1f, 0.22f), wood, false, 0.2f);
                Shapes.Prim(PrimitiveType.Cylinder, torch, new Vector3(0, 2.25f, 0), new Vector3(0.75f, 0.12f, 0.75f), iron, false, 0.5f);
                var flame = Shapes.Empty("Plomien", torch, new Vector3(0, 2.45f, 0)).transform;
                Shapes.Prim(PrimitiveType.Sphere, flame, new Vector3(0, 0.25f, 0), new Vector3(0.55f, 0.8f, 0.55f), Shapes.Hex("#FF6A1A"))
                    .GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FF6A1A"), 0.1f, 3.5f);
                Shapes.Prim(PrimitiveType.Sphere, flame, new Vector3(0, 0.2f, 0), new Vector3(0.32f, 0.5f, 0.32f), Shapes.Hex("#FFD45A"))
                    .GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FFD45A"), 0.1f, 5f);
                foreach (var rend in flame.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var light = Look.PointLight(torch, new Vector3(0, 3.1f, 0), Shapes.Hex("#FF9440"), 10f, 1.3f, 0.25f);
                light.GetComponent<Flicker>().Flame = flame;
            }
        }

        /// <summary>Grupy świecących kryształów w kolorze poświaty mapy.</summary>
        static void Crystals(Transform env, MapTheme t, float inner, float y, System.Random rng)
        {
            var root = Shapes.Empty("Krysztaly", env, Vector3.zero).transform;
            var mat = Shapes.Mat(t.Glow, 0.9f, 2.2f);
            var rock = Color.Lerp(RockColor(t), Color.black, 0.2f);
            for (int i = 0; i < 8; i++)
            {
                float a = (i + R(rng, 0.2f, 0.8f)) * Mathf.PI * 2f / 8f;
                float r = inner + R(rng, 2.5f, 8f);
                var group = Shapes.Empty("Krysztal", root, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)).transform;
                Rock(group, new Vector3(0, 0.1f, 0), new Vector3(2.2f, 1f, 2f), rock, rng.Next(6), R(rng, 0, 360));
                int shards = 3 + rng.Next(3);
                for (int k = 0; k < shards; k++)
                {
                    var shard = new GameObject("Odlamek");
                    shard.transform.SetParent(group, false);
                    float h = R(rng, 1.4f, 3.4f);
                    float wdt = R(rng, 0.5f, 0.9f);
                    shard.transform.localPosition = new Vector3(R(rng, -0.6f, 0.6f), h * 0.4f, R(rng, -0.6f, 0.6f));
                    shard.transform.localRotation = Quaternion.Euler(R(rng, -28f, 28f), R(rng, 0, 360), R(rng, -28f, 28f));
                    shard.transform.localScale = new Vector3(wdt, h, wdt);
                    shard.AddComponent<MeshFilter>().sharedMesh = Shapes.Diamond();
                    var mr = shard.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                Look.PointLight(group, new Vector3(0, 2f, 0), t.Glow, 11f, 1.5f, 0.06f);
            }
        }

        /// <summary>Drewniana skrzynia z ciemną ramą i ukośną deską.</summary>
        internal static Transform Crate(Transform parent, Vector3 pos, float s, float yaw)
        {
            var crate = Shapes.Empty("Skrzynia", parent, pos).transform;
            crate.localRotation = Quaternion.Euler(0, yaw, 0);
            var wood = Shapes.Hex("#9A6A38");
            var frame = Shapes.Hex("#4A2E18");
            float h = s * 0.5f, b = s * 0.13f;
            Shapes.Box(crate, new Vector3(0, h, 0), Vector3.one * s, wood, false);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Shapes.Box(crate, new Vector3(x * (h - b * 0.4f), h, z * (h - b * 0.4f)), new Vector3(b, s * 1.02f, b), frame, false);
            for (int k = 0; k < 2; k++)
            {
                float yy = k == 0 ? b * 0.4f : s - b * 0.4f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Shapes.Box(crate, new Vector3(0, yy, side * (h - b * 0.4f)), new Vector3(s * 1.02f, b, b), frame, false);
                    Shapes.Box(crate, new Vector3(side * (h - b * 0.4f), yy, 0), new Vector3(b, b, s * 1.02f), frame, false);
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var d1 = Shapes.Box(crate, new Vector3(0, h, side * (h + 0.01f)), new Vector3(s * 1.25f, b * 0.8f, b * 0.5f), frame, false);
                d1.transform.localRotation = Quaternion.Euler(0, 0, 45f * side);
                var d2 = Shapes.Box(crate, new Vector3(side * (h + 0.01f), h, 0), new Vector3(b * 0.5f, b * 0.8f, s * 1.25f), frame, false);
                d2.transform.localRotation = Quaternion.Euler(45f * side, 0, 0);
            }
            return crate;
        }

        static void Crates(Transform env, float inner, float y, System.Random rng)
        {
            var root = Shapes.Empty("Skrzynie", env, Vector3.zero).transform;
            for (int i = 0; i < 12; i++)
            {
                var p = RingPoint(rng, inner, y, 0.8f, 10f);
                float s = R(rng, 1.1f, 1.6f);
                float yaw = R(rng, 0, 90);
                Crate(root, p, s, yaw);
                int stack = rng.Next(3);
                if (stack >= 1) Crate(root, p + new Vector3(R(rng, -0.15f, 0.15f), s, R(rng, -0.15f, 0.15f)), s * 0.85f, yaw + R(rng, -20f, 20f));
                if (stack == 2) Crate(root, p + new Vector3(s * 1.05f, 0, 0), s * 0.9f, yaw + R(rng, -10f, 10f));
            }
        }

        static readonly Dictionary<Color32, Material> moteMats = new Dictionary<Color32, Material>();
        static Mesh moteMesh;

        /// <summary>Świecące iskierki/świetliki unoszące się nad areną.</summary>
        static void Motes(Transform env, MapTheme t, float arenaRadius)
        {
            Material mat;
            if (!moteMats.TryGetValue(t.Glow, out mat) || mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) return;
                mat = new Material(shader) { name = "Iskierki" };
                var c = t.Glow * 4f;
                c.a = 1f;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                moteMats[t.Glow] = mat;
            }
            var go = new GameObject("Iskierki");
            go.transform.SetParent(env, false);
            go.transform.localPosition = new Vector3(0, 2.5f, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            main.gravityModifier = -0.015f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 250;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(arenaRadius * 2.6f + 10f, 4f, arenaRadius * 2.6f + 10f);
            var emission = ps.emission;
            emission.rateOverTime = Mathf.Clamp(arenaRadius * 1.6f, 12f, 40f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.3f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            if (moteMesh == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                moteMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(tmp);
            }
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = moteMesh;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }
    }
}
