using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Ziemniaczane Sumo: okrągła platforma nad wodą. Wypychaj innych zrywem.
    /// Platforma z czasem się kurczy. Ostatni na platformie wygrywa.
    /// </summary>
    public class SumoGame : Minigame
    {
        public override string Title { get { return "Ziemniaczane Sumo"; } }
        public override string Description { get { return "Zepchnij wszystkich z platformy! Zryw (akcja) daje mocne pchnięcie. Platforma się kurczy, a przez tratwę przetaczają się wielkie dmuchane piłki!"; } }
        public override string Tip { get { return "Trzymaj się środka i używaj zrywu, gdy rywal stoi przy krawędzi."; } }
        public override MapId PreferredMap { get { return MapId.Beach; } }
        public override float TimeLimit { get { return 90f; } }
        public override float ArenaRadius { get { return 9f; } }

        Transform arena;
        float shrinkDelay = 15f;
        float currentRadius;
        bool announced, superDash;
        float ballTimer = 8f;
        readonly List<Rigidbody> balls = new List<Rigidbody>();
        const float MinRadius = 4.2f;

        public float CurrentRadius { get { return currentRadius; } }

        public override void BuildArena()
        {
            arena = Shapes.Empty("Platforma", Root, Vector3.zero).transform;
            MapBuilder.RoundArena(arena, ArenaRadius, Map);
            // Słupek podtrzymujący platformę
            MapBuilder.Disc(Root, new Vector3(0, -4f, 0), 2f, 6f, Map.Wall, false);
            currentRadius = ArenaRadius;
        }

        protected override void ConfigurePlayer(Potato p)
        {
            p.DashPower = 16f;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished || !Running) return;
            shrinkDelay -= dt;
            if (shrinkDelay <= 0f && currentRadius > MinRadius)
            {
                currentRadius = Mathf.Max(MinRadius, currentRadius - dt * 0.09f);
                float k = currentRadius / ArenaRadius;
                arena.localScale = new Vector3(k, 1f, k);
                if (!announced) { announced = true; GM.Hud.Toast("Platforma się kurczy!", Map.Accent); }
            }

            // dmuchane piłki przelatujące przez tratwę
            ballTimer -= dt;
            if (ballTimer <= 0f)
            {
                ballTimer = Random.Range(6f, 10f);
                SpawnBeachBall();
            }
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                if (balls[i] == null) { balls.RemoveAt(i); continue; }
                if (balls[i].position.y < -8f) { Object.Destroy(balls[i].gameObject); balls.RemoveAt(i); }
            }

            // końcówka: super zryw
            if (!superDash && TimeLeft < 30f)
            {
                superDash = true;
                foreach (var p in Alive) p.DashPower = 21f;
                GM.Hud.Toast("SUPER ZRYW! Pchnięcia są teraz dużo mocniejsze!", Map.Accent);
                AudioManager.Play(Sfx.Whoosh, 1f);
            }
        }

        void SpawnBeachBall()
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Dmuchana pilka";
            go.transform.SetParent(Root, false);
            go.transform.localScale = Vector3.one * 2.4f;
            go.transform.position = -dir * (currentRadius + 3f) + Vector3.up * 3f;
            go.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#FF6F91"), 0.7f);
            for (int k = 0; k < 3; k++)
            {
                var band = Shapes.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(1.01f, 0.3f, 1.01f), k == 0 ? Color.white : k == 1 ? Shapes.Hex("#FFD23F") : Shapes.Hex("#2EC4B6"), false, 0.7f);
                band.transform.localRotation = Quaternion.Euler(0, k * 60f, 90f);
            }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 2f;
            Phys.SetVelocity(rb, dir * 9f + Vector3.up * 2f + Vector3.Cross(Vector3.up, dir) * Random.Range(-2f, 2f));
            go.AddComponent<BonkBall>().Power = 1.2f;
            balls.Add(rb);
            GM.Hud.Toast("Uwaga, piłka!", Shapes.Hex("#FF6F91"));
        }

        public override string HudInfo()
        {
            return base.HudInfo() + "   ·   Na platformie: " + AliveCount;
        }

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            var flat = new Vector3(pos.x, 0, pos.z);
            // Za blisko krawędzi: wracaj do środka
            if (flat.magnitude > currentRadius * 0.72f)
                return BotCommand.Go(Vector3.zero);

            var t = Nearest(p);
            if (t == null) return BotCommand.Stay();
            var tp = t.transform.position;
            float d = FlatDist(tp, pos);
            // Ustaw się tak, żeby pchać przeciwnika na zewnątrz
            var tflat = new Vector3(tp.x, 0, tp.z);
            var outward = tflat.sqrMagnitude > 0.1f ? tflat.normalized : Random.insideUnitSphere;
            outward.y = 0;
            Vector3 aim = d > 3.5f ? tp - outward * 1.5f : tp;
            bool dash = d < 3.8f && p.DashCooldownLeft <= 0f && Random.value < 0.55f;
            if (Random.value < 0.08f) aim = Wander(p, currentRadius * 0.5f, 0.6f);
            return BotCommand.Go(aim, dash);
        }
    }
}
