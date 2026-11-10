using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PotatoParty
{
    /// <summary>
    /// Oświetlenie filmowe pod bliską kamerę. Zamiast płaskiego "słońca zza pleców" – klasyczny układ
    /// trzypunktowy przeniesiony na arenę:
    /// <code>
    ///  Światło          Typ          Obrót (X,Y)    Intensywność      Kolor        Cienie
    ///  Klucz (słońce)   Directional  42°, -50°      1.45 × mapa       mapa (ciepłe) Soft High, 0.9, 4 kaskady / 50 m
    ///  Kontra           Directional  18°, 165°      0.55              #FFD9AE      brak
    ///  Odbicie od ziemi Directional  -35°, 120°     0.16              kolor podłoża brak
    ///  Snop areny       Spot 90°     z góry         h² × 0.6          biel + glow  Soft (kontakt pod stopami)
    ///  Otoczenie        Trilight SH  niebo/horyzont/ziemia × 0.85 / 0.6 / 0.4
    /// </code>
    /// Niższe słońce (42° zamiast 50–52°) daje dłuższe cienie i modelunek brył; kontra z naprzeciwka kamery
    /// rysuje świetlisty kontur na krawędziach ziemniaków (razem z sheenem shadera).
    /// </summary>
    public static class Lighting
    {
        public static float SunElevation = 38f, SunAzimuth = -50f, SunIntensityMul = 1.35f;
        public static float ShadowDistance = 50f;

        static Light rim, bounce;
        static ReflectionProbe probe;
        static Material sky;

        public static void Setup(Camera cam, Light sun)
        {
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.25f;
            sun.shadowNearPlane = 0.2f;
            var sunData = sun.GetUniversalAdditionalLightData();
            if (sunData != null) sunData.softShadowQuality = SoftShadowQuality.High;

            rim = Extra("Kontra", sun.transform.parent, Quaternion.Euler(18f, 165f, 0f), Shapes.Hex("#FFD9AE"), 0.55f);
            bounce = Extra("Odbicie od ziemi", sun.transform.parent, Quaternion.Euler(-35f, 120f, 0f), Shapes.Hex("#B89A78"), 0.16f);

            try
            {
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urp != null)
                {
                    urp.msaaSampleCount = 4;
                    urp.renderScale = 1f;
                    urp.supportsHDR = true;
                    urp.supportsCameraDepthTexture = true;
                    urp.shadowDistance = ShadowDistance;
                    urp.shadowCascadeCount = 4;
                    // kaskady gęściej przy kamerze: 0–4 m, 4–11 m, 11–24 m, 24–50 m
                    urp.cascade4Split = new Vector3(0.08f, 0.22f, 0.48f);
                    urp.shadowDepthBias = 0.6f;
                    urp.shadowNormalBias = 0.5f;
                }
                cam.allowMSAA = true;
                cam.allowHDR = true;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                    data.dithering = true;
                    data.stopNaN = true;
                    data.requiresDepthTexture = true;
                }
                PostFx.Build(cam.transform.parent);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Potatoes Party] Efekty obrazu niedostępne: " + e.Message);
            }
        }

        static Light Extra(string name, Transform parent, Quaternion rot, Color c, float intensity)
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

        /// <summary>Atmosfera mapy: niebo, światło otoczenia, mgła powietrzna, słońce i gradacja.</summary>
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
                sky.SetColor("_SkyTint", Color.Lerp(t.Sky, Shapes.Hex("#4DA6FF"), 0.5f));
                sky.SetColor("_GroundColor", horizon);
                sky.SetFloat("_AtmosphereThickness", 0.7f);
                sky.SetFloat("_Exposure", 1.25f);
                sky.SetFloat("_SunDisk", 0f);
                RenderSettings.skybox = sky;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else cam.clearFlags = CameraClearFlags.SolidColor;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(Shapes.Hex("#CFE6FF"), t.Sky, 0.3f) * 0.5f;
            RenderSettings.ambientEquatorColor = Color.Lerp(Shapes.Hex("#E9EEF2"), t.Ambient, 0.4f) * 0.34f;
            RenderSettings.ambientGroundColor = Color.Lerp(Shapes.Hex("#9C8E7C"), t.Land, 0.4f) * 0.2f;

            // perspektywa powietrzna: bliska kamera = mgła zaczyna się bliżej, tło "odpływa" w głąb
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 95f;

            sun.color = t.SunColor;
            sun.intensity = t.SunIntensity * SunIntensityMul;
            sun.transform.rotation = Quaternion.Euler(SunElevation, SunAzimuth, 0f);
            if (rim != null)
            {
                rim.color = Color.Lerp(Shapes.Hex("#FFD9AE"), t.Glow, 0.35f);
                rim.intensity = t.Id == MapId.Volcano ? 0.85f : 0.55f;
            }
            if (bounce != null) bounce.color = Color.Lerp(t.Floor, t.Land, 0.5f);
            PostFx.Grade(t);
        }

        /// <summary>Odbicia: jedna sonda HDR nad areną, renderowana raz po zbudowaniu mapy.</summary>
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
                probe.resolution = 256;
                probe.hdr = true;
                probe.boxProjection = false;
                probe.size = new Vector3(400f, 200f, 400f);
                probe.importance = 1;
                probe.intensity = 1f;
                probe.nearClipPlane = 0.5f;
                probe.farClipPlane = 250f;
            }
            probe.transform.position = new Vector3(0f, 2.5f, 0f);
            probe.RenderProbe();
        }
    }
}
