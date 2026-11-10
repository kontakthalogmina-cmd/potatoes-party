using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PotatoParty
{
    /// <summary>
    /// Biblioteka efektów cząsteczkowych (Shuriken) budowanych w kodzie: miękkie tekstury generowane
    /// przy starcie, materiały URP Particles (alfa i addytywne) i gotowe "przepisy" efektów.
    /// Wszystko bez plików graficznych, zgodnie z resztą projektu.
    /// </summary>
    public static class Fx
    {
        public enum Tex { Dot, Smoke, Ring, Star, Leaf }

        static readonly Dictionary<int, Material> mats = new Dictionary<int, Material>();
        static readonly Dictionary<Tex, Texture2D> texs = new Dictionary<Tex, Texture2D>();
        static Material baseParticle;
        static bool baseTried;

        /// <summary>Kolor kurzu dla bieżącej mapy (piasek, śnieg, sadza...). Ustawiany przy budowie mapy.</summary>
        public static Color DustColor = new Color(0.93f, 0.9f, 0.84f, 0.8f);
        /// <summary>Kamera do wstrząsów (ustawiana przez GameManager).</summary>
        public static CinematicCamera Camera;

        // =====================================================================
        //  Tekstury
        // =====================================================================

        public static Texture2D Texture(Tex kind)
        {
            Texture2D t;
            if (texs.TryGetValue(kind, out t) && t != null) return t;
            int size = kind == Tex.Smoke || kind == Tex.Ring ? 128 : 64;
            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Fx " + kind, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = 0f;
                    switch (kind)
                    {
                        case Tex.Dot:
                            a = Mathf.Exp(-r * r * 4.5f) * Mathf.Clamp01((1f - r) * 6f);
                            break;
                        case Tex.Smoke:
                        {
                            float n = Mathf.PerlinNoise(u * 2.6f + 11.3f, v * 2.6f + 4.1f) * 0.65f + Mathf.PerlinNoise(u * 6f + 2.2f, v * 6f + 9.7f) * 0.35f;
                            float edge = Mathf.Clamp01(1f - r * (0.75f + n * 0.5f));
                            a = Mathf.SmoothStep(0f, 1f, edge) * (0.55f + n * 0.6f);
                            break;
                        }
                        case Tex.Ring:
                        {
                            float d = Mathf.Abs(r - 0.78f);
                            a = Mathf.Exp(-d * d * 260f) + Mathf.Exp(-d * d * 30f) * 0.25f * (r < 0.78f ? 1f : 0f);
                            a *= Mathf.Clamp01((1f - r) * 12f);
                            break;
                        }
                        case Tex.Star:
                        {
                            float ax = Mathf.Abs(u), ay = Mathf.Abs(v);
                            float rays = Mathf.Exp(-ax * 28f) * Mathf.Exp(-ay * 2.2f) + Mathf.Exp(-ay * 28f) * Mathf.Exp(-ax * 2.2f);
                            a = Mathf.Clamp01(rays + Mathf.Exp(-r * r * 22f));
                            a *= Mathf.Clamp01((1f - r) * 4f);
                            break;
                        }
                        case Tex.Leaf:
                        {
                            // wydłużony listek/płatek z żyłką
                            float e = (u * u) / 0.25f + (v * v) / 0.9f;
                            a = Mathf.Clamp01((1f - e) * 8f);
                            if (Mathf.Abs(u) < 0.03f && Mathf.Abs(v) < 0.8f) a *= 0.7f;
                            break;
                        }
                    }
                    byte b = (byte)(Mathf.Clamp01(a) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, b);
                }
            }
            t.SetPixels32(px);
            t.Apply(true, true);
            texs[kind] = t;
            return t;
        }

        // =====================================================================
        //  Materiały
        // =====================================================================

        static Material BaseParticle()
        {
            if (baseTried) return baseParticle;
            baseTried = true;
            // materiał z Resources (ProjectSetup) gwarantuje, że shader cząsteczek trafi do buildu
            var res = Resources.Load<Material>("PotatoParty/ParticleUnlit");
            if (res != null) { baseParticle = res; return baseParticle; }
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null || GraphicsSettings.currentRenderPipeline == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) baseParticle = new Material(shader);
            return baseParticle;
        }

        /// <summary>Materiał cząsteczek: miękka tekstura, mieszanie alfa albo addytywne (świecące).</summary>
        public static Material Mat(Tex kind, bool additive)
        {
            int key = (int)kind * 2 + (additive ? 1 : 0);
            Material m;
            if (mats.TryGetValue(key, out m) && m != null) return m;
            var b = BaseParticle();
            if (b == null) return null;
            m = new Material(b) { name = "Fx " + kind + (additive ? " (add)" : "") };
            var tex = Texture(kind);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2f : 0f);
            SetF(m, "_SrcBlend", additive ? (float)BlendMode.SrcAlpha : (float)BlendMode.SrcAlpha);
            SetF(m, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            SetF(m, "_SrcBlendAlpha", (float)BlendMode.One);
            SetF(m, "_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            SetF(m, "_ZWrite", 0f);
            SetF(m, "_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent + (additive ? 10 : 0);
            mats[key] = m;
            return m;
        }

        static void SetF(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }

        // =====================================================================
        //  Budowa systemu
        // =====================================================================

        // ---------------- pula jednorazowych efektów ----------------
        // Wcześniej każdy efekt = new GameObject + AddComponent<ParticleSystem> + Destroy po ~1 s.
        // Wybuch to 6 systemów i światło: kilkadziesiąt alokacji natywnych i zarządzanych, skoki GC
        // dokładnie w najbardziej "soczystym" momencie. Teraz system po zakończeniu się wyłącza
        // (StopAction.Disable) i czeka w puli pod swoją nazwą; przepis ustawia moduły od nowa.
        const int PoolCapPerRecipe = 24;
        static readonly Dictionary<string, List<ParticleSystem>> pool = new Dictionary<string, List<ParticleSystem>>();

        static ParticleSystem Rent(string name, Transform parent, Vector3 pos)
        {
            List<ParticleSystem> list;
            if (!pool.TryGetValue(name, out list)) return null;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var ps = list[i];
                if (ps == null) { list.RemoveAt(i); continue; } // rodzic (mapa) zniszczony
                if (ps.gameObject.activeSelf) continue;          // jeszcze gra
                var tr = ps.transform;
                tr.SetParent(parent, false);
                tr.SetPositionAndRotation(pos, Quaternion.identity);
                tr.localScale = Vector3.one;
                ps.gameObject.SetActive(true);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return ps;
            }
            return null;
        }

        /// <summary>Pusty system cząsteczek z rozsądnymi ustawieniami; jednorazowe wracają po zakończeniu do puli.</summary>
        public static ParticleSystem Make(string name, Transform parent, Vector3 pos, Tex tex, bool additive, bool loop = false)
        {
            if (!loop)
            {
                var pooled = Rent(name, parent, pos);
                if (pooled != null) return pooled;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = loop;
            main.duration = loop ? 5f : 0.5f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 400;
            if (!loop)
            {
                List<ParticleSystem> list;
                if (!pool.TryGetValue(name, out list)) { list = new List<ParticleSystem>(8); pool[name] = list; }
                if (list.Count < PoolCapPerRecipe)
                {
                    list.Add(ps);
                    main.stopAction = ParticleSystemStopAction.Disable;
                }
                else main.stopAction = ParticleSystemStopAction.Destroy; // nadmiar w szczycie: jak dawniej
            }
            var emission = ps.emission;
            emission.enabled = loop;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat(tex, additive);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = additive ? -10f : 0f;
            return ps;
        }

        // Krzywe i gradienty z pamięci podręcznej (klucz = hash wartości): przepisy wołają je przy każdym
        // efekcie, a AnimationCurve/Gradient to obiekty na stercie. Zostaje tylko tablica params (kilkadziesiąt B).
        static readonly Dictionary<int, AnimationCurve> curveCache = new Dictionary<int, AnimationCurve>();
        static readonly Dictionary<int, Gradient> gradientCache = new Dictionary<int, Gradient>();

        static int Hash(int h, float v) { unchecked { return h * 31 + v.GetHashCode(); } }
        static int Hash(int h, Color c) { return Hash(Hash(Hash(Hash(h, c.r), c.g), c.b), c.a); }

        static ParticleSystem.MinMaxCurve Curve(params float[] kv)
        {
            int h = 17;
            for (int i = 0; i < kv.Length; i++) h = Hash(h, kv[i]);
            AnimationCurve curve;
            if (!curveCache.TryGetValue(h, out curve))
            {
                var keys = new Keyframe[kv.Length / 2];
                for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(kv[i * 2], kv[i * 2 + 1]);
                curve = new AnimationCurve(keys);
                curveCache[h] = curve;
            }
            return new ParticleSystem.MinMaxCurve(1f, curve);
        }

        static ParticleSystem.MinMaxGradient Fade(Color a, Color b, float fadeIn = 0.05f)
        {
            int h = Hash(Hash(Hash(101, a), b), fadeIn);
            Gradient g;
            if (!gradientCache.TryGetValue(h, out g))
            {
                g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
                gradientCache[h] = g;
            }
            return new ParticleSystem.MinMaxGradient(g);
        }

        static ParticleSystem.MinMaxGradient Fade3(Color a, Color b, Color c)
        {
            int h = Hash(Hash(Hash(303, a), b), c);
            Gradient g;
            if (!gradientCache.TryGetValue(h, out g))
            {
                g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.35f), new GradientColorKey(c, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
                gradientCache[h] = g;
            }
            return new ParticleSystem.MinMaxGradient(g);
        }

        static void Sphere(ParticleSystem ps, float radius, float arc = 360f)
        {
            var s = ps.shape;
            s.enabled = true;
            s.shapeType = ParticleSystemShapeType.Sphere;
            s.radius = radius;
            s.arc = arc;
        }

        static void Hemisphere(ParticleSystem ps, float radius)
        {
            var s = ps.shape;
            s.enabled = true;
            s.shapeType = ParticleSystemShapeType.Hemisphere;
            s.radius = radius;
            ps.transform.rotation = Quaternion.Euler(-90f, 0, 0);
        }

        static void Burst(ParticleSystem ps, int count)
        {
            ps.Play();
            ps.Emit(Mathf.Max(1, count));
        }

        /// <summary>Wstrząs kamery (ograniczony, żeby nie męczył przy wielu wybuchach naraz).</summary>
        public static void Shake(float amount)
        {
            Feel.Shake(Mathf.Min(amount, 0.6f));
        }

        /// <summary>Krótki błysk światła (wybuch, uderzenie pioruna, zryw).</summary>
        static readonly List<LightFade> lightPool = new List<LightFade>();
        public static float MaxFlashIntensity = 6f;

        public static void FlashLight(Transform parent, Vector3 pos, Color c, float range, float intensity, float time)
        {
            LightFade f = null;
            for (int i = lightPool.Count - 1; i >= 0; i--)
            {
                var cand = lightPool[i];
                if (cand == null) { lightPool.RemoveAt(i); continue; }
                if (!cand.gameObject.activeSelf) { f = cand; break; }
            }
            if (f == null)
            {
                var go = new GameObject("Blysk");
                var nl = go.AddComponent<Light>();
                nl.type = LightType.Point;
                nl.shadows = LightShadows.None;
                f = go.AddComponent<LightFade>();
                f.Pooled = lightPool.Count < 12;
                if (f.Pooled) lightPool.Add(f);
                go.SetActive(false);
            }
            var tr = f.transform;
            tr.SetParent(parent, false);
            tr.position = pos;
            var l = f.GetComponent<Light>();
            l.color = c;
            l.range = range;
            // błyski 25–30 przy ziemniaku 0.5 m od źródła dawały piksele HDR rzędu tysięcy → bloom na cały ekran
            l.intensity = Mathf.Min(intensity, MaxFlashIntensity);
            f.Life = time;
            f.gameObject.SetActive(true); // OnEnable startuje zanikanie od bieżącej jasności
        }

        // =====================================================================
        //  Przepisy efektów
        // =====================================================================

        /// <summary>Wybuch: błysk, kula ognia, iskry, dym, fala uderzeniowa, światło i wstrząs.</summary>
        public static void Explosion(Transform parent, Vector3 pos, float size)
        {
            // pełne sprzężenie (hit stop, slow-mo, impuls, wibracje) + VFX Graph, jeśli jest w projekcie
            Feel.Explosion(pos, Mathf.Clamp01(size / 1.6f));
            if (Vfx.Explosion(pos, size)) { FlashLight(parent, pos, new Color(1f, 0.6f, 0.25f), 6f * size, 14f, 0.22f); return; }
            float s = Mathf.Max(0.3f, size);

            var flash = Make("Wybuch blysk", parent, pos, Tex.Dot, true);
            var m = flash.main;
            m.startLifetime = 0.16f;
            m.startSpeed = 0f;
            m.startSize = s * 3.2f;
            m.startColor = new Color(1f, 0.9f, 0.6f, 1f);
            var sol = flash.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.6f, 1, 1.2f);
            var col = flash.colorOverLifetime; col.enabled = true; col.color = Fade3(Color.white, new Color(1f, 0.8f, 0.4f), new Color(1f, 0.4f, 0.1f));
            Burst(flash, 1);

            var fire = Make("Wybuch ogien", parent, pos, Tex.Smoke, true);
            m = fire.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2f * s, 6f * s);
            m.startSize = new ParticleSystem.MinMaxCurve(0.9f * s, 1.6f * s);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Sphere(fire, 0.3f * s);
            var lv = fire.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 5f;
            sol = fire.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.4f, 0.3f, 1f, 1, 1.3f);
            col = fire.colorOverLifetime; col.enabled = true;
            col.color = Fade3(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.55f, 0.15f), new Color(0.6f, 0.12f, 0.05f));
            var rot = fire.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            Burst(fire, Mathf.RoundToInt(14 + 6 * s));

            var smoke = Make("Wybuch dym", parent, pos + Vector3.up * 0.3f * s, Tex.Smoke, false);
            m = smoke.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1f * s, 3.5f * s);
            m.startSize = new ParticleSystem.MinMaxCurve(1.2f * s, 2.2f * s);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.12f;
            Sphere(smoke, 0.5f * s);
            lv = smoke.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 2.5f;
            sol = smoke.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.5f, 1, 1.6f);
            col = smoke.colorOverLifetime; col.enabled = true;
            col.color = Fade(new Color(0.38f, 0.33f, 0.32f), new Color(0.75f, 0.72f, 0.72f), 0.15f);
            rot = smoke.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            Burst(smoke, Mathf.RoundToInt(10 + 4 * s));

            Sparks(parent, pos, new Color(1f, 0.7f, 0.25f), Mathf.RoundToInt(30 + 12 * s), 8f + 4f * s, 0.9f);
            Shockwave(parent, new Vector3(pos.x, pos.y - 0.4f * s, pos.z), new Color(1f, 0.85f, 0.6f, 0.9f), 3.2f * s, 0.35f);
            FlashLight(parent, pos + Vector3.up * 0.5f, new Color(1f, 0.6f, 0.25f), 6f + 4f * s, 30f * s, 0.45f);
            Shake(0.15f + 0.12f * s);
        }

        /// <summary>Iskry: rozciągnięte, świecące, spadające z grawitacją.</summary>
        public static void Sparks(Transform parent, Vector3 pos, Color c, int count, float speed, float life = 0.7f)
        {
            var ps = Make("Iskry", parent, pos, Tex.Dot, true);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            m.startColor = new Color(c.r * 2.5f, c.g * 2.5f, c.b * 2.5f, 1f);
            m.gravityModifier = 1.2f;
            Sphere(ps, 0.15f);
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 1.5f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 1, 1, 0);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 1.5f;
            Burst(ps, count);
        }

        /// <summary>Płaska fala uderzeniowa rozchodząca się po ziemi.</summary>
        public static void Shockwave(Transform parent, Vector3 pos, Color c, float size, float life)
        {
            var ps = Make("Fala", parent, pos, Tex.Ring, true);
            var m = ps.main;
            m.startLifetime = life;
            m.startSpeed = 0f;
            m.startSize = size;
            m.startColor = c;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.15f, 0.4f, 0.8f, 1, 1f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade3(Color.white, Color.white, Color.white);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            Burst(ps, 1);
        }

        static ParticleSystem dustPool;

        /// <summary>
        /// Obłoczek kurzu w kolorze podłoża mapy. Jeden wspólny system na świat (Emit z parametrami),
        /// bo kurz spod stóp leci kilka razy na sekundę od każdego gracza.
        /// </summary>
        public static void Dust(Transform parent, Vector3 pos, float size = 1f, int count = 7)
        {
            if (dustPool == null)
            {
                dustPool = Make("Kurz (wspolny)", parent, Vector3.zero, Tex.Smoke, false, true);
                var pm = dustPool.main;
                pm.maxParticles = 800;
                pm.gravityModifier = -0.05f;
                var pe = dustPool.emission;
                pe.rateOverTime = 0f;
                var lv = dustPool.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 3f;
                var sol = dustPool.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.5f, 1, 1.4f);
                var col = dustPool.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, Color.white);
                var rot = dustPool.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
                dustPool.Play();
            }
            var d = DustColor;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                ep.position = pos + dir * 0.2f * size;
                ep.velocity = dir * Random.Range(0.8f, 2.4f) * size + Vector3.up * Random.Range(0.4f, 1.1f);
                ep.startSize = Random.Range(0.35f, 0.7f) * size;
                ep.startLifetime = Random.Range(0.45f, 0.8f);
                ep.rotation = Random.value * 360f;
                ep.startColor = new Color(d.r, d.g, d.b, d.a);
                dustPool.Emit(ep, 1);
            }
        }

        /// <summary>Rozbryzg cieczy (woda, farba, błoto, zupa): kropelki z grawitacją + plama-pierścień.</summary>
        public static void Splash(Transform parent, Vector3 pos, Color c, float size = 1f, int count = 24)
        {
            if (Vfx.Splash(pos, c, size)) return;
            var ps = Make("Rozbryzg", parent, pos, Tex.Dot, false);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f * size, 7f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(0.12f * size, 0.3f * size);
            m.startColor = new Color(c.r, c.g, c.b, 1f);
            m.gravityModifier = 1.6f;
            Hemisphere(ps, 0.2f * size);
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 1, 0.8f, 0.8f, 1, 0);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 1.2f;
            Burst(ps, count);
            Shockwave(parent, pos + Vector3.up * 0.05f, new Color(c.r, c.g, c.b, 0.7f), 2.4f * size, 0.45f);
        }

        /// <summary>Duży plusk po wpadnięciu do wody (kolumna kropel i piana).</summary>
        public static void WaterSplash(Transform parent, Vector3 pos, Color water)
        {
            if (Vfx.Splash(pos, Color.Lerp(water, Color.white, 0.75f), 1.6f)) { Mist(parent, pos, Color.Lerp(water, Color.white, 0.75f), 1.2f); return; }
            var foam = Color.Lerp(water, Color.white, 0.75f);
            var ps = Make("Plusk", parent, pos, Tex.Dot, false);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            m.startColor = new Color(foam.r, foam.g, foam.b, 0.95f);
            m.gravityModifier = 1.8f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.4f;
            ps.transform.rotation = Quaternion.Euler(-90f, 0, 0);
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 1, 1, 0.3f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            Burst(ps, 40);
            Shockwave(parent, pos + Vector3.up * 0.05f, new Color(1f, 1f, 1f, 0.8f), 4f, 0.7f);
            Mist(parent, pos, foam, 1.2f);
        }

        /// <summary>Miękka mgiełka (para, pył wodny, szron).</summary>
        public static void Mist(Transform parent, Vector3 pos, Color c, float size = 1f)
        {
            var ps = Make("Mgielka", parent, pos, Tex.Smoke, false);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.9f * size, 1.6f * size);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new Color(c.r, c.g, c.b, 0.55f);
            m.gravityModifier = -0.05f;
            Sphere(ps, 0.4f * size);
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 2f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.6f, 1, 1.5f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, Color.white, 0.1f);
            Burst(ps, 8);
        }

        /// <summary>Błyszczące gwiazdki (zebrana moneta, nagroda).</summary>
        public static void Sparkle(Transform parent, Vector3 pos, Color c, float size = 1f)
        {
            var ps = Make("Gwiazdki", parent, pos, Tex.Star, true);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * size, 4.5f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(0.25f * size, 0.6f * size);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            m.startColor = new Color(c.r * 2f, c.g * 2f, c.b * 2f, 1f);
            m.gravityModifier = -0.1f;
            Sphere(ps, 0.2f);
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 3f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.2f, 0.2f, 1f, 1, 0f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            Burst(ps, 14);
            var ring = Make("Gwiazdki fala", parent, pos, Tex.Ring, true);
            m = ring.main;
            m.startLifetime = 0.3f;
            m.startSpeed = 0f;
            m.startSize = 1.6f * size;
            m.startColor = new Color(c.r * 1.5f, c.g * 1.5f, c.b * 1.5f, 0.9f);
            var rs = ring.sizeOverLifetime; rs.enabled = true; rs.size = Curve(0, 0.2f, 1, 1f);
            var col = ring.colorOverLifetime; col.enabled = true; col.color = Fade3(Color.white, Color.white, Color.white);
            Burst(ring, 1);
        }

        /// <summary>Uderzenie przy zderzeniu (zryw w przeciwnika): gwiazdka, iskry, mały wstrząs.</summary>
        public static void Impact(Transform parent, Vector3 pos, Color c)
        {
            var star = Make("Uderzenie", parent, pos, Tex.Star, true);
            var m = star.main;
            m.startLifetime = 0.18f;
            m.startSpeed = 0f;
            m.startSize = 1.8f;
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            m.startColor = new Color(1.6f, 1.5f, 1.3f, 1f);
            var sol = star.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.5f, 0.3f, 1f, 1, 0.2f);
            Burst(star, 1);
            Sparks(parent, pos, Color.Lerp(c, Color.white, 0.4f), 14, 7f, 0.4f);
            Shake(0.12f);
        }

        /// <summary>
        /// Trafienie zrywem w wersji "pełnej": gwiazdka HDR (rozbłyska w bloomie), kierunkowy snop iskier
        /// wzdłuż ciosu, pionowy pierścień-fala ustawiony prostopadle do kierunku, kurz pod stopami
        /// i krótkie kolorowe światło. Kamerę i hit-stop obsługuje Feel. To zapasowa wersja grafu VFX PP_Hit.
        /// </summary>
        public static void Impact(Transform parent, Vector3 pos, Color c, Vector3 dir, float power01)
        {
            float p = Mathf.Clamp01(power01);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();

            var star = Make("Uderzenie HDR", parent, pos, Tex.Star, true);
            var m = star.main;
            m.startLifetime = 0.16f + 0.06f * p;
            m.startSpeed = 0f;
            m.startSize = 1.6f + 1.2f * p;
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            // > 1 w HDR: przekracza próg bloomu (0.95), więc gwiazdka "pali" obraz przez 2–3 klatki
            m.startColor = new Color(2.4f, 2.2f, 1.9f, 1f);
            var sol = star.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.35f, 0.25f, 1f, 1, 0.15f);
            Burst(star, 1);

            // snop iskier: stożek 28° w kierunku ciosu, kolor napastnika rozjaśniony do bieli
            var sparks = Make("Iskry kierunkowe", parent, pos, Tex.Dot, true);
            m = sparks.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(6f + 4f * p, 13f + 7f * p);
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            var sc = Color.Lerp(c, Color.white, 0.45f);
            m.startColor = new Color(sc.r * 3f, sc.g * 3f, sc.b * 3f, 1f);
            m.gravityModifier = 0.9f;
            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.08f;
            sparks.transform.rotation = Quaternion.LookRotation(dir + Vector3.up * 0.25f);
            var lv = sparks.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 4f;
            sol = sparks.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 1, 1, 0);
            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.045f;
            sr.lengthScale = 2.2f;
            Burst(sparks, Mathf.RoundToInt(16 + 18 * p));

            // pionowa fala: płaszczyzna prostopadła do ciosu (Local alignment + obrót systemu)
            var ring = Make("Uderzenie fala", parent, pos, Tex.Ring, true);
            m = ring.main;
            m.startLifetime = 0.2f;
            m.startSpeed = 0f;
            m.startSize = 1.4f + 1.2f * p;
            m.startColor = new Color(sc.r * 1.8f, sc.g * 1.8f, sc.b * 1.8f, 0.9f);
            ring.transform.rotation = Quaternion.LookRotation(dir);
            var rs = ring.sizeOverLifetime; rs.enabled = true; rs.size = Curve(0, 0.2f, 0.5f, 0.85f, 1, 1f);
            var rc = ring.colorOverLifetime; rc.enabled = true; rc.color = Fade3(Color.white, Color.white, Color.white);
            var rr = ring.GetComponent<ParticleSystemRenderer>();
            rr.alignment = ParticleSystemRenderSpace.Local;
            Burst(ring, 1);

            Dust(parent, new Vector3(pos.x, pos.y - 0.85f, pos.z), 0.5f + 0.4f * p, 4 + Mathf.RoundToInt(4 * p));
            FlashLight(parent, pos, Color.Lerp(c, Color.white, 0.5f), 3.5f + 2f * p, 6f + 8f * p, 0.14f);
        }

        /// <summary>Grudy błota rozpryśnięte z punktu (fałszywe złoto, kałuża, kombajn).</summary>
        public static void MudSplat(Transform parent, Vector3 pos, Color mud)
        {
            Splash(parent, pos, mud, 1.1f, 30);
            var ps = Make("Blotko", parent, pos, Tex.Smoke, false);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new Color(mud.r, mud.g, mud.b, 1f);
            m.gravityModifier = 2f;
            Hemisphere(ps, 0.3f);
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 1, 1, 0.4f);
            Burst(ps, 10);
        }

        /// <summary>Szron: lodowe okruchy, chłodna mgiełka i błysk – przy zamrożeniu.</summary>
        public static void FrostBurst(Transform parent, Vector3 pos, float size = 1f)
        {
            var ice = new Color(0.75f, 0.92f, 1f);
            var ps = Make("Szron", parent, pos, Tex.Star, true);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * size, 4f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(0.15f * size, 0.4f * size);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            m.startColor = new Color(ice.r * 1.6f, ice.g * 1.6f, ice.b * 1.8f, 1f);
            m.gravityModifier = 0.4f;
            Sphere(ps, 0.4f * size);
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 2f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 1, 1, 0);
            Burst(ps, Mathf.RoundToInt(22 * size));
            Mist(parent, pos, ice, size);
            FlashLight(parent, pos, new Color(0.6f, 0.85f, 1f), 4f * size, 6f, 0.4f);
        }

        /// <summary>Żar: unoszące się, migoczące iskierki (przypalenie, lawa).</summary>
        public static void Embers(Transform parent, Vector3 pos, float size = 1f, int count = 18)
        {
            var ps = Make("Zar", parent, pos, Tex.Dot, true);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.06f * size, 0.16f * size);
            m.startColor = new Color(4f, 1.3f, 0.3f, 1f);
            m.gravityModifier = -0.25f;
            Sphere(ps, 0.4f * size);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.8f; noise.frequency = 1.2f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade3(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.45f, 0.1f), new Color(0.6f, 0.1f, 0.05f));
            Burst(ps, count);
        }

        // =====================================================================
        //  Atmosfera map (zapętlone)
        // =====================================================================

        /// <summary>Unoszące się drobinki/świetliki/płatki nad całą areną w zależności od mapy.</summary>
        public static void Ambient(MapTheme t, Transform env, float radius)
        {
            float area = radius * 2.4f + 14f;
            switch (t.Id)
            {
                case MapId.Ice:
                    Loop(env, "Snieg", Tex.Dot, false, area, 14f, -1f, 0.08f, 0.22f, Color.white, 70f, 8f, 0.25f, 0.6f);
                    Loop(env, "Iskrzenie lodu", Tex.Star, true, area, 1.2f, 0f, 0.15f, 0.35f, new Color(1.4f, 1.7f, 2f), 6f, 1.5f, 0f, 0f);
                    break;
                case MapId.Volcano:
                    Loop(env, "Zar w powietrzu", Tex.Dot, true, area, 1f, 1f, 0.05f, 0.14f, new Color(4f, 1.2f, 0.25f), 35f, 6f, 0.8f, 1.5f);
                    Loop(env, "Popiol", Tex.Dot, false, area, 16f, -1f, 0.05f, 0.12f, new Color(0.35f, 0.32f, 0.32f, 0.9f), 30f, 9f, 0.15f, 0.8f);
                    break;
                case MapId.Forest:
                    Loop(env, "Swietliki", Tex.Dot, true, area * 0.8f, 3f, 0f, 0.1f, 0.2f, new Color(1.6f, 2.6f, 0.6f), 14f, 7f, 0.6f, 0.5f, true);
                    Loop(env, "Liscie", Tex.Leaf, false, area, 15f, -1f, 0.25f, 0.45f, Shapes.Hex("#E89A3C"), 6f, 9f, 0.4f, 1f, false, true);
                    break;
                case MapId.Orchard:
                    Loop(env, "Platki", Tex.Leaf, false, area, 14f, -1f, 0.18f, 0.32f, Shapes.Hex("#FFC3D7"), 10f, 9f, 0.4f, 1f, false, true);
                    Loop(env, "Pylek", Tex.Dot, true, area, 3f, 0f, 0.05f, 0.1f, new Color(1.2f, 1.1f, 0.7f, 0.7f), 18f, 7f, 0.3f, 0.3f);
                    break;
                case MapId.Garden:
                    Loop(env, "Platki", Tex.Leaf, false, area, 14f, -1f, 0.16f, 0.3f, Shapes.Hex("#FF9EC4"), 9f, 9f, 0.4f, 1f, false, true);
                    Loop(env, "Pylek", Tex.Dot, true, area, 3f, 0f, 0.05f, 0.1f, new Color(1.2f, 1.1f, 0.7f, 0.7f), 16f, 7f, 0.3f, 0.3f);
                    break;
                case MapId.Beach:
                    Loop(env, "Blyski na wodzie", Tex.Star, true, area * 1.6f, 0.2f, 0f, 0.3f, 0.7f, new Color(2.2f, 2.1f, 1.8f), 30f, 0.6f, 0f, 0f, false, false, t.PitHeight + 0.15f);
                    break;
                case MapId.Kitchen:
                case MapId.Canteen:
                    Loop(env, "Kurz w swietle", Tex.Dot, true, area, 4f, 0f, 0.04f, 0.09f, new Color(1.1f, 1f, 0.85f, 0.6f), 20f, 8f, 0.2f, 0.2f);
                    break;
                default:
                    Loop(env, "Pylek", Tex.Dot, true, area, 3.5f, 0f, 0.05f, 0.11f, new Color(1.2f, 1.1f, 0.75f, 0.7f), 18f, 8f, 0.3f, 0.3f);
                    break;
            }
        }

        static ParticleSystem Loop(Transform env, string name, Tex tex, bool additive, float area, float height, float dir,
            float minSize, float maxSize, Color c, float rate, float life, float noiseStrength, float fallSpeed,
            bool blink = false, bool tumble = false, float y = float.NaN)
        {
            float baseY = float.IsNaN(y) ? (dir < 0 ? height : 0.6f) : y;
            var ps = Make(name, env, new Vector3(0, baseY, 0), tex, additive, true);
            var m = ps.main;
            m.prewarm = true;
            m.duration = 5f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            m.startColor = c;
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.maxParticles = 600;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area, dir < 0 ? 0.5f : height, area);
            if (dir < 0) shape.position = Vector3.zero; else shape.position = new Vector3(0, height * 0.5f, 0);
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = rate;
            if (fallSpeed > 0f)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
                vel.y = dir < 0 ? new ParticleSystem.MinMaxCurve(-fallSpeed * 1.4f, -fallSpeed * 0.7f) : new ParticleSystem.MinMaxCurve(fallSpeed * 0.3f, fallSpeed);
                vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            }
            if (noiseStrength > 0f)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = noiseStrength;
                noise.frequency = 0.35f;
                noise.scrollSpeed = 0.2f;
            }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            if (blink)
            {
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.1f, 0.3f), new GradientAlphaKey(1f, 0.5f),
                            new GradientAlphaKey(0.15f, 0.7f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
            }
            else col.color = Fade(Color.white, Color.white, 0.15f);
            if (tumble)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            }
            ps.Play();
            return ps;
        }
    }

    /// <summary>Szybko gasnące światło (błysk wybuchu).</summary>
    public class LightFade : MonoBehaviour
    {
        public float Life = 0.4f;
        /// <summary>true = po zgaśnięciu wraca do puli (wyłącza się), false = usuwa się.</summary>
        public bool Pooled;
        Light l;
        float start, t;

        void OnEnable()
        {
            if (l == null) l = GetComponent<Light>();
            start = l != null ? l.intensity : 0f;
            t = 0f;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, Life));
            if (l != null) l.intensity = start * (1f - k) * (1f - k);
            if (k >= 1f)
            {
                if (Pooled) gameObject.SetActive(false);
                else Destroy(gameObject);
            }
        }
    }
}
