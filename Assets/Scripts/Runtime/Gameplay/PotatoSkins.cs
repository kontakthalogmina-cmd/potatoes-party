using UnityEngine;
using UnityEngine.Rendering;

namespace PotatoParty
{
    /// <summary>Wzór proceduralny na skórce (zgodny z _PatternType w shaderze PotatoParty/PotatoHero).</summary>
    public enum SkinPattern { None = 0, Stripes = 1, Patches = 2, Cosmos = 3, Magma = 4, Glitter = 5 }

    /// <summary>Dodatek na głowie/twarzy ziemniaka.</summary>
    public enum SkinAccessory { Sprout, StrawHat, Beanie, CowboyHat, Sunglasses, Horns, SpaceRing, NinjaBand, Earmuffs }

    /// <summary>
    /// Skórka ziemniaka w stylu "Aksamitna Bulwa": kolor skórki, miąższ (SSS), meszek, wzór proceduralny,
    /// metal i akcesorium. Kolor gracza zostaje w konturze (rim), pierścieniu pod stopami i rękawiczkach,
    /// więc każdą skórkę może nosić każdy gracz i nadal wiadomo, kto jest kim.
    /// </summary>
    public class PotatoSkin
    {
        public string Name;
        public string Description;
        /// <summary>Kolor skórki; null = kolor gracza (skórka klasyczna).</summary>
        public Color? Body;
        public Color? Belly;
        public Color Face = Shapes.Hex("#FFD9A8");
        public Color Flesh = new Color(1.0f, 0.72f, 0.38f);
        public Color Sheen = new Color(0.35f, 0.30f, 0.25f);
        public float Smoothness = 0.7f;
        public float Speckle = 0.6f;
        public float Scatter = 0.7f;
        public SkinPattern Pattern = SkinPattern.None;
        public Color PatternColor = Color.white;
        public Color PatternGlow = Color.black;
        public float PatternScale = 1f;
        public float Metallic;
        public SkinAccessory Accessory = SkinAccessory.Sprout;
        public Color Accent = Color.white;
        /// <summary>Rękawiczki w kolorze gracza (dla skórek, których ciało nie ma koloru gracza).</summary>
        public bool PlayerGloves = true;

        /// <summary>Domyślna skórka to PRAWDZIWY ziemniak (płowy brąz) z lekką nutą koloru gracza – kolor gracza niosą rękawiczki, pierścień i kontur.</summary>
        public static Color PotatoTan = new Color(0.78f, 0.6f, 0.38f);
        public Color BodyColor(Color player) { return Body ?? Color.Lerp(PotatoTan, player, 0.22f); }
        public Color BellyColor(Color player) { return Belly ?? Color.Lerp(BodyColor(player), Color.white, 0.5f); }
        public Color SpotColor(Color player) { return Color.Lerp(BodyColor(player), Color.black, 0.25f); }
    }

