using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Tor Przeszkód: długi wyścig przez kuchnię, sześć odcinków.
    /// 1) Ściany z drzwiami: część drzwi jest fałszywa (zagadka!), 2) kruszące się kamienie nad przepaścią,
    /// 3) kręcące się wałki, 4) wąska kładka z wahadłami, 5) taśmociąg pod prąd z tarkami,
    /// 6) podjazd, z którego toczą się kule. Kto spadnie, wraca do ostatniego punktu kontrolnego.
    /// </summary>
    public class ObstacleRaceGame : RaceGame
    {
        public override string Title { get { return "Tor Przeszkód"; } }
        public override string Description { get { return "Zgadnij, które drzwi są prawdziwe, przeskocz kruszące się kamienie, przejdź kładkę z wahadłami, pokonaj taśmociąg i wbiegnij pod toczące się kule!"; } }
        public override string Tip { get { return "Prawdziwe drzwi wyważysz zrywem. Fałszywe cię odbiją, więc patrz, przez które przeszli inni."; } }
        public override string Goal { get { return "Pierwszy na mecie wygrywa!"; } }
        public override MapId PreferredMap { get { return MapId.Kitchen; } }
        public override float TimeLimit { get { return 150f; } }
        public override float ArenaRadius { get { return 52f; } }
        public override float KillY { get { return -4f; } }
        public override string Controls { get { return "Ruch · SKOK nad przepaściami · akcja = zryw (wyważ drzwi!)"; } }
        protected override float Grace { get { return 20f; } }

        const float HW = 4.5f;               // połowa szerokości toru
        const float StartZ = -42f, FinishZ = 57f;
        const float TopY = 2.5f;              // wysokość mety (na górze podjazdu)
        static readonly float[] Checkpoints = { -40f, -4f, 10f, 26f, 38f };
        static readonly float[] DoorRowsZ = { -33f, -27f };
        static readonly float[] SweeperZ = { 0f, 6.5f };
        static readonly float[] HammerZ = { 13.5f, 18f, 22.5f };
        const float BeltZ0 = 28f, BeltZ1 = 36f;
        const float RampZ0 = 38f, RampZ1 = 52f;

        readonly Dictionary<Potato, float> best = new Dictionary<Potato, float>();
        readonly List<FakeDoor>[] doorRows = { new List<FakeDoor>(), new List<FakeDoor>() };
        readonly Dictionary<Potato, int>[] doorChoice = { new Dictionary<Potato, int>(), new Dictionary<Potato, int>() };
        readonly List<CrumbleStone> stones = new List<CrumbleStone>();
        readonly List<Pendulum> hammers = new List<Pendulum>();
        readonly List<Transform> beltStripes = new List<Transform>();
        readonly List<Rigidbody> balls = new List<Rigidbody>();
        readonly HashSet<Collider> beltColliders = new HashSet<Collider>();
        float ballTimer = 2f;

        public override void BuildArena()
        {
            // ---- 1. Start i ściany z drzwiami ----
            Segment(StartZ, -24f, Map.Floor);
            for (int r = 0; r < DoorRowsZ.Length; r++) DoorRow(r, DoorRowsZ[r], r == 0 ? 3 : 2);

            // ---- 2. Kruszące się kamienie ----
            float[] stoneZ = { -21.5f, -18f, -14.5f, -11f, -7.5f };
            for (int i = 0; i < stoneZ.Length; i++)
            {
                for (int lane = -1; lane <= 1; lane++)
                {
                    // co drugi rząd kamienie są przesunięte, żeby trzeba było skakać na ukos
                    float x = lane * 2.8f + (i % 2 == 0 ? 0f : 1.2f);
                    if (Mathf.Abs(x) > HW - 0.8f) continue;
                    var stone = MapBuilder.Disc(Root, new Vector3(x, -0.5f, stoneZ[i]), 1.05f, 1f, i % 2 == 0 ? Map.FloorAlt : Map.Accent);
                    stone.name = "Kamien";
                    stones.Add(stone.AddComponent<CrumbleStone>());
                }
            }

            // ---- 3. Wałki do ciasta ----
            Segment(-6f, 10f, Map.FloorAlt);
            Checkpoint(-4f);
            for (int i = 0; i < SweeperZ.Length; i++)
            {
                var pivot = Shapes.Empty("Walek", Root, new Vector3(0, 0, SweeperZ[i]));
                var rotor = pivot.AddComponent<Rotor>();
                rotor.Speed = i % 2 == 0 ? 120f : -150f;
                Shapes.Prim(PrimitiveType.Cylinder, pivot.transform, new Vector3(0, 0.6f, 0), new Vector3(0.6f, 0.6f, 0.6f), Map.Wall);
                var pin = Shapes.Prim(PrimitiveType.Cylinder, pivot.transform, new Vector3(0, 0.45f, 0), new Vector3(0.55f, HW - 0.1f, 0.55f), Shapes.Hex("#E8C49A"), true);
                pin.transform.localRotation = Quaternion.Euler(0, 0, 90);
                Hazard.Add(pin, (p, h) => KnockAway(p, h.transform.position, 12f));
            }

            // ---- 4. Wąska kładka z wahadłami (bez barierek!) ----
            Shapes.Box(Root, new Vector3(0, -0.4f, 18f), new Vector3(1.9f, 0.8f, 16f), Map.Wall, true);
            Checkpoint(10f);
            for (int i = 0; i < HammerZ.Length; i++)
            {
                var frame = Shapes.Empty("Rama", Root, new Vector3(0, 0, HammerZ[i])).transform;
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Box(frame, new Vector3(3.2f * s, 3f, 0), new Vector3(0.4f, 6f, 0.4f), Map.Accent, false);
                Shapes.Box(frame, new Vector3(0, 6.1f, 0), new Vector3(6.8f, 0.4f, 0.4f), Map.Accent, false);
                var pivot = Shapes.Empty("Wahadlo", frame, new Vector3(0, 5.9f, 0));
                var pend = pivot.AddComponent<Pendulum>();
                pend.Amplitude = 58f;
                pend.Speed = 1.7f + i * 0.25f;
                pend.Phase = i * 2.1f;
                hammers.Add(pend);
                Shapes.Prim(PrimitiveType.Cylinder, pivot.transform, new Vector3(0, -2.2f, 0), new Vector3(0.15f, 2.2f, 0.15f), Shapes.Hex("#777777"));
                var head = Shapes.Box(pivot.transform, new Vector3(0, -4.9f, 0), new Vector3(1.7f, 1.1f, 1.1f), Shapes.Hex("#E2463F"), true);
                head.name = "Mlot";
                var pp = pend;
                Hazard.Add(head, (p, h) =>
                {
                    p.Knock(new Vector3(pp.SwingDir * 13f, 6f, 0), 0.9f);
                    AudioManager.Play(Sfx.Bump, 1f);
                    GM.Camera.Shake(0.2f);
                });
            }

            // ---- 5. Taśmociąg pod prąd + tarki ----
            Segment(26f, RampZ0, Map.Floor);
            Checkpoint(26f);
            var belt = Shapes.Box(Root, new Vector3(0, 0.02f, (BeltZ0 + BeltZ1) * 0.5f), new Vector3(HW * 2f - 0.4f, 0.1f, BeltZ1 - BeltZ0), Shapes.Hex("#3B3B46"), true);
            belt.name = "Tasmociag";
            beltColliders.Add(belt.GetComponent<Collider>());
            for (int i = 0; i < 8; i++)
            {
                var stripe = Shapes.Box(Root, new Vector3(0, 0.08f, BeltZ0 + i), new Vector3(HW * 2f - 0.6f, 0.02f, 0.25f), Shapes.Hex("#FFD23F"), false);
                beltStripes.Add(stripe.transform);
            }
            for (int i = 0; i < 3; i++)
            {
                float z = BeltZ0 + 1.5f + i * 2.6f;
                var grater = Shapes.Box(Root, new Vector3(0, 0.85f, z), new Vector3(1.4f, 1.6f, 0.5f), Shapes.Hex("#B5B5B5"), true);
                grater.name = "Tarka";
                var sl = grater.AddComponent<Slider>();
                sl.Distance = HW - 0.9f;
                sl.Speed = 1.6f + i * 0.4f;
                sl.Phase = i * 1.9f;
                Hazard.Add(grater, (p, h) => KnockAway(p, h.transform.position, 10f));
            }

            // ---- 6. Podjazd z toczącymi się kulami ----
            float rampLen = RampZ1 - RampZ0;
            float angle = Mathf.Atan2(TopY, rampLen) * Mathf.Rad2Deg;
            float slope = Mathf.Sqrt(rampLen * rampLen + TopY * TopY);
            var ramp = Shapes.Box(Root, new Vector3(0, TopY * 0.5f - 0.5f, (RampZ0 + RampZ1) * 0.5f), new Vector3(HW * 2f, 1f, slope + 0.2f), Map.FloorAlt, true);
            ramp.transform.localRotation = Quaternion.Euler(-angle, 0, 0);
            for (int s = -1; s <= 1; s += 2)
            {
                var rail = Shapes.Box(Root, new Vector3((HW + 0.2f) * s, TopY * 0.5f + 0.4f, (RampZ0 + RampZ1) * 0.5f), new Vector3(0.4f, 1f, slope), Map.Accent, true);
                rail.transform.localRotation = Quaternion.Euler(-angle, 0, 0);
            }
            Checkpoint(38f);
            // górna platforma z metą
            Shapes.Box(Root, new Vector3(0, TopY - 0.5f, (RampZ1 + FinishZ + 4f) * 0.5f), new Vector3(HW * 2f, 1f, FinishZ + 4f - RampZ1), Map.Floor, true);
            for (int s = -1; s <= 1; s += 2)
                Shapes.Box(Root, new Vector3((HW + 0.2f) * s, TopY + 0.4f, (RampZ1 + FinishZ + 4f) * 0.5f), new Vector3(0.4f, 1f, FinishZ + 4f - RampZ1), Map.Accent, true);
            Shapes.Box(Root, new Vector3(0, TopY + 0.6f, FinishZ + 4.2f), new Vector3(HW * 2f + 0.8f, 1.2f, 0.4f), Map.Accent, true);
            // rura, z której wypadają kule
            var pipe = Shapes.Prim(PrimitiveType.Cylinder, Root, new Vector3(0, TopY + 3.2f, RampZ1 + 1.5f), new Vector3(2.4f, 0.8f, 2.4f), Map.Wall);
            pipe.transform.localRotation = Quaternion.Euler(90, 0, 0);

            // Linia mety (szachownica) i brama
            for (int i = 0; i < 9; i++)
                Shapes.Box(Root, new Vector3(-HW + 0.5f + i, TopY + 0.01f, FinishZ), new Vector3(1f, 0.02f, 0.8f), i % 2 == 0 ? Color.white : Color.black, false);
            var arch = Shapes.Empty("Meta", Root, new Vector3(0, TopY, FinishZ)).transform;
            for (int s = -1; s <= 1; s += 2) Shapes.Box(arch, new Vector3((HW + 0.4f) * s, 2f, 0), new Vector3(0.4f, 4f, 0.4f), Map.Accent, false);
            Shapes.Box(arch, new Vector3(0, 4.2f, 0), new Vector3(HW * 2f + 1.2f, 0.6f, 0.4f), Map.Accent, false);
        }

        void Segment(float z0, float z1, Color c)
        {
            float len = z1 - z0;
            Shapes.Box(Root, new Vector3(0, -0.5f, (z0 + z1) * 0.5f), new Vector3(HW * 2f, 1f, len), c, true);
            for (int s = -1; s <= 1; s += 2)
                Shapes.Box(Root, new Vector3((HW + 0.15f) * s, 0.3f, (z0 + z1) * 0.5f), new Vector3(0.3f, 0.6f, len), Map.Accent, true);
        }

        void Checkpoint(float z)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                var flag = Shapes.Empty("Punkt kontrolny", Root, new Vector3((HW + 0.6f) * s, 0, z)).transform;
                Shapes.Prim(PrimitiveType.Cylinder, flag, new Vector3(0, 1.2f, 0), new Vector3(0.12f, 1.2f, 0.12f), Color.white);
                Shapes.Box(flag, new Vector3(-0.45f * s, 2f, 0), new Vector3(0.8f, 0.5f, 0.05f), Shapes.Hex("#5CC157"), false);
            }
        }

        void DoorRow(int row, float z, int realCount)
        {
            const int n = 5;
            float w = HW * 2f / n;
            var real = new List<int>();
            while (real.Count < realCount)
            {
                int k = Random.Range(0, n);
                if (!real.Contains(k)) real.Add(k);
            }
            for (int i = 0; i < n; i++)
            {
                float x = -HW + w * (i + 0.5f);
                var door = Shapes.Box(Root, new Vector3(x, 1.3f, z), new Vector3(w - 0.12f, 2.6f, 0.4f), i % 2 == 0 ? Map.Accent : Shapes.Hex("#8E6CEF"), true);
                door.name = "Drzwi";
                Shapes.Prim(PrimitiveType.Sphere, door.transform, new Vector3(0.3f, 0, -0.6f), new Vector3(0.12f, 0.06f, 0.4f), Shapes.Hex("#FFD23F"));
                var fd = door.AddComponent<FakeDoor>();
                fd.Real = real.Contains(i);
                doorRows[row].Add(fd);
            }
            // belka nad drzwiami
            Shapes.Box(Root, new Vector3(0, 2.85f, z), new Vector3(HW * 2f + 0.6f, 0.5f, 0.6f), Map.Wall, true);
        }

        void KnockAway(Potato p, Vector3 from, float power)
        {
            var d = p.transform.position - from;
            d.y = 0;
            if (d.sqrMagnitude < 0.01f) d = Vector3.right;
            p.Knock(d.normalized * power + Vector3.up * 5f, 0.7f);
            AudioManager.Play(Sfx.Bump, 0.7f);
        }

        public override Vector3 SpawnPoint(int i, int n)
        {
            float x = (i - (n - 1) * 0.5f) * 1.3f;
            return new Vector3(x, Potato.HalfHeight + 0.1f, StartZ + 2f);
        }

        protected override Vector3 RespawnPoint(Potato p)
        {
            float b;
            best.TryGetValue(p, out b);
            float cp = Checkpoints[0];
            foreach (var c in Checkpoints) if (b >= c) cp = c;
            return new Vector3(Random.Range(-0.6f, 0.6f), 3f, cp + 1f);
        }

        protected override float Progress(Potato p)
        {
            float z = p.transform.position.z;
            float b;
            if (!best.TryGetValue(p, out b) || z > b) best[p] = b = z;
            return b - StartZ;
        }

        protected override bool CrossedFinish(Potato p) { return p.transform.position.z > FinishZ && p.transform.position.y > TopY - 0.5f; }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished || !Running) return;

            // kamienie kruszą się pod stopami, taśmociąg pcha do tyłu
            foreach (var p in Players)
            {
                if (p == null) continue;
                p.Push = Vector3.zero;
                if (p.Eliminated || !p.Grounded || p.GroundCollider == null) continue;
                var st = p.GroundCollider.GetComponent<CrumbleStone>();
                if (st != null) st.Step();
                if (beltColliders.Contains(p.GroundCollider)) p.Push = new Vector3(0, 0, -5.5f);
            }
            foreach (var s in beltStripes)
            {
                var pos = s.localPosition;
                pos.z -= dt * 5.5f;
                if (pos.z < BeltZ0) pos.z += BeltZ1 - BeltZ0;
                s.localPosition = pos;
            }

            // kule
            ballTimer -= dt;
            if (ballTimer <= 0f)
            {
                ballTimer = Random.Range(0.9f, 1.6f);
                SpawnBall();
            }
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var b = balls[i];
                if (b == null) { balls.RemoveAt(i); continue; }
                if (b.position.y < -6f || b.position.z < RampZ0 - 8f)
                {
                    Object.Destroy(b.gameObject);
                    balls.RemoveAt(i);
                }
            }
        }

        void SpawnBall()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Kula";
            go.transform.SetParent(Root, false);
            float size = Random.Range(1.4f, 2.1f);
            go.transform.localScale = Vector3.one * size;
            go.transform.position = new Vector3(Random.Range(-HW + 1.2f, HW - 1.2f), TopY + 2.5f, RampZ1 + 1f);
            Color[] colors = { Shapes.Hex("#FF6F91"), Shapes.Hex("#2EC4B6"), Shapes.Hex("#FFD23F"), Shapes.Hex("#8E6CEF") };
            go.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(colors[Random.Range(0, colors.Length)], 0.7f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            Phys.SetVelocity(rb, new Vector3(Random.Range(-1.5f, 1.5f), 0, -6f));
            go.AddComponent<BonkBall>();
            balls.Add(rb);
        }

        public override string StatFor(Potato p)
        {
            var s = base.StatFor(p);
            return s != "" ? s : ScoreOf(p) + " m";
        }

        protected override void OnFinish()
        {
            foreach (var p in Players) if (p != null) p.Push = Vector3.zero;
        }

        // ---------------- Boty ----------------

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            float z = pos.z;

            // Drzwi: wybierz drzwi (otwarte > niesprawdzone), zapamiętaj fałszywe
            for (int r = 0; r < DoorRowsZ.Length; r++)
            {
                float dz = DoorRowsZ[r];
                if (z > dz - 4f && z < dz + 0.6f)
                {
                    int pick = PickDoor(p, r);
                    var door = doorRows[r][pick];
                    var dp = door.transform.position;
                    bool atDoor = z > dz - 1.6f;
                    if (atDoor && !door.Opened && door.KnownFake) { doorChoice[r].Remove(p); pick = PickDoor(p, r); dp = doorRows[r][pick].transform.position; }
                    bool dash = !door.Opened && Mathf.Abs(pos.x - dp.x) < 0.5f && z > dz - 2.5f && p.DashCooldownLeft <= 0f;
                    return BotCommand.Go(new Vector3(dp.x, 0, dz + 2f), dash);
                }
            }

            // Kamienie: skacz na najbliższy kamień przed sobą
            if (z > -25f && z < -6.5f)
            {
                CrumbleStone target = null;
                float bd = float.MaxValue;
                foreach (var s in stones)
                {
                    var sp = s.transform.position;
                    if (sp.z < z + 1.6f || sp.z > z + 4.5f || !s.Solid) continue;
                    float d = FlatDist(pos, sp) + Mathf.Abs(sp.x - pos.x) * 0.3f;
                    if (d < bd) { bd = d; target = s; }
                }
                Vector3 goal = target != null ? target.transform.position : new Vector3(pos.x, 0, z + 3.5f);
                if (z > -10f) goal = new Vector3(Mathf.Clamp(pos.x, -2f, 2f), 0, -4f);
                bool jump = p.Grounded && (GapAhead(p, goal) || p.GroundCollider != null && p.GroundCollider.GetComponent<CrumbleStone>() != null && FlatDist(pos, goal) < 3.8f);
                return BotCommand.Go(goal, false, jump);
            }

            // Wałki
            if (z >= -6.5f && z < 9.5f)
            {
                var goal = new Vector3(0, 0, z + 4f);
                bool jump = p.Grounded && NearSweeper(pos) && Random.value < 0.4f;
                return BotCommand.Go(goal, false, jump);
            }

            // Kładka: czekaj, aż młot odleci od środka
            if (z >= 9.5f && z < 26f)
            {
                foreach (var h in hammers)
                {
                    float hz = h.transform.position.z;
                    if (hz > z && hz - z < 2.2f && Mathf.Abs(h.Angle) < 30f && p.Slot.Index % 4 != 3)
                        return BotCommand.Go(new Vector3(0, 0, z - 0.3f));
                }
                return BotCommand.Go(new Vector3(0, 0, z + 3f));
            }

            // Taśmociąg i tarki
            if (z >= 26f && z < RampZ0)
            {
                float tx = Mathf.Sin(Time.time * 0.8f + p.Slot.Index) * 2.6f;
                bool dash = z > BeltZ0 && z < BeltZ1 && p.DashCooldownLeft <= 0f && Random.value < 0.25f;
                return BotCommand.Go(new Vector3(tx, 0, z + 4f), dash);
            }

            // Podjazd: uciekaj w bok przed kulą
            float x = pos.x;
            foreach (var b in balls)
            {
                if (b == null) continue;
                var bp = b.position;
                if (bp.z > z && bp.z - z < 6f && Mathf.Abs(bp.x - x) < 1.8f)
                {
                    x = bp.x > x ? Mathf.Max(-HW + 0.8f, bp.x - 2.6f) : Mathf.Min(HW - 0.8f, bp.x + 2.6f);
                    break;
                }
            }
            return BotCommand.Go(new Vector3(x, 0, z + 4f), false, p.Grounded && Random.value < 0.03f);
        }

        int PickDoor(Potato p, int row)
        {
            var doors = doorRows[row];
            for (int i = 0; i < doors.Count; i++) if (doors[i].Opened) return i;
            int c;
            if (doorChoice[row].TryGetValue(p, out c) && !doors[c].KnownFake) return c;
            var options = new List<int>();
            for (int i = 0; i < doors.Count; i++) if (!doors[i].KnownFake) options.Add(i);
            c = options.Count > 0 ? options[Random.Range(0, options.Count)] : Random.Range(0, doors.Count);
            doorChoice[row][p] = c;
            return c;
        }

        static bool NearSweeper(Vector3 pos)
        {
            foreach (var z in SweeperZ) if (Mathf.Abs(pos.z - z) < 4.6f) return true;
            return false;
        }

        public override void Cleanup()
        {
            foreach (var p in Players) if (p != null) p.Push = Vector3.zero;
            balls.Clear();
        }
    }
}
