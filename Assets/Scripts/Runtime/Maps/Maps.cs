using UnityEngine;
using UnityEngine.Rendering;

namespace PotatoParty
{
    /// <summary>Wygląd i "charakter" mapy: kolory, światło, przyczepność podłoża.</summary>
    public class MapTheme
    {
        public MapId Id;
        public string Name;
        public string Tagline;
        public Color Sky, Floor, FloorAlt, Wall, Accent, Pit, Land, LandAlt, SunColor, Ambient;
        /// <summary>Kolor poświaty mapy (kryształy, mgiełka, światło) i ciemne tło z mgłą.</summary>
        public Color Glow, Fog;
        public float SunIntensity = 1.1f;
        public float Grip = 1f;
        public float PitHeight = -7f;
        public Color WheelColor;

        public static readonly MapId[] AllIds =
        {
            MapId.Farm, MapId.Orchard, MapId.Beach, MapId.Ice, MapId.Kitchen, MapId.Volcano,
            MapId.Canteen, MapId.Stadium, MapId.Playground, MapId.Forest, MapId.Cornfield, MapId.Garden,
        };

        static MapTheme[] themes;

        public static MapTheme Get(MapId id)
        {
            if (themes == null) Init();
            return themes[(int)id];
        }

        static MapTheme T(MapId id, string name, string tag, string sky, string floor, string floorAlt, string wall, string accent,
            string pit, string land, string landAlt, float grip = 1f, float pitHeight = -7f, string sun = "#FFF2D6", string ambient = "#9DB4C8", float sunIntensity = 1.1f)
        {
            return new MapTheme
            {
                Id = id, Name = name, Tagline = tag,
                Sky = Shapes.Hex(sky), Floor = Shapes.Hex(floor), FloorAlt = Shapes.Hex(floorAlt), Wall = Shapes.Hex(wall),
                Accent = Shapes.Hex(accent), Pit = Shapes.Hex(pit), Land = Shapes.Hex(land), LandAlt = Shapes.Hex(landAlt),
                Grip = grip, PitHeight = pitHeight, SunColor = Shapes.Hex(sun), Ambient = Shapes.Hex(ambient),
                SunIntensity = sunIntensity, WheelColor = Shapes.Hex(floor),
            };
        }

