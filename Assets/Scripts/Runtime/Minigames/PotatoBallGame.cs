using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Ziemniaczana Piłka: dwie drużyny, jedna wielka piłka. Wbij ją do bramki rywali.
    /// Pchaj piłkę ciałem albo kopnij zrywem. Wygrywa drużyna z większą liczbą goli.
    /// </summary>
    public class PotatoBallGame : Minigame
    {
        public override string Title { get { return "Ziemniaczana Piłka"; } }
        public override string Description { get { return "Czerwoni kontra Niebiescy! Pchaj piłkę albo kopnij ją zrywem. Na boisku stoją sprężynujące grzybki. W ostatnich 15 sekundach każdy gol liczy się podwójnie!"; } }
        public override string Tip { get { return "Zryw tuż przy piłce to mocny strzał. Ustaw się za piłką, twarzą do bramki."; } }
        public override string Goal { get { return "Więcej goli w 60 sekund wygrywa!"; } }
        public override MapId PreferredMap { get { return MapId.Stadium; } }
        public override float TimeLimit { get { return 60f; } }
        public override float ArenaRadius { get { return 12f; } }
        public override bool FallEliminates { get { return false; } }
        public override bool EndsWhenOneLeft { get { return false; } }
        public override float CameraPull { get { return 0.25f; } }
        public override string Controls { get { return "Ruch · skok · akcja = mocne kopnięcie piłki"; } }

        const float HalfX = 13f, HalfZ = 8f, GoalHalf = 2.6f;
        static readonly Color[] TeamColors = { Shapes.Hex("#E2463F"), Shapes.Hex("#3F7FE8") };
        static readonly string[] TeamNames = { "Czerwoni", "Niebiescy" };

        readonly Dictionary<Potato, int> team = new Dictionary<Potato, int>();
        readonly int[] goals = new int[2];
        Rigidbody ball;
        float resetTimer = -1f;
        bool doubleGoals;

        public override void BuildArena()
        {
            Shapes.Box(Root, new Vector3(0, -0.5f, 0), new Vector3(HalfX * 2f + 6f, 1f, HalfZ * 2f), Map.Floor, true);
            for (int i = 0; i < 8; i++)
                Shapes.Box(Root, new Vector3(-HalfX + 1.625f + i * 3.25f, 0.004f, 0), new Vector3(3.25f, 0.01f, HalfZ * 2f), i % 2 == 0 ? Map.FloorAlt : Map.Floor, false);
            // linie boiska
            Shapes.Box(Root, new Vector3(0, 0.01f, 0), new Vector3(0.15f, 0.01f, HalfZ * 2f), Color.white, false);
            var circle = MapBuilder.Disc(Root, new Vector3(0, 0.008f, 0), 2.5f, 0.01f, Color.white, false);
            MapBuilder.Disc(circle.transform.parent, new Vector3(0, 0.012f, 0), 2.35f, 0.01f, Map.Floor, false);

            // bandy wzdłuż boiska
            for (int s = -1; s <= 1; s += 2)
                Shapes.Box(Root, new Vector3(0, 0.75f, (HalfZ + 0.3f) * s), new Vector3(HalfX * 2f + 6f, 1.5f, 0.6f), Map.Wall, true);
            // ściany za liniami końcowymi, z otworem na bramkę
            for (int side = -1; side <= 1; side += 2)
            {
                float x = HalfX * side;
                float segLen = HalfZ - GoalHalf;
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Box(Root, new Vector3(x, 0.75f, (GoalHalf + segLen * 0.5f) * s), new Vector3(0.6f, 1.5f, segLen), Map.Wall, true);
                // bramka
                Color c = TeamColors[side < 0 ? 0 : 1];
                Shapes.Box(Root, new Vector3(x + 2.6f * side, 1.2f, 0), new Vector3(0.3f, 2.4f, GoalHalf * 2f + 0.6f), Color.white, true);
                for (int s = -1; s <= 1; s += 2)
                {
                    Shapes.Box(Root, new Vector3(x + 1.3f * side, 1.2f, (GoalHalf + 0.15f) * s), new Vector3(2.6f, 2.4f, 0.3f), c, true);
                }
                Shapes.Box(Root, new Vector3(x + 1.3f * side, 2.5f, 0), new Vector3(2.9f, 0.25f, GoalHalf * 2f + 0.6f), c, true);
            }

            // sprężynujące grzybki: odbijają piłkę i graczy
            for (int s = -1; s <= 1; s += 2)
            {
                var pos = new Vector3(0, 0.6f, 4.3f * s);
                var stem = MapBuilder.Disc(Root, pos, 0.7f, 1.2f, Color.white);
                stem.name = "Grzybek";
                var cap = Shapes.Prim(PrimitiveType.Sphere, Root, pos + Vector3.up * 0.7f, new Vector3(2f, 0.9f, 2f), Shapes.Hex("#E2463F"), false, 0.6f);
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI * 0.5f;
                    Shapes.Prim(PrimitiveType.Sphere, cap.transform, new Vector3(Mathf.Cos(a) * 0.3f, 0.35f, Mathf.Sin(a) * 0.3f), new Vector3(0.18f, 0.25f, 0.18f), Color.white);
                }
                var zone = Shapes.Prim(PrimitiveType.Cylinder, Root, pos, new Vector3(1.9f, 0.7f, 1.9f), Color.white, true);
                zone.GetComponent<Renderer>().enabled = false;
                Hazard.Add(zone, (p, h) =>
                {
                    var d = p.transform.position - h.transform.position;
                    d.y = 0;
                    p.Knock((d.sqrMagnitude > 0.01f ? d.normalized : Vector3.right) * 11f + Vector3.up * 5f, 0.5f);
                    AudioManager.Play(Sfx.Jump, 1f, 0.2f);
                });
            }

            // piłka
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.name = "Pilka";
            b.transform.SetParent(Root, false);
            b.transform.localScale = Vector3.one * 1.6f;
            b.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Color.white, 0.5f);
            for (int k = 0; k < 6; k++)
            {
                var patch = Shapes.Prim(PrimitiveType.Sphere, b.transform, Random.onUnitSphere * 0.42f, Vector3.one * 0.28f, Shapes.Hex("#222222"));
                patch.name = "Lata";
            }
            var col = b.GetComponent<SphereCollider>();
