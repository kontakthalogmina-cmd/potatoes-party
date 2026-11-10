using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// <b>Chybotliwa Patelnia</b> – ogromna patelnia (r 6.5 m) zawieszona 4 m nad ogniskiem w grządce.
    /// Hook: patelnia przechyla się od ciężaru ziemniaków (moment sił, sprężyna 2. rzędu, niedotłumiona).
    /// Lądowanie po skoku daje impuls kątowy, a szybko unosząca się krawędź katapultuje stojących na niej.
    /// Co 20 s babcia wylewa olej: 5 s śliskości (grip 0.35). Na patelni turla się klops (masa 3).
    /// 3 życia; spadnięcie = zwęglenie i powrót z góry; ostatni z życiami wygrywa.
    /// </summary>
    public class TiltPanGame : Minigame
    {
        // ---------------- strojenie ----------------
        public static float PanRadius = 6.5f, PanHeight = 4f, LostBelow = 2.2f;
        public static float Gain = 0.16f;          // rad/s² na (masa·m) momentu
        public static float Stiffness = 6f;        // rad/s² na rad wychylenia (powrót do poziomu)
        public static float Damping = 2.2f;        // 1/s (niedotłumione: patelnia "faluje")
        public static float MaxTilt = 24f;         // stopnie
        public static float LandKick = 0.012f;     // rad/s na (m/s spadania · m ramienia)
        public static float CatapultSpeed = 2.6f;  // m/s pionowej prędkości krawędzi, od której wyrzuca
        public static float SlidePush = 9f;        // m/s pchnięcia w dół zbocza na 1 rad przechyłu
        public static float GreaseEvery = 20f, GreaseTime = 5f, GreaseGrip = 0.35f;
        public static int Lives = 3;
        public static float MeatballMass = 3f;

        public override string Title { get { return "Chybotliwa Patelnia"; } }
        public override string Description { get { return "Gigantyczna patelnia wisi nad ogniskiem i przechyla się pod waszym ciężarem. Zbijcie się po jednej stronie, a wszyscy zjadą do ognia. Skacz, żeby katapultować rywali z unoszącej się krawędzi!"; } }
        public override string Goal { get { return "Ostatni ziemniak z życiami wygrywa (3 życia)."; } }
        public override string Tip { get { return "Lądowanie po skoku przy krawędzi mocno buja patelnią. Klops waży jak 3 ziemniaki!"; } }
        public override string Controls { get { return "Ruch · skok = bujnięcie patelni · zryw = pchnij rywala"; } }
        public override MapId PreferredMap { get { return MapId.Garden; } }
        public override float TimeLimit { get { return 150f; } }
        public override float ArenaRadius { get { return 22f; } }
        public override float KillY { get { return GardenMap.KillY; } }
        public override bool FallEliminates { get { return false; } }
        public override bool EndsWhenOneLeft { get { return true; } }
        public override float CameraPull { get { return 0.35f; } }
        public override Vector3 ArenaCenter { get { return Center; } }

        GardenLayout L;
        Vector3 Center { get { return L != null ? new Vector3(L.PitCenter.x, PanHeight, L.PitCenter.z) : new Vector3(0, PanHeight, 0); } }
        Rigidbody pan;
        Vector2 tilt, tiltVel;   // x = obrót wokół X (przód w dół), y = obrót wokół Z (prawo w górę)
        float greaseTimer, greaseLeft = -1f;
        bool greaseWarned;
        Rigidbody meatball;
        readonly Dictionary<Potato, int> lives = new Dictionary<Potato, int>();
        readonly Dictionary<Potato, float> catapultCd = new Dictionary<Potato, float>();
        readonly List<Light> fireLights = new List<Light>();

        public override void BuildArena()
        {
            L = GardenMap.Build(Root, Map, GardenCenter.Pan);
            var c = Center;

            // ---- patelnia: kinematyczne ciało obracane MoveRotation (ziemniaki stoją na fizycznej powierzchni) ----
            var go = Shapes.Empty("Patelnia", Root, c);
            pan = go.AddComponent<Rigidbody>();
            pan.isKinematic = true;
            pan.interpolation = RigidbodyInterpolation.Interpolate;
            var iron = Shapes.Hex("#2E2A33");
            MapBuilder.Disc(go.transform, Vector3.down * 0.2f, PanRadius, 0.4f, iron);
            var inside = MapBuilder.Disc(go.transform, Vector3.up * 0.01f, PanRadius - 0.35f, 0.02f, Shapes.Hex("#4A4450"), false);
            inside.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#4A4450"), 0.85f);
            // niski rant z 4 przerwami (rant trzyma, przerwy to wyjścia awaryjne… do ognia)
            const int seg = 28;
            for (int i = 0; i < seg; i++)
            {
                if (i % 7 == 3) continue;
                float a = (i + 0.5f) * Mathf.PI * 2f / seg;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (PanRadius - 0.1f);
                Shapes.Box(go.transform, p + Vector3.up * 0.15f, new Vector3(0.25f, 0.3f, 2f * Mathf.PI * PanRadius / seg + 0.05f), iron, true, -a * Mathf.Rad2Deg);
            }
            // rączka: wąska belka 5 m (stanie na niej = ogromne ramię momentu)
            Shapes.Box(go.transform, new Vector3(PanRadius + 2.4f, 0f, 0f), new Vector3(5f, 0.35f, 1.1f), Shapes.Hex("#6B3E26"));
            // łańcuch / zawieszenie (wizualne)
            Shapes.Prim(PrimitiveType.Cylinder, Root, c + Vector3.up * 6f, new Vector3(0.18f, 6f, 0.18f), Shapes.Hex("#8A8A90"), false, 0.8f);

            // ---- ognisko pod patelnią ----
            var floor = L.PitCenter;
            for (int i = 0; i < 9; i++)
            {
                var off = new Vector3(Random.Range(-4.5f, 4.5f), 0f, Random.Range(-4.5f, 4.5f));
                var log = Shapes.Prim(PrimitiveType.Cylinder, Root, floor + off + Vector3.up * 0.25f, new Vector3(0.45f, 1.1f, 0.45f), Shapes.Hex("#3A2418"), false, 0.2f);
                log.transform.rotation = Quaternion.Euler(90f, Random.value * 180f, 0f);
                var flame = Shapes.Prim(PrimitiveType.Sphere, Root, floor + off + Vector3.up * 0.8f, new Vector3(0.9f, 1.6f, 0.9f), Shapes.Hex("#FF7A1A"), false, 0.2f);
                flame.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FF7A1A"), 0.2f, 2.2f);
                flame.AddComponent<Bobber>().Amplitude = 0.2f;
            }
            for (int i = 0; i < 3; i++)
                fireLights.Add(Look.PointLight(Root, floor + new Vector3((i - 1) * 3.5f, 1.2f, 0f), Shapes.Hex("#FF8A2A"), 11f, 1.8f, 0.35f));

            // ---- klops: ciężka kula toczona przez przechył ----
            var mb = Shapes.Prim(PrimitiveType.Sphere, Root, c + new Vector3(-2f, 1.2f, 1f), Vector3.one * 1.3f, Shapes.Hex("#7A3B22"), true, 0.35f);
            mb.name = "Klops";
            meatball = mb.AddComponent<Rigidbody>();
            meatball.mass = 6f;
            meatball.interpolation = RigidbodyInterpolation.Interpolate;
            mb.AddComponent<BonkBall>().Power = 0.9f;

            greaseTimer = GreaseEvery;
            Potato.Landed += OnLanded;
        }

        public override Vector3 SpawnPoint(int i, int n)
        {
            float a = i / (float)Mathf.Max(1, n) * Mathf.PI * 2f;
            return Center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3.6f + Vector3.up * (Potato.HalfHeight + 0.4f);
        }

        protected override Vector3 RespawnPoint(Potato p)
        {
            var r = Random.insideUnitCircle * 2.5f;
            return Center + new Vector3(r.x, 6f, r.y);
        }

        protected override void ConfigurePlayer(Potato p)
        {
            lives[p] = Lives;
            Score[p] = Lives;
            catapultCd[p] = 0f;
        }

        public override IEnumerable<Transform> ExtraCameraTargets() { if (pan != null) yield return pan.transform; }

        // ---------------- fizyka patelni ----------------

        Quaternion Rot { get { return Quaternion.Euler(tilt.x * Mathf.Rad2Deg, 0f, tilt.y * Mathf.Rad2Deg); } }

        bool OnPan(Potato p)
        {
            if (p == null || p.Eliminated || !p.Grounded) return false;
            var col = p.GroundCollider;
            return col != null && pan != null && col.transform.IsChildOf(pan.transform);
        }

        void OnLanded(Potato p, float fallSpeed, Vector3 point)
        {
            if (!Running || pan == null || !lives.ContainsKey(p)) return;
            var r = point - Center;
            if (new Vector2(r.x, r.z).magnitude > PanRadius + 5.5f || Mathf.Abs(r.y) > 2f) return;
            // uderzenie w punkcie r: przód (z+) w dół = +x, prawo (x+) w dół = −y
            tiltVel += new Vector2(r.z, -r.x) * fallSpeed * LandKick;
            if (fallSpeed > 7f) Feel.Shake(0.15f);
        }

        void StepPan(float dt)
        {
            Vector2 moment = Vector2.zero;
            foreach (var p in Players)
            {
                if (!OnPan(p)) continue;
                var r = p.transform.position - Center;
                moment += new Vector2(r.z, -r.x);
            }
            if (meatball != null)
            {
                var r = meatball.position - Center;
                if (r.y > -1.5f && new Vector2(r.x, r.z).magnitude < PanRadius) moment += new Vector2(r.z, -r.x) * MeatballMass;
            }
            var acc = moment * Gain - tilt * Stiffness - tiltVel * Damping;
            tiltVel += acc * dt;
            tilt += tiltVel * dt;
            float max = MaxTilt * Mathf.Deg2Rad;
            if (tilt.magnitude > max) { tilt = tilt.normalized * max; tiltVel *= 0.3f; } // twardy ogranicznik: stuk o łańcuch
            pan.MoveRotation(Rot);

            // ześlizg i katapulta dla stojących na patelni
            var rot = Rot;
            var normal = rot * Vector3.up;
            var downhill = new Vector3(normal.x, 0f, normal.z);
            foreach (var p in Players)
            {
                if (p == null || p.Eliminated || !lives.ContainsKey(p)) continue;
                catapultCd[p] -= dt;
                if (!OnPan(p)) { p.Push = Vector3.zero; continue; }
                p.Push = downhill * SlidePush * (greaseLeft > 0f ? 1.8f : 1f);
                // prędkość punktu powierzchni pod ziemniakiem: v = ω × r (ω w osi świata)
                var r = p.transform.position - Center;
                var omega = new Vector3(tiltVel.x, 0f, tiltVel.y);
                float vy = Vector3.Cross(omega, r).y;
                if (vy > CatapultSpeed && catapultCd[p] <= 0f)
                {
                    catapultCd[p] = 0.8f;
                    var outward = new Vector3(r.x, 0f, r.z).normalized;
                    p.Knock(Vector3.up * (6f + vy * 2.2f) + outward * vy * 1.4f, 0.35f);
                    Fx.Dust(Root, p.transform.position + Vector3.down * 0.5f, 1f, 6);
                    AudioManager.PlayPitched(Sfx.Jump, 0.7f, 0.8f);
                }
            }
        }

        // ---------------- pętla ----------------

        public override void Tick(float dt)
        {
            if (Running && pan != null) StepPan(dt);

            // spadnięcie z patelni = ogień (wcześniej niż KillY)
            foreach (var p in Players)
            {
                if (p == null || p.Eliminated || !lives.ContainsKey(p)) continue;
                if (p.transform.position.y < LostBelow) LoseLife(p);
            }
            if (meatball != null && meatball.position.y < LostBelow - 3f)
            {
                meatball.position = Center + Vector3.up * 5f;
                Phys.SetVelocity(meatball, Vector3.zero);
                GM.Hud.Toast("Babcia dorzuca klopsa!", Shapes.Hex("#C2603A"));
            }

            base.Tick(dt);
            if (Finished || !Running) return;
            Grease(dt);
            for (int i = 0; i < fireLights.Count; i++) if (fireLights[i] != null) fireLights[i].intensity = 1.8f + Mathf.PerlinNoise(Time.time * 3f, i) * 1.4f;
        }

        void LoseLife(Potato p)
        {
            lives[p]--;
            Score[p] = lives[p];
            Fx.Sparks(Root, p.transform.position, Shapes.Hex("#FF8A2A"), 18, 6f);
            if (p.Surface != null) p.Surface.Scorch(p.transform.position + Vector3.down, 0.6f);
            p.Push = Vector3.zero;
            if (lives[p] <= 0) { Eliminate(p, true, " spada do ognia – frytka!"); return; }
            GM.Hud.Toast(p.Slot.Name + " przypieczony! (" + lives[p] + " ż.)", p.Color);
            Respawn(p);
        }

        void Grease(float dt)
        {
            if (greaseLeft > 0f)
            {
                greaseLeft -= dt;
                if (greaseLeft <= 0f) { SetGrip(Map.Grip); GM.Hud.Toast("Olej wsiąkł", Color.white); }
                return;
            }
            greaseTimer -= dt;
            if (!greaseWarned && greaseTimer < 2f) { greaseWarned = true; GM.Hud.Toast("Babcia sięga po olej…", Shapes.Hex("#F7C548")); AudioManager.Play(Sfx.Tick, 1f); }
            if (greaseTimer > 0f) return;
            greaseTimer = GreaseEvery;
            greaseWarned = false;
            greaseLeft = GreaseTime;
            SetGrip(GreaseGrip);
            for (int i = 0; i < 8; i++) Fx.Splash(Root, Center + Vector3.up * 0.4f + Random.insideUnitSphere * 4f, Shapes.Hex("#F7C548"), 0.9f);
            AudioManager.Play(Sfx.Whoosh, 0.8f);
            GM.Hud.Toast("OLEJ! Ślisko przez 5 s", Shapes.Hex("#F7C548"));
        }

        void SetGrip(float g) { foreach (var p in Players) if (p != null) p.Grip = g; }

        protected override void OnEliminated(Potato p) { p.Push = Vector3.zero; }

        public override string StatFor(Potato p)
        {
            int l;
            if (!lives.TryGetValue(p, out l)) return "";
            return p.Eliminated ? "frytka" : new string('♥', Mathf.Max(0, l));
        }

        public override string HudInfo()
        {
            float deg = tilt.magnitude * Mathf.Rad2Deg;
            string g = greaseLeft > 0f ? "OLEJ " + Mathf.CeilToInt(greaseLeft) + " s · " : "";
            return g + "Przechył " + Mathf.RoundToInt(deg) + "°";
        }

        public override void Cleanup()
        {
            Potato.Landed -= OnLanded;
            foreach (var p in Players) if (p != null) p.Push = Vector3.zero;
        }

        // ---------------- boty ----------------

        public override BotCommand Bot(Potato p)
        {
            var c = Center;
            var pos = p.transform.position;
            if (pos.y < c.y - 1f) return BotCommand.Go(c, false, true);
            // punkt równowagi: przeciwnie do opadającej strony (normalna wskazuje w dół zbocza)
            var n = Rot * Vector3.up;
            var balance = c - new Vector3(n.x, 0f, n.z) * 18f;
            var off = balance - c; off.y = 0f;
            if (off.magnitude > 3.5f) balance = c + off.normalized * 3.5f;
            // okazja: rywal przy krawędzi, ja bliżej środka i za nim → zryw na zewnątrz
            var t = Nearest(p, o => FlatDist(o.transform.position, c) > PanRadius - 2f);
            if (t != null)
            {
                float dt = FlatDist(t.transform.position, pos);
                bool behind = FlatDist(pos, c) < FlatDist(t.transform.position, c);
                if (dt < 3.2f && behind)
                    return BotCommand.Go(t.transform.position, p.DashCooldownLeft <= 0f && Random.value < 0.4f);
            }
            // skok po wysokiej stronie, gdy rywal stoi naprzeciwko (bujnięcie wyrzuci go do góry)
            bool hop = FlatDist(pos, c) > 3f && Random.value < 0.01f;
            return BotCommand.Go(balance + Random.insideUnitSphere * 0.6f, false, hop);
        }
    }
}
