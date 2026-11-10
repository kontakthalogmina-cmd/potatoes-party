using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// <b>Wieża Ziemniaków</b> – wskakuj rywalom na głowy i buduj żywe wieże.
    /// Wskoczenie (w powietrzu, &lt; 0.95 m od podstawy, ponad jej głową) = wspinaczka na szczyt wieży.
    /// Punkty co sekundę dla każdego w wieży: 2 piętra = 1, 3 = 2, 4+ = 4; wieża na stole babci ×2.
    /// Fizyka: każda wieża to odwrócone wahadło – przechył rośnie sam z siebie (tym szybciej, im wyższa),
    /// przyspieszenie podstawy go zmienia, a gałki jeźdźców go równoważą. Ponad 35° wieża się wali.
    /// Podstawa traci 15% prędkości na piętro i nie skacze; skok jeźdźca = zeskok (wszyscy nad nim spadają).
    /// Zryw w podstawę to mocne bujnięcie wieży.
    /// </summary>
    public class TowerGame : Minigame
    {
        // ---------------- strojenie ----------------
        public static float Step = 1.22f;              // m między środkami pięter
        public static float ToppleDeg = 35f;
        public static float Unstable = 1.4f, UnstablePerLevel = 0.45f;  // rad/s² na rad (dodatnie sprzężenie)
        public static float Coupling = 0.055f;          // rad/s² na m/s² przyspieszenia podstawy
        public static float Control = 4f;               // rad/s² z pełnej gałki jednego jeźdźca
        public static float Damp = 1.6f;
        public static float DashKick = 1.6f, DashKickPerLevel = 0.4f;   // rad/s
        public static float MountRadius = 0.95f, MountAbove = 0.75f;
        public static float SlowPerLevel = 0.15f;
        public static int MaxSize = 6;
        public static Vector3 TableCenter = new Vector3(0f, 2.5f, 0f);
        public static float TableHalf = 4f;

        public override string Title { get { return "Wieża Ziemniaków"; } }
        public override string Description { get { return "Wskakuj innym na głowy i budujcie żywe wieże – każdy w wieży zdobywa punkty co sekundę, a im wyżej, tym więcej. Wieża sama się chwieje: jeźdźcy muszą ją równoważyć gałką. Zrywem w podstawę walisz cudze wieże!"; } }
        public override string Goal { get { return "Najwięcej punktów po 2 minutach (wieża na stole babci liczy się podwójnie)."; } }
        public override string Tip { get { return "Jako jeździec trzymaj gałkę przeciwnie do przechyłu. Skok = zeskok."; } }
        public override string Controls { get { return "Skok przy kimś = wspinaczka · na wieży: gałka = równowaga, skok = zeskok · zryw = bujnij wieżę"; } }
        public override MapId PreferredMap { get { return MapId.Garden; } }
        public override float TimeLimit { get { return 120f; } }
        public override float ArenaRadius { get { return 22f; } }
        public override float KillY { get { return GardenMap.KillY; } }
        public override bool FallEliminates { get { return false; } }
        public override bool EndsWhenOneLeft { get { return false; } }
        public override bool ShowLeaderCrown { get { return true; } }
        public override float CameraPull { get { return 0.1f; } }

        class Tower { public Vector2 Lean, LeanVel; public Vector3 LastVel; }

        GardenLayout L;
        readonly Dictionary<Potato, Potato> below = new Dictionary<Potato, Potato>();   // jeździec → pod nim
        readonly Dictionary<Potato, Potato> above = new Dictionary<Potato, Potato>();   // pod → jeździec
        readonly Dictionary<Potato, Tower> towers = new Dictionary<Potato, Tower>();    // podstawa → stan wahadła
        readonly Dictionary<Potato, float> mountedAt = new Dictionary<Potato, float>();
        readonly Dictionary<Potato, float> acc = new Dictionary<Potato, float>();
        readonly List<Potato> tmp = new List<Potato>();

        // ---------------- arena ----------------

        public override void BuildArena()
        {
            L = GardenMap.Build(Root, Map, GardenCenter.Table);
            TableCenter = new Vector3(L.PitCenter.x, 2.5f, L.PitCenter.z);
            var wood = Shapes.Hex("#B07A45"); var dark = Shapes.Hex("#7A5230");
            // blat 8×8 na 4 nogach, wyrastających z dna grządki
            Shapes.Box(Root, TableCenter + Vector3.down * 0.2f, new Vector3(2 * TableHalf, 0.4f, 2 * TableHalf), wood);
            Shapes.Box(Root, TableCenter + Vector3.up * 0.01f, new Vector3(2 * TableHalf - 0.6f, 0.03f, 2 * TableHalf - 0.6f), Shapes.Hex("#E8D9B5"), false); // obrus
            float legH = TableCenter.y - L.PitCenter.y;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Shapes.Box(Root, new Vector3(TableCenter.x + sx * (TableHalf - 0.5f), L.PitCenter.y + legH * 0.5f - 0.2f, TableCenter.z + sz * (TableHalf - 0.5f)), new Vector3(0.6f, legH, 0.6f), dark);
            // dwie rampy z trawnika (W i E) na blat
            GardenMap.Ramp(Root, new Vector3(TableCenter.x - L.PitHalf - 3f, 0f, TableCenter.z), new Vector3(TableCenter.x - TableHalf, TableCenter.y, TableCenter.z), 2.2f, dark);
            GardenMap.Ramp(Root, new Vector3(TableCenter.x + L.PitHalf + 3f, 0f, TableCenter.z), new Vector3(TableCenter.x + TableHalf, TableCenter.y, TableCenter.z), 2.2f, dark);
            // dzbanek z kompotem: tylko ozdoba i punkt orientacyjny
            Shapes.Prim(PrimitiveType.Cylinder, Root, TableCenter + new Vector3(2.8f, 0.6f, 2.8f), new Vector3(0.7f, 0.6f, 0.7f), Shapes.Hex("#D9534F"), true, 0.8f);
            Look.PointLight(Root, TableCenter + Vector3.up * 4f, Shapes.Hex("#FFE3A3"), 12f, 1.2f);
        }

        public override Vector3 SpawnPoint(int i, int n)
        {
            float a = i / (float)Mathf.Max(1, n) * Mathf.PI * 2f + 0.3f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 10.5f + Vector3.up * (Potato.HalfHeight + 0.1f);
        }

        protected override Vector3 RespawnPoint(Potato p)
        {
            float a = Random.value * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 11f + Vector3.up * 3f;
        }

        protected override void ConfigurePlayer(Potato p) { acc[p] = 0f; }

        // ---------------- struktura wieży ----------------

        Potato BaseOf(Potato p) { Potato b; while (below.TryGetValue(p, out b)) p = b; return p; }
        Potato TopOf(Potato p) { Potato a; while (above.TryGetValue(p, out a)) p = a; return p; }
        int SizeOf(Potato basePotato) { int n = 1; var p = basePotato; Potato a; while (above.TryGetValue(p, out a)) { p = a; n++; } return n; }
        bool Riding(Potato p) { return below.ContainsKey(p); }
        bool OnTable(Vector3 pos) { return Mathf.Abs(pos.x - TableCenter.x) < TableHalf && Mathf.Abs(pos.z - TableCenter.z) < TableHalf && pos.y > TableCenter.y - 0.2f; }

        void Mount(Potato rider, Potato basePotato)
        {
            var top = TopOf(basePotato);
            if (SizeOf(basePotato) >= MaxSize) return;
            below[rider] = top;
            above[top] = rider;
            mountedAt[rider] = Time.time;
            Phys.SetVelocity(rider.Body, Vector3.zero);
            rider.Body.isKinematic = true;
            rider.GetComponent<Collider>().enabled = false;
            Tower t;
            if (!towers.TryGetValue(basePotato, out t)) towers[basePotato] = t = new Tower { LastVel = Phys.GetVelocity(basePotato.Body) };
            t.LeanVel += Random.insideUnitCircle * 0.4f; // wskoczenie lekko buja
            RefreshBase(basePotato);
            int size = SizeOf(basePotato);
            Fx.Dust(Root, top.transform.position + Vector3.up * 0.6f, 0.7f, 5);
            AudioManager.PlayPitched(Sfx.Join, 0.6f, 0.9f + size * 0.08f);
            if (size >= 3) GM.Hud.Toast("Wieża " + basePotato.Slot.Name + ": " + size + " piętra!", basePotato.Color);
        }

        /// <summary>Odczepia jeźdźca i wszystkich nad nim; impuls rośnie z wysokością.</summary>
        void Dismount(Potato rider, Vector3 dir, float power)
        {
            tmp.Clear();
            var p = rider;
            tmp.Add(p);
            Potato a;
            while (above.TryGetValue(p, out a)) { tmp.Add(a); p = a; }
            var b = below.ContainsKey(rider) ? below[rider] : null;
            if (b != null) above.Remove(b);
            for (int i = 0; i < tmp.Count; i++)
            {
                var r = tmp[i];
                below.Remove(r);
                above.Remove(r);
                r.transform.rotation = Quaternion.identity;
                r.Body.isKinematic = false;
                r.GetComponent<Collider>().enabled = true;
                if (r.Motor != null) r.Motor.ResetState();
                float lift = power * (1f + i * 0.35f);
                var spread = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * dir;
                r.Knock(spread * lift * 0.9f + Vector3.up * (5f + lift * 0.5f), 0.4f + 0.1f * i);
            }
            if (b != null) RefreshBase(BaseOf(b));
        }

        void Topple(Potato basePotato, Vector3 dir)
        {
            Potato first;
            if (!above.TryGetValue(basePotato, out first)) return;
            int size = SizeOf(basePotato);
            Dismount(first, dir, 4f + size);
            basePotato.Knock(-dir * 3f, 0.5f);
            Fx.Impact(Root, basePotato.transform.position + Vector3.up * 1.5f, Color.white, dir, 0.8f);
            AudioManager.Play(Sfx.Crack, 0.9f);
            Feel.Shake(0.1f + 0.05f * size);
            GM.Hud.Toast("Wieża " + basePotato.Slot.Name + " runęła! (" + size + ")", basePotato.Color);
            towers.Remove(basePotato);
        }

        void RefreshBase(Potato b)
        {
            int n = SizeOf(b) - 1;
            b.SpeedMultiplier = Mathf.Max(0.4f, 1f - SlowPerLevel * n);
            b.CanJump = n == 0;
            if (n == 0) towers.Remove(b);
        }

        public override void OnBump(Potato hitter, Potato victim, bool dash)
        {
            Tower t;
            if (Riding(victim)) victim = BaseOf(victim);
            if (!towers.TryGetValue(victim, out t)) return;
            var d = victim.transform.position - hitter.transform.position; d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            d.Normalize();
            int levels = SizeOf(victim) - 1;
            float k = dash ? DashKick + DashKickPerLevel * levels : 0.4f;
            t.LeanVel += new Vector2(d.x, d.z) * k;
        }

        // ---------------- pętla ----------------

        public override void Tick(float dt)
        {
            // wieża spadająca z mapy rozpada się, zanim baza przeniesie podstawę na start
            foreach (var p in Players)
                if (p != null && above.ContainsKey(p) && !Riding(p) && p.transform.position.y < KillY + 2f) Topple(p, Vector3.up);

            if (Running && !Finished)
            {
                Mounting();
                Dismounting();
                Balance(dt);
                Scoring(dt);
            }
            base.Tick(dt);
        }

        void Mounting()
        {
            foreach (var p in Players)
            {
                if (p == null || p.Eliminated || Riding(p) || above.ContainsKey(p) || p.Grounded || p.IsStunned) continue;
                foreach (var c in Players)
                {
                    if (c == null || c == p || c.Eliminated || Riding(c)) continue;
                    if (FlatDist(p.transform.position, c.transform.position) > MountRadius) continue;
                    if (p.transform.position.y < c.transform.position.y + MountAbove) continue;
                    Mount(p, c);
                    break;
                }
            }
        }

        void Dismounting()
        {
            tmp.Clear();
            foreach (var kv in below)
            {
                var r = kv.Key;
                float t;
                if (r.Motor != null && r.Motor.JumpHeld && mountedAt.TryGetValue(r, out t) && Time.time - t > 0.35f) tmp.Add(r);
            }
            if (tmp.Count == 0) return;
            var jumpers = new List<Potato>(tmp);
            foreach (var r in jumpers)
            {
                if (!Riding(r)) continue; // już spadł razem z niższym
                var f = r.Facing; f.y = 0f;
                Dismount(r, f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward, 3f);
                AudioManager.Play(Sfx.Jump, 0.5f);
            }
        }

        void Balance(float dt)
        {
            tmp.Clear();
            foreach (var kv in towers) tmp.Add(kv.Key);
            float limit = ToppleDeg * Mathf.Deg2Rad;
            foreach (var b in tmp)
            {
                Tower t;
                if (b == null || !towers.TryGetValue(b, out t)) continue;
                int size = SizeOf(b);
                int levels = size - 1;
                if (levels <= 0) { towers.Remove(b); continue; }
                var v = Phys.GetVelocity(b.Body);
                var a = (v - t.LastVel) / Mathf.Max(dt, 1e-3f);
                t.LastVel = v;
                var a2 = Vector2.ClampMagnitude(new Vector2(a.x, a.z), 60f);
                // sterowanie: suma gałek jeźdźców (każdy pcha szczyt w stronę swojej gałki)
                Vector2 control = Vector2.zero;
                Potato r;
                var cur = b;
                while (above.TryGetValue(cur, out r)) { if (r.Motor != null) control += Vector2.ClampMagnitude(r.Motor.Move, 1f); cur = r; }
                float unstable = (Unstable + UnstablePerLevel * levels) * (b.IsStunned ? 2f : 1f);
                var noise = new Vector2(Mathf.PerlinNoise(Time.time * 0.9f, b.Slot.Index) - 0.5f, Mathf.PerlinNoise(b.Slot.Index, Time.time * 0.9f) - 0.5f) * 0.6f * levels;
                var accel = t.Lean * unstable - a2 * Coupling + control * Control - t.LeanVel * Damp + noise;
                t.LeanVel += accel * dt;
                t.Lean += t.LeanVel * dt;
                if (t.Lean.magnitude > limit)
                {
                    var d = new Vector3(t.Lean.x, 0f, t.Lean.y).normalized;
                    Topple(b, d);
                    continue;
                }
                Place(b, t);
            }
        }

        /// <summary>Ustawia jeźdźców nad podstawą wzdłuż przechylonej osi (przechył narasta z wysokością).</summary>
        void Place(Potato b, Tower t)
        {
            float ang = t.Lean.magnitude;
            var dir = ang > 1e-4f ? new Vector3(t.Lean.x, 0f, t.Lean.y) / ang : Vector3.zero;
            var pos = b.transform.position;
            int levels = SizeOf(b) - 1;
            int i = 0;
            Potato r;
            var cur = b;
            while (above.TryGetValue(cur, out r))
            {
                i++;
                float k = ang * (0.6f + 0.8f * i / Mathf.Max(1f, levels)); // dolne piętra prostsze, górne bardziej wygięte
                var up = new Vector3(dir.x * Mathf.Sin(k), Mathf.Cos(k), dir.z * Mathf.Sin(k));
                pos += up * Step;
                r.Body.position = pos;
                r.transform.position = pos;
                r.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
                cur = r;
            }
        }

        void Scoring(float dt)
        {
            foreach (var p in Players)
            {
                if (p == null || p.Eliminated || Riding(p) || !above.ContainsKey(p)) continue;
                int size = SizeOf(p);
                float rate = size >= 4 ? 4f : size == 3 ? 2f : 1f;
                if (OnTable(p.transform.position)) rate *= 2f;
                var cur = p;
                while (cur != null)
                {
                    acc[cur] += rate * dt;
                    int whole = Mathf.FloorToInt(acc[cur]);
                    if (whole > 0) { Score[cur] += whole; acc[cur] -= whole; }
                    Potato r;
                    cur = above.TryGetValue(cur, out r) ? r : null;
                }
            }
        }

        public override string StatFor(Potato p)
        {
            if (Riding(p)) return "piętro " + (SizeOf(BaseOf(p)) - SizeOf(p) + 1) + " · " + ScoreOf(p) + " pkt";
            if (above.ContainsKey(p)) return "podstawa (" + SizeOf(p) + ") · " + ScoreOf(p) + " pkt";
            return ScoreOf(p) + " pkt";
        }

        public override string HudInfo()
        {
            int best = 1; Potato who = null;
            foreach (var p in Players)
            {
                if (p == null || Riding(p) || !above.ContainsKey(p)) continue;
                int s = SizeOf(p);
                if (s > best) { best = s; who = p; }
            }
            return who == null ? "Wskakuj innym na głowy!" : "Najwyższa wieża: " + best + " (" + who.Slot.Name + (OnTable(who.transform.position) ? ", na stole ×2" : "") + ")";
        }

        public override void Cleanup()
        {
            foreach (var p in Players)
            {
                if (p == null) continue;
                if (p.Body != null) p.Body.isKinematic = false;
                var c = p.GetComponent<Collider>();
                if (c != null) c.enabled = true;
                p.transform.rotation = Quaternion.identity;
                p.SpeedMultiplier = 1f;
                p.CanJump = true;
            }
            below.Clear(); above.Clear(); towers.Clear();
        }

        // ---------------- boty ----------------

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            Tower t;
            if (Riding(p))
            {
                var b = BaseOf(p);
                if (!towers.TryGetValue(b, out t)) return BotCommand.Stay();
                // PD na przechył: gałka przeciwnie do przechyłu i jego prędkości (z lekkim błędem)
                var u = -(t.Lean * 2.4f + t.LeanVel * 0.9f) + Random.insideUnitCircle * 0.15f;
                u = Vector2.ClampMagnitude(u, 1f);
                bool bail = t.Lean.magnitude > ToppleDeg * Mathf.Deg2Rad * 0.85f && Random.value < 0.15f;
                return BotCommand.Go(pos + new Vector3(u.x, 0f, u.y) * 1.2f, false, bail);
            }
            if (above.ContainsKey(p))
            {
                // podstawa: na stół (×2), potem spokojnie w miejscu, uciekając przed zrywami
                var threat = Nearest(p, o => !Riding(o) && !above.ContainsKey(o));
                if (threat != null && FlatDist(threat.transform.position, pos) < 3f && threat.IsDashing)
                {
                    var away = pos - threat.transform.position; away.y = 0f;
                    return BotCommand.Go(pos + away.normalized * 3f);
                }
                if (!OnTable(pos)) return BotCommand.Go(NearestRampOrTable(pos));
                return BotCommand.Go(TableCenter + new Vector3(Mathf.Sin(Time.time * 0.3f + p.Slot.Index), 0f, Mathf.Cos(Time.time * 0.25f)) * 1.5f);
            }
            // wolny: dołącz do najlepszej wieży albo zwal cudzą
            Potato bestBase = null; float bestScore = -1e9f;
            foreach (var c in Players)
            {
                if (c == null || c == p || c.Eliminated || Riding(c)) continue;
                float s = SizeOf(c) * 4f - FlatDist(c.transform.position, pos) - Mathf.Abs(c.transform.position.y - pos.y) * 3f;
                if (s > bestScore) { bestScore = s; bestBase = c; }
            }
            if (bestBase == null) return BotCommand.Go(Wander(p, 12f));
            float d = FlatDist(bestBase.transform.position, pos);
            bool wrecker = (p.Slot.Index + Mathf.FloorToInt(Time.time / 12f)) % 3 == 0 && SizeOf(bestBase) >= 3;
            if (wrecker) return BotCommand.Go(bestBase.transform.position, d < 3.2f && p.DashCooldownLeft <= 0f);
            bool jump = d < 1.25f && p.Grounded && Mathf.Abs(bestBase.transform.position.y - pos.y) < 0.6f;
            return BotCommand.Go(bestBase.transform.position, false, jump);
        }

        Vector3 NearestRampOrTable(Vector3 pos)
        {
            if (pos.y > TableCenter.y - 0.5f) return TableCenter;
            var w = new Vector3(TableCenter.x - L.PitHalf - 3f, 0f, TableCenter.z);
            var e = new Vector3(TableCenter.x + L.PitHalf + 3f, 0f, TableCenter.z);
            var foot = FlatDist(pos, w) < FlatDist(pos, e) ? w : e;
            // przy stopie rampy: idź po rampie na blat
            return FlatDist(pos, foot) < 1.5f || Mathf.Abs(pos.z - TableCenter.z) < 1f && pos.y > 0.3f ? TableCenter : foot;
        }
    }
}