#if UNITY_6000_0_OR_NEWER
            var mat = new PhysicsMaterial("Pilka") { bounciness = 0.6f, dynamicFriction = 0.3f, staticFriction = 0.3f, bounceCombine = PhysicsMaterialCombine.Maximum };
#else
            var mat = new PhysicMaterial("Pilka") { bounciness = 0.6f, dynamicFriction = 0.3f, staticFriction = 0.3f, bounceCombine = PhysicMaterialCombine.Maximum };
#endif
            col.sharedMaterial = mat;
            ball = b.AddComponent<Rigidbody>();
            ball.mass = 0.6f;
            Phys.SetDamping(ball, 0.35f);
            ball.interpolation = RigidbodyInterpolation.Interpolate;
            ball.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            b.AddComponent<BallKicker>().Game = this;
            ResetBall();
        }

        void ResetBall()
        {
            ball.position = new Vector3(0, 2.5f, 0);
            ball.transform.position = ball.position;
            Phys.SetVelocity(ball, Vector3.zero);
            ball.angularVelocity = Vector3.zero;
        }

        protected override void ConfigurePlayer(Potato p)
        {
            int i = Players.IndexOf(p);
            int t = i % 2;
            team[p] = t;
            // kolorowy krążek drużyny pod nogami
            var ring = MapBuilder.Disc(p.transform, new Vector3(0, -Potato.HalfHeight + 0.03f, 0), 0.75f, 0.03f, TeamColors[t], false);
            ring.name = "Druzyna";
        }

        public override Vector3 SpawnPoint(int i, int n)
        {
            int t = i % 2;
            int inTeam = i / 2;
            int teamSize = (n + 1 - t) / 2;
            float z = (inTeam - (teamSize - 1) * 0.5f) * 3f;
            float x = (t == 0 ? -1f : 1f) * (5f + (inTeam % 2) * 2f);
            return new Vector3(x, Potato.HalfHeight + 0.1f, z);
        }

        void ResetPlayers()
        {
            int n = Players.Count;
            for (int i = 0; i < n; i++)
            {
                var p = Players[i];
                if (p == null) continue;
                p.Teleport(SpawnPoint(i, n));
                p.FaceTowards(new Vector3(team[p] == 0 ? 1 : -1, 0, 0));
            }
        }

        public void Kick(Potato p, Vector3 point)
        {
            if (!Running) return;
            var dir = ball.position - p.transform.position;
            dir.y = 0;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : p.Facing;
            float power = p.IsDashing ? 15f : 4f;
            var v = Phys.GetVelocity(ball);
            v += dir * power + Vector3.up * (p.IsDashing ? 3.5f : 0.5f);
            Phys.SetVelocity(ball, Vector3.ClampMagnitude(v, 22f));
            if (p.IsDashing) AudioManager.Play(Sfx.Bump, 0.9f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished || !Running) return;
            if (resetTimer >= 0f)
            {
                resetTimer -= dt;
                if (resetTimer < 0f)
                {
                    ResetBall();
                    ResetPlayers();
                }
                return;
            }
            var bp = ball.position;
            if (!doubleGoals && TimeLeft < 15f)
            {
                doubleGoals = true;
                GM.Hud.Toast("ZŁOTE GOLE! Teraz każdy gol liczy się podwójnie!", Shapes.Hex("#FFC61A"));
                AudioManager.Play(Sfx.Fanfare, 0.7f);
                ball.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FFC61A"), 0.9f, 0.4f);
            }
            if (Mathf.Abs(bp.x) > HalfX + 0.9f && Mathf.Abs(bp.z) < GoalHalf + 0.3f)
            {
                int scorer = bp.x > 0 ? 0 : 1; // czerwoni atakują w prawo (+x)
                goals[scorer] += doubleGoals ? 2 : 1;
                foreach (var kv in team) Score[kv.Key] = goals[kv.Value];
                AudioManager.Play(Sfx.Fanfare, 1f, 0f);
                GM.Camera.Shake(0.3f);
                Effects.Confetti(Root, new Vector3(Mathf.Sign(bp.x) * (HalfX + 1.3f), 0.5f, 0), 160, false, 2f);
                GM.Hud.Toast("GOL! " + TeamNames[scorer] + " " + goals[0] + " : " + goals[1], TeamColors[scorer]);
                resetTimer = 1.5f;
            }
            else if (bp.y < KillY || Mathf.Abs(bp.z) > HalfZ + 2f || Mathf.Abs(bp.x) > HalfX + 4f)
            {
                ResetBall();
            }
        }

        public override List<List<Potato>> Ranking()
        {
            var a = new List<Potato>();
            var b = new List<Potato>();
            foreach (var kv in team)
            {
                if (kv.Key == null) continue;
                if (kv.Value == 0) a.Add(kv.Key); else b.Add(kv.Key);
            }
            var result = new List<List<Potato>>();
            if (goals[0] == goals[1]) { a.AddRange(b); result.Add(a); }
            else if (goals[0] > goals[1]) { result.Add(a); result.Add(b); }
            else { result.Add(b); result.Add(a); }
            return result;
        }

        public override IEnumerable<Transform> ExtraCameraTargets() { yield return ball.transform; }

        public override string HudInfo()
        {
            return TeamNames[0] + " " + goals[0] + " : " + goals[1] + " " + TeamNames[1] + "   ·   " + base.HudInfo();
        }

        public override string StatFor(Potato p)
        {
            int t;
            return team.TryGetValue(p, out t) ? TeamNames[t] : "";
        }

        public override BotCommand Bot(Potato p)
        {
            int t;
            if (!team.TryGetValue(p, out t)) return BotCommand.Stay();
            float attackDir = t == 0 ? 1f : -1f;
            var goal = new Vector3(HalfX * attackDir + attackDir, 0, 0);
            var own = new Vector3(-HalfX * attackDir, 0, 0);
            var bp = ball.position;
            bp.y = 0;
            var pos = p.transform.position;

            // Co drugi bot w drużynie broni bramki
            int idx = 0;
            foreach (var kv in team) { if (kv.Key == p) break; if (kv.Value == t) idx++; }
            bool defender = idx % 2 == 1 && Players.Count > 3;
            if (defender && Mathf.Sign(bp.x) != attackDir)
                return BotCommand.Go(new Vector3(own.x + attackDir * 2.5f, 0, Mathf.Clamp(bp.z, -GoalHalf, GoalHalf)));

            var toGoal = (goal - bp).normalized;
            var behind = bp - toGoal * 1.6f;
            float dBehind = FlatDist(pos, behind);
            if (dBehind > 1.2f && Vector3.Dot(pos - bp, toGoal) > -0.3f)
            {
                // obejdź piłkę bokiem, żeby nie pchać jej do własnej bramki
                var side = Vector3.Cross(Vector3.up, toGoal) * (pos.z > bp.z ? 1f : -1f) * 2f;
                return BotCommand.Go(behind + side);
            }
            bool kick = FlatDist(pos, bp) < 2.2f && p.DashCooldownLeft <= 0f && Random.value < 0.4f;
            return BotCommand.Go(bp + toGoal, kick);
        }
    }

    public class BallKicker : MonoBehaviour
    {
        public PotatoBallGame Game;

        void OnCollisionEnter(Collision c)
        {
            var p = c.collider.GetComponent<Potato>();
            if (p != null && Game != null) Game.Kick(p, c.GetContact(0).point);
        }
    }
}
