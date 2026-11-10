using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// "Strzępki ziemniaka": przy trafieniu zrywem z ofiary odrywają się i sypią żółte kawałki miąższu,
    /// płatki skórki w jej kolorze i drobne okruchy. Kawałki są fizyczne (kolizja ze światem, odbicie,
    /// toczenie), leżą chwilę na ziemi i znikają – po ostrej rundzie arena jest "posypana" frytkami.
    /// <para>Implementacja: trzy TRWAŁE ParticleSystem w przestrzeni świata (miąższ, skórka, okruchy),
    /// do których emitujemy ręcznie przez <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/>
    /// z parametrami każdej cząstki (pozycja, prędkość, rotacja 3D, kolor, rozmiar). Setki trafień = 3 draw calle,
    /// zero alokacji, zero Instantiate. Siatki kawałków są proceduralne (nieregularne bryłki i zawinięte płatki).</para>
    /// </summary>
    public static class PotatoShreds
    {
        // ---------------- strojenie (Inspector-owe wartości jako pola statyczne) ----------------
        /// <summary>Globalny mnożnik liczby kawałków (np. 0.5 na słabszym sprzęcie).</summary>
        public static float Amount = 1f;
        public static int FleshMin = 10, FleshMax = 26;    // bryłki miąższu: muśnięcie → pełny zryw
        public static int PeelMin = 4, PeelMax = 12;       // płatki skórki
        public static int CrumbMin = 14, CrumbMax = 40;    // okruchy
        public static float SpeedMin = 3.5f, SpeedMax = 10f; // m/s wzdłuż ciosu
        public static float UpMin = 2.5f, UpMax = 6.5f;      // m/s w górę
        public static float ConeDeg = 55f;                   // rozrzut wokół kierunku ciosu
        public static float FleshLife = 3.2f, PeelLife = 2.6f, CrumbLife = 0.9f; // s
        public static float GroundLinger = 0.75f;            // ułamek życia, po którym kawałek zaczyna się kurczyć

        // miąższ: ciepłe, nasycone żółcie (pod ACES wychodzą jak świeżo przekrojony ziemniak)
        static readonly Color FleshA = new Color(1.00f, 0.86f, 0.36f);
        static readonly Color FleshB = new Color(0.98f, 0.76f, 0.24f);
        static readonly Color FleshC = new Color(1.00f, 0.93f, 0.58f);

        static ParticleSystem flesh, peel, crumbs;
        static Transform root;
        static ParticleSystem.EmitParams ep;

        /// <summary>Wyrwij strzępki z ofiary. contact – punkt zderzenia, dir – kierunek ciosu (od napastnika), power01 – siła.</summary>
        public static void Burst(Potato victim, Vector3 contact, Vector3 dir, float power01)
        {
            if (Amount <= 0f) return;
            Ensure();
            if (flesh == null) return;

            power01 = Mathf.Clamp01(power01);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();

            Color skin = victim != null && victim.Skin != null ? victim.Skin.BodyColor(victim.Color) : new Color(0.62f, 0.45f, 0.28f);
            // skórka ziemniaka to zawsze trochę ziemi: przyciemniamy i zbrązowiamy kolor gracza
            Color peelCol = Color.Lerp(skin, new Color(0.45f, 0.31f, 0.18f), 0.35f) * 0.9f;
            peelCol.a = 1f;

            // kawałki wylatują z PRZECIWNEJ strony ofiary niż napastnik (jakby cios ją "przebił")
            Vector3 origin = contact + dir * 0.25f;
            float k = power01 * power01;

            Emit(flesh, Count(FleshMin, FleshMax, k), origin, dir, power01, 0.07f, 0.17f, FleshLife, FleshA, FleshB, FleshC, 1f);
            Emit(peel, Count(PeelMin, PeelMax, k), origin, dir, power01, 0.09f, 0.2f, PeelLife, peelCol, peelCol * 0.85f, Color.Lerp(peelCol, Color.white, 0.12f), 0.85f);
            Emit(crumbs, Count(CrumbMin, CrumbMax, k), origin, dir, power01, 0.025f, 0.05f, CrumbLife, FleshA, FleshC, Color.white, 1.25f);
        }

        /// <summary>Zmiana mapy / restart: sprzątamy kawałki leżące na ziemi.</summary>
        public static void Clear()
        {
            if (flesh != null) flesh.Clear();
            if (peel != null) peel.Clear();
            if (crumbs != null) crumbs.Clear();
        }

        static int Count(int min, int max, float k)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(min, max, k) * Amount));
        }

        static void Emit(ParticleSystem ps, int count, Vector3 origin, Vector3 dir, float power01,
            float sizeMin, float sizeMax, float life, Color a, Color b, Color c, float speedMul)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            for (int i = 0; i < count; i++)
            {
                // kierunek: stożek wokół ciosu, spłaszczony w pionie (kawałki lecą "wachlarzem", nie fontanną)
                float yaw = Random.Range(-ConeDeg, ConeDeg) * Mathf.Deg2Rad;
                Vector3 d = dir * Mathf.Cos(yaw) + side * Mathf.Sin(yaw);
                float speed = Mathf.Lerp(SpeedMin, SpeedMax, power01) * Random.Range(0.55f, 1.15f) * speedMul;
                float up = Random.Range(UpMin, Mathf.Lerp(UpMin, UpMax, 0.4f + 0.6f * power01));
                // 15% kawałków odbija wstecz (rozbryzg na napastnika) – czytelniej, że to ofiara "się sypie"
                if (Random.value < 0.15f) d = Vector3.Reflect(d, dir) * 0.6f;

                ep.position = origin + Random.insideUnitSphere * 0.22f + Vector3.up * Random.Range(-0.1f, 0.25f);
                ep.velocity = d * speed + Vector3.up * up;
                ep.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                ep.angularVelocity3D = Random.insideUnitSphere * Random.Range(360f, 1100f);
                float s = Random.Range(sizeMin, sizeMax) * (0.8f + 0.4f * power01);
                // bryłki nie są sześcianami: lekko rozciągnięte w jednej osi
                ep.startSize3D = new Vector3(s * Random.Range(0.8f, 1.5f), s * Random.Range(0.6f, 1f), s * Random.Range(0.8f, 1.3f));
                ep.startLifetime = life * Random.Range(0.75f, 1.2f);
                float t = Random.value;
                Color col = t < 0.5f ? Color.Lerp(a, b, t * 2f) : Color.Lerp(b, c, (t - 0.5f) * 2f);
                col.a = 1f;
                ep.startColor = col;
                ps.Emit(ep, 1);
            }
        }

        // ---------------- budowa systemów ----------------

        static void Ensure()
        {
            if (flesh != null) return;
            if (root == null)
            {
                var go = new GameObject("Strzępki ziemniaka");
                Object.DontDestroyOnLoad(go);
                root = go.transform;
            }
            var lit = LitMaterial();
            flesh = Build("Miąższ", 900, lit, new[] { ChunkMesh(1), ChunkMesh(2), ChunkMesh(3) }, 1.7f, 0.32f, 0.28f, true);
            peel = Build("Skórka", 400, lit, new[] { PeelMesh(4), PeelMesh(5) }, 1.25f, 0.2f, 0.12f, true);
            crumbs = Build("Okruchy", 700, lit, new[] { ChunkMesh(6) }, 1.4f, 0.4f, 0.35f, false);
        }

        static ParticleSystem Build(string name, int max, Material mat, Mesh[] meshes, float gravity, float bounce, float dampen, bool linger)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = max;
            main.gravityModifier = gravity;           // cięższe niż iskry: kawałki "plaskają", nie szybują
            main.startSize3D = true;
            main.startRotation3D = true;
            main.startSpeed = 0f;
            main.useUnscaledTime = false;             // bez hit-stopu w zrywie i tak nie ma spowolnienia
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var em = ps.emission;
            em.enabled = false;                       // emitujemy tylko ręcznie
            var shape = ps.shape;
            shape.enabled = false;

            // kurczenie się na końcu życia (kawałek leży, potem "wsiąka" w ziemię)
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            float hold = linger ? GroundLinger : 0.35f;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.06f, 1f), new Keyframe(hold, 1f), new Keyframe(1f, 0f)));

            // opór powietrza: wirujące płatki zwalniają, bryłki prawie nie
            var lim = ps.limitVelocityOverLifetime;
            lim.enabled = true;
            lim.limit = 40f;
            lim.drag = linger ? 0.25f : 0.9f;
            lim.multiplyDragByParticleSize = false;
            lim.multiplyDragByParticleVelocity = true;

            // fizyka: odbijają się od areny i toczą (kolizja tylko ze światem, bez zdarzeń – tanio)
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.quality = ParticleSystemCollisionQuality.Medium;
            col.bounce = bounce;
            col.dampen = dampen;
            col.lifetimeLoss = 0f;
            col.radiusScale = 0.6f;
            col.enableDynamicColliders = false;
            col.sendCollisionMessages = false;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.SetMeshes(meshes);
            r.sharedMaterial = mat;
            r.alignment = ParticleSystemRenderSpace.World;
            r.shadowCastingMode = linger ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            r.enableGPUInstancing = true;

            ps.Play();
            return ps;
        }

        /// <summary>Oświetlony materiał cząsteczek z kolorem z wierzchołków (miąższ ma błyszczeć jak mokry).</summary>
        static Material LitMaterial()
        {
            var res = Resources.Load<Material>("PotatoParty/ParticleLit");
            if (res != null) return res;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Lit");
            if (sh == null) sh = Shader.Find("Particles/Standard Surface");
            if (sh == null) return Fx.Mat(Fx.Tex.Dot, false);
            var m = new Material(sh) { name = "PotatoShreds (lit)" };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.62f);  // świeży, wilgotny miąższ
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.62f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            m.enableInstancing = true;
            return m;
        }

        // ---------------- siatki ----------------

        static Mesh[] shared;
        /// <summary>Siatka bryłki miąższu (0–3) do użycia poza cząsteczkami, np. zbieralne strzępki.</summary>
        public static Mesh Chunk(int i)
        {
            if (shared == null) shared = new[] { ChunkMesh(11), ChunkMesh(12), ChunkMesh(13), ChunkMesh(14) };
            return shared[Mathf.Abs(i) % shared.Length];
        }

        /// <summary>Nieregularna bryłka: sześcian o losowo przesuniętych narożnikach, płaskie normalne (ostre krawędzie cięcia).</summary>
        static Mesh ChunkMesh(int seed)
        {
            var rng = new System.Random(seed * 7919);
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var b = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
                c[i] = b + new Vector3(R(rng, 0.22f), R(rng, 0.22f), R(rng, 0.22f));
            }
            int[][] faces =
            {
                new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 },
                new[] { 2, 6, 7, 3 }, new[] { 0, 4, 6, 2 }, new[] { 1, 3, 7, 5 },
            };
            return FlatMesh("chunk" + seed, c, faces);
        }

        /// <summary>Płatek skórki: cienki, wygięty czworokąt z grubością (widać brzeg miąższu).</summary>
        static Mesh PeelMesh(int seed)
        {
            var rng = new System.Random(seed * 104729);
            float bend = 0.25f + (float)rng.NextDouble() * 0.2f;
            const float th = 0.12f;
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float x = (i & 1) == 0 ? -0.5f : 0.5f;
                float z = (i & 4) == 0 ? -0.5f : 0.5f;
                float y = ((i & 2) == 0 ? -th : th) * 0.5f + bend * (x * x * 2f - 0.25f);
                c[i] = new Vector3(x + R(rng, 0.12f), y, z + R(rng, 0.15f));
            }
            int[][] faces =
            {
                new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 },
                new[] { 2, 6, 7, 3 }, new[] { 0, 4, 6, 2 }, new[] { 1, 3, 7, 5 },
            };
            return FlatMesh("peel" + seed, c, faces);
        }

        static Mesh FlatMesh(string name, Vector3[] corners, int[][] quads)
        {
            var v = new Vector3[quads.Length * 4];
            var n = new Vector3[v.Length];
            var t = new int[quads.Length * 6];
            var center = Vector3.zero;
            for (int i = 0; i < corners.Length; i++) center += corners[i];
            center /= corners.Length;
            for (int f = 0; f < quads.Length; f++)
            {
                var q = quads[f];
                for (int j = 0; j < 4; j++) v[f * 4 + j] = corners[q[j]];
                var normal = Vector3.Cross(v[f * 4 + 1] - v[f * 4], v[f * 4 + 2] - v[f * 4]).normalized;
                // ściana ma patrzeć NA ZEWNĄTRZ bryłki (Unity: trójkąty zgodnie z ruchem wskazówek zegara od frontu)
                var fc = (v[f * 4] + v[f * 4 + 1] + v[f * 4 + 2] + v[f * 4 + 3]) * 0.25f;
                bool flip = Vector3.Dot(normal, fc - center) < 0f;
                if (flip) normal = -normal;
                for (int j = 0; j < 4; j++) n[f * 4 + j] = normal;
                int a = f * 4, b = flip ? f * 4 + 1 : f * 4 + 3, c = f * 4 + 2, d = flip ? f * 4 + 3 : f * 4 + 1;
                t[f * 6 + 0] = a; t[f * 6 + 1] = d; t[f * 6 + 2] = c;
                t[f * 6 + 3] = a; t[f * 6 + 4] = c; t[f * 6 + 5] = b;
            }
            var m = new Mesh { name = name, vertices = v, normals = n, triangles = t };
            m.RecalculateBounds();
            return m;
        }

        static float R(System.Random rng, float a) { return ((float)rng.NextDouble() * 2f - 1f) * a; }
    }
}
