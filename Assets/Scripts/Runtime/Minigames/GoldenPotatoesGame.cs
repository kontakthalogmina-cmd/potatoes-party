using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Złote Ziemniaki: zbieraj złote ziemniaczki rozsypane po arenie. Zrywem w przeciwnika
    /// wybijasz mu część zdobyczy. Po upływie czasu wygrywa ten, kto ma najwięcej.
    /// </summary>
    public class GoldenPotatoesGame : Minigame
    {
        public override string Title { get { return "Złote Ziemniaki"; } }
        public override string Description { get { return "Zbieraj złote ziemniaczki! Duży wart jest 3. Uderz zrywem rywala, a wypadną mu dwa. Uwaga: fałszywe złoto się nie kręci i zabiera punkty! W ostatnich 10 s złoto liczy się podwójnie."; } }
        public override string Tip { get { return "Fałszywe złoto się nie kręci. Omijaj je!"; } }
        public override string Goal { get { return "Najwięcej złota po 45 sekundach wygrywa!"; } }
        public override MapId PreferredMap { get { return MapId.Orchard; } }
        public override bool FallEliminates { get { return false; } }
        public override bool EndsWhenOneLeft { get { return false; } }
        public override float TimeLimit { get { return 45f; } }
        public override float ArenaRadius { get { return 11f; } }
        public override bool ShowLeaderCrown { get { return true; } }
        bool rush;

        readonly List<GoldenCoin> coins = new List<GoldenCoin>();
        float spawnTimer, bigTimer = 6f;

        public override void BuildArena()
        {
            MapBuilder.RoundArena(Root, ArenaRadius, Map);
            MapBuilder.WallRing(Root, ArenaRadius, Map);
            // Środkowy podest
            MapBuilder.Disc(Root, new Vector3(0, 0.25f, 0), 2.2f, 0.5f, Map.Accent);
            for (int i = 0; i < 8; i++) SpawnCoin(RandomSpot(), 1, false);
        }

        Vector3 RandomSpot()
        {
            var c = Random.insideUnitCircle * (ArenaRadius - 1.5f);
            float y = c.magnitude < 2.4f ? 1.3f : 0.8f;
            return new Vector3(c.x, y, c.y);
        }

        void SpawnCoin(Vector3 pos, int value, bool dropped)
        {
            // co któraś zwykła moneta to fałszywe złoto (nie kręci się!)
            bool fake = !dropped && value == 1 && Random.value < 0.18f;
            var go = new GameObject(value > 1 ? "Duzy zloty ziemniak" : "Zloty ziemniak");
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = value > 1 ? 0.9f : 0.6f;
            var coin = go.AddComponent<GoldenCoin>();
            coin.Game = this;
            coin.Value = value;
            float s = value > 1 ? 1.1f : 0.6f;
            var vis = Shapes.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(s, s * 1.25f, s * 0.9f), Shapes.Hex("#FFD23F"), false, 0.9f);
            vis.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FFC61A"), 0.9f, 0.6f);
            coin.Fake = fake;
            if (!fake) vis.AddComponent<Spinner>().Speed = new Vector3(0, 140f, 0);
            coin.Visual = vis.transform;
            if (dropped)
            {
                coin.Velocity = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * Random.Range(3f, 5f) + Vector3.up * 6f;
                coin.PickupDelay = 0.5f;
            }
            coins.Add(coin);
        }

        public void Collect(GoldenCoin coin, Potato p)
        {
            if (!Running || p.Eliminated || !Score.ContainsKey(p)) return;
            if (coin.Fake)
            {
                Score[p] = Mathf.Max(0, Score[p] - 2);
                p.Knock(Vector3.up * 7f, 0.8f);
                AudioManager.Play(Sfx.Explosion, 0.5f, 0.1f);
                Fx.MudSplat(Root, coin.transform.position, Shapes.Hex("#5A3E26"));
                // fałszywe złoto wybucha błotem: ziemniak zostaje ochlapany od strony monety
                if (p.Surface != null) p.Surface.SplashMud(coin.transform.position, 0.7f);
                GM.Hud.Toast(p.Slot.Name + " złapał fałszywe złoto! -2", p.Color);
            }
            else
            {
                Score[p] += coin.Value * (rush ? 2 : 1);
                AudioManager.Play(Sfx.Coin, 0.8f, 0.12f);
                Effects.Sparkle(Root, coin.transform.position, Shapes.Hex("#FFD23F"));
            }
            coins.Remove(coin);
            Object.Destroy(coin.gameObject);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished) return;
            coins.RemoveAll(c => c == null);
            if (!rush && TimeLeft <= 10f && Running)
            {
                rush = true;
                GM.Hud.Toast("ZŁOTA GORĄCZKA! Złoto liczy się podwójnie!", Shapes.Hex("#FFC61A"));
                AudioManager.Play(Sfx.Fanfare, 0.7f);
                for (int i = 0; i < 6; i++) SpawnCoin(RandomSpot() + Vector3.up * 6f, 1, false);
            }

            spawnTimer -= dt;
            if (spawnTimer <= 0f && coins.Count < 14)
            {
                spawnTimer = Random.Range(0.4f, 0.9f);
                SpawnCoin(RandomSpot() + Vector3.up * 5f, 1, false);
            }
            bigTimer -= dt;
            if (bigTimer <= 0f)
            {
                bigTimer = Random.Range(6f, 9f);
                SpawnCoin(RandomSpot() + Vector3.up * 6f, 3, false);
                GM.Hud.Toast("Duży złoty ziemniak!", Shapes.Hex("#FFC61A"));
            }
        }

        public override void OnBump(Potato hitter, Potato victim, bool dash)
        {
            if (!dash || !Running) return;
            int lost = Mathf.Min(2, Score[victim]);
            if (lost <= 0) return;
            Score[victim] -= lost;
            for (int i = 0; i < lost; i++) SpawnCoin(victim.transform.position + Vector3.up * 1.2f, 1, true);
        }

        public override string StatFor(Potato p) { return ScoreOf(p) + " zł."; }

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            GoldenCoin best = null;
            float bestScore = float.MaxValue;
            foreach (var c in coins)
            {
                if (c == null || c.PickupDelay > 0f) continue;
                // boty czasem dają się nabrać na fałszywe złoto (to część zabawy)
                if (c.Fake && (p.Slot.Index % 3 != 0)) continue;
                float d = FlatDist(c.transform.position, pos) / c.Value;
                // Inni gracze bliżej tej monety? Wtedy mniej opłacalna
                var rival = Nearest(p);
                if (rival != null && FlatDist(rival.transform.position, c.transform.position) < d * 0.5f) d *= 1.5f;
                if (d < bestScore) { bestScore = d; best = c; }
            }
            var enemy = Nearest(p, o => Score[o] >= 2);
            bool attack = enemy != null && FlatDist(enemy.transform.position, pos) < 3f && p.DashCooldownLeft <= 0f && Random.value < 0.3f;
            if (attack) return BotCommand.Go(enemy.transform.position, true);
            if (best == null) return BotCommand.Go(Wander(p, ArenaRadius * 0.7f));
            var target = best.transform.position;
            bool jump = target.y > 1.1f && FlatDist(target, pos) < 3f && p.Grounded && Random.value < 0.3f;
            return BotCommand.Go(target, false, jump);
        }

        public override void Cleanup() { coins.Clear(); }
    }

    public class GoldenCoin : MonoBehaviour
    {
        public GoldenPotatoesGame Game;
        public int Value = 1;
        public bool Fake;
        public Transform Visual;
        public Vector3 Velocity;
        public float PickupDelay;
        float landY = -999f;

        void Start()
        {
            // Monety spadają z nieba na podłoże
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, 20f, ~(1 << Potato.Layer), QueryTriggerInteraction.Ignore))
                landY = hit.point.y + 0.7f;
            else landY = 0.7f;
        }

        void Update()
        {
            PickupDelay -= Time.deltaTime;
            var p = transform.position;
            if (p.y > landY + 0.01f || Velocity.y > 0f)
            {
                Velocity += Vector3.down * 18f * Time.deltaTime;
                p += Velocity * Time.deltaTime;
                float r = new Vector2(p.x, p.z).magnitude;
                if (r > 10f) { var f = new Vector2(p.x, p.z).normalized * 10f; p.x = f.x; p.z = f.y; Velocity.x = Velocity.z = 0; }
                if (p.y <= landY && Velocity.y <= 0f)
                {
                    p.y = landY;
                    Velocity = Vector3.zero;
                    landY = Recalc(p);
                    if (p.y > landY + 0.05f) Velocity = Vector3.zero; // spadnie jeszcze niżej
                }
                transform.position = p;
            }
            if (Visual != null) Visual.localPosition = Vector3.up * Mathf.Sin(Time.time * 3f + p.x) * 0.12f;
        }

        float Recalc(Vector3 p)
        {
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out hit, 20f, ~(1 << Potato.Layer), QueryTriggerInteraction.Ignore))
                return hit.point.y + 0.7f;
            return 0.7f;
        }

        void OnTriggerStay(Collider other)
        {
            if (PickupDelay > 0f || Game == null) return;
            var p = other.GetComponent<Potato>();
            if (p != null) Game.Collect(this, p);
        }
    }
}
