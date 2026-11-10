using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// <b>Kretowisko</b> – asymetryczne polowanie na zasypanej grządce i trawniku.
    /// Kret(y) ryją pod ziemią: niewidoczne, bez kolizji z ogrodnikami, zdradza je tylko kopczyk ziemi
    /// (przy prędkości &gt; 4.5 m/s) i drżenie gruntu. Akcja kreta = wyskok: wyrzuca w górę ogrodników w promieniu 1.4 m (+3 pkt każdy).
    /// Ogrodnicy: skok i "tupnięcie" (lądowanie ≥ 6 m/s w 1.6 m od kreta) ogłusza go i odkrywa (+2 pkt, kret −2).
    /// Kamienne płyty: kret nad nimi jest widoczny. Miękka ziemia: kret ×1.3 szybciej, ogrodnik ×0.8.
    /// Role rotują co 45 s, każdy jest kretem tyle samo razy.
    /// </summary>
    public class MoleGame : Minigame
    {
        // ---------------- strojenie ----------------
        public static float RoleTime = 45f;
        public static float EmergeRadius = 1.4f, EmergeUp = 14f, EmergeCooldown = 2.5f, RevealTime = 1.2f;
        public static float StompSpeed = 6f, StompRadius = 1.6f, StompStun = 1.5f, StompReveal = 2f;
        public static float MoundSpeed = 4.5f, MoleSpeed = 8.2f, GardenerSpeed = 7.2f;
        public static int EmergePoints = 3, StompPoints = 2, StompPenalty = 2;

        public override string Title { get { return "Kretowisko"; } }
        public override string Description { get { return "Jedni ryją pod ziemią jako krety, reszta pilnuje grządki. Kret wyskakuje spod ziemi i wystrzeliwuje ogrodników w powietrze. Ogrodnik, który tupnie (wyląduje po skoku) nad kretem, ogłusza go. Role zmieniają się co 45 sekund."; } }
        public override string Goal { get { return "Najwięcej punktów po 3 rundach ról."; } }
        public override string Tip { get { return "Kret: zwolnij, a nie zostawisz kopczyka. Ogrodnik: obserwuj drżenie ziemi i skacz nad nim!"; } }
        public override string Controls { get { return "Kret: ruch · akcja = wyskok   |   Ogrodnik: ruch · skok = tupnięcie · zryw"; } }
        public override MapId PreferredMap { get { return MapId.Garden; } }
        public override float TimeLimit { get { return RoleTime * 3f; } }
        public override float ArenaRadius { get { return 22f; } }
        public override float KillY { get { return GardenMap.KillY; } }
        public override bool FallEliminates { get { return false; } }
        public override bool EndsWhenOneLeft { get { return false; } }
        public override bool ShowLeaderCrown { get { return true; } }
        public override float CameraPull { get { return 0.1f; } }

        class MoleState { public float Cooldown, RevealLeft, DustTimer, MoundTimer; public bool Up; }

        GardenLayout L;
        readonly Dictionary<Potato, MoleState> moles = new Dictionary<Potato, MoleState>();
        readonly List<Vector3> slabs = new List<Vector3>();
        readonly List<Transform> mounds = new List<Transform>();
        readonly List<float> moundAge = new List<float>();
        int round = -1, molesPerRound = 1;
        float roleTimer;

        public override void BuildArena()
        {
            L = GardenMap.Build(Root, Map, GardenCenter.Soil);
            // kamienne płyty na zasypanej grządce i trawniku: kret nad nimi jest odkryty
            var stone = Shapes.Hex("#A39E94");
            var spots = new[] { new Vector3(-4f, 0f, -4f), new Vector3(4f, 0f, 4f), new Vector3(4f, 0f, -4f), new Vector3(-4f, 0f, 4f), new Vector3(0f, 0f, 12f), new Vector3(-13f, 0f, 0f), new Vector3(13f, 0f, -2f), new Vector3(2f, 0f, -13f) };
            foreach (var s in spots)
            {
                var pos = L.PitCenter + new Vector3(s.x, 1.5f, s.z);
                pos.y = 0.02f;
                Shapes.Box(Root, pos, new Vector3(2.4f, 0.06f, 2.4f), stone, false, Random.Range(0f, 40f));
                slabs.Add(pos);
            }
            Potato.Landed += OnLanded;
        }

        public override Vector3 SpawnPoint(int i, int n)
        {
            float a = i / (float)Mathf.Max(1, n) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 9f + Vector3.up * (Potato.HalfHeight + 0.1f);
        }

        protected override Vector3 RespawnPoint(Potato p)
        {
            var r = Random.insideUnitCircle * 8f;
            return new Vector3(r.x, 3f, r.y);
        }

        public override void Begin()
        {
            base.Begin();
            molesPerRound = Mathf.Max(1, Mathf.CeilToInt(Players.Count / 3f));
            NextRound();
        }

        // ---------------- role ----------------

        void NextRound()
        {
            round++;
            roleTimer = RoleTime;
            foreach (var p in new List<Potato>(moles.Keys)) Unearth(p, true);
            moles.Clear();
            var names = "";
            for (int k = 0; k < molesPerRound; k++)
            {
                int idx = (round * molesPerRound + k) % Mathf.Max(1, Players.Count);
                var p = Players[idx];
                if (p == null || moles.ContainsKey(p)) continue;
                moles[p] = new MoleState { Cooldown = 1f };
                Burrow(p);
                names += (names.Length > 0 ? ", " : "") + p.Slot.Name;
            }
            foreach (var p in Players)
            {
                if (p == null) continue;
                bool mole = moles.ContainsKey(p);
                p.CanDash = !mole;
                p.CanJump = !mole;
            }
            GM.Hud.Toast("Runda " + (round + 1) + "/3 · krety: " + names, Shapes.Hex("#8B5A3C"));
            AudioManager.Play(Sfx.Whoosh, 0.8f);
        }

        void SetVisible(Potato p, bool visible)
        {
            if (p.Visual != null) p.Visual.gameObject.SetActive(visible);
            var ring = p.transform.Find("Pierscien gracza");
            if (ring != null) ring.gameObject.SetActive(visible);
        }

        void SetGhost(Potato p, bool ghost)
        {
            var mine = p.GetComponent<Collider>();
            foreach (var o in Players)
            {
                if (o == null || o == p) continue;
                var oc = o.GetComponent<Collider>();
                if (mine != null && oc != null) Physics.IgnoreCollision(mine, oc, ghost);
            }
        }

        void Burrow(Potato p)
        {
            SetVisible(p, false);
            SetGhost(p, true);
            MoleState s;
            if (moles.TryGetValue(p, out s)) s.Up = false;
            Fx.Dust(Root, p.transform.position + Vector3.down * 0.5f, 1.2f, 10);
        }

        /// <summary>Kret wychodzi na stałe (koniec rundy) albo na chwilę (wyskok / ogłuszenie).</summary>
        void Unearth(Potato p, bool forGood)
        {
            if (p == null) return;
            SetVisible(p, true);
            SetGhost(p, false);
            if (forGood) { p.CanDash = true; p.CanJump = true; }
        }

        void Emerge(Potato p)
        {
            MoleState s;
            if (!Running || !moles.TryGetValue(p, out s) || s.Up || s.Cooldown > 0f || p.IsStunned) return;
            s.Up = true;
            s.RevealLeft = RevealTime;
            s.Cooldown = EmergeCooldown + RevealTime;
            Unearth(p, false);
            var pos = p.transform.position;
            p.Knock(Vector3.up * 8f, 0.15f);
            Fx.Dust(Root, pos + Vector3.down * 0.4f, 2.2f, 18);
            Fx.Impact(Root, pos, Shapes.Hex("#6B4A32"), Vector3.up, 0.8f);
            Feel.Shake(0.2f);
            AudioManager.Play(Sfx.Crack, 0.9f);
            int hits = 0;
            foreach (var o in Players)
            {
                if (o == null || o == p || o.Eliminated || moles.ContainsKey(o)) continue;
                var d = o.transform.position - pos; d.y = 0f;
                if (d.magnitude > EmergeRadius || Mathf.Abs(o.transform.position.y - pos.y) > 1.5f) continue;
                o.Knock(Vector3.up * EmergeUp + d.normalized * 3f, 1f);
                if (o.Surface != null) o.Surface.SplashMud(pos, 0.5f, Shapes.Hex("#6B4A32"));
                hits++;
            }
            if (hits > 0)
            {
                Score[p] += EmergePoints * hits;
                GM.Hud.Toast(p.Slot.Name + " wyskakuje! +" + EmergePoints * hits, p.Color);
            }
        }

        void OnLanded(Potato g, float fallSpeed, Vector3 point)
        {
            if (!Running || fallSpeed < StompSpeed || moles.ContainsKey(g) || !Score.ContainsKey(g)) return;
            Fx.Dust(Root, point, 0.9f, 6);
            foreach (var kv in moles)
            {
                var m = kv.Key;
                if (m == null || kv.Value.Up) continue;
                if (FlatDist(m.transform.position, point) > StompRadius) continue;
                kv.Value.Up = true;
                kv.Value.RevealLeft = StompReveal;
                kv.Value.Cooldown = StompReveal + 1f;
                Unearth(m, false);
                m.Knock(Vector3.up * 7f + (m.transform.position - point).normalized * 2f, StompStun);
                Score[g] += StompPoints;
                Score[m] = Mathf.Max(0, Score[m] - StompPenalty);
                Fx.Impact(Root, m.transform.position, Color.white, Vector3.up, 1f);
                AudioManager.Play(Sfx.Bump, 1f);
                Feel.Shake(0.25f);
                GM.Hud.Toast(g.Slot.Name + " tupie w kreta " + m.Slot.Name + "! +" + StompPoints, g.Color);
            }
        }

        public override void OnBump(Potato hitter, Potato victim, bool dash)
        {
            // odkryty kret trafiony zrywem: +1 i szybciej wraca pod ziemię (ucieka)
            MoleState s;
            if (!dash || moles.ContainsKey(hitter) || !moles.TryGetValue(victim, out s) || !s.Up) return;
            Score[hitter] += 1;
            GM.Hud.Toast(hitter.Slot.Name + " przegania kreta! +1", hitter.Color);
        }

        // ---------------- pętla ----------------

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished || !Running) return;
            roleTimer -= dt;
            if (roleTimer <= 0f && round < 2) NextRound();

            foreach (var p in Players)
            {
                if (p == null || p.Eliminated) continue;
                bool mole = moles.ContainsKey(p);
                bool soft = OnSoftSoil(p.transform.position);
                p.MoveSpeed = mole ? MoleSpeed * (soft ? 1.3f : 1f) : GardenerSpeed * (soft ? 0.8f : 1f);
            }

            foreach (var kv in moles)
            {
                var p = kv.Key; var s = kv.Value;
                if (p == null) continue;
                s.Cooldown -= dt;
                if (s.Up)
                {
                    s.RevealLeft -= dt;
                    if (s.RevealLeft <= 0f && !p.IsStunned) Burrow(p);
                    continue;
                }
                var v = Phys.GetVelocity(p.Body); v.y = 0f;
                float speed = v.magnitude;
                var pos = p.transform.position;
                // na płycie albo poza ziemią (taras, dach) – widoczny
                bool exposed = OnSlab(pos) || pos.y > 1.5f;
                SetVisible(p, exposed);
                // drżenie gruntu (zawsze, gdy rusza się), kopczyk przy szybkim ryciu
                s.DustTimer -= dt;
                if (speed > 1f && s.DustTimer <= 0f)
                {
                    s.DustTimer = speed > MoundSpeed ? 0.1f : 0.35f;
                    Fx.Dust(Root, pos + Vector3.down * 0.55f, speed > MoundSpeed ? 0.8f : 0.35f, speed > MoundSpeed ? 4 : 2);
                }
                s.MoundTimer -= dt;
                if (speed > MoundSpeed && s.MoundTimer <= 0f)
                {
                    s.MoundTimer = 0.22f;
                    var m = Shapes.Prim(PrimitiveType.Sphere, Root, new Vector3(pos.x, pos.y - Potato.HalfHeight, pos.z), new Vector3(0.9f, 0.45f, 0.9f), Shapes.Hex("#5A3D28"), false, 0.15f, "Kopczyk");
                    mounds.Add(m.transform);
                    moundAge.Add(0f);
                }
            }

            for (int i = mounds.Count - 1; i >= 0; i--)
            {
                moundAge[i] += dt;
                if (mounds[i] == null || moundAge[i] > 4f) { if (mounds[i] != null) Object.Destroy(mounds[i].gameObject); mounds.RemoveAt(i); moundAge.RemoveAt(i); continue; }
                float k = 1f - moundAge[i] / 4f;
                mounds[i].localScale = new Vector3(0.9f, 0.45f * k, 0.9f) * (0.6f + 0.4f * k);
            }
        }

        bool OnSlab(Vector3 pos)
        {
            foreach (var s in slabs) if (Mathf.Abs(pos.x - s.x) < 1.3f && Mathf.Abs(pos.z - s.z) < 1.3f) return true;
            return false;
        }

        bool OnSoftSoil(Vector3 pos)
        {
            if (Mathf.Abs(pos.x - L.PitCenter.x) < L.PitHalf && Mathf.Abs(pos.z - L.PitCenter.z) < L.PitHalf) return true; // zasypana grządka
            foreach (var s in L.SoftSoil) if (FlatDist(pos, s) < 2.4f) return true;
            return false;
        }

        protected override void ConfigurePlayer(Potato p)
        {
            p.MoveSpeed = GardenerSpeed;
            p.ActionPressed += Emerge;
        }

        public override string StatFor(Potato p)
        {
            return (moles.ContainsKey(p) ? "KRET · " : "ogrodnik · ") + ScoreOf(p) + " pkt";
        }

        public override string HudInfo()
        {
            return "Runda ról " + (round + 1) + "/3 · zmiana za " + Mathf.CeilToInt(Mathf.Max(0f, roleTimer)) + " s";
        }

        public override void Cleanup()
        {
            Potato.Landed -= OnLanded;
            foreach (var p in Players)
            {
                if (p == null) continue;
                p.ActionPressed -= Emerge;
                Unearth(p, true);
                p.MoveSpeed = 7.5f;
            }
            moles.Clear();
        }

        // ---------------- boty ----------------

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            MoleState s;
            if (moles.TryGetValue(p, out s))
            {
                // kret: najbliższy ogrodnik w zasięgu, wyskok tuż pod nim
                var t = Nearest(p, o => !moles.ContainsKey(o));
                if (t == null) return BotCommand.Go(Wander(p, 12f));
                float d = FlatDist(t.transform.position, pos);
                bool emerge = d < 1.1f && s.Cooldown <= 0f && !s.Up;
                return BotCommand.Go(t.transform.position, emerge);
            }
            // ogrodnik: szukaj kreta po kopczykach / odkryciu
            Potato seen = null; float best = 12f;
            foreach (var kv in moles)
            {
                var m = kv.Key;
                if (m == null) continue;
                var v = Phys.GetVelocity(m.Body); v.y = 0f;
                bool visible = kv.Value.Up || v.magnitude > MoundSpeed || OnSlab(m.transform.position);
                float d = FlatDist(m.transform.position, pos);
                if (visible && d < best) { best = d; seen = m; }
            }
            if (seen == null) return BotCommand.Go(Wander(p, 14f, 2.2f));
            MoleState ms = moles[seen];
            if (ms.Up) return BotCommand.Go(seen.transform.position, best < 3f && p.DashCooldownLeft <= 0f);
            // tupnięcie: wyprzedź kreta i skocz, gdy jest blisko
            var lead = seen.transform.position + Phys.GetVelocity(seen.Body) * 0.35f;
            return BotCommand.Go(lead, false, best < 2.2f && p.Grounded && Random.value < 0.5f);
        }
    }
}