        static void Init()
        {
            themes = new MapTheme[AllIds.Length];
            themes[(int)MapId.Farm] = T(MapId.Farm, "Ziemniaczana Farma", "Zielone pola, stodoła i wiatrak",
                "#9FD8FF", "#7CCB5B", "#6AB84B", "#C9874A", "#E2463F", "#6B4A2B", "#8BD66A", "#B98A52");
            themes[(int)MapId.Orchard] = T(MapId.Orchard, "Jabłkowy Sad", "Drzewa pełne jabłek i skrzynki owoców",
                "#A8DCFF", "#9AD96B", "#88C85A", "#A86B3C", "#F2C230", "#5E4128", "#9FDB74", "#86C960");
            themes[(int)MapId.Beach] = T(MapId.Beach, "Tratwa na Morzu", "Drewniana tratwa wśród fal, plaża w oddali",
                "#7FD3F5", "#C99A62", "#B88A55", "#2FB5C9", "#FF6F91", "#2E9CCA", "#F6E1AE", "#E9C98B", 0.95f, -3f, "#FFF5DE", "#A9C9D9", 1.25f);
            themes[(int)MapId.Ice] = T(MapId.Ice, "Zamarznięte Jezioro", "Śnieg, bałwany i śliski lód",
                "#C9E6FF", "#BFE9FF", "#A5DDFB", "#FFFFFF", "#5FA8FF", "#1F5F8B", "#F4FAFF", "#DDEEFA", 0.35f, -5f, "#EAF4FF", "#B5C8E0");
            themes[(int)MapId.Kitchen] = T(MapId.Kitchen, "Wielka Kuchnia", "Deski, garnki i tarki na blacie",
                "#FFE7C2", "#E8C49A", "#D9B285", "#B5B5B5", "#E2463F", "#F3F0EA", "#C98F5A", "#B97F4C", 1f, -8f, "#FFF3E0", "#C9B8A6");
            themes[(int)MapId.Volcano] = T(MapId.Volcano, "Ziemniaczany Wulkan", "Gorąca lawa i czarne skały",
                "#5A3A3A", "#6B5B55", "#5A4B46", "#3A302D", "#FF7A1F", "#FF5A14", "#4A3C38", "#3B302C", 1f, -5f, "#FFC9A0", "#8A6A62", 1.0f);
            themes[(int)MapId.Canteen] = T(MapId.Canteen, "Szkolna Stołówka", "Stoły, tace i latające garnki",
                "#DDEBF5", "#F2F2F2", "#7FB7E6", "#7D8C99", "#FFB000", "#9AA7B2", "#E7E2D6", "#D8D2C4", 1f, -6f, "#FFFFFF", "#B9C3CC");
            themes[(int)MapId.Stadium] = T(MapId.Stadium, "Ziemniaczany Stadion", "Trybuny pełne kibiców",
                "#86C8FF", "#4FB848", "#45A83F", "#FFFFFF", "#FFD23F", "#3B8F36", "#7A8796", "#6A7686");
            themes[(int)MapId.Playground] = T(MapId.Playground, "Plac Zabaw", "Zjeżdżalnia, huśtawki i piaskownica",
                "#A9E1FF", "#E9E4DA", "#DCD6CA", "#FF6F91", "#2EC4B6", "#9C7B5A", "#8ED16B", "#F1DC9B");
            themes[(int)MapId.Forest] = T(MapId.Forest, "Leśne Wzgórze", "Sosny, grzyby i omszałe kamienie",
                "#9CCBE8", "#6FAE52", "#5F9C45", "#7A5A3C", "#E2463F", "#3F5F35", "#5E9E48", "#4F8A3D", 1f, -7f, "#FFF0D0", "#90A88C");
            themes[(int)MapId.Cornfield] = T(MapId.Cornfield, "Pole Kukurydzy", "Wysoka kukurydza i strach na wróble",
                "#FFD9A0", "#C9A45C", "#B8934F", "#8A6A3E", "#E2463F", "#7A5B35", "#D9B866", "#C8A757", 1f, -7f, "#FFE2B0", "#C2A98A", 1.15f);
            themes[(int)MapId.Garden] = T(MapId.Garden, "Ogrodowy Labirynt", "Żywopłot, kwiaty i fontanna",
                "#B5E3FF", "#D9CDB0", "#CFC2A3", "#3F8F3A", "#FF8FB1", "#6B8F4E", "#7CC85E", "#6BB84F");

            // Kolor poświaty i ciemnego tła (używane przez opcjonalną, nocną oprawę)
            SetGlow(MapId.Farm, "#FFB347", "#121A16");
            SetGlow(MapId.Orchard, "#FFC857", "#17190F");
            SetGlow(MapId.Beach, "#3FE0D0", "#0B1A24");
            SetGlow(MapId.Ice, "#7FE3FF", "#0E1828");
            SetGlow(MapId.Kitchen, "#FFB066", "#1C140F");
            SetGlow(MapId.Volcano, "#FF5A1F", "#1C0B08");
            SetGlow(MapId.Canteen, "#FFE08A", "#15171D");
            SetGlow(MapId.Stadium, "#DCEBFF", "#0E141D");
            SetGlow(MapId.Playground, "#FF7AD9", "#181222");
            SetGlow(MapId.Forest, "#7CFF6B", "#0B1710");
            SetGlow(MapId.Cornfield, "#FF9F3F", "#1B140B");
            SetGlow(MapId.Garden, "#9CFF7A", "#0E1810");

            // Świeże, czyste kolory: trochę mocniejsze nasycenie, bez przesady, i pogodne niebo
            // zamiast brązowego błota pod areną: ładna, błękitna woda (wulkan zostaje z lawą)
            foreach (var id in new[] { MapId.Farm, MapId.Orchard, MapId.Playground, MapId.Forest, MapId.Cornfield, MapId.Garden })
                themes[(int)id].Pit = Shapes.Hex("#4FC3F0");
            themes[(int)MapId.Kitchen].Pit = Shapes.Hex("#7FD6C8");  // miętowe kafelki pod blatem
            themes[(int)MapId.Canteen].Pit = Shapes.Hex("#9ED8F5");
            foreach (var t in themes)
            {
                t.Floor = Fresh(t.Floor); t.FloorAlt = Fresh(t.FloorAlt); t.Wall = Fresh(t.Wall); t.Accent = Fresh(t.Accent);
                t.Land = Fresh(t.Land); t.LandAlt = Fresh(t.LandAlt);
                if (t.Id != MapId.Volcano) t.Sky = Color.Lerp(t.Sky, Shapes.Hex("#7CC8FF"), 0.3f);
            }
        }

