using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PotatoParty
{
    /// <summary>
    /// Jasny, słoneczny i schludny wygląd: miękkie cienie, niebo z gradientem, delikatna mgiełka
    /// na horyzoncie, lekki bloom i czyste, żywe kolory. Do tego chmurki i balony w tle.
    /// </summary>
    public static class Look
    {
        static Volume volume;
        static Bloom bloom;
        static ColorAdjustments colorAdj;
        static WhiteBalance whiteBalance;
        static SplitToning splitToning;
        static DepthOfField dof;
        static Vignette vignette;
        static ChromaticAberration chroma;
        static LensDistortion lens;
        static Spring punch;
        static float vignetteBase = 0.16f;
        static Light fill, rimLight;
        static ReflectionProbe probe;

        public static void Setup(Camera cam, Light sun)
        {
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;

            // Oświetlenie trzypunktowe: słońce (klucz) + chłodne wypełnienie z naprzeciwka + ciepła kontra od tyłu.
            // Bez cieni – tanie, a postacie i dekoracje dostają objętość i czytelną sylwetkę.
            fill = ExtraDirectional("Wypelnienie", sun.transform.parent, Quaternion.Euler(28f, 150f, 0f), Shapes.Hex("#9FC4FF"), 0.28f);
            rimLight = ExtraDirectional("Kontra", sun.transform.parent, Quaternion.Euler(18f, 200f, 0f), Shapes.Hex("#FFE2B8"), 0.35f);

            try
            {
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urp != null)
                {
                    urp.msaaSampleCount = 4;
                    urp.renderScale = 1f;
                    urp.shadowDistance = 80f;
                    urp.shadowCascadeCount = 4;
                    urp.supportsHDR = true;
                    urp.supportsCameraDepthTexture = true;
                }
                cam.allowMSAA = true;
                cam.allowHDR = true;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                    data.dithering = true;      // bez pasków w gradientach nieba i mgły
                    data.stopNaN = true;
                    data.requiresDepthTexture = true;
                }
                BuildVolume(cam.transform.parent);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Potatoes Party] Efekty obrazu niedostępne: " + e.Message);
            }
        }

        static void BuildVolume(Transform parent)
        {
            if (volume != null) return;
            var go = new GameObject("Efekty obrazu");
            go.transform.SetParent(parent, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.68f);
            bloom.highQualityFiltering.Override(true);
            bloom.tint.Override(new Color(1f, 0.95f, 0.88f));

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.12f);
            colorAdj.contrast.Override(8f);
            colorAdj.saturation.Override(14f);

            whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(0f);
            whiteBalance.tint.Override(0f);

            // delikatny "filmowy" podział tonów: chłodne cienie, ciepłe światła
            splitToning = profile.Add<SplitToning>(true);
            splitToning.shadows.Override(new Color(0.42f, 0.47f, 0.62f));
            splitToning.highlights.Override(new Color(0.62f, 0.55f, 0.45f));
            splitToning.balance.Override(10f);

            // głębia ostrości: tylko w menu i lobby (rozmyte, dalekie tło), w grze wyłączona dla czytelności
            dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Off);
            dof.gaussianStart.Override(32f);
            dof.gaussianEnd.Override(70f);
            dof.gaussianMaxRadius.Override(1.2f);
            dof.highQualitySampling.Override(true);

            // "Aksamitna Bulwa": cienie lekko chłodne i fioletowe (nigdy szare), światła ciepłe.
            // Kontrast temperatur zamiast kontrastu jasności – ziemniaki odcinają się od tła bez ciemnych cieni.
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.94f, 0.94f, 1.08f, 0f));
            smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.01f, 0.95f, 0f));
            smh.shadowsStart.Override(0f);
            smh.shadowsEnd.Override(0.3f);
            smh.highlightsStart.Override(0.55f);
            smh.highlightsEnd.Override(1f);

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.55f);
            vignette.color.Override(Shapes.Hex("#1B1440"));

            // Puls trafienia: aberracja i beczka 0 w spoczynku = efekty wyłączone w uber-shaderze (zero kosztu),
            // włączają się tylko na ~0.2 s po mocnym ciosie (Look.Punch).
            chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0f);
            lens = profile.Add<LensDistortion>(true);
            lens.intensity.Override(0f);
            lens.scale.Override(1f);
        }

        /// <summary>
        /// Puls obrazu po trafieniu (0..1): aberracja chromatyczna, lekka "beczka" i przyciemnienie rogów.
        /// Sprężyna z przestrzałem: obraz najpierw się "zasysa", potem minimalnie odbija.
        /// </summary>
        public static void Punch(float amount)
        {
            if (chroma == null) return;
            GameFeel.Ensure();
            punch.Value = Mathf.Max(punch.Value, Mathf.Clamp01(amount));
            punch.Velocity = 0f;
        }

        /// <summary>Wywoływane co klatkę (czas rzeczywisty) przez GameFeel.</summary>
        public static void TickPunch(float dt)
        {
            if (chroma == null) return;
            if (Mathf.Abs(punch.Value) < 0.002f && Mathf.Abs(punch.Velocity) < 0.01f)
            {
                if (punch.Value != 0f)
                {
                    punch.Reset();
                    chroma.intensity.Override(0f);
                    lens.intensity.Override(0f);
                    vignette.intensity.Override(vignetteBase);
                }
                return;
            }
            punch.Step(0f, 3.2f, 0.55f, dt);
            float p = punch.Value;
            chroma.intensity.Override(Mathf.Clamp01(Mathf.Max(0f, p) * 0.55f));
            // ujemna beczka = obraz "zasysa się" do środka, dodatnia przy odbiciu
            lens.intensity.Override(Mathf.Clamp(-p * 0.22f, -0.3f, 0.12f));
            vignette.intensity.Override(vignetteBase + Mathf.Max(0f, p) * 0.12f);
        }

        static Light ExtraDirectional(string name, Transform parent, Quaternion rot, Color c, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = rot;
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = c;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>Rozmyte tło (menu, lobby, podium) albo ostry obraz w grze.</summary>
        public static void SetBackgroundBlur(bool on, float start = 30f, float end = 70f)
        {
            if (dof == null) return;
            dof.mode.Override(on ? DepthOfFieldMode.Gaussian : DepthOfFieldMode.Off);
            dof.gaussianStart.Override(start);
            dof.gaussianEnd.Override(end);
        }

        /// <summary>Gradacja kolorów dopasowana do mapy: temperatura, nasycenie, kolor poświaty.</summary>
        public static void Grade(MapTheme t)
        {
            if (colorAdj == null) return;
            float temp = 0f, tint = 0f, sat = 14f, contrast = 8f, exposure = 0.12f, bloomI = 0.45f;
            switch (t.Id)
            {
                case MapId.Ice: temp = -14f; sat = 6f; exposure = 0.05f; bloomI = 0.6f; break;
                case MapId.Volcano: temp = 14f; tint = 4f; sat = 18f; contrast = 14f; exposure = 0f; bloomI = 0.75f; break;
                case MapId.Cornfield: temp = 12f; sat = 16f; break;
                case MapId.Beach: temp = 4f; sat = 18f; exposure = 0.18f; break;
                case MapId.Forest: temp = -4f; tint = -6f; sat = 12f; break;
                case MapId.Kitchen: temp = 8f; sat = 10f; break;
                case MapId.Canteen: temp = -2f; sat = 8f; break;
                case MapId.Garden: tint = 4f; sat = 16f; break;
                case MapId.Orchard: temp = 6f; sat = 16f; break;
            }
            whiteBalance.temperature.Override(temp);
            whiteBalance.tint.Override(tint);
            colorAdj.saturation.Override(sat);
            colorAdj.contrast.Override(contrast);
            colorAdj.postExposure.Override(exposure);
            bloom.intensity.Override(bloomI);
            bloom.tint.Override(Color.Lerp(Color.white, t.Glow, 0.25f));
            if (fill != null) fill.color = Color.Lerp(Shapes.Hex("#9FC4FF"), t.Sky, 0.4f);
            if (rimLight != null) rimLight.color = Color.Lerp(Shapes.Hex("#FFE2B8"), t.Glow, 0.35f);
            if (rimLight != null) rimLight.intensity = t.Id == MapId.Volcano ? 0.6f : 0.35f;
        }

        /// <summary>
        /// Odbicia otoczenia: jedna sonda nad areną renderowana raz po zbudowaniu mapy, więc błyszczące
        /// materiały (lód, metal złotej bulwy, mokre błoto) odbijają prawdziwe niebo i dekoracje.
        /// </summary>
        public static void RefreshReflections(Transform world)
        {
            if (world == null) return;
            if (probe == null)
            {
                var go = new GameObject("Sonda odbic");
                go.transform.SetParent(world.parent, false);
                probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                probe.resolution = 128;
                probe.hdr = true;
                probe.boxProjection = false;
                probe.size = new Vector3(400f, 200f, 400f);
                probe.importance = 1;
                probe.intensity = 1f;
                probe.nearClipPlane = 0.5f;
                probe.farClipPlane = 250f;
            }
            probe.transform.position = new Vector3(0f, 4f, 0f);
            probe.RenderProbe();
        }

        /// <summary>Jasne, miękkie światło otoczenia z trzech stron (niebo / horyzont / ziemia).</summary>
        public static void Ambient(MapTheme t)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(Shapes.Hex("#CFE6FF"), t.Sky, 0.3f) * 0.95f;
            RenderSettings.ambientEquatorColor = Color.Lerp(Shapes.Hex("#E9EEF2"), t.Ambient, 0.4f) * 0.8f;
            RenderSettings.ambientGroundColor = Color.Lerp(Shapes.Hex("#9C8E7C"), t.Land, 0.4f) * 0.55f;
        }

        static Material sky;

        /// <summary>Słoneczny dzień: niebo z gradientem, ciepłe słońce, delikatna mgiełka daleko na horyzoncie.</summary>
        public static void Daylight(MapTheme t, Camera cam, Light sun)
        {
            if (sky == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader != null) sky = new Material(shader);
            }
            var horizon = Color.Lerp(t.Sky, Color.white, 0.35f);
            cam.backgroundColor = horizon;
            if (sky != null)
            {
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", Color.Lerp(t.Sky, Shapes.Hex("#4DA6FF"), 0.5f));
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", horizon);
                if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.6f);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.3f);
                if (sky.HasProperty("_SunDisk")) sky.SetFloat("_SunDisk", 0f);
                RenderSettings.skybox = sky;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else cam.clearFlags = CameraClearFlags.SolidColor;
            Ambient(t);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 190f;
            sun.color = t.SunColor;
            sun.intensity = t.SunIntensity * 1.1f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Grade(t);
        }

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
            l.intensity = h * h * 0.75f;
            l.shadows = LightShadows.None;
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