    public static class PotatoSkins
    {
        public static readonly PotatoSkin[] All =
        {
            new PotatoSkin
            {
                Name = "Klasyk", Description = "Gładka skórka w kolorze gracza i zielony kiełek.",
                Accessory = SkinAccessory.Sprout, PlayerGloves = false,
            },
            new PotatoSkin
            {
                Name = "Rumiana Bulwa", Description = "Prawdziwy ziemniak prosto z pola, w słomkowym kapeluszu.",
                Body = Shapes.Hex("#D8A25E"), Belly = Shapes.Hex("#EFC98F"), Smoothness = 0.45f, Speckle = 1f,
                Sheen = new Color(0.45f, 0.36f, 0.25f), Accessory = SkinAccessory.StrawHat, Accent = Shapes.Hex("#F2CF6B"),
            },
            new PotatoSkin
            {
                Name = "Vitelotte", Description = "Fioletowa odmiana w paski, z ciepłą czapką z pomponem.",
                Body = Shapes.Hex("#5A2C83"), Belly = Shapes.Hex("#8E62C4"), Flesh = new Color(0.85f, 0.45f, 1f),
                Sheen = new Color(0.45f, 0.35f, 0.6f), Pattern = SkinPattern.Stripes, PatternColor = Shapes.Hex("#A77BE0"), PatternScale = 1f,
                Accessory = SkinAccessory.Beanie, Accent = Shapes.Hex("#FF5FA2"),
            },
            new PotatoSkin
            {
                Name = "Krówka", Description = "Łaciata bulwa z dzikiego zachodu.",
                Body = Shapes.Hex("#FBF5EA"), Belly = Shapes.Hex("#FFE3E8"), Flesh = new Color(1f, 0.75f, 0.6f), Speckle = 0.15f,
                Pattern = SkinPattern.Patches, PatternColor = Shapes.Hex("#3B2A26"), PatternScale = 1f,
                Accessory = SkinAccessory.CowboyHat, Accent = Shapes.Hex("#8A5A33"),
            },
            new PotatoSkin
            {
                Name = "Złota Bulwa", Description = "Lśniący metal z brokatem. Gwiazda każdej imprezy.",
                Body = Shapes.Hex("#E9B640"), Belly = Shapes.Hex("#F7D77A"), Smoothness = 0.82f, Speckle = 0.25f, Metallic = 0.85f,
                Sheen = new Color(0.6f, 0.45f, 0.15f), Scatter = 0.25f, Pattern = SkinPattern.Glitter, PatternColor = Shapes.Hex("#FFF2B0"), PatternScale = 1f,
                Accessory = SkinAccessory.Sunglasses, Accent = Shapes.Hex("#1E1430"),
            },
            new PotatoSkin
            {
                Name = "Magmowa", Description = "Zastygła lawa z żarzącymi się pęknięciami. Gorąca sprawa!",
                Body = Shapes.Hex("#2E2422"), Belly = Shapes.Hex("#4A3530"), Face = Shapes.Hex("#F2B28A"), Flesh = new Color(1f, 0.35f, 0.1f),
                Sheen = new Color(0.25f, 0.12f, 0.08f), Smoothness = 0.35f, Speckle = 0.3f,
                Pattern = SkinPattern.Magma, PatternColor = Shapes.Hex("#FF7A1F"), PatternGlow = new Color(5f, 1.4f, 0.25f), PatternScale = 1f,
                Accessory = SkinAccessory.Horns, Accent = Shapes.Hex("#2A1A18"),
            },
            new PotatoSkin
            {
                Name = "Kosmiczna", Description = "Mgławica, gwiazdy i własny pierścień jak Saturn.",
                Body = Shapes.Hex("#1C2257"), Belly = Shapes.Hex("#3A3F86"), Face = Shapes.Hex("#E8D6FF"), Flesh = new Color(0.55f, 0.45f, 1f),
                Sheen = new Color(0.4f, 0.4f, 0.8f), Speckle = 0.1f, Smoothness = 0.75f,
                Pattern = SkinPattern.Cosmos, PatternColor = Shapes.Hex("#B04CC8"), PatternGlow = new Color(3.5f, 3.4f, 4.2f), PatternScale = 1f,
                Accessory = SkinAccessory.SpaceRing, Accent = Shapes.Hex("#7FE3FF"),
            },
            new PotatoSkin
            {
                Name = "Ninja", Description = "Cichy jak noc. Opaska w kolorze gracza.",
                Body = Shapes.Hex("#2C2E3A"), Belly = Shapes.Hex("#3D4050"), Flesh = new Color(0.65f, 0.6f, 0.9f),
                Sheen = new Color(0.3f, 0.3f, 0.4f), Smoothness = 0.5f, Speckle = 0.2f,
                Accessory = SkinAccessory.NinjaBand,
            },
            new PotatoSkin
            {
                Name = "Szronik", Description = "Mroźna bulwa z puchatymi nausznikami.",
                Body = Shapes.Hex("#BFE4FF"), Belly = Shapes.Hex("#EAF6FF"), Face = Shapes.Hex("#FFE6E0"), Flesh = new Color(0.55f, 0.85f, 1f),
                Sheen = new Color(0.6f, 0.75f, 0.9f), Smoothness = 0.85f, Speckle = 0.15f,
                Accessory = SkinAccessory.Earmuffs, Accent = Shapes.Hex("#FFFFFF"),
            },
        };

        public static int Count { get { return All.Length; } }

        public static PotatoSkin Get(int index)
        {
            return All[((index % All.Length) + All.Length) % All.Length];
        }

        // ---------------- zapamiętany wybór graczy (per kolor gracza) ----------------

        public static int Saved(int colorIndex) { return Mathf.Clamp(PlayerPrefs.GetInt("pp_skin_" + colorIndex, 0), 0, All.Length - 1); }

