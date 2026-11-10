using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PotatoParty
{
    /// <summary>
    /// Post-processing filmowy (URP Volume). Kluczowe decyzje:
    /// <list type="bullet">
    /// <item><b>Tonemapping ACES</b> + HDR 64-bit: miękkie przepalenia świateł zamiast twardego obcięcia.</item>
    /// <item><b>Bloom fizyczny</b>: próg 1.0 – świeci tylko to, co w HDR jest jaśniejsze niż biel
    ///       (iskry, żar, odblaski), a nie jasna koszulka. Scatter 0.72, clamp 40.</item>
    /// <item><b>Bokeh DoF z automatyczną przysłoną</b>: ostrość zawsze na punkcie kadru, a liczba f dobierana co
    ///       klatkę tak, żeby KAŻDY żywy gracz miał krążek rozmycia ≤ 1.2 px. Tło rozmywa się tak mocno,
    ///       jak tylko pozwala rozstawienie graczy – efekt "miniatury" bez utraty czytelności.</item>
    /// <item><b>Motion blur</b> tylko od ruchu kamery, skalowany prędkością (0 w spoczynku).</item>
    /// <item>Puls trafienia: aberracja chromatyczna + beczka + winieta; błysk: ekspozycja.</item>
    /// </list>
    /// </summary>
    public static class PostFx
    {
        // ---------------- strojenie ----------------
        public static float GameFocalLength = 85f;      // mm
        public static float ShowcaseFocalLength = 105f; // mm
        public static float MaxPlayerBlurPx = 1.2f;     // px krążka rozmycia na najdalszym/najbliższym graczu
        public static float MinAperture = 1.8f, MaxAperture = 16f;
        public static float ShowcaseAperture = 2.2f;
        public static float MotionBlurPerSpeed = 0.018f; // intensywność na m/s ruchu kamery
        public static float MotionBlurMax = 0.32f;
        public static float BaseExposure = 0.3f;

        static Volume volume;
        static Bloom bloom;
        static Tonemapping tonemap;
        static ColorAdjustments color;
        static WhiteBalance white;
        static SplitToning split;
        static ShadowsMidtonesHighlights smh;
        static DepthOfField dof;
        static MotionBlur motion;
        static Vignette vignette;
        static ChromaticAberration chroma;
        static LensDistortion lens;
        static FilmGrain grain;

        static bool showcase;
        static Spring punch, flash;
        static float exposure = 0.3f, vignetteBase = 0.22f, chromaBase = 0.035f, aperture = 8f, focus = 12f;
        static Runner runner;

        public static void Build(Transform parent)
        {
            if (volume != null) return;
            var go = new GameObject("Post-processing filmowy");
            go.transform.SetParent(parent, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.32f);
            bloom.scatter.Override(0.72f);
            bloom.clamp.Override(40f);
            bloom.highQualityFiltering.Override(true);
            bloom.tint.Override(Color.white);

            color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(BaseExposure);
            color.contrast.Override(12f);
            color.saturation.Override(16f); // ACES zjada nasycenie – oddajemy je tutaj

            white = profile.Add<WhiteBalance>(true);
            white.temperature.Override(0f);
            white.tint.Override(0f);

            split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.40f, 0.46f, 0.64f));
            split.highlights.Override(new Color(0.64f, 0.55f, 0.44f));
            split.balance.Override(8f);

            // "Aksamitna Bulwa": cienie chłodne i lekko fioletowe, nigdy szare; światła ciepłe
            smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.93f, 0.94f, 1.09f, -0.02f));
            smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.01f, 0.95f, 0f));
            smh.shadowsStart.Override(0f);
            smh.shadowsEnd.Override(0.3f);
            smh.highlightsStart.Override(0.55f);
            smh.highlightsEnd.Override(1f);

            dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(12f);
            dof.focalLength.Override(GameFocalLength);
            dof.aperture.Override(8f);
            dof.bladeCount.Override(7);
            dof.bladeCurvature.Override(0.85f);
            dof.bladeRotation.Override(12f);

            motion = profile.Add<MotionBlur>(true);
            motion.mode.Override(MotionBlurMode.CameraOnly);
            motion.quality.Override(MotionBlurQuality.Medium);
            motion.intensity.Override(0f);
            motion.clamp.Override(0.05f);

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(vignetteBase);
            vignette.smoothness.Override(0.45f);
            vignette.rounded.Override(false);
            vignette.color.Override(Shapes.Hex("#140F33"));

            chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(chromaBase); // obiektyw: delikatne rozszczepienie na brzegach

            lens = profile.Add<LensDistortion>(true);
            lens.intensity.Override(0f);
            lens.scale.Override(1f);

            grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.8f);

            ApplyQuality(QualitySettings.GetQualityLevel());

            if (runner == null)
            {
                var r = new GameObject("PostFx Runner");
                r.hideFlags = HideFlags.HideInHierarchy;
                Object.DontDestroyOnLoad(r);
                runner = r.AddComponent<Runner>();
            }
        }

        /// <summary>Niska jakość: Gaussian DoF zamiast Bokeh, bez motion bluru i ziarna.</summary>
        public static void ApplyQuality(int level)
        {
            if (dof == null) return;
            bool low = level <= 1;
            motion.active = !low;
            grain.active = !low;
            bloom.highQualityFiltering.Override(!low);
            if (low)
            {
                dof.mode.Override(showcase ? DepthOfFieldMode.Gaussian : DepthOfFieldMode.Off);
                dof.gaussianStart.Override(28f);
                dof.gaussianEnd.Override(70f);
                dof.gaussianMaxRadius.Override(1f);
            }
            else dof.mode.Override(DepthOfFieldMode.Bokeh);
        }

        /// <summary>Menu / lobby / podium: dłuższy obiektyw i otwarta przysłona – płytka głębia jak w zwiastunie.</summary>
        public static void SetShowcase(bool on)
        {
            showcase = on;
            if (dof == null) return;
            dof.focalLength.Override(on ? ShowcaseFocalLength : GameFocalLength);
            ApplyQuality(QualitySettings.GetQualityLevel());
        }

        /// <summary>Gradacja kolorów mapy (pod ACES).</summary>
        public static void Grade(MapTheme t)
        {
            if (color == null) return;
            float temp = 0f, tint = 0f, sat = 16f, contrast = 12f, exp = 0.3f, bloomI = 0.32f;
            switch (t.Id)
            {
                case MapId.Ice: temp = -16f; sat = 8f; exp = 0.22f; bloomI = 0.45f; break;
                case MapId.Volcano: temp = 16f; tint = 4f; sat = 20f; contrast = 18f; exp = 0.12f; bloomI = 0.6f; break;
                case MapId.Cornfield: temp = 14f; sat = 18f; break;
                case MapId.Beach: temp = 5f; sat = 20f; exp = 0.38f; break;
                case MapId.Forest: temp = -5f; tint = -6f; sat = 14f; contrast = 14f; break;
                case MapId.Kitchen: temp = 9f; sat = 12f; break;
                case MapId.Canteen: temp = -2f; sat = 10f; break;
                case MapId.Garden: tint = 4f; sat = 18f; break;
                case MapId.Orchard: temp = 7f; sat = 18f; break;
            }
            white.temperature.Override(temp);
            white.tint.Override(tint);
            color.saturation.Override(sat);
            color.contrast.Override(contrast);
            exposure = exp;
            bloom.intensity.Override(bloomI);
            bloom.tint.Override(Color.Lerp(Color.white, t.Glow, 0.2f));
        }

        /// <summary>Puls trafienia (0..1): aberracja, "zassanie" beczką, mocniejsza winieta.</summary>
        public static void Punch(float amount)
        {
            if (chroma == null) return;
            punch.Set(Mathf.Max(punch.Value, Mathf.Clamp01(amount)));
        }

        /// <summary>Błysk ekspozycji (wybuch, gol): +1.2 EV przy amount = 1, gaśnie w ~0.25 s.</summary>
        public static void Flash(float amount)
        {
            if (color == null) return;
            flash.Set(Mathf.Max(flash.Value, Mathf.Clamp01(amount)));
        }

        // ---------------- co klatkę ----------------

        static void Tick(float dt)
        {
            if (dof == null) return;
            var gm = GameManager.Instance;
            var rig = gm != null ? gm.Camera : null;
            var cam = rig != null ? rig.Cam : null;

            // --- DoF: ostrość na punkcie kadru, przysłona z rozstawienia graczy
            if (cam != null)
            {
                Vector3 cp = cam.transform.position, fwd = cam.transform.forward;
                float target = Mathf.Max(1f, Vector3.Dot(rig.FocusPoint - cp, fwd));
                focus = Mathf.Lerp(focus, target, 1f - Mathf.Exp(-dt * 8f));
                dof.focusDistance.Override(focus);

                float f = (showcase ? ShowcaseFocalLength : GameFocalLength) / 1000f;
                float needN = showcase ? ShowcaseAperture : MinAperture;
                if (!showcase && gm.Potatoes.Count > 0)
                {
                    // krążek rozmycia na sensorze 24 mm: c = f²·|S2−S1| / (N·S2·(S1−f)) → N = f²·|S2−S1| / (c·S2·(S1−f))
                    float cMax = MaxPlayerBlurPx / Mathf.Max(240f, cam.pixelHeight) * 0.024f;
                    for (int i = 0; i < gm.Potatoes.Count; i++)
                    {
                        var p = gm.Potatoes[i];
                        if (p == null || p.Eliminated) continue;
                        float s2 = Mathf.Max(0.5f, Vector3.Dot(p.transform.position - cp, fwd));
                        float n = f * f * Mathf.Abs(s2 - focus) / (cMax * s2 * Mathf.Max(0.01f, focus - f));
                        if (n > needN) needN = n;
                    }
                }
                needN = Mathf.Clamp(needN, MinAperture, MaxAperture);
                // przysłona domyka się szybko (gracz wybiega z ostrości), otwiera powoli (bez "pompowania")
                float k = needN > aperture ? 10f : 1.5f;
                aperture = Mathf.Lerp(aperture, needN, 1f - Mathf.Exp(-dt * k));
                dof.aperture.Override(aperture);

                motion.intensity.Override(Mathf.Clamp(rig.CameraSpeed * MotionBlurPerSpeed, 0f, MotionBlurMax));
            }

            // --- puls i błysk
            punch.Step(0f, 3.2f, 0.55f, dt);
            flash.Step(0f, 2.4f, 1f, dt);
            float p01 = punch.Value;
            chroma.intensity.Override(Mathf.Clamp01(chromaBase + Mathf.Max(0f, p01) * 0.55f));
            lens.intensity.Override(Mathf.Clamp(-p01 * 0.22f, -0.3f, 0.12f));
            vignette.intensity.Override(vignetteBase + Mathf.Max(0f, p01) * 0.12f);
            color.postExposure.Override(exposure + Mathf.Max(0f, flash.Value) * 1.2f);
        }

        sealed class Runner : MonoBehaviour
        {
            void LateUpdate() { Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f)); }
        }
    }
}
