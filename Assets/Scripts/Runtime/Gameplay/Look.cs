using UnityEngine;
using UnityEngine.Rendering;

namespace PotatoParty
{
    /// <summary>
    /// Dekoracje nieba i światła lokalne: chmurki, balony, snop światła areny, pochodnie.
    /// Oświetlenie główne i post-processing są w <see cref="Lighting"/> i <see cref="PostFx"/>.
    /// </summary>
    public static class Look
    {
        /// <summary>Puchate chmurki krążące powoli wysoko nad mapą.</summary>
        public static void Clouds(Transform parent, float radius)
        {
            var ring = Shapes.Empty("Chmury", parent, Vector3.zero);
            ring.AddComponent<Spinner>().Speed = new Vector3(0, 1.2f, 0);
            var rng = new System.Random(77);
            var white = Shapes.Mat(Color.white, 0.1f, 0.3f);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f + (float)rng.NextDouble() * 0.3f;
                float r = radius + 28f + (float)rng.NextDouble() * 30f;
                var c = Shapes.Empty("Chmura", ring.transform, new Vector3(Mathf.Cos(a) * r, 22f + (float)rng.NextDouble() * 12f, Mathf.Sin(a) * r)).transform;
                c.localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
                c.gameObject.AddComponent<Bobber>().Amplitude = 0.6f;
                int puffs = 3 + rng.Next(3);
                for (int k = 0; k < puffs; k++)
                {
                    float s = 4f + (float)rng.NextDouble() * 4f;
                    var puff = Shapes.Prim(PrimitiveType.Sphere, c, new Vector3((k - puffs * 0.5f) * 3.2f, (float)rng.NextDouble() * 1.5f, (float)rng.NextDouble() * 2f), new Vector3(s * 1.2f, s * 0.8f, s), Color.white);
                    var rend = puff.GetComponent<Renderer>();
                    rend.sharedMaterial = white;
                    rend.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        static readonly string[] balloonColors = { "#FF5FA2", "#FFC93C", "#4DB8FF", "#34D17A", "#9B7BFF", "#FF8A3D" };

        /// <summary>Kilka kolorowych balonów bujających się w oddali (żywe, ale nie zagracone tło).</summary>
        public static void Balloons(Transform parent, float radius)
        {
            var root = Shapes.Empty("Balony", parent, Vector3.zero);
            var rng = new System.Random(4242);
            const int count = 8;
            for (int i = 0; i < count; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / count + (float)rng.NextDouble() * 0.25f;
                // od strony kamery (dół ekranu) balonów nie stawiamy, żeby nie zasłaniały gry
                if (Mathf.Sin(a) < -0.3f) continue;
                float r = radius + 14f + (float)rng.NextDouble() * 14f;
                var holder = Shapes.Empty("Balon", root.transform, new Vector3(Mathf.Cos(a) * r, 9f + (float)rng.NextDouble() * 8f, Mathf.Sin(a) * r));
                var bob = holder.AddComponent<Bobber>();
                bob.Amplitude = 0.8f + (float)rng.NextDouble() * 0.6f;
                bob.Speed = 0.5f + (float)rng.NextDouble() * 0.4f;
                var c = Shapes.Hex(balloonColors[i % balloonColors.Length]);
                float s = 2.4f + (float)rng.NextDouble() * 1.6f;
                Shapes.Prim(PrimitiveType.Sphere, holder.transform, Vector3.zero, new Vector3(s, s * 1.18f, s), c, false, 0.8f);
                Shapes.Prim(PrimitiveType.Sphere, holder.transform, new Vector3(-s * 0.2f, s * 0.28f, -s * 0.38f), Vector3.one * s * 0.16f, Color.white)
                    .GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Color.white, 0.9f, 0.5f);
                Shapes.Prim(PrimitiveType.Cylinder, holder.transform, new Vector3(0, -s * 0.62f, 0), new Vector3(s * 0.14f, s * 0.06f, s * 0.14f), c);
                Shapes.Prim(PrimitiveType.Cylinder, holder.transform, new Vector3(0, -s * 1.5f, 0), new Vector3(0.05f, s * 0.85f, 0.05f), Color.white);
                foreach (var rend in holder.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>Snop światła z góry na środek areny (jasne "serce" planszy jak w Pummel Party).</summary>
        public static void KeyLight(Transform parent, float arenaRadius, MapTheme t)
        {
            float h = 11f + arenaRadius * 0.35f;
            var go = Shapes.Empty("Swiatlo areny", parent, new Vector3(0, h, 0));
            go.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = Color.Lerp(Color.white, t.Glow, 0.4f);
            l.spotAngle = Mathf.Clamp(2f * Mathf.Atan((arenaRadius + 4f) / h) * Mathf.Rad2Deg, 60f, 150f);
            l.innerSpotAngle = l.spotAngle * 0.45f;
            l.range = h * 2.2f;
            l.intensity = h * h * 0.6f;
            // miękki cień z góry = cień kontaktowy pod każdym ziemniakiem (czytelna wysokość skoku)
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.55f;
            l.shadowNearPlane = 0.5f;
        }

        /// <summary>Światło punktowe z delikatnym migotaniem (pochodnie, ogniska, kryształy).</summary>
        public static Light PointLight(Transform parent, Vector3 localPos, Color c, float range, float brightness, float flicker = 0f)
        {
            var go = Shapes.Empty("Swiatlo", parent, localPos);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            // URP gaśnie z kwadratem odległości, więc jasność skalujemy z zasięgiem
            l.intensity = brightness * range * range * 0.16f;
            l.shadows = LightShadows.None;
            if (flicker > 0f) go.AddComponent<Flicker>().Amount = flicker;
            return l;
        }

        static Mesh torus;

        /// <summary>Siatka torusa (pierścień) budowana raz, średnica ok. 1.4.</summary>
        public static Mesh Torus()
        {
            if (torus != null) return torus;
            const int seg = 32, side = 12;
            const float R = 0.5f, r = 0.2f;
            var verts = new Vector3[(seg + 1) * (side + 1)];
            var norms = new Vector3[verts.Length];
            for (int i = 0; i <= seg; i++)
            {
                float u = i * Mathf.PI * 2f / seg;
                var center = new Vector3(Mathf.Cos(u) * R, 0, Mathf.Sin(u) * R);
                for (int j = 0; j <= side; j++)
                {
                    float v = j * Mathf.PI * 2f / side;
                    var n = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                    verts[i * (side + 1) + j] = center + n * r;
                    norms[i * (side + 1) + j] = n;
                }
            }
            var tris = new int[seg * side * 6];
            int t = 0;
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    int a = i * (side + 1) + j, b = (i + 1) * (side + 1) + j;
                    tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
                    tris[t++] = b; tris[t++] = a + 1; tris[t++] = b + 1;
                }
            torus = new Mesh { name = "Torus", vertices = verts, normals = norms, triangles = tris };
            torus.RecalculateBounds();
            return torus;
        }
    }

    /// <summary>Migotanie światła i płomienia (szum Perlina).</summary>
    public class Flicker : MonoBehaviour
    {
        public float Amount = 0.2f;
        public Transform Flame;
        Light l;
        float baseIntensity, seed;
        Vector3 flameScale;

        void Start()
        {
            l = GetComponent<Light>();
            if (l != null) baseIntensity = l.intensity;
            if (Flame != null) flameScale = Flame.localScale;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * 7f) - 0.5f;
            if (l != null) l.intensity = baseIntensity * (1f + n * Amount * 2f);
            if (Flame != null) Flame.localScale = new Vector3(flameScale.x * (1f - n * 0.3f), flameScale.y * (1f + n * 0.8f), flameScale.z * (1f - n * 0.3f));
        }
    }
}
