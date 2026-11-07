using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PotatoParty
{
    /// <summary>Stanowisko garderoby w lobby: wejdź na podest, żeby założyć skórkę.</summary>
    public class WardrobePad
    {
        public int Skin;
        public Vector3 Position;
        public float Radius = 0.95f;
        public Transform Glow;
        public Material GlowMat;
        public Color Color;
    }

    public static partial class MapBuilder
    {
        public const float HubRadius = 15f;

        /// <summary>
        /// Duży plac imprezowy (menu, lobby, podium): szeroka, kamienna arena z mozaiką, girlandy
        /// lampek, latarnie, fontanna i garderoba ze wszystkimi skórkami do przymierzenia.
        /// </summary>
        public static List<WardrobePad> BuildHub(Transform world, MapTheme t, bool wardrobe)
        {
            var pads = new List<WardrobePad>();
            var hub = Shapes.Empty("Plac imprezowy", world, Vector3.zero).transform;
            float r = HubRadius;

            // mozaika: koncentryczne pierścienie kostki brukowej w ciepłych kolorach
            Color stone = Shapes.Hex("#EADFCB"), stoneAlt = Shapes.Hex("#DCCCB0"), stoneDark = Shapes.Hex("#C9B593");
            for (int ring = 0; ring < 4; ring++)
            {
                float rr = r * (1f - ring * 0.22f);
                Disc(hub, new Vector3(0, 0.004f * (ring + 1), 0), rr, 0.02f, ring % 2 == 0 ? stoneAlt : stone, false);
            }
            // promienie mozaiki
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var ray = Shapes.Box(hub, dir * (r * 0.62f) + Vector3.up * 0.022f, new Vector3(0.18f, 0.02f, r * 0.7f), stoneDark, false);
                ray.transform.localRotation = Quaternion.LookRotation(dir);
            }
            // środek: gwiazda z płytek w kolorach graczy
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                var tile = Shapes.Box(hub, new Vector3(Mathf.Cos(a), 0.03f, Mathf.Sin(a)) * 1.3f, new Vector3(0.9f, 0.02f, 1.6f), PlayerColors.All[i], false);
                tile.transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90f, 0);
            }

            StringLights(hub, r);
            Lanterns(hub, r);
            Fountain(hub, new Vector3(0, 0, r - 3.4f));
            Flowerbeds(hub, r);

            if (wardrobe) pads = Wardrobe(hub);
            return pads;
        }

        /// <summary>Girlandy żarówek i chorągiewek rozpięte między słupkami wokół placu.</summary>
        static void StringLights(Transform hub, float r)
        {
            const int posts = 12;
            var bulbCols = new[] { Shapes.Hex("#FFD66B"), Shapes.Hex("#FF8FB1"), Shapes.Hex("#7FE3FF"), Shapes.Hex("#B5FF8A") };
            var flagCols = new[] { Shapes.Hex("#FF5A5F"), Shapes.Hex("#FFC93C"), Shapes.Hex("#1FC2B2"), Shapes.Hex("#7B61FF"), Shapes.Hex("#FF8A3D") };
            var tops = new Vector3[posts];
            for (int i = 0; i < posts; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / posts;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (r + 0.35f);
                Shapes.Prim(PrimitiveType.Cylinder, hub, p + Vector3.up * 2.4f, new Vector3(0.22f, 2.4f, 0.22f), Shapes.Hex("#8A5A33"));
                Shapes.Prim(PrimitiveType.Sphere, hub, p + Vector3.up * 4.9f, Vector3.one * 0.38f, Shapes.Hex("#FFC93C"), false, 0.7f);
                tops[i] = p + Vector3.up * 4.7f;
            }
            int k = 0;
            for (int i = 0; i < posts; i++)
            {
                // od strony kamery (dół ekranu) nie wieszamy girland, żeby nie zasłaniały gry
                var a = tops[i];
                var b = tops[(i + 1) % posts];
                if ((a.z + b.z) * 0.5f < -r * 0.35f) continue;
                const int n = 9;
                for (int j = 1; j < n; j++)
                {
                    float t = j / (float)n;
                    var pos = Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 1.1f;
                    if (j % 2 == 0)
                    {
                        var c = bulbCols[k++ % bulbCols.Length];
                        var bulb = Shapes.Prim(PrimitiveType.Sphere, hub, pos, Vector3.one * 0.22f, c);
                        bulb.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(c, 0.6f, 3f);
                        bulb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }
                    else
                    {
                        var flag = Shapes.Prim(PrimitiveType.Cube, hub, pos + Vector3.down * 0.25f, new Vector3(0.45f, 0.5f, 0.03f), flagCols[k++ % flagCols.Length]);
                        flag.transform.localRotation = Quaternion.LookRotation(b - a) * Quaternion.Euler(0, 90f, 45f);
                        flag.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }
        }

        /// <summary>Latarnie z ciepłym światłem wokół placu.</summary>
        static void Lanterns(Transform hub, float r)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f + Mathf.PI / 6f;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (r - 0.8f);
                if (p.z < -r * 0.5f) continue; // nie zasłaniamy kamery
                var lamp = Shapes.Empty("Latarnia", hub, p).transform;
                Shapes.Prim(PrimitiveType.Cylinder, lamp, new Vector3(0, 0.15f, 0), new Vector3(0.6f, 0.15f, 0.6f), Shapes.Hex("#3B3552"));
                Shapes.Prim(PrimitiveType.Cylinder, lamp, new Vector3(0, 1.6f, 0), new Vector3(0.14f, 1.5f, 0.14f), Shapes.Hex("#3B3552"), false, 0.6f);
                var glass = Shapes.Prim(PrimitiveType.Sphere, lamp, new Vector3(0, 3.3f, 0), new Vector3(0.6f, 0.7f, 0.6f), Shapes.Hex("#FFE3A0"));
                glass.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FFE3A0"), 0.8f, 2.2f);
                glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Shapes.Prim(PrimitiveType.Cylinder, lamp, new Vector3(0, 3.72f, 0), new Vector3(0.7f, 0.06f, 0.7f), Shapes.Hex("#3B3552"));
                Look.PointLight(lamp, new Vector3(0, 3.3f, 0), Shapes.Hex("#FFC77A"), 7f, 0.9f, 0.08f);
            }
        }

        /// <summary>Fontanna z bijącą wodą (cząsteczki) – serce placu za garderobą.</summary>
        static void Fountain(Transform hub, Vector3 pos)
        {
            var f = Shapes.Empty("Fontanna", hub, pos).transform;
            var stone = Shapes.Hex("#D9CBB2");
            var basin = Disc(f, new Vector3(0, 0.35f, 0), 2.2f, 0.7f, stone);
            basin.name = "Niecka";
            var water = Disc(f, new Vector3(0, 0.62f, 0), 1.95f, 0.1f, Shapes.Hex("#56C7F2"), false);
            water.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#56C7F2"), 0.95f, 0.15f);
            Shapes.Prim(PrimitiveType.Cylinder, f, new Vector3(0, 1.3f, 0), new Vector3(0.45f, 0.9f, 0.45f), stone);
            Disc(f, new Vector3(0, 2.15f, 0), 0.9f, 0.25f, stone, false);
            // złoty ziemniak na czubku
            var gold = Shapes.Prim(PrimitiveType.Sphere, f, new Vector3(0, 2.75f, 0), new Vector3(0.62f, 0.8f, 0.58f), Shapes.Hex("#FFC61A"), false, 0.9f);
            gold.AddComponent<Spinner>().Speed = new Vector3(0, 35f, 0);

            var ps = Fx.Make("Woda fontanny", f, f.position + Vector3.up * 3.1f, Fx.Tex.Dot, false, true);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.2f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3.2f, 4f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            m.startColor = new Color(0.85f, 0.95f, 1f, 0.9f);
            m.gravityModifier = 1f;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 28f;
            sh.radius = 0.1f;
            ps.transform.rotation = Quaternion.Euler(-90f, 0, 0);
            var em = ps.emission;
            em.rateOverTime = 70f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            ps.Play();
        }

        static void Flowerbeds(Transform hub, float r)
        {
            var rng = new System.Random(321);
            var cols = new[] { Shapes.Hex("#FF6F91"), Shapes.Hex("#FFC93C"), Shapes.Hex("#FFFFFF"), Shapes.Hex("#B57BFF"), Shapes.Hex("#FF8A3D") };
            for (int i = 0; i < 90; i++)
            {
                float a = R(rng, 0, Mathf.PI * 2f);
                if (Mathf.Sin(a) < -0.3f) continue; // pierwszy plan zostaje czysty
                float rr = r - R(rng, 0.35f, 1.3f);
                var p = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                Shapes.Prim(PrimitiveType.Cylinder, hub, p + Vector3.up * 0.25f, new Vector3(0.05f, 0.25f, 0.05f), Shapes.Hex("#3E9A3A"));
                var head = Shapes.Prim(PrimitiveType.Sphere, hub, p + Vector3.up * 0.55f, new Vector3(0.32f, 0.2f, 0.32f), cols[rng.Next(cols.Length)], false, 0.5f);
                head.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>Garderoba: łuk podestów, na każdym manekin w innej skórce i jej nazwa.</summary>
        static List<WardrobePad> Wardrobe(Transform hub)
        {
            var pads = new List<WardrobePad>();
            var root = Shapes.Empty("Garderoba", hub, Vector3.zero).transform;
            int n = PotatoSkins.Count;
            var center = new Vector3(0, 0, -1.5f);
            const float padR = 6.2f, standR = 7.9f;
            var font = UIKit.TitleFont;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(162f, 18f, i / (float)(n - 1)) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var skin = PotatoSkins.Get(i);
                var accent = skin.Body ?? PlayerColors.All[0];
                var glowCol = Color.Lerp(accent, Color.white, 0.35f);

                // podest-przycisk do wejścia
                var padPos = center + dir * padR;
                var pad = new WardrobePad { Skin = i, Position = padPos, Color = glowCol };
                Disc(root, padPos + Vector3.up * 0.04f, 0.95f, 0.08f, Shapes.Hex("#FFFFFF"), false);
                var glow = new GameObject("Swiecacy pierscien");
                glow.transform.SetParent(root, false);
                glow.transform.localPosition = padPos + Vector3.up * 0.1f;
                glow.transform.localScale = new Vector3(1.45f, 0.08f, 1.45f);
                glow.AddComponent<MeshFilter>().sharedMesh = Look.Torus();
                var gr = glow.AddComponent<MeshRenderer>();
                pad.GlowMat = Shapes.UniqueMat(glowCol, 0.6f);
                if (pad.GlowMat.HasProperty("_EmissionColor"))
                {
                    pad.GlowMat.EnableKeyword("_EMISSION");
                    pad.GlowMat.SetColor("_EmissionColor", glowCol * 0.6f);
                }
                gr.sharedMaterial = pad.GlowMat;
                gr.shadowCastingMode = ShadowCastingMode.Off;
                pad.Glow = glow.transform;
                pads.Add(pad);

                // stojak z manekinem w tej skórce
                var standPos = center + dir * standR;
                var stand = Shapes.Empty("Stojak " + skin.Name, root, standPos).transform;
                Shapes.Prim(PrimitiveType.Cylinder, stand, new Vector3(0, 0.3f, 0), new Vector3(1.5f, 0.3f, 1.5f), Shapes.Hex("#3B3552"), false, 0.6f);
                var top = Shapes.Prim(PrimitiveType.Cylinder, stand, new Vector3(0, 0.63f, 0), new Vector3(1.3f, 0.04f, 1.3f), glowCol);
                top.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(glowCol, 0.5f, 0.8f);
                var slot = new PlayerSlot { Index = i % PlayerColors.All.Length, Name = skin.Name, Color = Shapes.Hex("#EDE7FF"), Kind = ControlKind.Bot, Skin = i };
                var dummy = Potato.CreateMannequin(slot, stand, new Vector3(0, 0.66f + Potato.HalfHeight, 0));
                dummy.FaceTowards(new Vector3(0, 0, -1));
                var spin = dummy.gameObject.AddComponent<Spinner>();
                spin.Speed = new Vector3(0, i % 2 == 0 ? 28f : -28f, 0);

                // nazwa skórki pod stojakiem
                var label = new GameObject("Nazwa");
                label.transform.SetParent(stand, false);
                label.transform.localPosition = new Vector3(0, 0.08f, -1.05f);
                label.transform.rotation = Quaternion.Euler(70f, 0, 0);
                var tm = label.AddComponent<TextMesh>();
                tm.text = skin.Name;
                tm.font = font;
                tm.fontSize = 48;
                tm.characterSize = 0.045f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = Color.white;
                var mr = label.GetComponent<MeshRenderer>();
                if (font != null) mr.sharedMaterial = font.material;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            // tabliczka
            var sign = Shapes.Empty("Tabliczka", root, center + new Vector3(0, 0, 9.6f)).transform;
            Shapes.Box(sign, new Vector3(0, 2.6f, 0), new Vector3(5.2f, 1.1f, 0.25f), Shapes.Hex("#FF8A3D"), false);
            Shapes.Prim(PrimitiveType.Cylinder, sign, new Vector3(-2.2f, 1.1f, 0), new Vector3(0.18f, 1.1f, 0.18f), Shapes.Hex("#8A5A33"));
            Shapes.Prim(PrimitiveType.Cylinder, sign, new Vector3(2.2f, 1.1f, 0), new Vector3(0.18f, 1.1f, 0.18f), Shapes.Hex("#8A5A33"));
            var txt = new GameObject("Napis");
            txt.transform.SetParent(sign, false);
            txt.transform.localPosition = new Vector3(0, 2.6f, -0.14f);
            var t = txt.AddComponent<TextMesh>();
            t.text = "GARDEROBA";
            t.font = font;
            t.fontSize = 64;
            t.characterSize = 0.07f;
            t.anchor = TextAnchor.MiddleCenter;
            t.color = Color.white;
            if (font != null) txt.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return pads;
        }
    }
}
