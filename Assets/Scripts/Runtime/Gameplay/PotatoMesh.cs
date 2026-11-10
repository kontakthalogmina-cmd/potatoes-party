using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Proceduralna bryła ziemniaka zamiast idealnej kuli. Każdy gracz dostaje własny, powtarzalny kształt (seed):
    /// <list type="bullet">
    /// <item>asymetria: jedna strona "brzuchata", lekkie wygięcie w pionie (ziemniak nigdy nie jest prosty),</item>
    /// <item>guzy: dwie oktawy szumu 3D (duże bulwy + drobne nierówności),</item>
    /// <item>"oczka" ziemniaka: 6–9 wgłębień rozsianych po bokach i tyle, z małym wałkiem wokół,</item>
    /// <item>strefa twarzy (przód, góra) wygładzona, żeby oczy i buzia postaci siedziały równo.</item>
    /// </list>
    /// Promień bazowy 0.5 (jak sfera Unity), więc skala z <see cref="Potato"/> działa bez zmian.
    /// </summary>
    public static class PotatoMesh
    {
        public const int Lon = 56, Lat = 36;
        public static float BigLumps = 0.075f, SmallLumps = 0.022f, Lopsided = 0.09f, Bend = 0.06f;
        public static float EyeDepth = 0.04f, EyeRadius = 0.11f;

        static readonly Dictionary<int, Mesh> cache = new Dictionary<int, Mesh>();

        public static Mesh Get(int seed)
        {
            Mesh m;
            if (cache.TryGetValue(seed, out m) && m != null) return m;
            m = Build(seed);
            cache[seed] = m;
            return m;
        }

        static Mesh Build(int seed)
        {
            var rng = new System.Random(seed * 92821 + 17);
            Vector3 noiseOff = new Vector3(F(rng) * 50f, F(rng) * 50f, F(rng) * 50f);
            // kierunek "brzucha" – zawsze trochę w bok/tył, nie w twarz
            float ang = Mathf.Lerp(100f, 260f, (float)rng.NextDouble()) * Mathf.Deg2Rad;
            Vector3 bulgeDir = new Vector3(Mathf.Sin(ang), F(rng) * 0.4f, Mathf.Cos(ang)).normalized;
            float bendSign = rng.NextDouble() < 0.5 ? -1f : 1f;

            // oczka ziemniaka: losowe kierunki poza strefą twarzy
            var eyes = new List<Vector3>();
            int eyeCount = 6 + rng.Next(4);
            for (int guard = 0; eyes.Count < eyeCount && guard < 200; guard++)
            {
                var d = new Vector3(F(rng), F(rng), F(rng));
                if (d.sqrMagnitude < 0.01f || d.sqrMagnitude > 1f) continue;
                d.Normalize();
                if (FaceWeight(d) > 0.05f) continue;
                bool close = false;
                for (int i = 0; i < eyes.Count; i++) if (Vector3.Dot(eyes[i], d) > 0.86f) { close = true; break; }
                if (!close) eyes.Add(d);
            }

            int vc = (Lon + 1) * (Lat + 1);
            var verts = new Vector3[vc];
            var uvs = new Vector2[vc];
            int k = 0;
            for (int y = 0; y <= Lat; y++)
            {
                float v = (float)y / Lat;
                float theta = v * Mathf.PI;               // 0 = góra
                float sy = Mathf.Cos(theta), sr = Mathf.Sin(theta);
                for (int x = 0; x <= Lon; x++)
                {
                    float u = (float)x / Lon;
                    float phi = u * Mathf.PI * 2f;
                    var n = new Vector3(sr * Mathf.Sin(phi), sy, sr * Mathf.Cos(phi)); // z+ = przód
                    float face = FaceWeight(n);

                    float r = 1f;
                    // asymetria: jedna strona pełniejsza
                    r += Lopsided * Mathf.Max(0f, Vector3.Dot(n, bulgeDir)) * Mathf.Max(0f, Vector3.Dot(n, bulgeDir));
                    // guzy (dwie oktawy); w strefie twarzy mocno przygaszone
                    float lumps = BigLumps * Noise(n * 1.7f + noiseOff) + SmallLumps * Noise(n * 4.6f + noiseOff * 1.3f);
                    r += lumps * Mathf.Lerp(1f, 0.25f, face);
                    // oczka: wgłębienie (gauss) z delikatnym wałkiem dookoła
                    for (int i = 0; i < eyes.Count; i++)
                    {
                        float dd = Vector3.Distance(n, eyes[i]) / EyeRadius;
                        if (dd > 2.5f) continue;
                        r -= EyeDepth * Mathf.Exp(-dd * dd * 1.8f);
                        r += EyeDepth * 0.25f * Mathf.Exp(-(dd - 1.4f) * (dd - 1.4f) * 3f);
                    }

                    var p = n * (0.5f * r);
                    // wygięcie: ziemniak jest lekko "banan"
                    p.x += bendSign * Bend * (p.y * p.y * 4f - 0.25f);
                    verts[k] = p;
                    uvs[k] = new Vector2(u, 1f - v);
                    k++;
                }
            }

            var tris = new int[Lon * Lat * 6];
            int t = 0;
            for (int y = 0; y < Lat; y++)
                for (int x = 0; x < Lon; x++)
                {
                    int a = y * (Lon + 1) + x, b = a + Lon + 1;
                    // Unity: przód = zgodnie z ruchem wskazówek zegara patrząc z zewnątrz
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }

            var mesh = new Mesh { name = "Ziemniak " + seed };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            WeldSeamNormals(mesh);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Szew UV (u=0 i u=1) i bieguny mają osobne wierzchołki – uśredniamy normalne, żeby nie było rysy.</summary>
        static void WeldSeamNormals(Mesh mesh)
        {
            var n = mesh.normals;
            for (int y = 0; y <= Lat; y++)
            {
                int a = y * (Lon + 1), b = a + Lon;
                var avg = (n[a] + n[b]).normalized;
                n[a] = avg; n[b] = avg;
            }
            for (int pole = 0; pole < 2; pole++)
            {
                int row = pole == 0 ? 0 : Lat;
                Vector3 sum = Vector3.zero;
                for (int x = 0; x <= Lon; x++) sum += n[row * (Lon + 1) + x];
                sum.Normalize();
                for (int x = 0; x <= Lon; x++) n[row * (Lon + 1) + x] = sum;
            }
            mesh.normals = n;
        }

        /// <summary>1 w środku twarzy (przód, nieco powyżej równika), 0 poza nią.</summary>
        static float FaceWeight(Vector3 n)
        {
            float front = Mathf.InverseLerp(0.35f, 0.75f, n.z);
            float band = 1f - Mathf.InverseLerp(0.3f, 0.65f, Mathf.Abs(n.y - 0.15f));
            float side = 1f - Mathf.InverseLerp(0.45f, 0.75f, Mathf.Abs(n.x));
            return front * band * side;
        }

        static float F(System.Random r) { return (float)r.NextDouble() * 2f - 1f; }

        // gładki szum wartości 3D w zakresie ok. -1..1
        static float Noise(Vector3 p)
        {
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
            float fx = p.x - ix, fy = p.y - iy, fz = p.z - iz;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
            float a = Mathf.Lerp(H(ix, iy, iz), H(ix + 1, iy, iz), fx);
            float b = Mathf.Lerp(H(ix, iy + 1, iz), H(ix + 1, iy + 1, iz), fx);
            float c = Mathf.Lerp(H(ix, iy, iz + 1), H(ix + 1, iy, iz + 1), fx);
            float d = Mathf.Lerp(H(ix, iy + 1, iz + 1), H(ix + 1, iy + 1, iz + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz) * 2f - 1f;
        }

        static float H(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
