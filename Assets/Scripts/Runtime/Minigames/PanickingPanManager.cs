using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// <b>Wielka Patelnia Babci</b> – survival na ogromnej, żeliwnej patelni.
    /// <para>
    /// Nad patelnią krąży ręka babci z butelką oleju. Co chwilę wybiera cel (losowo, w gracza, w kryjówkę,
    /// linią, pierścieniem, "deszczem"), pokazuje czerwone pierścienie ostrzegawcze i leje. Olej rozlewa się
    /// plamami (strefy zagrożenia), które po pewnym czasie stygną i znikają. Z przenoszonej butelki kapie też olej.
    /// </para>
    /// <para>
    /// Każdy ziemniak ma nad głową <b>Sizzle Meter</b> (0–100%). Stanie na gorącej plamie (stopy niżej niż
    /// <see cref="SafeHeight"/> nad olejem) albo pod strumieniem podnosi licznik; poza olejem licznik powoli opada.
    /// 100% = ziemniak usmażony (rozpada się na frytki) i odpada. Ostatni na patelni wygrywa; po czasie
    /// wygrywają ocaleni z najniższym licznikiem.
    /// </para>
    /// <para>
    /// Trudność <see cref="Difficulty"/> (0→1) rośnie płynnie z czasem rundy i steruje wszystkim naraz:
    /// przerwą między wylewaniami, rozmiarem plam, tempem smażenia, czasem ostrzeżenia, życiem plam,
    /// prędkością ręki, kapaniem i wagami wzorów (nowe wzory "wchodzą" łagodnie, bez skoków).
    /// </para>
    /// </summary>
    public class PanickingPanManager : Minigame
    {
        // ======================================================================
        //  Strojenie (public static: można podkręcać z konsoli/Inspektora debug)
        // ======================================================================

        public static float PanRadius = 13f;
        public static float RoundTime = 100f;
        /// <summary>Po tylu sekundach trudność osiąga 1 (dalej zostaje maksymalna).</summary>
        public static float RampTime = 85f;

        // wartości (start, max) – interpolowane przez Difficulty
        public static Vector2 IdleGap = new Vector2(1.7f, 0.15f);      // s przerwy po wylaniu
        public static Vector2 WarnTime = new Vector2(1.35f, 0.6f);     // s ostrzeżenia przed wylaniem
        public static Vector2 PuddleRadius = new Vector2(1.5f, 3.1f);  // m (bazowy promień plamy)
        public static Vector2 SizzleRate = new Vector2(0.38f, 1.05f);  // licznik/s na oleju (100% po ~2.6 s → ~1 s)
        public static Vector2 PuddleLife = new Vector2(8f, 14f);       // s gorącej plamy
        public static Vector2 HandSpeed = new Vector2(0.38f, 0.16f);   // s SmoothDamp ręki (mniej = szybciej)
        public static Vector2 SweepSpeed = new Vector2(5f, 9.5f);      // m/s przesuwu strumienia po ścieżce
        public static Vector2 DripRate = new Vector2(0f, 1.3f);        // kapnięć/s z przenoszonej butelki

        public static float CoolRate = 0.12f;         // licznik/s poza olejem
        public static float CoolDelay = 0.7f;         // s po zejściu z oleju, zanim licznik zacznie opadać
        public static float StreamRate = 1.8f;        // dodatkowy licznik/s pod strumieniem
        public static float SafeHeight = 0.45f;       // m: stopy wyżej nad plamą = nie parzy (skok ratuje!)
        public static float SpoutHeight = 5.2f;       // m: wylot butelki nad patelnią
        public static float MaxCoverage = 0.42f;      // maks. ułamek patelni pod gorącym olejem
        public static int MaxPuddles = 26;
        public static float OilGrip = 0.6f;           // olej jest śliski...
        public static float OilSpeed = 1.12f;         // ...a gorące stopy przebierają szybciej

        // ======================================================================
        //  Opis gry (intro, ekran ładowania, HUD)
        // ======================================================================

        public override string Title { get { return "Wielka Patelnia Babci"; } }
        public override string Description
        {
            get
            {
                return "Babcia smaży obiad, a wy jesteście na patelni! Ręka z butelką leje gorący olej w coraz to " +
                       "nowe miejsca. Uciekaj z plam, skacz nad nimi i spychaj rywali w skwierczący tłuszcz. " +
                       "Licznik nad głową pokazuje, jak bardzo jesteś już usmażony – przy 100% zostają z ciebie frytki.";
            }
        }
        public override string Goal { get { return "Nie daj się usmażyć – ostatni ziemniak wygrywa!"; } }
        public override string Tip { get { return "Nad olejem nie parzy: skok ratuje skórkę. Czerwony pierścień = tam zaraz poleje."; } }
        public override string Controls { get { return "Ruch · skok (w locie olej nie parzy) · zryw = wepchnij rywala w olej"; } }
        public override MapId PreferredMap { get { return MapId.Kitchen; } }
        public override float TimeLimit { get { return RoundTime; } }
        public override float ArenaRadius { get { return PanRadius; } }
        public override bool FallEliminates { get { return true; } }
        public override bool EndsWhenOneLeft { get { return true; } }
        public override float CameraPull { get { return 0.22f; } }
        public override float KillY { get { return -2.5f; } }

        // ======================================================================
        //  Stan
        // ======================================================================

        /// <summary>Wzory wylewania – kolejne "odblokowują się" płynnie wraz z trudnością.</summary>
        public enum Pattern { Blob, Targeted, Line, Refuge, Ring, Rain }

        enum HandPhase { Idle, Aim, Pour, Lift }

        /// <summary>Jedna plama oleju: koło główne + "wypustki" (nieregularny kształt), rośnie, żyje, stygnie.</summary>
        sealed class Puddle
        {
            public Transform Root;
            public Vector3 Center;
            public float Radius, Age, Life, Grow, Scale;
            public Vector2[] Off;   // przesunięcia kół względem środka (m, przy skali 1)
            public float[] R;       // promienie kół (m, przy skali 1)
            public bool Cooling { get { return Age > Life; } }
            /// <summary>Parzy, dopóki nie wystygła do połowy.</summary>
            public bool Hot { get { return Scale > 0.3f && Age < Life + CoolTime * 0.5f; } }
        }

        sealed class Warning
        {
            public Transform T;
            public Vector3 Pos;
            public float Radius, Age, Duration;
        }

        sealed class Drop
        {
            public Transform T;
            public Vector3 Pos;
            public float Vy, Radius, GroundY;
            public Warning Mark;
        }

        sealed class Job
        {
            public Pattern Kind;
            public readonly List<Vector3> Points = new List<Vector3>();
            public readonly List<float> Radii = new List<float>();
            public readonly List<bool> Spawned = new List<bool>();
            public readonly List<Warning> Marks = new List<Warning>();
            public float PathLength;
        }

        const float CoolTime = 1.6f;
        const float NeckLength = 2.15f;     // od nadgarstka do wylotu butelki (lokalnie)
        const float PourTilt = 118f, RestTilt = 18f, AimTilt = 55f;

        float elapsed;
        float nextJobIn = 1.6f;
        Pattern lastPattern = Pattern.Rain;
        HandPhase phase = HandPhase.Idle;
        float phaseTime;
        Job job;
        float pourProgress;
        float dripTimer;

        readonly List<Puddle> puddles = new List<Puddle>();
        readonly List<Warning> warnings = new List<Warning>();
        readonly List<Drop> drops = new List<Drop>();
        readonly Stack<Warning> warnPool = new Stack<Warning>();

        readonly Dictionary<Potato, float> sizzle = new Dictionary<Potato, float>();
        readonly Dictionary<Potato, float> outOfOil = new Dictionary<Potato, float>();
        readonly Dictionary<Potato, float> fxTimer = new Dictionary<Potato, float>();
        readonly Dictionary<Potato, float> survived = new Dictionary<Potato, float>();
        readonly Dictionary<Potato, float> botSkill = new Dictionary<Potato, float>();

        // ręka babci
        Transform hand, wrist, stream, butter;
        Vector3 spout, spoutVel;
        float tilt, tiltVel, streamLen, sway;
        float butterHeight = 0.9f, butterResize;

        // kryjówki (wyższe elementy) – cele wzoru Refuge
        readonly List<Vector3> refuges = new List<Vector3>();
        readonly List<float> refugeRadius = new List<float>();

        // materiały (z pamięci podręcznej Shapes – bez tworzenia materiałów w trakcie gry)
        Material oilMat, oilDarkMat, warnMat, streamMat;
        static readonly Color OilColor = new Color(0.93f, 0.66f, 0.16f);
        static readonly Color OilDark = new Color(0.62f, 0.36f, 0.08f);
        static readonly Color WarnColor = new Color(1f, 0.22f, 0.12f);
        float fxBudget;

        /// <summary>Trudność 0..1: łagodny start, potem prawie liniowo – bez schodków.</summary>
        public float Difficulty
        {
            get
            {
                float x = Mathf.Clamp01(elapsed / Mathf.Max(1f, RampTime));
                return Mathf.Clamp01(0.55f * x * x * (3f - 2f * x) + 0.45f * x);
            }
        }

        float Tuned(Vector2 range) { return Mathf.Lerp(range.x, range.y, Difficulty); }

        static float Ramp(float d, float unlock, float width = 0.2f) { return Mathf.Clamp01((d - unlock) / width); }

        // ======================================================================
        //  Budowa patelni
        // ======================================================================

        public override void BuildArena()
        {
            oilMat = Shapes.Mat(OilColor, 0.97f, 0.55f);
            oilDarkMat = Shapes.Mat(OilDark, 0.95f, 0.25f);
            warnMat = Shapes.Mat(WarnColor, 0.3f, 1.6f);
            streamMat = Shapes.Mat(Color.Lerp(OilColor, Color.white, 0.15f), 0.98f, 0.9f);

            var iron = Shapes.Hex("#3A3942");
            var ironAlt = Shapes.Hex("#33323A");
            var ironEdge = Shapes.Hex("#4A4853");
            float R = PanRadius;

            // kuchenka pod patelnią (spadający lecą na nią – i tak odpadają, KillY = -2.5)
            Shapes.Box(Root, new Vector3(0f, -3.6f, 0f), new Vector3(40f, 1f, 40f), Shapes.Hex("#26252D"), false).name = "Kuchenka";

            // dno patelni: jeden collider siatki (bez szwów między płytkami → brak "zaczepiania" na krawędziach)
            var floor = MapBuilder.Disc(Root, new Vector3(0f, -0.35f, 0f), R + 0.35f, 0.7f, iron);
            floor.name = "Dno patelni";
            floor.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(iron, 0.72f);
            // dekoracyjne słoje "przypieczenia" (bez colliderów, minimalnie nad dnem)
            for (int i = 1; i <= 4; i++)
            {
                float r = R * (1f - i * 0.2f);
                MapBuilder.Disc(Root, new Vector3(0f, 0.003f * i, 0f), r, 0.01f, i % 2 == 1 ? ironAlt : iron, false)
                    .GetComponent<Renderer>().sharedMaterial = Shapes.Mat(i % 2 == 1 ? ironAlt : iron, 0.75f);
            }

            BuildRim(R, iron, ironEdge);
            BuildHandle(R);
            BuildFlames(R);
            BuildFood(R);
            BuildHand();

            // kropelki oleju przy brzegu – dekoracja, ale też "rozgrzewka" materiałów oleju pod ekranem ładowania
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f + 0.3f;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (R - 0.9f);
                var d = MapBuilder.Disc(Root, p + Vector3.up * 0.012f, 0.18f + 0.05f * (i % 3), 0.01f, OilColor, false);
                d.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? oilMat : oilDarkMat;
            }
        }

        /// <summary>
        /// Ścianka patelni: 48 nachodzących na siebie, odchylonych na zewnątrz płyt. Normalna wewnętrznej
        /// ściany ma y = sin 28° ≈ 0.47 &lt; 0.55, więc ziemniak nie "staje" na ściance, tylko się z niej zsuwa
        /// (brak kampienia na brzegu), a krawędź wierzchu jest na ~2.05 m – wyżej niż skok.
        /// </summary>
        void BuildRim(float R, Color iron, Color edge)
        {
            const int n = 48;
            const float lean = 28f, height = 2.6f, thick = 0.32f;
            float segW = 2f * Mathf.PI * (R + 0.6f) / n + 0.3f;
            var rim = Shapes.Empty("Scianka patelni", Root, Vector3.zero).transform;
            PhysicsMaterialRef slick = null;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(lean, 0f, 0f);
                // dół wewnętrznej krawędzi płyty leży na krawędzi dna, lekko pod nim (bez szczeliny)
                var bottomInner = dir * R + Vector3.down * 0.3f;
                var center = bottomInner + rot * new Vector3(0f, height * 0.5f, thick * 0.5f);
                var seg = Shapes.Box(rim, center, new Vector3(segW, height, thick), i % 2 == 0 ? iron : edge, true);
                seg.transform.localRotation = rot;
                seg.name = "Scianka";
                var col = seg.GetComponent<Collider>();
                if (slick == null) { Phys.Slippery(col); slick = new PhysicsMaterialRef(col); }
                else slick.ApplyTo(col); // jeden wspólny materiał zamiast 48 kopii
            }
            // błyszczący brzeg (dekor, bez collidera): wąska listwa na wierzchu każdej płyty
            var lipMat = Shapes.Mat(Shapes.Hex("#5C5A66"), 0.85f);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(lean, 0f, 0f);
                var top = dir * R + Vector3.down * 0.3f + rot * new Vector3(0f, height + 0.04f, thick * 0.5f);
                var l = Shapes.Box(rim, top, new Vector3(segW, 0.1f, thick + 0.1f), Color.white, false);
                l.transform.localRotation = rot;
                l.GetComponent<Renderer>().sharedMaterial = lipMat;
            }
        }

        void BuildHandle(float R)
        {
            float a = 20f * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var handle = Shapes.Empty("Raczka", Root, dir * (R + 2.2f) + Vector3.up * 1.7f).transform;
            handle.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(-8f, 0f, 0f);
            Shapes.Box(handle, new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.55f, 3.4f), Shapes.Hex("#4A4853"), false);
            Shapes.Box(handle, new Vector3(0f, 0.05f, 5.4f), new Vector3(1.5f, 0.75f, 7.6f), Shapes.Hex("#5B3A29"), false);
            for (int i = 0; i < 3; i++)
                Shapes.Prim(PrimitiveType.Cylinder, handle, new Vector3(0f, 0.45f, 3.2f + i * 2.4f), new Vector3(0.22f, 0.05f, 0.22f), Shapes.Hex("#C9A15A"), false, 0.9f);
            Shapes.Prim(PrimitiveType.Cylinder, handle, new Vector3(0f, 0.05f, 8.6f), new Vector3(0.7f, 0.42f, 0.7f), Shapes.Hex("#2B2A33"), false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>Niebieskie języki płomieni palnika wystające spod patelni + migoczące światło.</summary>
        void BuildFlames(float R)
        {
            var flameMat = Shapes.Mat(Shapes.Hex("#5AA8FF"), 0.2f, 2.2f);
            var coreMat = Shapes.Mat(Shapes.Hex("#E6F3FF"), 0.2f, 2.6f);
            var ring = Shapes.Empty("Palnik", Root, Vector3.zero).transform;
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI * 2f / 18f + 0.1f;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (R + 0.15f) + Vector3.down * 0.95f;
                var holder = Shapes.Empty("Plomien", ring, p).transform;
                var f = Shapes.Prim(PrimitiveType.Sphere, holder, Vector3.zero, new Vector3(0.45f, 1.1f, 0.45f), Color.white);
                f.GetComponent<Renderer>().sharedMaterial = flameMat;
                f.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var c = Shapes.Prim(PrimitiveType.Sphere, holder, new Vector3(0f, -0.2f, 0f), new Vector3(0.2f, 0.5f, 0.2f), Color.white);
                c.GetComponent<Renderer>().sharedMaterial = coreMat;
                c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                holder.gameObject.AddComponent<Flicker>().Flame = holder;
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                Look.PointLight(Root, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (R + 0.8f) + Vector3.down * 0.8f, Shapes.Hex("#6FB6FF"), 6f, 1.1f, 0.35f);
            }
        }

        /// <summary>
        /// "Skomplikowana powierzchnia": wysoki garb na środku (0.75 m – trzeba wskoczyć, chroni przed olejem
        /// z dna), kostka masła (topnieje przez całą rundę), niskie placki i boczek (0.18 m – przejezdne),
        /// sadzone jajka (dekor). Wysokości dobrane tak, by nie było progów "pomiędzy" (0.3–0.5 m), na
        /// których kapsuła ziemniaka podskakuje i drga.
        /// </summary>
        void BuildFood(float R)
        {
            // garb patelni
            var dome = MapBuilder.Disc(Root, new Vector3(0f, 0.375f, 0f), 2.5f, 0.75f, Shapes.Hex("#46444F"));
            dome.name = "Garb patelni";
            dome.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#46444F"), 0.7f);
            refuges.Add(new Vector3(0f, 0.75f, 0f)); refugeRadius.Add(2.3f);

            // kostka masła
            float ba = 205f * Mathf.Deg2Rad;
            var bp = new Vector3(Mathf.Cos(ba), 0f, Mathf.Sin(ba)) * 9.6f;
            var b = Shapes.Box(Root, bp + Vector3.up * (butterHeight * 0.5f), new Vector3(1.9f, butterHeight, 1.9f), Shapes.Hex("#FFE08A"), true, 22f);
            b.name = "Maslo";
            b.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FFE08A"), 0.65f, 0.08f);
            butter = b.transform;
            var melt = MapBuilder.Disc(Root, bp + Vector3.up * 0.008f, 1.7f, 0.01f, Shapes.Hex("#FFD35A"), false);
            melt.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FFD35A"), 0.95f, 0.15f);
            refuges.Add(bp + Vector3.up * butterHeight); refugeRadius.Add(0.85f);

            // placki ziemniaczane (niskie, z chrupiącą krawędzią)
            var placek = Shapes.Hex("#D9A441");
            for (int i = 0; i < 3; i++)
            {
                float a = (35f + i * 120f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.6f;
                var d = MapBuilder.Disc(Root, p + Vector3.up * 0.09f, 1.55f + 0.15f * i, 0.18f, placek);
                d.name = "Placek";
                d.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(placek, 0.45f);
                MapBuilder.Disc(Root, p + Vector3.up * 0.185f, 1.1f + 0.15f * i, 0.01f, Shapes.Hex("#E8BE5E"), false);
            }

            // boczek: niskie paski przy brzegu
            var meat = Shapes.Hex("#D9776A");
            var fat = Shapes.Hex("#F5D7C8");
            for (int i = 0; i < 3; i++)
            {
                float a = (95f + i * 120f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var strip = Shapes.Empty("Boczek", Root, dir * 10.2f + Vector3.up * 0.09f).transform;
                strip.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 90f, 0f);
                var s = Shapes.Box(strip, Vector3.zero, new Vector3(0.9f, 0.18f, 3.6f), meat, true);
                s.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(meat, 0.6f);
                for (int k = -1; k <= 1; k += 2)
                    Shapes.Box(strip, new Vector3(k * 0.22f, 0.095f, 0f), new Vector3(0.12f, 0.01f, 3.5f), fat, false);
            }

            // sadzone jajka (dekor – płaskie)
            for (int i = 0; i < 2; i++)
            {
                float a = (150f + i * 165f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 7.4f;
                MapBuilder.Disc(Root, p + Vector3.up * 0.015f, 1.3f, 0.02f, Shapes.Hex("#FBF6EC"), false);
                Shapes.Prim(PrimitiveType.Sphere, Root, p + new Vector3(0.2f, 0.04f, 0.1f), new Vector3(0.9f, 0.22f, 0.9f), Shapes.Hex("#FFB21E"), false, 0.9f);
            }
        }

        /// <summary>
        /// Ręka babci: rękaw swetra w groszki, koronkowy mankiet, przedramię, dłoń z obrączką i butelka oleju.
        /// Hierarchia: hand (pozycja nadgarstka, tylko obrót Y) → wrist (przechył butelki wokół X) → dłoń + butelka.
        /// Przedramię i rękaw są dziećmi "hand", więc nie obracają się z butelką. Ramię wychodzi do góry i
        /// "od kamery" (+Z), żeby nie zasłaniać graczy.
        /// </summary>
        void BuildHand()
        {
            var skin = Shapes.Hex("#F1C7A8");
            var skinDark = Shapes.Hex("#E2B091");
            hand = Shapes.Empty("Reka babci", Root, new Vector3(0f, SpoutHeight + 3f, PanRadius + 4f)).transform;
            wrist = Shapes.Empty("Nadgarstek", hand, Vector3.zero).transform;

            // przedramię, mankiet, rękaw (nie przechylają się z butelką)
            var armDir = new Vector3(0f, 0.78f, 0.62f).normalized;
            Limb(hand, Vector3.zero + armDir * 1.6f, armDir, 3.2f, 0.78f, skin);
            var cuff = new GameObject("Mankiet").transform;
            cuff.SetParent(hand, false);
            cuff.localPosition = armDir * 3.25f;
            cuff.localRotation = Quaternion.FromToRotation(Vector3.up, armDir);
            cuff.localScale = new Vector3(1.25f, 0.9f, 1.25f);
            cuff.gameObject.AddComponent<MeshFilter>().sharedMesh = Look.Torus();
            cuff.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FFF8EE"), 0.3f);
            var sleeveColor = Shapes.Hex("#9B6BC9");
            Limb(hand, armDir * 7.2f, armDir, 8f, 1.25f, sleeveColor);
            var dots = Shapes.Hex("#FFE3F0");
            var side = Vector3.Cross(armDir, Vector3.right).normalized;
            for (int i = 0; i < 9; i++)
            {
                float along = 3.9f + i * 0.8f;
                float ang = i * 2.1f;
                var radial = Quaternion.AngleAxis(ang * Mathf.Rad2Deg, armDir) * side;
                Shapes.Prim(PrimitiveType.Sphere, hand, armDir * along + radial * 0.6f, Vector3.one * 0.26f, dots, false, 0.3f);
            }

            // dłoń (przechyla się z butelką)
            Shapes.Prim(PrimitiveType.Sphere, wrist, new Vector3(0f, 0.05f, 0.2f), new Vector3(0.95f, 0.9f, 0.85f), skin, false, 0.5f);
            Shapes.Prim(PrimitiveType.Sphere, wrist, new Vector3(0f, 0.62f, 0.42f), new Vector3(0.85f, 0.7f, 0.5f), skin, false, 0.5f);
            for (int i = 0; i < 4; i++)
            {
                var f = Shapes.Prim(PrimitiveType.Capsule, wrist, new Vector3(0f, 0.32f + i * 0.2f, -0.5f), new Vector3(0.18f, 0.36f, 0.18f), i % 2 == 0 ? skin : skinDark, false, 0.5f);
                f.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            var thumb = Shapes.Prim(PrimitiveType.Capsule, wrist, new Vector3(0.5f, 0.95f, -0.05f), new Vector3(0.19f, 0.3f, 0.19f), skin, false, 0.5f);
            thumb.transform.localRotation = Quaternion.Euler(-30f, 0f, 20f);
            var ring = new GameObject("Obraczka").transform;
            ring.SetParent(wrist, false);
            ring.localPosition = new Vector3(-0.42f, 0.52f, -0.5f);
            ring.localRotation = Quaternion.Euler(0f, 0f, 90f);
            ring.localScale = Vector3.one * 0.2f;
            ring.gameObject.AddComponent<MeshFilter>().sharedMesh = Look.Torus();
            ring.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FFC61A"), 0.95f, 0.3f);

            // butelka oleju (oś +Y nadgarstka; wylot na NeckLength)
            var glass = Shapes.Mat(Shapes.Hex("#F2C14E"), 0.96f, 0.25f);
            var body = Shapes.Prim(PrimitiveType.Cylinder, wrist, new Vector3(0f, 0.85f, 0f), new Vector3(0.82f, 0.62f, 0.82f), Color.white);
            body.GetComponent<Renderer>().sharedMaterial = glass;
            Shapes.Prim(PrimitiveType.Cylinder, wrist, new Vector3(0f, 0.82f, 0f), new Vector3(0.85f, 0.22f, 0.85f), Shapes.Hex("#D9453B"), false, 0.4f);
            var shoulder = Shapes.Prim(PrimitiveType.Sphere, wrist, new Vector3(0f, 1.48f, 0f), new Vector3(0.82f, 0.5f, 0.82f), Color.white);
            shoulder.GetComponent<Renderer>().sharedMaterial = glass;
            var neck = Shapes.Prim(PrimitiveType.Cylinder, wrist, new Vector3(0f, 1.88f, 0f), new Vector3(0.3f, 0.27f, 0.3f), Color.white);
            neck.GetComponent<Renderer>().sharedMaterial = glass;
            Shapes.Prim(PrimitiveType.Cylinder, wrist, new Vector3(0f, NeckLength - 0.04f, 0f), new Vector3(0.34f, 0.04f, 0.34f), Shapes.Hex("#2E7D4F"), false, 0.6f);

            // strumień oleju (włączany przy wylewaniu)
            stream = Shapes.Prim(PrimitiveType.Cylinder, Root, Vector3.zero, new Vector3(0.26f, 1f, 0.26f), Color.white).transform;
            stream.name = "Strumien oleju";
            var sr = stream.GetComponent<Renderer>();
            sr.sharedMaterial = streamMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stream.gameObject.SetActive(false);

            spout = new Vector3(0f, SpoutHeight, PanRadius * 0.5f);
            tilt = RestTilt;
            PlaceHand();
        }

        static void Limb(Transform parent, Vector3 center, Vector3 dir, float length, float diameter, Color c)
        {
            var go = Shapes.Prim(PrimitiveType.Cylinder, parent, center, new Vector3(diameter, length * 0.5f, diameter), c, false, 0.4f);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        }

        /// <summary>Nadgarstek liczony tak, by wylot przechylonej butelki był dokładnie w punkcie <see cref="spout"/>.</summary>
        void PlaceHand()
        {
            var tiltRot = Quaternion.Euler(-tilt, 0f, 0f);
            var neckOffset = tiltRot * new Vector3(0f, NeckLength, 0f);
            float yaw = Mathf.Sin(sway * 0.7f) * 8f;
            var yawRot = Quaternion.Euler(0f, yaw, Mathf.Sin(sway) * 4f);
            hand.localRotation = yawRot;
            hand.localPosition = spout - yawRot * neckOffset;
            wrist.localRotation = tiltRot;
        }

        Vector3 SpoutWorld { get { return wrist.TransformPoint(new Vector3(0f, NeckLength, 0f)); } }

        public override Vector3 SpawnPoint(int i, int n)
        {
            float a = i / (float)Mathf.Max(1, n) * Mathf.PI * 2f + Mathf.PI * 0.5f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 7.5f + Vector3.up * (Potato.HalfHeight + 0.1f);
        }

        protected override void ConfigurePlayer(Potato p)
        {
            sizzle[p] = 0f;
            outOfOil[p] = 99f;
            fxTimer[p] = 0f;
            survived[p] = 0f;
            botSkill[p] = 0.55f + 0.4f * Mathf.Repeat(p.Slot.Index * 0.618f + 0.2f, 1f);
            p.KnockMultiplier = 1.15f; // spychanie w olej ma być zabawne
            GM.Hud.SetMeter(p, 0f);
        }

        // ======================================================================
        //  Pętla
        // ======================================================================

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished || !Running) return;
            elapsed += dt;
            fxBudget = Mathf.Min(fxBudget + dt * 14f, 6f);
            sway += dt * (1.2f + Difficulty);

            UpdateHand(dt);
            UpdateDrops(dt);
            UpdateWarnings(dt);
            UpdatePuddles(dt);
            UpdateButter(dt);
            UpdatePlayers(dt);
        }

        // ---------------- ręka: Idle → Aim → Pour → Lift ----------------

        void UpdateHand(float dt)
        {
            phaseTime += dt;
            float smooth = Tuned(HandSpeed);
            Vector3 target = spout;

            switch (phase)
            {
                case HandPhase.Idle:
                    // krąży nad graczami, "wybierając" ofiarę
                    target = IdleTarget();
                    tilt = Mathf.SmoothDamp(tilt, RestTilt, ref tiltVel, 0.25f, Mathf.Infinity, dt);
                    nextJobIn -= dt;
                    dripTimer -= dt * Tuned(DripRate);
                    if (dripTimer <= 0f) { dripTimer += 1f; if (Tuned(DripRate) > 0.05f) StartDrip(); }
                    if (nextJobIn <= 0f) BeginJob();
                    break;

                case HandPhase.Aim:
                    target = new Vector3(job.Points[0].x, SpoutHeight, job.Points[0].z);
                    tilt = Mathf.SmoothDamp(tilt, AimTilt, ref tiltVel, 0.18f, Mathf.Infinity, dt);
                    if (phaseTime >= Tuned(WarnTime))
                    {
                        phase = HandPhase.Pour;
                        phaseTime = 0f;
                        pourProgress = 0f;
                        streamLen = 0f;
                        AudioManager.PlayPitched(Sfx.Whoosh, 0.55f, 0.8f + Random.value * 0.2f);
                    }
                    break;

                case HandPhase.Pour:
                    tilt = Mathf.SmoothDamp(tilt, PourTilt, ref tiltVel, 0.08f, Mathf.Infinity, dt);
                    target = PourPath(dt);
                    smooth = 0.06f; // strumień trzyma się ścieżki
                    break;

                case HandPhase.Lift:
                    tilt = Mathf.SmoothDamp(tilt, RestTilt, ref tiltVel, 0.12f, Mathf.Infinity, dt);
                    target = spout + Vector3.up * dt * 2f;
                    if (phaseTime > 0.35f)
                    {
                        phase = HandPhase.Idle;
                        phaseTime = 0f;
                        nextJobIn = Tuned(IdleGap) * Random.Range(0.8f, 1.2f);
                        job = null;
                    }
                    break;
            }

            target.y = SpoutHeight + (phase == HandPhase.Idle ? 1.2f + Mathf.Sin(sway * 1.3f) * 0.3f : 0f);
            spout = Vector3.SmoothDamp(spout, target, ref spoutVel, smooth, 60f, dt);
            PlaceHand();
            UpdateStream(dt);
        }

        Vector3 IdleTarget()
        {
            // nad środkiem ciężkości żywych graczy, z lekkim krążeniem
            Vector3 c = Vector3.zero;
            int n = 0;
            foreach (var p in Alive) { c += p.transform.position; n++; }
            if (n > 0) c /= n;
            c += new Vector3(Mathf.Cos(sway * 0.6f), 0f, Mathf.Sin(sway * 0.6f)) * 2.5f;
            c = Clamp(c, PanRadius - 2f);
            return new Vector3(c.x, SpoutHeight, c.z);
        }

        /// <summary>Wylewanie: strumień przesuwa się po punktach zadania, każdy punkt rodzi plamę.</summary>
        Vector3 PourPath(float dt)
        {
            int count = job.Points.Count;
            if (count == 1)
            {
                // pojedyncze wylanie: stoimy i lejemy, plama rośnie razem ze strumieniem
                if (!job.Spawned[0] && streamLen >= SpoutHeight - 0.4f) { job.Spawned[0] = true; SpawnPuddle(job.Points[0], job.Radii[0], 0.7f); ReleaseMark(job, 0); }
                if (phaseTime > 0.75f) EndPour();
                return new Vector3(job.Points[0].x, SpoutHeight, job.Points[0].z);
            }

            float speed = Tuned(SweepSpeed) * (job.Kind == Pattern.Rain ? 1.8f : 1f);
            if (streamLen >= SpoutHeight - 0.4f) pourProgress += speed * dt;
            float walked = 0f;
            Vector3 at = job.Points[count - 1];
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    float seg = FlatDist(job.Points[i - 1], job.Points[i]);
                    if (pourProgress < walked + seg)
                    {
                        at = Vector3.Lerp(job.Points[i - 1], job.Points[i], (pourProgress - walked) / Mathf.Max(0.01f, seg));
                        break;
                    }
                    walked += seg;
                }
                if (!job.Spawned[i] && pourProgress >= walked - 0.01f && streamLen >= SpoutHeight - 0.4f)
                {
                    job.Spawned[i] = true;
                    SpawnPuddle(job.Points[i], job.Radii[i], 0.45f);
                    ReleaseMark(job, i);
                }
            }
            bool all = true;
            for (int i = 0; i < count; i++) all &= job.Spawned[i];
            if (all && pourProgress >= job.PathLength + 0.6f) EndPour();
            return new Vector3(at.x, SpoutHeight, at.z);
        }

        void EndPour()
        {
            if (job != null) for (int i = 0; i < job.Marks.Count; i++) ReleaseMark(job, i);
            phase = HandPhase.Lift;
            phaseTime = 0f;
        }

        void UpdateStream(float dt)
        {
            bool on = phase == HandPhase.Pour;
            if (!on)
            {
                streamLen = Mathf.MoveTowards(streamLen, 0f, dt * 40f);
                if (streamLen <= 0.01f) { if (stream.gameObject.activeSelf) stream.gameObject.SetActive(false); return; }
            }
            else streamLen = Mathf.MoveTowards(streamLen, 30f, dt * 38f); // olej spada ~38 m/s (szybko, ale widać)

            var top = SpoutWorld;
            float ground = SurfaceY(top);
            float len = Mathf.Min(streamLen, top.y - ground);
            if (len <= 0.02f) { stream.gameObject.SetActive(false); return; }
            if (!stream.gameObject.activeSelf) stream.gameObject.SetActive(true);
            float w = 0.24f + Mathf.Sin(Time.time * 37f) * 0.03f;
            stream.position = top + Vector3.down * (len * 0.5f);
            stream.rotation = Quaternion.identity;
            stream.localScale = new Vector3(w, len * 0.5f, w);

            if (on && streamLen >= top.y - ground && fxBudget >= 1f)
            {
                fxBudget -= 1f;
                Fx.Splash(Root, new Vector3(top.x, ground + 0.05f, top.z), OilColor, 0.45f, 6);
            }
        }

        // ---------------- zadania (wzory wylewania) ----------------

        void BeginJob()
        {
            float d = Difficulty;
            var kind = PickPattern(d);
            lastPattern = kind;
            job = new Job { Kind = kind };
            float baseR = Tuned(PuddleRadius);

            switch (kind)
            {
                case Pattern.Blob:
                    AddPoint(RandomPoint(PanRadius - 1.6f), baseR * Random.Range(0.85f, 1.15f));
                    break;

                case Pattern.Targeted:
                {
                    var v = Victim();
                    Vector3 aim = v != null ? Predict(v, Tuned(WarnTime) * 0.55f) : RandomPoint(PanRadius - 2f);
                    AddPoint(Clamp(aim, PanRadius - 1.4f), baseR * Random.Range(1.0f, 1.25f));
                    break;
                }

                case Pattern.Line:
                {
                    var v = Victim();
                    Vector3 mid = v != null ? v.transform.position : RandomPoint(PanRadius - 3f);
                    float ang = Random.value * Mathf.PI;
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    int n = Mathf.RoundToInt(Mathf.Lerp(3f, 5f, d));
                    float len = Mathf.Lerp(5f, 9f, d);
                    for (int i = 0; i < n; i++)
                    {
                        var p = mid + dir * Mathf.Lerp(-len * 0.5f, len * 0.5f, i / (float)(n - 1));
                        AddPoint(Clamp(p, PanRadius - 1.2f), baseR * 0.62f);
                    }
                    break;
                }

                case Pattern.Refuge:
                {
                    int k = Random.Range(0, refuges.Count);
                    var r = refuges[k];
                    AddPoint(new Vector3(r.x, 0f, r.z), Mathf.Min(baseR, refugeRadius[k]));
                    break;
                }

                case Pattern.Ring:
                {
                    // pułapka: pierścień kropli wokół gracza – trzeba przeskoczyć
                    var v = Victim();
                    Vector3 c = v != null ? v.transform.position : Vector3.zero;
                    c = Clamp(c, PanRadius - 4.5f);
                    int n = Mathf.RoundToInt(Mathf.Lerp(5f, 7f, d));
                    float rr = Mathf.Lerp(3.4f, 2.8f, d);
                    float start = Random.value * Mathf.PI * 2f;
                    for (int i = 0; i < n; i++)
                    {
                        float a = start + i * Mathf.PI * 2f / n;
                        AddPoint(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr, baseR * 0.5f);
                    }
                    break;
                }

                case Pattern.Rain:
                {
                    // babcia potrząsa butelką: dużo małych kropli w przypadkowych miejscach
                    int n = Mathf.RoundToInt(Mathf.Lerp(6f, 10f, d));
                    Vector3 prev = RandomPoint(PanRadius - 2f);
                    for (int i = 0; i < n; i++)
                    {
                        var p = Clamp(prev + (Random.insideUnitCircle * 5f).ToXZ(), PanRadius - 1.2f);
                        AddPoint(p, baseR * Random.Range(0.35f, 0.5f));
                        prev = p;
                    }
                    break;
                }
            }

            job.PathLength = 0f;
            for (int i = 1; i < job.Points.Count; i++) job.PathLength += FlatDist(job.Points[i - 1], job.Points[i]);
            float warn = Tuned(WarnTime);
            for (int i = 0; i < job.Points.Count; i++)
            {
                var m = ShowWarning(job.Points[i], job.Radii[i], warn + i * 0.12f);
                job.Marks.Add(m);
            }
            phase = HandPhase.Aim;
            phaseTime = 0f;
            AudioManager.PlayPitched(Sfx.Tick, 0.5f, 0.9f + d * 0.4f);
        }

        Pattern PickPattern(float d)
        {
            // wagi rosną/maleją ciągle z trudnością → różnorodność narasta płynnie
            float[] w =
            {
                1.0f - 0.6f * d,                   // Blob
                Ramp(d, 0.10f) * 1.0f,             // Targeted
                Ramp(d, 0.22f) * 0.9f,             // Line
                Ramp(d, 0.30f) * 0.45f,            // Refuge
                Ramp(d, 0.45f) * 0.8f,             // Ring
                Ramp(d, 0.65f) * 0.7f,             // Rain
            };
            w[(int)lastPattern] *= 0.35f; // rzadko ten sam wzór dwa razy z rzędu
            float sum = 0f;
            for (int i = 0; i < w.Length; i++) sum += w[i];
            float r = Random.value * sum;
            for (int i = 0; i < w.Length; i++)
            {
                if (r < w[i]) return (Pattern)i;
                r -= w[i];
            }
            return Pattern.Blob;
        }

        void AddPoint(Vector3 p, float radius)
        {
            p.y = 0f;
            job.Points.Add(p);
            job.Radii.Add(radius);
            job.Spawned.Add(false);
        }

        /// <summary>Ofiara: losowo, ale chętniej ten, kto ma najniższy licznik (babcia lubi wyrównywać szanse).</summary>
        Potato Victim()
        {
            Potato best = null;
            float bestW = -1f;
            foreach (var p in Alive)
            {
                float s;
                sizzle.TryGetValue(p, out s);
                float w = (1.2f - s) * Random.Range(0.5f, 1.5f) * (p.Slot.IsBot ? 0.85f : 1.15f);
                if (w > bestW) { bestW = w; best = p; }
            }
            return best;
        }

        static Vector3 Predict(Potato p, float t)
        {
            var v = p.Body != null && !p.Body.isKinematic ? Phys.GetVelocity(p.Body) : Vector3.zero;
            v.y = 0f;
            return p.transform.position + Vector3.ClampMagnitude(v * t, 4f);
        }

        Vector3 RandomPoint(float radius)
        {
            var c = Random.insideUnitCircle * radius;
            return new Vector3(c.x, 0f, c.y);
        }

        // ---------------- ostrzeżenia ----------------

        Warning ShowWarning(Vector3 pos, float radius, float duration)
        {
            Warning w = warnPool.Count > 0 ? warnPool.Pop() : null;
            if (w == null)
            {
                w = new Warning();
                var go = new GameObject("Ostrzezenie");
                go.transform.SetParent(Root, false);
                go.AddComponent<MeshFilter>().sharedMesh = Look.Torus();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = warnMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                w.T = go.transform;
            }
            w.Pos = new Vector3(pos.x, SurfaceY(pos + Vector3.up * 6f) + 0.035f, pos.z);
            w.Radius = radius;
            w.Age = 0f;
            w.Duration = duration;
            w.T.gameObject.SetActive(true);
            w.T.position = w.Pos;
            warnings.Add(w);
            return w;
        }

        void ReleaseMark(Job j, int i)
        {
            if (j == null || i >= j.Marks.Count) return;
            var m = j.Marks[i];
            if (m == null) return;
            j.Marks[i] = null;
            HideWarning(m);
        }

        void HideWarning(Warning w)
        {
            if (!warnings.Remove(w)) return;
            w.T.gameObject.SetActive(false);
            warnPool.Push(w);
        }

        void UpdateWarnings(float dt)
        {
            for (int i = warnings.Count - 1; i >= 0; i--)
            {
                var w = warnings[i];
                w.Age += dt;
                float k = Mathf.Clamp01(w.Age / Mathf.Max(0.05f, w.Duration));
                // pierścień zaciska się do rozmiaru plamy i pulsuje coraz szybciej
                float pulse = 1f + 0.08f * Mathf.Sin(w.Age * Mathf.Lerp(8f, 26f, k));
                float r = w.Radius * Mathf.Lerp(1.45f, 1f, k * k) * pulse;
                // torus: zewnętrzna średnica 1.4 przy skali 1 → zewnętrzny brzeg pierścienia = promień plamy
                w.T.localScale = new Vector3(r * 2f / 1.4f, 0.05f, r * 2f / 1.4f);
                w.T.Rotate(0f, dt * 90f, 0f, Space.World);
                if (w.Age > w.Duration + 6f) HideWarning(w); // bezpiecznik (najdłuższa ścieżka pierścienia ~3 s)
            }
        }

        // ---------------- kapanie z butelki ----------------

        void StartDrip()
        {
            var top = SpoutWorld;
            var p = new Vector3(top.x, 0f, top.z);
            if (FlatDist(p, Vector3.zero) > PanRadius - 1f) return;
            var go = Shapes.Prim(PrimitiveType.Sphere, Root, top, new Vector3(0.22f, 0.32f, 0.22f), Color.white);
            go.name = "Kropla";
            go.GetComponent<Renderer>().sharedMaterial = streamMat;
            var d = new Drop { T = go.transform, Pos = top, Vy = -2f, Radius = Tuned(PuddleRadius) * Random.Range(0.3f, 0.45f) };
            d.GroundY = SurfaceY(top);
            d.Mark = ShowWarning(p, d.Radius, 0.5f);
            drops.Add(d);
        }

        void UpdateDrops(float dt)
        {
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                d.Vy -= 24f * dt;
                d.Pos.y += d.Vy * dt;
                d.T.position = d.Pos;
                if (d.Pos.y > d.GroundY) continue;
                SpawnPuddle(new Vector3(d.Pos.x, 0f, d.Pos.z), d.Radius, 0.3f);
                if (d.Mark != null) HideWarning(d.Mark);
                Object.Destroy(d.T.gameObject);
                drops.RemoveAt(i);
            }
        }

        // ---------------- plamy ----------------

        /// <summary>Wysokość powierzchni patelni pod punktem (dno, garb, masło, placek...).</summary>
        static float SurfaceY(Vector3 from)
        {
            RaycastHit hit;
            var o = new Vector3(from.x, 8f, from.z);
            if (Physics.Raycast(o, Vector3.down, out hit, 12f, ~(1 << Potato.Layer), QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }

        void SpawnPuddle(Vector3 at, float radius, float grow)
        {
            float y = SurfaceY(at);
            var pd = new Puddle
            {
                Center = new Vector3(at.x, y, at.z),
                Radius = radius,
                Life = Tuned(PuddleLife) * Random.Range(0.85f, 1.15f),
                Grow = grow,
            };
            // nieregularny kształt: koło główne + 3–4 wypustki
            int lobes = 3 + Random.Range(0, 2);
            pd.Off = new Vector2[lobes + 1];
            pd.R = new float[lobes + 1];
            pd.Off[0] = Vector2.zero;
            pd.R[0] = radius;
            for (int i = 1; i <= lobes; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float dist = radius * Random.Range(0.55f, 0.85f);
                pd.Off[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist;
                pd.R[i] = radius * Random.Range(0.35f, 0.55f);
            }

            var root = Shapes.Empty("Plama oleju", Root, pd.Center + Vector3.up * (0.018f + 0.0015f * (puddles.Count % 8))).transform;
            for (int i = 0; i < pd.R.Length; i++)
            {
                var disc = Shapes.Prim(PrimitiveType.Cylinder, root, new Vector3(pd.Off[i].x, 0f, pd.Off[i].y), new Vector3(pd.R[i] * 2f, 0.008f, pd.R[i] * 2f), Color.white);
                var r = disc.GetComponent<Renderer>();
                r.sharedMaterial = oilMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // ciemniejszy, "przypalony" środek
            var core = Shapes.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 0.004f, 0f), new Vector3(radius * 1.1f, 0.008f, radius * 1.1f), Color.white);
            core.GetComponent<Renderer>().sharedMaterial = oilDarkMat;
            core.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.localScale = new Vector3(0.05f, 1f, 0.05f);
            pd.Root = root;
            puddles.Add(pd);

            AudioManager.PlayPitched(Sfx.Crack, 0.35f, 0.55f + Random.value * 0.25f);
            Fx.Splash(Root, pd.Center + Vector3.up * 0.1f, OilColor, 0.5f + radius * 0.25f, 14);
            Rumble.PulseAll(GM.Potatoes, pd.Center, 0.12f, 0.18f, 6f);

            // limity: liczba plam i pokrycie patelni (gra musi zostać grywalna)
            if (puddles.Count > MaxPuddles) ForceCool(1);
            float panArea = Mathf.PI * PanRadius * PanRadius;
            float hot = 0f;
            for (int i = 0; i < puddles.Count; i++) if (!puddles[i].Cooling) hot += Mathf.PI * puddles[i].Radius * puddles[i].Radius * 1.25f;
            if (hot / panArea > MaxCoverage) ForceCool(2);
        }

        void ForceCool(int n)
        {
            for (int i = 0; i < puddles.Count && n > 0; i++)
            {
                if (puddles[i].Cooling) continue;
                puddles[i].Age = Mathf.Max(puddles[i].Age, puddles[i].Life);
                n--;
            }
        }

        void UpdatePuddles(float dt)
        {
            for (int i = puddles.Count - 1; i >= 0; i--)
            {
                var pd = puddles[i];
                pd.Age += dt;
                float s;
                if (!pd.Cooling)
                {
                    float k = Mathf.Clamp01(pd.Age / Mathf.Max(0.05f, pd.Grow));
                    s = EaseOutBack(k);
                }
                else s = 1f - Mathf.Clamp01((pd.Age - pd.Life) / CoolTime);
                pd.Scale = s;
                pd.Root.localScale = new Vector3(Mathf.Max(0.02f, s), 1f, Mathf.Max(0.02f, s));
                if (pd.Cooling && s <= 0f)
                {
                    Object.Destroy(pd.Root.gameObject);
                    puddles.RemoveAt(i);
                    continue;
                }
                // skwierczenie: bąbelki i para (z budżetem efektów)
                if (pd.Hot && fxBudget >= 1f && Random.value < dt * (1.2f + pd.Radius))
                {
                    fxBudget -= 1f;
                    var off = Random.insideUnitCircle * pd.Radius * s * 0.8f;
                    var at = pd.Center + new Vector3(off.x, 0.08f, off.y);
                    if (Random.value < 0.6f) Fx.Sparks(Root, at, Color.Lerp(OilColor, Color.white, 0.4f), 2, 1.6f, 0.35f);
                    else Fx.Mist(Root, at, new Color(1f, 1f, 1f, 0.5f), 0.45f);
                }
            }
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float t = x - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        /// <summary>Czy punkt (stopy gracza) jest w gorącej plamie.</summary>
        bool InOil(Vector3 feet)
        {
            for (int i = 0; i < puddles.Count; i++)
            {
                var pd = puddles[i];
                if (!pd.Hot) continue;
                if (feet.y - pd.Center.y > SafeHeight || feet.y < pd.Center.y - 0.35f) continue;
                float dx = feet.x - pd.Center.x, dz = feet.z - pd.Center.z;
                float reach = pd.Radius * pd.Scale + 0.9f;
                if (dx * dx + dz * dz > reach * reach * 2.2f) continue;
                for (int k = 0; k < pd.R.Length; k++)
                {
                    float ox = dx - pd.Off[k].x * pd.Scale, oz = dz - pd.Off[k].y * pd.Scale;
                    float r = pd.R[k] * pd.Scale + 0.12f; // 0.12: brzeg ziemniaka
                    if (ox * ox + oz * oz < r * r) return true;
                }
            }
            return false;
        }

        bool UnderStream(Vector3 pos)
        {
            if (phase != HandPhase.Pour || !stream.gameObject.activeSelf) return false;
            var top = SpoutWorld;
            return FlatDist(pos, top) < 0.75f && pos.y < top.y;
        }

        // ---------------- masło topnieje ----------------

        void UpdateButter(float dt)
        {
            if (butter == null) return;
            float target = Mathf.Lerp(0.9f, 0.2f, Mathf.Clamp01(elapsed / RoundTime));
            butterHeight = Mathf.MoveTowards(butterHeight, target, dt * 0.05f);
            butterResize -= dt;
            if (butterResize > 0f) return;
            butterResize = 0.5f; // collider statyczny: rzadkie zmiany zamiast co klatkę
            var s = butter.localScale;
            butter.localScale = new Vector3(s.x, butterHeight, s.z);
            butter.localPosition = new Vector3(butter.localPosition.x, butterHeight * 0.5f, butter.localPosition.z);
            if (refuges.Count > 1) refuges[1] = new Vector3(refuges[1].x, butterHeight, refuges[1].z);
        }

        // ---------------- Sizzle Meter ----------------

        void UpdatePlayers(float dt)
        {
            float rate = Tuned(SizzleRate);
            foreach (var p in Players)
            {
                if (p == null || p.Eliminated || !sizzle.ContainsKey(p)) continue;
                survived[p] += dt;
                var pos = p.transform.position;
                var feet = pos + Vector3.down * (Potato.HalfHeight - 0.05f);
                bool oil = InOil(feet);
                bool streamHit = UnderStream(pos);
                float s = sizzle[p];

                if (oil || streamHit)
                {
                    outOfOil[p] = 0f;
                    s += (oil ? rate : 0f) * dt + (streamHit ? StreamRate * dt : 0f);
                    p.Grip = Map.Grip * OilGrip;
                    p.SpeedMultiplier = OilSpeed;
                    SizzleFeedback(p, feet, s, dt);
                }
                else
                {
                    outOfOil[p] += dt;
                    if (outOfOil[p] > CoolDelay) s = Mathf.Max(0f, s - CoolRate * dt);
                    p.Grip = Map.Grip;
                    p.SpeedMultiplier = 1f;
                }

                sizzle[p] = s;
                GM.Hud.SetMeter(p, s);
                if (s >= 1f) Fry(p);
            }
        }

        void SizzleFeedback(Potato p, Vector3 feet, float s, float dt)
        {
            if (p.Surface != null) p.Surface.Scorch(feet, dt * 0.3f, 0.35f + 0.65f * s);
            float t = fxTimer[p] - dt;
            if (t <= 0f)
            {
                t = 0.14f;
                Fx.Sparks(Root, feet + Vector3.up * 0.1f, Color.Lerp(OilColor, Color.white, 0.5f), 2, 2.4f, 0.3f);
                if (Random.value < 0.45f) Fx.Mist(Root, feet + Vector3.up * 0.3f, new Color(1f, 1f, 1f, 0.55f), 0.5f);
                AudioManager.PlayPitched(Sfx.Crack, 0.12f + 0.15f * s, 1.5f + Random.value * 0.6f);
                Rumble.Pulse(p.Slot, 0.05f, 0.15f + 0.4f * s);
                if (p.Anim != null && Random.value < 0.5f) p.Anim.OnBump(); // dreptanie na gorącym
            }
            fxTimer[p] = t;
        }

        /// <summary>100%: ziemniak zamienia się w frytki i odpada.</summary>
        void Fry(Potato p)
        {
            var pos = p.transform.position;
            Fx.Splash(Root, pos + Vector3.up * 0.4f, OilColor, 1.1f, 20);
            Fx.Embers(Root, pos, 1.2f, 22);
            Fx.Sparks(Root, pos + Vector3.up * 0.5f, Shapes.Hex("#FFE38A"), 10, 5f);
            FriesBurst(pos);
            GM.Hud.SetMeter(p, 1f);
            Eliminate(p, false, " – usmażony na frytki!");
        }

        void FriesBurst(Vector3 pos)
        {
            var fry = Shapes.Mat(Shapes.Hex("#F6C453"), 0.55f, 0.15f);
            for (int i = 0; i < 7; i++)
            {
                var go = Shapes.Prim(PrimitiveType.Cube, Root, pos + Random.insideUnitSphere * 0.3f + Vector3.up * 0.4f, new Vector3(0.14f, 0.14f, 0.75f), Color.white, true);
                go.name = "Frytka";
                go.layer = Potato.Layer; // raycasty powierzchni ich nie widzą
                go.GetComponent<Renderer>().sharedMaterial = fry;
                go.transform.rotation = Random.rotation;
                var col = go.GetComponent<Collider>();
                foreach (var o in Players) if (o != null) { var oc = o.GetComponent<Collider>(); if (oc != null) Physics.IgnoreCollision(col, oc, true); }
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                var dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.8f;
                Phys.SetVelocity(rb, dir.normalized * Random.Range(5f, 9f));
                rb.angularVelocity = Random.insideUnitSphere * 14f;
                Object.Destroy(go, 3f);
            }
        }

        // ======================================================================
        //  Wynik, HUD, sprzątanie
        // ======================================================================

        protected override void OnFinish()
        {
            // ocaleni: lepszy ten, kto jest mniej usmażony (grupy remisu co 5%)
            foreach (var p in Players)
            {
                if (p == null || p.Eliminated) continue;
                float s;
                sizzle.TryGetValue(p, out s);
                Score[p] = 100 - Mathf.RoundToInt(s * 20f) * 5;
            }
            stream.gameObject.SetActive(false);
        }

        public override string StatFor(Potato p)
        {
            if (p.Eliminated) return sizzle.ContainsKey(p) && sizzle[p] >= 1f ? "usmażony" : "wypadł";
            float s;
            sizzle.TryGetValue(p, out s);
            return "usmażenie " + Mathf.RoundToInt(s * 100f) + "%";
        }

        public override string HudInfo()
        {
            int heat = Mathf.RoundToInt(Difficulty * 100f);
            string what;
            switch (phase)
            {
                case HandPhase.Aim: what = "Babcia celuje…"; break;
                case HandPhase.Pour: what = "LEJE OLEJ!"; break;
                default: what = "Babcia szuka miejsca"; break;
            }
            return what + " · żar patelni " + heat + "%";
        }

        public override void Cleanup()
        {
            if (GM != null && GM.Hud != null) GM.Hud.ClearMeters();
            foreach (var p in Players)
            {
                if (p == null) continue;
                p.Grip = Map != null ? Map.Grip : 1f;
                p.SpeedMultiplier = 1f;
            }
            puddles.Clear();
            warnings.Clear();
            drops.Clear();
            warnPool.Clear();
        }

        // ======================================================================
        //  Boty: unikają plam, ostrzeżeń i strumienia, skaczą nad olejem, spychają rywali
        // ======================================================================

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            float skill;
            if (!botSkill.TryGetValue(p, out skill)) skill = 0.7f;
            bool seesWarnings = Random.value < skill;
            var feet = pos + Vector3.down * (Potato.HalfHeight - 0.05f);
            bool inOil = InOil(feet);

            // 1) spychanie: rywal blisko i obok niego gorąca plama → zryw
            if (!inOil && p.DashCooldownLeft <= 0f && Random.value < 0.35f * skill)
            {
                var o = Nearest(p);
                if (o != null && FlatDist(o.transform.position, pos) < 3.2f)
                {
                    var push = o.transform.position - pos; push.y = 0f;
                    var behind = o.transform.position + push.normalized * 1.8f;
                    if (InOil(behind + Vector3.down * (Potato.HalfHeight - 0.05f)))
                        return BotCommand.Go(o.transform.position, true);
                }
            }

            // 2) trzymaj bezpieczny cel, dopóki nadal jest bezpieczny
            Vector3 goal;
            if (TryGetBotGoal(p, out goal) && Danger(goal, seesWarnings) < 1f && !inOil)
                return BotCommand.Go(goal, false, false);

            // 3) wybierz najbezpieczniejszy punkt z próbek wokół
            Vector3 best = pos;
            float bestScore = Danger(pos, seesWarnings) + 0.5f;
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f + Random.value * 0.4f;
                float r = i % 2 == 0 ? 2.6f : 5.2f;
                var c = Clamp(pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r, PanRadius - 1.3f);
                c.y = pos.y;
                float sc = Danger(c, seesWarnings) + FlatDist(c, pos) * 0.06f + c.magnitude * 0.015f;
                // przejście przez plamę kosztuje (chyba że skoczymy)
                if (InOil(Vector3.Lerp(pos, c, 0.5f) + Vector3.down * (Potato.HalfHeight - 0.05f))) sc += 0.8f;
                if (sc < bestScore) { bestScore = sc; best = c; }
            }
            SetBotGoal(p, best, 0.45f);

            // skok: stoi w oleju albo między nim a celem jest plama
            bool jump = p.Grounded && (inOil || InOil(Vector3.Lerp(pos, best, 0.35f) + Vector3.down * (Potato.HalfHeight - 0.05f))) && Random.value < 0.4f + 0.5f * skill;
            return BotCommand.Go(best, false, jump);
        }

        float Danger(Vector3 at, bool useWarnings)
        {
            float d = 0f;
            var feet = new Vector3(at.x, at.y - (Potato.HalfHeight - 0.05f), at.z);
            for (int i = 0; i < puddles.Count; i++)
            {
                var pd = puddles[i];
                if (!pd.Hot) continue;
                if (feet.y - pd.Center.y > SafeHeight) continue;
                float dist = FlatDist(at, pd.Center);
                float r = pd.Radius * Mathf.Max(pd.Scale, 0.6f) * 1.3f + 0.6f;
                if (dist < r) d += 3f + (r - dist);
            }
            if (useWarnings)
            {
                for (int i = 0; i < warnings.Count; i++)
                {
                    float dist = FlatDist(at, warnings[i].Pos);
                    float r = warnings[i].Radius * 1.25f + 0.7f;
                    if (dist < r) d += 2.5f + (r - dist);
                }
            }
            if (phase == HandPhase.Pour && FlatDist(at, SpoutWorld) < 1.6f) d += 4f;
            float edge = at.magnitude - (PanRadius - 2f);
            if (edge > 0f) d += edge * 1.5f;
            return d;
        }
    }

    /// <summary>Przenosi współdzielony materiał fizyczny między colliderami (bez #if na typ PhysicMaterial/PhysicsMaterial).</summary>
    sealed class PhysicsMaterialRef
    {
        readonly Collider source;
        public PhysicsMaterialRef(Collider c) { source = c; }
        public void ApplyTo(Collider c) { c.sharedMaterial = source.sharedMaterial; }
    }

    static class PanVectorExt
    {
        /// <summary>Wektor 2D (x, y) → płaszczyzna XZ.</summary>
        public static Vector3 ToXZ(this Vector2 v) { return new Vector3(v.x, 0f, v.y); }
    }
}