        static void SetGlow(MapId id, string glow, string fog)
        {
            themes[(int)id].Glow = Shapes.Hex(glow);
            themes[(int)id].Fog = Shapes.Hex(fog);
        }

        static Color Fresh(Color c)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            s = Mathf.Clamp01(s * 1.15f);
            v = Mathf.Clamp01(Mathf.Max(v, 0.42f) * 1.04f);
            return Color.HSVToRGB(h, s, v);
        }
    }

    public static partial class MapBuilder
    {
        public static void ApplyAtmosphere(MapTheme t, Camera cam, Light sun)
        {
            Look.Daylight(t, cam, sun);
        }

        /// <summary>Płaski dysk (walec) z dokładnym colliderem siatki zamiast kapsuły.</summary>
        public static GameObject Disc(Transform parent, Vector3 pos, float radius, float height, Color c, bool collider = true)
        {
            var go = Shapes.Prim(PrimitiveType.Cylinder, parent, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), c, false);
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            }
            return go;
        }

        /// <summary>Okrągła arena z wzorkiem pierścieni, wierzch na y = 0.</summary>
        public static GameObject RoundArena(Transform parent, float radius, MapTheme t)
        {
            var floor = Disc(parent, new Vector3(0, -0.75f, 0), radius, 1.5f, t.Floor);
            floor.name = "Arena";
            for (int i = 1; i <= 3; i++)
            {
                float r = radius * (1f - i * 0.25f);
                if (r < 1f) break;
                Disc(parent, new Vector3(0, 0.005f * i, 0), r, 0.02f, i % 2 == 1 ? t.FloorAlt : t.Floor, false);
            }
            // Brzeg areny
            int n = 32;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (radius - 0.1f);
                var b = Shapes.Box(parent, p + Vector3.down * 0.7f, new Vector3(radius * 2f * Mathf.PI / n + 0.05f, 1.45f, 0.3f), i % 2 == 0 ? t.Accent : t.Wall, false);
                b.transform.localRotation = Quaternion.LookRotation(p.normalized);
            }
            return floor;
        }

        /// <summary>Niski płotek dookoła areny, który nie pozwala spaść.</summary>
        public static void WallRing(Transform parent, float radius, MapTheme t, float height = 1.4f)
        {
            int n = 36;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var p = dir * (radius + 0.35f);
                var w = Shapes.Box(parent, p + Vector3.up * (height * 0.5f), new Vector3(radius * 2f * Mathf.PI / n + 0.2f, height, 0.6f), i % 2 == 0 ? t.Wall : t.Accent, true);
                w.transform.localRotation = Quaternion.LookRotation(dir);
                if (i % 3 == 0)
                    Shapes.Prim(PrimitiveType.Sphere, parent, p + Vector3.up * (height + 0.15f), Vector3.one * 0.55f, t.Accent);
            }
        }

        /// <summary>Otoczenie: to, w co się wpada, ląd wokół i dekoracje danej mapy.</summary>
        public static void BuildEnvironment(MapTheme t, Transform root, float arenaRadius)
        {
            var env = Shapes.Empty("Otoczenie", root, Vector3.zero).transform;

            // Dno (błoto / morze / lodowata woda)
            var pit = Shapes.Box(env, new Vector3(0, t.PitHeight - 0.5f, 0), new Vector3(260, 1, 260), t.Pit, false);
            pit.name = "Dno";
            if (t.Pit == Shapes.Hex("#4FC3F0") || t.Id == MapId.Beach || t.Id == MapId.Ice || t.Id == MapId.Volcano) pit.AddComponent<Bobber>().Amplitude = 0.15f;
            if (t.Pit == Shapes.Hex("#4FC3F0")) pit.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(t.Pit, 0.9f);
            if (t.Id == MapId.Volcano) pit.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(t.Pit, 0.3f, 1.2f);

            // Pierścień lądu dookoła
            float inner = arenaRadius + 6f;
            float depth = 46f;
            int segs = 24;
            float landTop = t.Id == MapId.Beach ? -1.2f : -0.9f;
            for (int i = 0; i < segs; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / segs;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                float mid = inner + depth * 0.5f;
                float width = 2f * Mathf.PI * (inner + depth) / segs + 1f;
                var seg = Shapes.Box(env, dir * mid + Vector3.up * (landTop - 4f), new Vector3(width, 8f, depth), t.Land, false);
                seg.transform.localRotation = Quaternion.LookRotation(dir);
            }

            var rng = new System.Random((int)t.Id * 977 + 13);
            Look.Clouds(env, inner);
            Look.Balloons(env, inner);
            switch (t.Id)
            {
                case MapId.Farm: Farm(env, t, inner, landTop, rng); break;
                case MapId.Beach: Beach(env, t, inner, landTop, rng); break;
                case MapId.Ice: Ice(env, t, inner, landTop, rng); break;
                default: Decorate(env, t, inner, landTop, rng); break;
            }
        }

        internal static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        internal static Vector3 RingPoint(System.Random rng, float inner, float y, float minR = 2f, float maxR = 18f)
        {
            float a = R(rng, 0, Mathf.PI * 2f);
            float r = inner + R(rng, minR, maxR);
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        internal static void Tree(Transform p, Vector3 pos, float s, Color leaves)
        {
            Shapes.Prim(PrimitiveType.Cylinder, p, pos + Vector3.up * 1.2f * s, new Vector3(0.45f, 1.2f, 0.45f) * s, Shapes.Hex("#8A5A33"));
            Shapes.Prim(PrimitiveType.Sphere, p, pos + Vector3.up * 3.1f * s, new Vector3(2.6f, 2.3f, 2.6f) * s, leaves);
            Shapes.Prim(PrimitiveType.Sphere, p, pos + new Vector3(0.7f, 2.6f, 0.3f) * s, new Vector3(1.6f, 1.5f, 1.6f) * s, leaves * 0.92f + new Color(0, 0, 0, 0.08f));
        }

        static void Farm(Transform env, MapTheme t, float inner, float y, System.Random rng)
        {
            // Stodoła za areną (widoczna u góry ekranu)
            var barn = Shapes.Empty("Stodola", env, new Vector3(-6f, y, inner + 12f)).transform;
            Shapes.Box(barn, new Vector3(0, 3f, 0), new Vector3(9, 6, 7), Shapes.Hex("#D9453B"), false);
            var roofL = Shapes.Box(barn, new Vector3(-2.3f, 7f, 0), new Vector3(5.6f, 0.5f, 7.6f), Shapes.Hex("#5B3A29"), false);
            roofL.transform.localRotation = Quaternion.Euler(0, 0, 35);
            var roofR = Shapes.Box(barn, new Vector3(2.3f, 7f, 0), new Vector3(5.6f, 0.5f, 7.6f), Shapes.Hex("#5B3A29"), false);
            roofR.transform.localRotation = Quaternion.Euler(0, 0, -35);
            Shapes.Box(barn, new Vector3(0, 2f, -3.55f), new Vector3(3.4f, 4f, 0.2f), Color.white, false);
            Shapes.Box(barn, new Vector3(0, 2f, -3.6f), new Vector3(2.9f, 3.5f, 0.2f), Shapes.Hex("#A8322B"), false);

            // Wiatrak
            var mill = Shapes.Empty("Wiatrak", env, new Vector3(14f, y, inner + 10f)).transform;
            Shapes.Prim(PrimitiveType.Cylinder, mill, new Vector3(0, 4f, 0), new Vector3(2.2f, 4f, 2.2f), Shapes.Hex("#F2E6CF"));
            Shapes.Prim(PrimitiveType.Sphere, mill, new Vector3(0, 8f, 0), new Vector3(2.4f, 1.6f, 2.4f), Shapes.Hex("#5B3A29"));
            var blades = Shapes.Empty("Smigla", mill, new Vector3(0, 7.2f, -1.3f));
            blades.AddComponent<Spinner>().Speed = new Vector3(0, 0, 40f);
            for (int i = 0; i < 4; i++)
            {
                var b = Shapes.Box(blades.transform, Vector3.zero, new Vector3(0.6f, 7f, 0.1f), Color.white, false);
                b.transform.localRotation = Quaternion.Euler(0, 0, i * 90f);
                b.transform.localPosition = b.transform.localRotation * new Vector3(0, 3.2f, 0);
            }

            for (int i = 0; i < 26; i++)
                Tree(env, RingPoint(rng, inner, y, 6f, 30f), R(rng, 0.8f, 1.3f), i % 3 == 0 ? Shapes.Hex("#4FAE4A") : Shapes.Hex("#5CC157"));

            // Snopki siana
            for (int i = 0; i < 12; i++)
            {
                var hp = RingPoint(rng, inner, y + 0.7f, 1.5f, 10f);
                var hay = Shapes.Prim(PrimitiveType.Cylinder, env, hp, new Vector3(1.4f, 0.7f, 1.4f), Shapes.Hex("#E8C35A"));
                hay.transform.localRotation = Quaternion.Euler(90, R(rng, 0, 180), 0);
            }

            // Słoneczniki
            for (int i = 0; i < 18; i++)
            {
                var sp = RingPoint(rng, inner, y, 0.5f, 6f);
                Shapes.Prim(PrimitiveType.Cylinder, env, sp + Vector3.up * 0.8f, new Vector3(0.12f, 0.8f, 0.12f), Shapes.Hex("#3E9A3A"));
                var head = Shapes.Prim(PrimitiveType.Sphere, env, sp + Vector3.up * 1.7f, new Vector3(0.9f, 0.9f, 0.25f), Shapes.Hex("#FFD23F"));
                head.transform.LookAt(Vector3.up * 1.7f);
                Shapes.Prim(PrimitiveType.Sphere, head.transform, new Vector3(0, 0, 0.3f), new Vector3(0.45f, 0.45f, 0.6f), Shapes.Hex("#6B4423"));
            }

            // Płotek przy krawędzi pola
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2f / 40f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var fp = dir * (inner + 0.8f) + Vector3.up * y;
                Shapes.Box(env, fp + Vector3.up * 0.5f, new Vector3(0.2f, 1f, 0.2f), Color.white, false);
                var rail = Shapes.Box(env, fp + Vector3.up * 0.7f, new Vector3(2f * Mathf.PI * (inner + 0.8f) / 40f, 0.15f, 0.08f), Color.white, false);
                rail.transform.localRotation = Quaternion.LookRotation(dir);
            }
        }

        static void Beach(Transform env, MapTheme t, float inner, float y, System.Random rng)
        {
            for (int i = 0; i < 18; i++)
            {
                var pp = RingPoint(rng, inner, y, 3f, 26f);
                var palm = Shapes.Empty("Palma", env, pp).transform;
                palm.localRotation = Quaternion.Euler(R(rng, -12, 12), R(rng, 0, 360), R(rng, -12, 12));
                for (int k = 0; k < 5; k++)
                    Shapes.Prim(PrimitiveType.Cylinder, palm, new Vector3(0.08f * k, 0.6f + k * 1.1f, 0), new Vector3(0.5f - k * 0.04f, 0.6f, 0.5f - k * 0.04f), k % 2 == 0 ? Shapes.Hex("#A0723F") : Shapes.Hex("#8B6234"));
                for (int k = 0; k < 6; k++)
                {
                    var leaf = Shapes.Prim(PrimitiveType.Sphere, palm, new Vector3(0.4f, 6f, 0), new Vector3(0.7f, 0.15f, 3.2f), Shapes.Hex("#3DBE5A"));
                    leaf.transform.localRotation = Quaternion.Euler(18f, k * 60f, 0);
                    leaf.transform.localPosition = new Vector3(0.4f, 5.9f, 0) + leaf.transform.localRotation * new Vector3(0, 0, 1.4f);
                }
                Shapes.Prim(PrimitiveType.Sphere, palm, new Vector3(0.5f, 5.7f, 0.2f), Vector3.one * 0.45f, Shapes.Hex("#6B4A2B"));
            }

            Color[] umbrella = { Shapes.Hex("#FF6F91"), Shapes.Hex("#FFC94A"), Shapes.Hex("#4DD0E1"), Shapes.Hex("#9C6ADE") };
            for (int i = 0; i < 10; i++)
            {
                var up = RingPoint(rng, inner, y, 1f, 9f);
                Shapes.Prim(PrimitiveType.Cylinder, env, up + Vector3.up * 1.3f, new Vector3(0.12f, 1.3f, 0.12f), Color.white);
                Shapes.Prim(PrimitiveType.Sphere, env, up + Vector3.up * 2.6f, new Vector3(3.2f, 0.9f, 3.2f), umbrella[i % umbrella.Length]);
                var towel = Shapes.Box(env, up + new Vector3(1.2f, 0.03f, 0.5f), new Vector3(1.2f, 0.06f, 2.2f), umbrella[(i + 1) % umbrella.Length], false);
                towel.transform.localRotation = Quaternion.Euler(0, R(rng, 0, 180), 0);
            }

            // Zamek z piasku
            var castle = Shapes.Empty("Zamek z piasku", env, new Vector3(8f, y, inner + 6f)).transform;
            Shapes.Box(castle, new Vector3(0, 0.8f, 0), new Vector3(4, 1.6f, 3), Shapes.Hex("#E4C07D"), false);
            for (int k = 0; k < 4; k++)
            {
                var tp = new Vector3(k % 2 == 0 ? -2f : 2f, 1.4f, k < 2 ? -1.5f : 1.5f);
                Shapes.Prim(PrimitiveType.Cylinder, castle, tp, new Vector3(1f, 1.4f, 1f), Shapes.Hex("#E9C98B"));
                Shapes.Prim(PrimitiveType.Sphere, castle, tp + Vector3.up * 1.5f, new Vector3(1.1f, 0.7f, 1.1f), Shapes.Hex("#D9B470"));
            }
            Shapes.Prim(PrimitiveType.Cube, castle, new Vector3(0, 3.2f, 0), new Vector3(0.05f, 1.6f, 0.05f), Color.white);
            Shapes.Prim(PrimitiveType.Cube, castle, new Vector3(0.35f, 3.7f, 0), new Vector3(0.7f, 0.45f, 0.04f), Shapes.Hex("#FF6F91"));

            // Piłki plażowe i muszelki
            for (int i = 0; i < 6; i++)
            {
                var bp = RingPoint(rng, inner, y + 0.5f, 1f, 12f);
                var ball = Shapes.Prim(PrimitiveType.Sphere, env, bp, Vector3.one, umbrella[i % umbrella.Length], false, 0.6f);
                ball.AddComponent<Bobber>().Amplitude = 0.25f;
            }

            // Słońce na niebie
            Shapes.Prim(PrimitiveType.Sphere, env, new Vector3(-30f, 25f, inner + 60f), Vector3.one * 12f, Shapes.Hex("#FFE27A"));
        }

        static void Ice(Transform env, MapTheme t, float inner, float y, System.Random rng)
        {
            for (int i = 0; i < 28; i++)
            {
                var tp = RingPoint(rng, inner, y, 3f, 30f);
                float s = R(rng, 0.8f, 1.4f);
                var tree = Shapes.Empty("Choinka", env, tp).transform;
                Shapes.Prim(PrimitiveType.Cylinder, tree, new Vector3(0, 0.6f, 0) * s, new Vector3(0.4f, 0.6f, 0.4f) * s, Shapes.Hex("#7A5234"));
                for (int k = 0; k < 3; k++)
                {
                    float w = (2.6f - k * 0.7f) * s;
                    Shapes.Prim(PrimitiveType.Sphere, tree, new Vector3(0, (1.6f + k * 1.1f) * s, 0), new Vector3(w, 1.3f * s, w), Shapes.Hex("#2F7D5B"));
                    Shapes.Prim(PrimitiveType.Sphere, tree, new Vector3(0, (1.95f + k * 1.1f) * s, 0), new Vector3(w * 0.75f, 0.55f * s, w * 0.75f), Color.white);
                }
            }

            for (int i = 0; i < 6; i++)
            {
                var sp = RingPoint(rng, inner, y, 1f, 10f);
                var man = Shapes.Empty("Balwan", env, sp).transform;
                man.localRotation = Quaternion.Euler(0, R(rng, 0, 360), 0);
                Shapes.Prim(PrimitiveType.Sphere, man, new Vector3(0, 0.8f, 0), Vector3.one * 1.6f, Color.white, false, 0.4f);
                Shapes.Prim(PrimitiveType.Sphere, man, new Vector3(0, 2.0f, 0), Vector3.one * 1.15f, Color.white, false, 0.4f);
                Shapes.Prim(PrimitiveType.Sphere, man, new Vector3(0, 2.95f, 0), Vector3.one * 0.8f, Color.white, false, 0.4f);
                var nose = Shapes.Prim(PrimitiveType.Cylinder, man, new Vector3(0, 2.95f, 0.45f), new Vector3(0.12f, 0.2f, 0.12f), Shapes.Hex("#FF8A2A"));
                nose.transform.localRotation = Quaternion.Euler(90, 0, 0);
                Shapes.Prim(PrimitiveType.Cylinder, man, new Vector3(0, 3.4f, 0), new Vector3(0.6f, 0.25f, 0.6f), Shapes.Hex("#E2463F"));
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Prim(PrimitiveType.Sphere, man, new Vector3(0.14f * s, 3.05f, 0.33f), Vector3.one * 0.1f, Color.black);
            }

            // Igloo
            var igloo = Shapes.Empty("Igloo", env, new Vector3(-10f, y, inner + 8f)).transform;
            Shapes.Prim(PrimitiveType.Sphere, igloo, Vector3.zero, new Vector3(7, 5, 7), Shapes.Hex("#F0F8FF"), false, 0.5f);
            Shapes.Prim(PrimitiveType.Sphere, igloo, new Vector3(0, 0.3f, -3.1f), new Vector3(2, 2.4f, 1.6f), Shapes.Hex("#5B7FA3"));

            // Kryształy lodu
            for (int i = 0; i < 14; i++)
            {
                var cp = RingPoint(rng, inner, y + 0.6f, 0.5f, 14f);
                var c = Shapes.Prim(PrimitiveType.Cube, env, cp, new Vector3(0.7f, R(rng, 1.4f, 2.8f), 0.7f), Shapes.Hex("#9FD9FF"), false, 0.95f);
                c.transform.localRotation = Quaternion.Euler(R(rng, -20, 20), R(rng, 0, 90), R(rng, -20, 20));
            }

            // Kawałki kry na wodzie
            for (int i = 0; i < 10; i++)
            {
                var fp = RingPoint(rng, 0, t.PitHeight + 0.1f, 2f, inner - 1f);
                var floe = Disc(env, fp, R(rng, 0.8f, 2f), 0.3f, Color.white, false);
                floe.AddComponent<Bobber>().Amplitude = 0.12f;
            }

            Snowfall.Create(env, inner + 10f);
        }
    }

    /// <summary>Delikatne bujanie w górę i w dół (woda, piłki, monety).</summary>
    public class Bobber : MonoBehaviour
    {
        public float Amplitude = 0.2f;
        public float Speed = 1.3f;
        Vector3 start;
        float phase;

        void Start()
        {
            start = transform.localPosition;
            phase = Random.value * 10f;
        }

        void Update()
        {
            transform.localPosition = start + Vector3.up * Mathf.Sin(Time.time * Speed + phase) * Amplitude;
        }
    }

    public class Spinner : MonoBehaviour
    {
        public Vector3 Speed = new Vector3(0, 90, 0);

        void Update() { transform.Rotate(Speed * Time.deltaTime, Space.Self); }
    }

    /// <summary>Padający śnieg z małych płatków.</summary>
    public class Snowfall : MonoBehaviour
    {
        Transform[] flakes;
        float radius;

        public static void Create(Transform parent, float radius)
        {
            var go = Shapes.Empty("Snieg", parent, Vector3.zero);
            var s = go.AddComponent<Snowfall>();
            s.radius = radius;
            s.flakes = new Transform[90];
            for (int i = 0; i < s.flakes.Length; i++)
            {
                var f = Shapes.Prim(PrimitiveType.Cube, go.transform, s.RandomPos(Random.Range(-2f, 22f)), Vector3.one * 0.14f, Color.white);
                f.transform.rotation = Random.rotation;
                s.flakes[i] = f.transform;
            }
        }

        Vector3 RandomPos(float y)
        {
            var c = Random.insideUnitCircle * radius;
            return new Vector3(c.x, y, c.y);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < flakes.Length; i++)
            {
                var f = flakes[i];
                var p = f.localPosition;
                p.y -= dt * 1.6f;
                p.x += Mathf.Sin(Time.time + i) * dt * 0.6f;
                if (p.y < -3f) p = RandomPos(22f);
                f.localPosition = p;
                f.Rotate(40f * dt, 25f * dt, 0);
            }
        }
    }
}