        public static void Save(int colorIndex, int skin)
        {
            PlayerPrefs.SetInt("pp_skin_" + colorIndex, skin);
            PlayerPrefs.Save();
        }

        // ---------------- akcesoria (z brył, w przestrzeni Visual ziemniaka) ----------------

        /// <summary>
        /// Buduje akcesorium skórki. Wszystko jest dzieckiem <paramref name="visual"/>, więc rozciąga się
        /// i kręci razem z postacią (zgniatanie przy skoku, fikołki).
        /// </summary>
        public static void BuildAccessory(PotatoSkin skin, Transform visual, Color player)
        {
            var root = Shapes.Empty("Dodatek", visual, Vector3.zero).transform;
            switch (skin.Accessory)
            {
                case SkinAccessory.Sprout: Sprout(root); break;
                case SkinAccessory.StrawHat: StrawHat(root, skin.Accent, player); break;
                case SkinAccessory.Beanie: Beanie(root, player, skin.Accent); break;
                case SkinAccessory.CowboyHat: CowboyHat(root, skin.Accent, player); break;
                case SkinAccessory.Sunglasses: Sunglasses(root); Sprout(root, Shapes.Hex("#E9B640")); break;
                case SkinAccessory.Horns: Horns(root, skin.Accent); break;
                case SkinAccessory.SpaceRing: SpaceRing(root, skin.Accent, player); break;
                case SkinAccessory.NinjaBand: NinjaBand(root, player); break;
                case SkinAccessory.Earmuffs: Earmuffs(root, player, skin.Accent); break;
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;
        }

        static GameObject P(PrimitiveType t, Transform p, Vector3 pos, Vector3 scale, Color c, float smooth = 0.5f, float emission = 0f)
        {
            var go = Shapes.Prim(t, p, pos, scale, c, false, smooth);
            if (emission > 0f) go.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(c, smooth, emission);
            return go;
        }

        static GameObject Ring(Transform p, Vector3 pos, Vector3 scale, Color c, float smooth = 0.5f, Vector3? euler = null)
        {
            var go = new GameObject("Obrecz");
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.AddComponent<MeshFilter>().sharedMesh = Look.Torus();
            go.AddComponent<MeshRenderer>().sharedMaterial = Shapes.Mat(c, smooth);
            return go;
        }

        static void Sprout(Transform root, Color? leaves = null)
        {
            var sprout = Shapes.Empty("Kielek", root, new Vector3(0, 0.64f, 0)).transform;
            var leaf = leaves ?? Shapes.Hex("#5BD65A");
            P(PrimitiveType.Cylinder, sprout, new Vector3(0, 0.08f, 0), new Vector3(0.06f, 0.09f, 0.06f), Color.Lerp(leaf, Color.black, 0.15f));
            for (int s = -1; s <= 1; s += 2)
            {
                var l = P(PrimitiveType.Sphere, sprout, new Vector3(0.12f * s, 0.2f, 0), new Vector3(0.24f, 0.07f, 0.13f), leaf, 0.6f);
                l.transform.localRotation = Quaternion.Euler(0, 0, 25f * s);
            }
            sprout.gameObject.AddComponent<Wiggle>();
        }

        static void StrawHat(Transform root, Color straw, Color player)
        {
            var hat = Shapes.Empty("Kapelusz", root, new Vector3(0, 0.56f, -0.02f)).transform;
            hat.localRotation = Quaternion.Euler(-8f, 0, 6f);
            P(PrimitiveType.Cylinder, hat, Vector3.zero, new Vector3(1.55f, 0.022f, 1.5f), straw, 0.25f);
            P(PrimitiveType.Sphere, hat, new Vector3(0, 0.13f, 0), new Vector3(0.78f, 0.42f, 0.76f), straw, 0.25f);
            Ring(hat, new Vector3(0, 0.07f, 0), new Vector3(0.82f, 0.28f, 0.8f), Color.Lerp(player, Color.black, 0.1f), 0.4f);
            // źdźbło słomy w zębach kapelusza
            var stalk = P(PrimitiveType.Cylinder, hat, new Vector3(0.36f, 0.1f, 0.12f), new Vector3(0.03f, 0.16f, 0.03f), Color.Lerp(straw, Color.white, 0.3f));
            stalk.transform.localRotation = Quaternion.Euler(0, 0, -60f);
        }

        static void Beanie(Transform root, Color main, Color accent)
        {
            var cap = Shapes.Empty("Czapka", root, new Vector3(0, 0.52f, -0.02f)).transform;
            P(PrimitiveType.Sphere, cap, new Vector3(0, 0.08f, 0), new Vector3(0.98f, 0.62f, 0.94f), main, 0.15f);
            Ring(cap, new Vector3(0, -0.02f, 0), new Vector3(0.88f, 0.4f, 0.84f), Color.Lerp(main, Color.white, 0.75f), 0.15f);
            Ring(cap, new Vector3(0, 0.18f, 0), new Vector3(0.8f, 0.3f, 0.76f), accent, 0.15f);
            var pom = P(PrimitiveType.Sphere, cap, new Vector3(0, 0.42f, 0), Vector3.one * 0.28f, Color.Lerp(accent, Color.white, 0.4f), 0.05f);
            pom.AddComponent<Wiggle>().Amount = 12f;
        }

        static void CowboyHat(Transform root, Color leather, Color player)
        {
            var hat = Shapes.Empty("Kowbojski", root, new Vector3(0, 0.6f, -0.02f)).transform;
            hat.localRotation = Quaternion.Euler(-10f, 0, -4f);
            // rondo podwinięte po bokach: trzy spłaszczone kule
            P(PrimitiveType.Sphere, hat, Vector3.zero, new Vector3(1.5f, 0.06f, 1.25f), leather, 0.35f);
            for (int s = -1; s <= 1; s += 2)
            {
                var side = P(PrimitiveType.Sphere, hat, new Vector3(0.62f * s, 0.07f, 0), new Vector3(0.32f, 0.06f, 1.0f), leather, 0.35f);
                side.transform.localRotation = Quaternion.Euler(0, 0, 38f * s);
            }
            P(PrimitiveType.Cylinder, hat, new Vector3(0, 0.2f, 0), new Vector3(0.66f, 0.2f, 0.6f), leather, 0.35f);
            P(PrimitiveType.Sphere, hat, new Vector3(0, 0.38f, 0), new Vector3(0.66f, 0.12f, 0.6f), Color.Lerp(leather, Color.black, 0.15f), 0.35f);
            Ring(hat, new Vector3(0, 0.08f, 0), new Vector3(0.68f, 0.3f, 0.62f), player, 0.4f);
        }

        static void Sunglasses(Transform root)
        {
            var frame = Shapes.Hex("#1E1430");
            var g = Shapes.Empty("Okulary", root, new Vector3(0, 0.27f, 0.5f)).transform;
            for (int s = -1; s <= 1; s += 2)
            {
                var lens = P(PrimitiveType.Sphere, g, new Vector3(0.16f * s, 0, 0.02f), new Vector3(0.27f, 0.2f, 0.08f), frame, 0.95f);
                lens.transform.localRotation = Quaternion.Euler(0, 14f * s, 0);
                // odblask na szkle
                P(PrimitiveType.Sphere, lens.transform, new Vector3(-0.2f, 0.22f, 0.45f), new Vector3(0.35f, 0.18f, 0.2f), Color.white, 0.9f, 1.2f);
            }
            P(PrimitiveType.Cube, g, new Vector3(0, 0.04f, 0.03f), new Vector3(0.1f, 0.03f, 0.03f), Shapes.Hex("#E9B640"), 0.9f);
            for (int s = -1; s <= 1; s += 2)
            {
                var arm = P(PrimitiveType.Cube, g, new Vector3(0.3f * s, 0.02f, -0.14f), new Vector3(0.025f, 0.03f, 0.3f), Shapes.Hex("#E9B640"), 0.9f);
                arm.transform.localRotation = Quaternion.Euler(0, -32f * s, 0);
            }
        }

        static void Horns(Transform root, Color dark)
        {
            var bone = Shapes.Hex("#F3E6CF");
            for (int s = -1; s <= 1; s += 2)
            {
                var h = Shapes.Empty("Rog", root, new Vector3(0.26f * s, 0.52f, -0.02f)).transform;
                h.localRotation = Quaternion.Euler(0, 0, -28f * s);
                // rożek z trzech coraz mniejszych, wygiętych kawałków
                for (int k = 0; k < 4; k++)
                {
                    float t = k / 3f;
                    var seg = P(PrimitiveType.Sphere, h, new Vector3(0.05f * t * t * s, 0.07f + k * 0.075f, 0), Vector3.one * Mathf.Lerp(0.17f, 0.06f, t), Color.Lerp(dark, bone, t), 0.6f);
                    seg.transform.localScale = new Vector3(seg.transform.localScale.x, seg.transform.localScale.y * 1.3f, seg.transform.localScale.z);
                }
            }
            // ognik nad głową
            var ember = P(PrimitiveType.Sphere, root, new Vector3(0, 0.82f, 0), Vector3.one * 0.12f, Shapes.Hex("#FF8A2A"), 0.2f, 4f);
            ember.AddComponent<Bobber>().Amplitude = 0.04f;
        }

        static void SpaceRing(Transform root, Color glow, Color player)
        {
            var ring = Ring(root, new Vector3(0, 0.02f, 0), new Vector3(1.75f, 0.06f, 1.75f), Color.Lerp(glow, Color.white, 0.2f), 0.85f, new Vector3(16f, 0, -10f));
            ring.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(glow, 0.85f, 0.9f);
            ring.AddComponent<Spinner>().Speed = new Vector3(0, 40f, 0);
            // antenka z świecącą kulką w kolorze gracza
            var ant = Shapes.Empty("Antenka", root, new Vector3(0.1f, 0.62f, 0)).transform;
            ant.localRotation = Quaternion.Euler(0, 0, -12f);
            P(PrimitiveType.Cylinder, ant, new Vector3(0, 0.14f, 0), new Vector3(0.035f, 0.14f, 0.035f), Shapes.Hex("#C9CCE8"), 0.9f);
            var tip = P(PrimitiveType.Sphere, ant, new Vector3(0, 0.3f, 0), Vector3.one * 0.13f, player, 0.6f, 2.5f);
            tip.AddComponent<Wiggle>().Amount = 8f;
            ant.gameObject.AddComponent<Wiggle>();
        }

        static void NinjaBand(Transform root, Color player)
        {
            // opaska przez czoło i powiewające końce z tyłu
            Ring(root, new Vector3(0, 0.49f, 0), new Vector3(0.8f, 0.34f, 0.76f), player, 0.3f);
            var knot = Shapes.Empty("Wezel", root, new Vector3(0, 0.49f, -0.38f)).transform;
            P(PrimitiveType.Sphere, knot, Vector3.zero, Vector3.one * 0.13f, player, 0.3f);
            for (int s = -1; s <= 1; s += 2)
            {
                var tail = P(PrimitiveType.Sphere, knot, new Vector3(0.08f * s, -0.1f, -0.12f), new Vector3(0.09f, 0.04f, 0.32f), player, 0.3f);
                tail.transform.localRotation = Quaternion.Euler(-30f, 20f * s, 0);
                tail.AddComponent<Wiggle>().Amount = 14f;
            }
        }

        static void Earmuffs(Transform root, Color player, Color fluff)
        {
            var arc = Ring(root, new Vector3(0, 0.2f, -0.02f), new Vector3(1.06f, 0.3f, 1.06f), player, 0.5f, new Vector3(90f, 0, 0));
            arc.name = "Palak";
            for (int s = -1; s <= 1; s += 2)
            {
                var muff = P(PrimitiveType.Sphere, root, new Vector3(0.5f * s, 0.2f, -0.02f), new Vector3(0.18f, 0.3f, 0.3f), fluff, 0.05f);
                muff.name = "Nausznik";
            }
        }
    }

    /// <summary>Lekkie kołysanie dodatku (kiełek, pompon, końce opaski) – żywsza postać bez animacji szkieletowej.</summary>
    public class Wiggle : MonoBehaviour
    {
        public float Amount = 6f;
        public float Speed = 5f;
        Quaternion start;
        Vector3 lastPos;
        Vector3 lag;
        float seed;

        void Start()
        {
            start = transform.localRotation;
            lastPos = transform.position;
            seed = Random.value * 10f;
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var v = (transform.position - lastPos) / dt;
            lastPos = transform.position;
            // bezwładność: dodatek "zostaje w tyle" przy ruchu i skokach
            lag = Vector3.Lerp(lag, Vector3.ClampMagnitude(v, 12f), dt * 6f);
            var local = transform.parent != null ? transform.parent.InverseTransformDirection(lag) : lag;
            float idle = Mathf.Sin(Time.time * Speed + seed) * Amount * 0.35f;
            transform.localRotation = start * Quaternion.Euler(-local.z * Amount * 0.6f + idle, 0, local.x * Amount * 0.6f);
        }
    }
}
