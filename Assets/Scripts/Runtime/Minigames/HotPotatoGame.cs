using System.Linq;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Gorący Ziemniak: jeden gracz ma bombę. Dotknij kogoś, żeby ją przekazać.
    /// Gdy lont się wypali, posiadacz bomby odpada. Ostatni ziemniak wygrywa.
    /// </summary>
    public class HotPotatoGame : Minigame
    {
        public override string Title { get { return "Gorący Ziemniak"; } }
        public override string Description { get { return "Ktoś ma bombę! Dotknij innego gracza, żeby ją przekazać. Nie wiesz, ile zostało lontu: patrz, jak szybko bomba miga! Nie można oddać bomby od razu temu, kto ci ją dał."; } }
        public override string Tip { get { return "Bomba miga coraz szybciej tuż przed wybuchem. Wtedy szukaj kogoś blisko!"; } }
        public override string Goal { get { return "Nie trzymaj bomby, gdy wybuchnie!"; } }
        public override MapId PreferredMap { get { return MapId.Farm; } }
        public override bool FallEliminates { get { return false; } }
        public override float TimeLimit { get { return 9999f; } }
        public override float ArenaRadius { get { return 10.5f; } }

        Potato holder;
        Transform bomb;
        Material bombMat;
        float fuse, fuseStart, passCooldown, nextBombDelay;
        Potato giver;
        float noReturn;

        public override void BuildArena()
        {
            MapBuilder.RoundArena(Root, ArenaRadius, Map);
            MapBuilder.WallRing(Root, ArenaRadius, Map);
            // Kilka przeszkód, za którymi można się chować
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 5f;
                var hay = Shapes.Prim(PrimitiveType.Cylinder, Root, p + Vector3.up * 0.6f, new Vector3(1.6f, 0.6f, 1.6f), Shapes.Hex("#E8C35A"), true);
                hay.name = "Przeszkoda";
            }

            var b = Shapes.Empty("Bomba", Root, Vector3.up * 50f).transform;
            var ball = Shapes.Prim(PrimitiveType.Sphere, b, Vector3.zero, Vector3.one * 0.75f, Color.black, false, 0.6f);
            bombMat = Shapes.UniqueMat(new Color(0.12f, 0.12f, 0.14f), 0.6f);
            ball.GetComponent<Renderer>().sharedMaterial = bombMat;
            Shapes.Prim(PrimitiveType.Cylinder, b, new Vector3(0, 0.42f, 0), new Vector3(0.12f, 0.12f, 0.12f), Shapes.Hex("#C9874A"));
            var spark = Shapes.Prim(PrimitiveType.Sphere, b, new Vector3(0, 0.6f, 0), Vector3.one * 0.2f, Shapes.Hex("#FFB300"));
            spark.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FFB300"), 0.2f, 2f);
            spark.AddComponent<Bobber>().Amplitude = 0.05f;
            bomb = b;
        }

        public override void Begin()
        {
            base.Begin();
            GiveBomb(Alive.ElementAt(Random.Range(0, AliveCount)), true);
        }

        void GiveBomb(Potato p, bool fresh)
        {
            if (holder != null) holder.SpeedMultiplier = 1f;
            giver = fresh ? null : holder;
            noReturn = 1.5f;
            holder = p;
            holder.SpeedMultiplier = 1.12f;
            passCooldown = 0.6f;
            if (fresh)
            {
                int alive = AliveCount;
                fuseStart = Mathf.Lerp(9f, 15f, Mathf.InverseLerp(2, 6, alive)) + Random.Range(-1.5f, 2f);
                fuse = fuseStart;
            }
            GM.Hud.Toast(p.Slot.Name + " ma bombę!", p.Color);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished) return;
            passCooldown -= dt;
            noReturn -= dt;

            if (holder == null)
            {
                bomb.position = Vector3.up * 50f;
                nextBombDelay -= dt;
                if (nextBombDelay <= 0f && AliveCount >= 2)
                    GiveBomb(Alive.ElementAt(Random.Range(0, AliveCount)), true);
                return;
            }

            fuse -= dt;
            bomb.position = holder.HeadAnchor.position + Vector3.up * (0.3f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.15f);
            float danger = 1f - Mathf.Clamp01(fuse / fuseStart);
            float blink = Mathf.Sin(Time.time * Mathf.Lerp(4f, 30f, danger)) > 0 ? danger : 0f;
            Shapes.SetColor(bombMat, Color.Lerp(new Color(0.12f, 0.12f, 0.14f), new Color(1f, 0.2f, 0.1f), blink));
            bomb.localScale = Vector3.one * (1f + blink * 0.15f);

            if (fuse <= 0f) Explode();
        }

        void Explode()
        {
            var victim = holder;
            holder.SpeedMultiplier = 1f;
            holder = null;
            nextBombDelay = 1.6f;
            Vector3 c = victim.transform.position;
            Effects.Explosion(Root, c, 2.8f);
            foreach (var o in Alive)
            {
                if (o == victim) continue;
                var d = o.transform.position - c;
                d.y = 0;
                if (d.magnitude < 4f)
                {
                    o.Knock(d.normalized * 9f + Vector3.up * 5f, 0.4f);
                    // przypalona skórka od strony wybuchu (żar gaśnie szybko, sadza zostaje)
                    if (o.Surface != null) o.Surface.Scorch(c, Mathf.Lerp(0.85f, 0.35f, d.magnitude / 4f));
                }
            }
            if (victim.Surface != null) victim.Surface.Scorch(c + Vector3.down, 1f);
            Eliminate(victim, false);
            // każdy, kto przetrwał wybuch, dostaje punkt (przyda się przy remisach)
            foreach (var o in Alive) Score[o] = ScoreOf(o) + 1;
        }

        public override void OnBump(Potato a, Potato b, bool dash)
        {
            if (holder == null || passCooldown > 0f) return;
            var other = a == holder ? b : b == holder ? a : null;
            if (other == null || other.Eliminated) return;
            if (other == giver && noReturn > 0f) return; // bez oddawania od razu
            GiveBomb(other, false);
            AudioManager.Play(Sfx.Tick, 1f);
            // zabawny "przerzut": nowy posiadacz podskakuje ze strachu
            other.Knock(Vector3.up * 6f, 0.15f);
        }

        public override string HudInfo()
        {
            if (holder == null) return "Nowa bomba za chwilę...";
            return "Bomba ma: " + holder.Slot.Name + "   ·   Lont: ???";
        }

        public override string StatFor(Potato p)
        {
            if (p.Eliminated) return "wybuchł";
            return p == holder ? "MA BOMBĘ!" : "";
        }

        public override BotCommand Bot(Potato p)
        {
            if (holder == null) return BotCommand.Go(Wander(p, ArenaRadius * 0.6f));
            if (p == holder)
            {
                var t = Nearest(p);
                if (t == null) return BotCommand.Stay();
                var tp = t.transform.position + Phys.GetVelocity(t.Body) * 0.3f;
                float d = FlatDist(tp, p.transform.position);
                return BotCommand.Go(tp, d < 3.2f && Random.value < 0.5f);
            }
            var hp = holder.transform.position;
            var pos = p.transform.position;
            float dist = FlatDist(hp, pos);
            if (dist > 7f) return BotCommand.Go(Wander(p, ArenaRadius * 0.7f, 2f));
            var away = pos - hp;
            away.y = 0;
            away = away.sqrMagnitude < 0.01f ? Vector3.right : away.normalized;
            // Przy ścianie uciekaj wzdłuż niej zamiast w nią
            var flatPos = new Vector3(pos.x, 0, pos.z);
            if (flatPos.magnitude > ArenaRadius * 0.7f)
            {
                var tangent = Vector3.Cross(Vector3.up, flatPos.normalized);
                if (Vector3.Dot(tangent, away) < 0) tangent = -tangent;
                away = (tangent * 1.5f - flatPos.normalized).normalized;
            }
            bool dodge = dist < 2.2f && Random.value < 0.35f;
            return BotCommand.Go(Clamp(pos + away * 4f, ArenaRadius * 0.85f), dodge, dodge && Random.value < 0.5f);
        }

        public override void Cleanup()
        {
            if (holder != null) holder.SpeedMultiplier = 1f;
        }
    }
}
