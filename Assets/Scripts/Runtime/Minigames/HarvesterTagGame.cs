using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Ucieczka przed Kombajnem: po polu kukurydzy jeździ kombajn i goni najbliższego ziemniaka.
    /// Skręca powoli, więc trzeba uskakiwać w bok. Z czasem przyspiesza, a potem wyjeżdża drugi.
    /// </summary>
    public class HarvesterTagGame : Minigame
    {
        public override string Title { get { return "Ucieczka przed Kombajnem"; } }
        public override string Description { get { return "Kombajn goni ziemniaka oznaczonego wykrzyknikiem! Czasem nagle zmienia ofiarę. Skręca powoli, więc uskakuj w bok w ostatniej chwili, a zielone strzałki dają przyspieszenie. Wpychaj innych pod kombajn!"; } }
        public override string Tip { get { return "Kombajn skręca powoli. Uskocz w bok w ostatniej chwili."; } }
        public override string Goal { get { return "Ostatni nieskoszony ziemniak wygrywa!"; } }
        public override MapId PreferredMap { get { return MapId.Cornfield; } }
        public override float TimeLimit { get { return 75f; } }
        public override float ArenaRadius { get { return 11f; } }
        public override bool FallEliminates { get { return false; } }
        public override float CameraPull { get { return 0.2f; } }
        public override string Controls { get { return "Ruch · skok · akcja = zryw (szybki unik!)"; } }

        const float Half = 11f;
        const float HalfW = 1.9f, HalfL = 2.3f;

        class Harvester
        {
            public Transform T;
            public Transform Reel;
            public float Heading;
            public Potato Target;
            public float Retarget;
            public Transform Warn;
        }

        static readonly Vector3[] Pads = { new Vector3(-6f, 0, -6f), new Vector3(6f, 0, 6f), new Vector3(6f, 0, -6f), new Vector3(-6f, 0, 6f) };
        readonly Dictionary<Potato, float> boost = new Dictionary<Potato, float>();

        readonly List<Harvester> harvesters = new List<Harvester>();
        readonly List<Transform> stalks = new List<Transform>();
        float elapsed;
        bool second;

        public override void BuildArena()
        {
            Shapes.Box(Root, new Vector3(0, -0.5f, 0), new Vector3(Half * 2f, 1f, Half * 2f), Map.Floor, true);
            // bruzdy
            for (int i = 0; i < 11; i++)
                Shapes.Box(Root, new Vector3(-Half + 1f + i * 2f, 0.01f, 0), new Vector3(0.9f, 0.02f, Half * 2f - 0.4f), Map.FloorAlt, false);
            // płot z bel siana
            for (int s = 0; s < 4; s++)
            {
                var rot = Quaternion.Euler(0, s * 90f, 0);
                var w = Shapes.Box(Root, rot * new Vector3(0, 0.6f, Half + 0.4f), new Vector3(Half * 2f + 1.6f, 1.2f, 0.8f), Map.Wall, true);
                w.transform.localRotation = rot;
            }
            // kukurydza do skoszenia (bez kolizji)
            for (int i = 0; i < 70; i++)
            {
                var p = new Vector3(Random.Range(-Half + 1f, Half - 1f), 0, Random.Range(-Half + 1f, Half - 1f));
                if (p.magnitude < 6.5f && p.magnitude > 3f) continue; // wolne miejsce przy starcie
                stalks.Add(MapBuilder.CornStalk(Root, p, Random.Range(0.55f, 0.75f)).transform);
            }
            AddHarvester(new Vector3(0, 0, -Half + 3f), 0f);

            // zielone strzałki przyspieszenia
            foreach (var pad in Pads)
            {
                var g = Shapes.Empty("Przyspieszenie", Root, pad).transform;
                MapBuilder.Disc(g, new Vector3(0, 0.02f, 0), 1.1f, 0.04f, Shapes.Hex("#5CC157"), false);
                for (int k = 0; k < 2; k++)
                {
                    var arrow = Shapes.Box(g, new Vector3(0, 0.06f, -0.3f + k * 0.5f), new Vector3(0.9f, 0.04f, 0.18f), Color.white, false);
                    arrow.transform.localRotation = Quaternion.Euler(0, 45f, 0);
                    var arrow2 = Shapes.Box(g, new Vector3(0, 0.06f, -0.3f + k * 0.5f), new Vector3(0.9f, 0.04f, 0.18f), Color.white, false);
                    arrow2.transform.localRotation = Quaternion.Euler(0, -45f, 0);
                }
                g.gameObject.AddComponent<Spinner>().Speed = new Vector3(0, 60f, 0);
            }
        }

        void AddHarvester(Vector3 pos, float heading)
        {
            var h = new Harvester { Heading = heading };
            var t = Shapes.Empty("Kombajn", Root, pos).transform;
            var body = Map.Accent;
            Shapes.Box(t, new Vector3(0, 1.1f, -0.4f), new Vector3(2.4f, 1.4f, 3f), body, false);
            Shapes.Box(t, new Vector3(0, 2.3f, -0.1f), new Vector3(1.4f, 1.1f, 1.3f), Shapes.Hex("#BFE3F2"), false);
            Shapes.Box(t, new Vector3(0, 2.9f, -0.1f), new Vector3(1.6f, 0.12f, 1.5f), body, false);
            var pipe = Shapes.Prim(PrimitiveType.Cylinder, t, new Vector3(1.4f, 2.2f, -1.2f), new Vector3(0.3f, 1f, 0.3f), body);
            pipe.transform.localRotation = Quaternion.Euler(0, 0, -50f);
            for (int s = -1; s <= 1; s += 2)
            {
                var w = Shapes.Prim(PrimitiveType.Cylinder, t, new Vector3(1.25f * s, 0.6f, 0.4f), new Vector3(1.2f, 0.25f, 1.2f), Shapes.Hex("#2B2B2B"));
                w.transform.localRotation = Quaternion.Euler(0, 0, 90);
                var w2 = Shapes.Prim(PrimitiveType.Cylinder, t, new Vector3(1.2f * s, 0.45f, -1.5f), new Vector3(0.9f, 0.2f, 0.9f), Shapes.Hex("#2B2B2B"));
                w2.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            // heder z obracającym się nagarniaczem
            Shapes.Box(t, new Vector3(0, 0.45f, 1.7f), new Vector3(HalfW * 2f, 0.5f, 0.9f), Shapes.Hex("#F5D042"), false);
            var reel = Shapes.Empty("Nagarniacz", t, new Vector3(0, 0.95f, 1.9f)).transform;
            var axle = Shapes.Prim(PrimitiveType.Cylinder, reel, Vector3.zero, new Vector3(0.15f, HalfW, 0.15f), Shapes.Hex("#555555"));
            axle.transform.localRotation = Quaternion.Euler(0, 0, 90);
            for (int k = 0; k < 4; k++)
            {
                var bar = Shapes.Box(reel, Quaternion.Euler(k * 90f, 0, 0) * new Vector3(0, 0.4f, 0), new Vector3(HalfW * 2f - 0.2f, 0.1f, 0.1f), Shapes.Hex("#E2463F"), false);
                bar.transform.localRotation = Quaternion.Euler(k * 90f, 0, 0);
            }
            h.T = t;
            h.Reel = reel;
            // czerwony wykrzyknik nad głową ściganego
            h.Warn = Shapes.Empty("Wykrzyknik", Root, Vector3.up * -50f).transform;
            Shapes.Box(h.Warn, new Vector3(0, 0.35f, 0), new Vector3(0.18f, 0.5f, 0.18f), Shapes.Hex("#E2463F"), false);
            Shapes.Prim(PrimitiveType.Sphere, h.Warn, Vector3.zero, Vector3.one * 0.2f, Shapes.Hex("#E2463F"));
            t.localRotation = Quaternion.Euler(0, heading, 0);
            harvesters.Add(h);
        }

        public override Vector3 SpawnPoint(int i, int n)
        {
            float a = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f + Mathf.PI * 0.5f;
            return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 4.5f + Vector3.up * (Potato.HalfHeight + 0.1f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished || !Running) return;
            elapsed += dt;
            if (!second && elapsed > 40f)
            {
                second = true;
                AddHarvester(new Vector3(0, 0, Half - 3f), 180f);
                GM.Hud.Toast("Drugi kombajn wjeżdża na pole!", Map.Accent);
                AudioManager.Play(Sfx.Whoosh, 0.9f);
            }

            // przyspieszenia
            foreach (var p in Alive)
            {
                float b;
                boost.TryGetValue(p, out b);
                foreach (var pad in Pads)
                    if (FlatDist(p.transform.position, pad) < 1.1f && b <= 0f) { b = 2.2f; AudioManager.Play(Sfx.Whoosh, 0.5f); }
                b -= dt;
                boost[p] = b;
                p.SpeedMultiplier = b > 0f ? 1.5f : 1f;
            }

            float speed = Mathf.Lerp(3.6f, 8f, Mathf.Clamp01(elapsed / 60f));
            float turn = Mathf.Lerp(70f, 110f, Mathf.Clamp01(elapsed / 60f));
            foreach (var h in harvesters)
            {
                h.Retarget -= dt;
                if (h.Target == null || h.Target.Eliminated || h.Retarget <= 0f)
                {
                    h.Retarget = 2f;
                    var old = h.Target;
                    // czasem kombajn "upatruje" sobie kogoś innego (zwód!)
                    if (Random.value < 0.22f && AliveCount > 1)
                    {
                        var alive = new List<Potato>(Alive);
                        h.Target = alive[Random.Range(0, alive.Count)];
                        h.Retarget = 4f;
                    }
                    else h.Target = NearestTo(h.T.position);
                    if (h.Target != old && h.Target != null && old != null)
                    {
                        GM.Hud.Toast("Kombajn upatrzył sobie: " + h.Target.Slot.Name + "!", h.Target.Color);
                        AudioManager.Play(Sfx.Tick, 0.6f);
                    }
                }
                if (h.Warn != null)
                    h.Warn.position = h.Target != null ? h.Target.HeadAnchor.position + Vector3.up * (0.4f + Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.2f) : Vector3.up * -50f;
                if (h.Target != null)
                {
                    var to = h.Target.transform.position - h.T.position;
                    float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    h.Heading = Mathf.MoveTowardsAngle(h.Heading, want, turn * dt);
                }
                var fwd = Quaternion.Euler(0, h.Heading, 0) * Vector3.forward;
                var next = h.T.position + fwd * speed * dt;
                // odbicie od płotu
                if (Mathf.Abs(next.x) > Half - 2.2f || Mathf.Abs(next.z) > Half - 2.2f)
                {
                    next = h.T.position;
                    h.Heading += 120f * dt * 3f;
                }
                h.T.position = next;
                h.T.rotation = Quaternion.Euler(0, h.Heading, 0);
                h.Reel.localRotation = Quaternion.Euler(Time.time * 400f, 0, 0);

                foreach (var p in new List<Potato>(Alive))
                {
                    var local = h.T.InverseTransformPoint(p.transform.position);
                    if (Mathf.Abs(local.x) < HalfW && local.z > -HalfL && local.z < HalfL + 0.3f && local.y < 2.5f)
                    {
                        Effects.Explosion(Root, p.transform.position, 0.8f);
                        Fx.MudSplat(Root, p.transform.position, Shapes.Hex("#7A5B35"));
                        Fx.Splash(Root, p.transform.position + Vector3.up * 0.5f, Shapes.Hex("#E8C35A"), 1.2f, 26);
                        GM.Camera.Shake(0.3f);
                        Eliminate(p, false, " wpada pod kombajn!");
                    }
                }
                for (int i = stalks.Count - 1; i >= 0; i--)
                {
                    var s = stalks[i];
                    if (s == null) { stalks.RemoveAt(i); continue; }
                    var local = h.T.InverseTransformPoint(s.position);
                    if (Mathf.Abs(local.x) < HalfW + 0.2f && Mathf.Abs(local.z) < HalfL + 0.5f)
                    {
                        Object.Destroy(s.gameObject);
                        stalks.RemoveAt(i);
                    }
                }
            }
        }

        Potato NearestTo(Vector3 pos)
        {
            Potato best = null;
            float bd = float.MaxValue;
            foreach (var p in Alive)
            {
                float d = FlatDist(p.transform.position, pos);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        public override string HudInfo()
        {
            return base.HudInfo() + "   ·   W grze: " + AliveCount;
        }

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            Harvester danger = null;
            float bd = float.MaxValue;
            foreach (var h in harvesters)
            {
                float d = FlatDist(pos, h.T.position);
                if (d < bd) { bd = d; danger = h; }
            }
            if (danger == null || bd > 9f) return BotCommand.Go(Wander(p, Half - 3f, 2f));

            var local = danger.T.InverseTransformPoint(pos);
            var right = danger.T.right;
            var fwd = danger.T.forward;
            Vector3 flee;
            if (local.z > 0f && Mathf.Abs(local.x) < HalfW + 1.5f)
            {
                // jest przed kombajnem: uciekaj w bok (w stronę, gdzie jest więcej miejsca)
                float side = Mathf.Abs(local.x) > 0.3f ? Mathf.Sign(local.x) : (Vector3.Dot(right, -pos) > 0 ? 1f : -1f);
                flee = right * side * 1.2f + fwd * 0.3f;
            }
            else
            {
                var away = pos - danger.T.position;
                away.y = 0;
                flee = away.normalized;
            }
            var target = pos + flee.normalized * 4f;
            // nie wbijaj się w róg: ciągnij lekko do środka
            target = Vector3.Lerp(target, Vector3.zero, Mathf.Clamp01((Mathf.Max(Mathf.Abs(pos.x), Mathf.Abs(pos.z)) - (Half - 4f)) / 3f) * 0.6f);
            target.x = Mathf.Clamp(target.x, -Half + 1.2f, Half - 1.2f);
            target.z = Mathf.Clamp(target.z, -Half + 1.2f, Half - 1.2f);
            bool dash = bd < 3.8f && local.z > 0f && p.DashCooldownLeft <= 0f && Random.value < 0.5f;
            return BotCommand.Go(target, dash);
        }

        public override void Cleanup() { harvesters.Clear(); stalks.Clear(); }
    }
}
