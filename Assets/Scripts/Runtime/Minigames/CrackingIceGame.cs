using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Pękający Lód: plansza z płyt, które pękają i wpadają do wody chwilę po tym,
    /// jak ktoś na nie wejdzie. Nie stój w miejscu! Ostatni na lodzie wygrywa.
    /// </summary>
    public class CrackingIceGame : Minigame
    {
        public override string Title { get { return "Pękający Lód"; } }
        public override string Description { get { return "Każda płyta, na którą wejdziesz, pęka i wpada do wody. Ale kilka płyt to twardy lód, który nigdy nie pęka. Znajdź je (lekko błyszczą) i broń ich przed innymi!"; } }
        public override string Tip { get { return "Kilka płyt lekko błyszczy. To twardy lód, który nigdy nie pęka."; } }
        public override MapId PreferredMap { get { return MapId.Ice; } }
        public override float TimeLimit { get { return 120f; } }
        public override float ArenaRadius { get { return 10f; } }
        public override float KillY { get { return -3f; } }

        const int Grid = 8;
        const float Size = 2.5f;
        readonly List<IceTile> tiles = new List<IceTile>();
        float randomCrackTimer = 25f;

        public override void BuildArena()
        {
            float half = (Grid - 1) * Size * 0.5f;
            for (int x = 0; x < Grid; x++)
            {
                for (int z = 0; z < Grid; z++)
                {
                    var pos = new Vector3(x * Size - half, -0.4f, z * Size - half);
                    var go = Shapes.Box(Root, pos, new Vector3(Size - 0.12f, 0.8f, Size - 0.12f), (x + z) % 2 == 0 ? Map.Floor : Map.FloorAlt, true);
                    go.name = "Plyta";
                    var tile = go.AddComponent<IceTile>();
                    tile.Init(this, (x + z) % 2 == 0 ? Map.Floor : Map.FloorAlt);
                    tiles.Add(tile);
                }
            }
            // Kilka płyt z twardego lodu: nigdy nie pękają (lekko błyszczą)
            var shuffled = new List<IceTile>(tiles);
            for (int i = 0; i < 5 && shuffled.Count > 0; i++)
            {
                int k = Random.Range(0, shuffled.Count);
                shuffled[k].MakeSolid();
                shuffled.RemoveAt(k);
            }
            // Ramka dookoła (dekoracyjna, bez kolizji)
            float edge = half + Size * 0.5f + 0.3f;
            for (int s = 0; s < 4; s++)
            {
                var rot = Quaternion.Euler(0, s * 90f, 0);
                var b = Shapes.Box(Root, rot * new Vector3(0, -0.7f, edge), new Vector3(edge * 2f + 0.6f, 0.5f, 0.4f), Map.Accent, false);
                b.transform.localRotation = rot;
            }
        }

        protected override void ConfigurePlayer(Potato p)
        {
            // Na płytach trzeba precyzji, więc lód jest tu trochę mniej śliski
            p.Grip = Mathf.Max(p.Grip, 0.6f);
        }

        public override Vector3 SpawnPoint(int i, int n)
        {
            float a = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f + Mathf.PI * 0.25f;
            return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 6f + Vector3.up * (Potato.HalfHeight + 0.1f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished || !Running) return;
            foreach (var p in Alive)
            {
                if (!p.Grounded || p.GroundCollider == null) continue;
                var tile = p.GroundCollider.GetComponent<IceTile>();
                if (tile != null) tile.Crack();
            }
            randomCrackTimer -= dt;
            if (randomCrackTimer <= 0f)
            {
                randomCrackTimer = Mathf.Lerp(1.4f, 0.5f, Mathf.InverseLerp(TimeLimit - 25f, 0f, TimeLeft));
                var idle = tiles.FindAll(t => t != null && t.IsIdle && !t.Solid);
                if (idle.Count > 0) idle[Random.Range(0, idle.Count)].Crack();
            }
        }

        public override string HudInfo()
        {
            return base.HudInfo() + "   ·   Na lodzie: " + AliveCount;
        }

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            IceTile under = (p.GroundCollider != null) ? p.GroundCollider.GetComponent<IceTile>() : null;
            if (under != null && under.Solid && p.Slot.Index % 2 == 0)
            {
                // stoi na twardym lodzie: broni miejsca i odpycha intruzów
                var intruder = Nearest(p);
                bool push = intruder != null && FlatDist(intruder.transform.position, pos) < 2.2f && p.DashCooldownLeft <= 0f;
                return push ? BotCommand.Go(intruder.transform.position, true) : BotCommand.Go(under.transform.position);
            }

            Vector3 goal;
            bool haveGoal = TryGetBotGoal(p, out goal);
            if (haveGoal)
            {
                // Cel nadal stoi?
                var hits = Physics.OverlapSphere(goal + Vector3.down * 0.4f, 0.3f, ~(1 << Potato.Layer), QueryTriggerInteraction.Ignore);
                bool ok = false;
                foreach (var h in hits)
                {
                    var t = h.GetComponent<IceTile>();
                    if (t != null && t.IsIdle) ok = true;
                }
                if (!ok || FlatDist(goal, pos) < 0.8f) haveGoal = false;
            }
            if (!haveGoal)
            {
                IceTile best = null;
                float bestScore = float.MaxValue;
                foreach (var t in tiles)
                {
                    if (t == null || !t.IsIdle || t == under) continue;
                    float d = FlatDist(t.transform.position, pos);
                    if (d < 1.5f) continue;
                    // Wolimy płyty blisko, ale otoczone innymi całymi płytami
                    int around = 0;
                    foreach (var o in tiles)
                        if (o != null && o.IsIdle && FlatDist(o.transform.position, t.transform.position) < Size * 1.5f) around++;
                    float s = d - around * 0.9f + Random.value * 1.5f - (t.Solid && p.Slot.Index % 2 == 0 ? 6f : 0f);
                    if (d > 7.5f) s += 10f;
                    if (s < bestScore) { bestScore = s; best = t; }
                }
                goal = best != null ? best.transform.position + Vector3.up * 0.4f : pos;
                SetBotGoal(p, goal, Random.Range(0.6f, 1.2f));
            }

            bool jump = p.Grounded && GapAhead(p, goal);
            var enemy = Nearest(p);
            bool dash = enemy != null && FlatDist(enemy.transform.position, pos) < 2.5f && Random.value < 0.25f && !GapAhead(p, enemy.transform.position);
            return BotCommand.Go(goal, dash, jump);
        }

        public override void Cleanup() { tiles.Clear(); }
    }

    public class IceTile : MonoBehaviour
    {
        CrackingIceGame game;
        Material mat;
        Color baseColor;
        Vector3 home;
        float timer;
        int state; // 0 cała, 1 pęka, 2 spada

        public bool IsIdle { get { return state == 0; } }
        public bool Solid { get; private set; }

        public void MakeSolid()
        {
            Solid = true;
            baseColor = Color.Lerp(baseColor, Color.white, 0.25f);
            Shapes.SetColor(mat, baseColor);
            var glint = Shapes.Prim(PrimitiveType.Sphere, transform, new Vector3(0.3f, 0.52f, -0.25f), new Vector3(0.06f, 0.06f, 0.06f), Color.white);
            glint.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Color.white, 0.9f, 1.5f);
            glint.AddComponent<Bobber>().Amplitude = 0.03f;
        }

        public void Init(CrackingIceGame g, Color c)
        {
            game = g;
            baseColor = c;
            mat = Shapes.UniqueMat(c, 0.85f);
            GetComponent<Renderer>().sharedMaterial = mat;
            home = transform.localPosition;
        }

        public void Crack()
        {
            if (state != 0 || Solid) return;
            state = 1;
            timer = 0.65f;
            AudioManager.Play(Sfx.Crack, 0.35f, 0.2f);
        }

        void Update()
        {
            if (state == 1)
            {
                timer -= Time.deltaTime;
                float k = 1f - timer / 0.65f;
                Shapes.SetColor(mat, Color.Lerp(baseColor, new Color(0.35f, 0.55f, 0.75f), k));
                transform.localPosition = home + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * 0.06f * k;
                if (timer <= 0f)
                {
                    state = 2;
                    transform.localPosition = home;
                    var rb = gameObject.AddComponent<Rigidbody>();
                    rb.mass = 5f;
                    rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
                    GetComponent<Collider>().enabled = false;
                    Destroy(gameObject, 3f);
                }
            }
        }
    }
}
