using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Król Polany: na leśnej polanie świeci złoty krąg. Kto stoi w nim SAM, zdobywa punkty.
    /// Krąg co jakiś czas się przenosi. Spadnięcie z polany nie eliminuje, ale traci się czas.
    /// </summary>
    public class KingOfTheHillGame : Minigame
    {
        public override string Title { get { return "Król Polany"; } }
        public override string Description { get { return "Stań w złotym kręgu i zostań w nim SAM, żeby zbierać punkty. Wypychaj intruzów zrywem! Krąg przenosi się coraz częściej, a jego następne miejsce zdradza mrugający cień."; } }
        public override string Tip { get { return "Mrugający cień pokazuje, gdzie krąg skoczy. Bądź tam pierwszy!"; } }
        public override string Goal { get { return "Najdłużej sam w kręgu wygrywa!"; } }
        public override MapId PreferredMap { get { return MapId.Forest; } }
        public override float TimeLimit { get { return 60f; } }
        public override float ArenaRadius { get { return 10f; } }
        public override bool FallEliminates { get { return false; } }
        public override bool EndsWhenOneLeft { get { return false; } }
        public override string Controls { get { return "Ruch · skok · akcja = zryw (wypchnij z kręgu!)"; } }
        public override bool ShowLeaderCrown { get { return true; } }

        const float ZoneRadius = 2.3f;
        const float MoveEvery = 15f;
        static readonly Vector3[] Spots = { Vector3.zero, new Vector3(5f, 0, 2f), new Vector3(-4f, 0, 4.5f), new Vector3(-3f, 0, -5f), new Vector3(4f, 0, -4f) };

        readonly Dictionary<Potato, float> held = new Dictionary<Potato, float>();
        Transform zone;
        Material zoneMat;
        Vector3 zoneTarget;
        int spot, nextSpot = -1;
        float moveTimer = MoveEvery;
        Transform ghost;
        Potato king;

        public override void BuildArena()
        {
            MapBuilder.RoundArena(Root, ArenaRadius, Map);
            // pieńki jako przeszkody (z kolizją)
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + 0.3f;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 7.2f;
                var stump = MapBuilder.Disc(Root, p + Vector3.up * 0.4f, 0.55f, 0.8f, Shapes.Hex("#8A5A34"));
                stump.name = "Pienek";
                MapBuilder.Disc(Root, p + Vector3.up * 0.81f, 0.45f, 0.02f, Shapes.Hex("#E2C08D"), false);
            }
            zone = MapBuilder.Disc(Root, new Vector3(0, 0.03f, 0), ZoneRadius, 0.04f, Color.yellow, false).transform;
            zone.name = "Krag";
            zoneMat = Shapes.UniqueMat(new Color(1f, 0.85f, 0.2f), 0.6f);
            zone.GetComponent<Renderer>().sharedMaterial = zoneMat;
            // korona nad kręgiem
            var crown = Shapes.Empty("Korona", zone, new Vector3(0, 60f, 0)).transform;
            crown.localScale = new Vector3(1f / (ZoneRadius * 2f), 1f / 0.02f, 1f / (ZoneRadius * 2f));
            Shapes.Prim(PrimitiveType.Cylinder, crown, Vector3.zero, new Vector3(0.9f, 0.2f, 0.9f), Shapes.Hex("#FFD23F"), false, 0.8f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Shapes.Prim(PrimitiveType.Sphere, crown, new Vector3(Mathf.Cos(a) * 0.4f, 0.3f, Mathf.Sin(a) * 0.4f), Vector3.one * 0.22f, Shapes.Hex("#FFD23F"), false, 0.8f);
            }
            crown.gameObject.AddComponent<Spinner>();
            crown.gameObject.AddComponent<Bobber>().Amplitude = 12f; // w skali krążka (0.02) to ok. 0.25 m
            zoneTarget = Spots[0];
            ghost = MapBuilder.Disc(Root, new Vector3(0, -50f, 0), ZoneRadius, 0.02f, new Color(1f, 0.95f, 0.7f), false).transform;
            ghost.name = "Nastepny krag";
        }

        public override Vector3 SpawnPoint(int i, int n)
        {
            float a = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f + Mathf.PI * 0.5f;
            return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 5.5f + Vector3.up * (Potato.HalfHeight + 0.1f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished || !Running) return;

            moveTimer -= dt;
            if (moveTimer < 3f && nextSpot < 0)
            {
                nextSpot = (spot + Random.Range(1, Spots.Length)) % Spots.Length;
                ghost.localPosition = Spots[nextSpot] + Vector3.up * 0.02f;
            }
            if (nextSpot >= 0) ghost.gameObject.SetActive(Mathf.Sin(Time.time * 14f) > 0f);
            if (moveTimer <= 0f)
            {
                // im dalej w grze, tym częściej krąg skacze
                moveTimer = Mathf.Lerp(MoveEvery, 8f, 1f - TimeLeft / TimeLimit);
                spot = nextSpot >= 0 ? nextSpot : (spot + 1) % Spots.Length;
                nextSpot = -1;
                ghost.localPosition = new Vector3(0, -50f, 0);
                zoneTarget = Spots[spot];
                GM.Hud.Toast("Krąg się przenosi!", new Color(1f, 0.85f, 0.2f));
                AudioManager.Play(Sfx.Whoosh, 0.7f);
            }
            zone.localPosition = Vector3.MoveTowards(zone.localPosition, zoneTarget + Vector3.up * 0.03f, dt * 6f);

            Potato inside = null;
            int count = 0;
            foreach (var p in Alive)
            {
                if (FlatDist(p.transform.position, zone.position) < ZoneRadius && p.transform.position.y < 2.5f)
                {
                    inside = p;
                    count++;
                }
            }
            var newKing = count == 1 ? inside : null;
            if (newKing != null)
            {
                float h;
                held.TryGetValue(newKing, out h);
                h += dt;
                held[newKing] = h;
                Score[newKing] = Mathf.FloorToInt(h);
                if (newKing != king) AudioManager.Play(Sfx.Coin, 0.5f);
            }
            king = newKing;
            Shapes.SetColor(zoneMat, count == 0 ? new Color(1f, 0.85f, 0.2f) : count == 1 ? Color.Lerp(inside.Color, Color.white, 0.25f) : new Color(0.6f, 0.6f, 0.6f));
        }

        public override string StatFor(Potato p) { return ScoreOf(p) + " s" + (p == king ? " (KRÓL)" : ""); }

        public override string HudInfo()
        {
            string s = base.HudInfo();
            s += king != null ? "   ·   W kręgu: " + king.Slot.Name : "   ·   Krąg wolny!";
            return s;
        }

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            var z = zone.position;
            var rival = Nearest(p, o => FlatDist(o.transform.position, z) < ZoneRadius + 1f);
            if (rival != null && FlatDist(pos, rival.transform.position) < 3f)
            {
                bool dash = p.DashCooldownLeft <= 0f && Random.value < 0.15f;
                // ustaw się tak, by pchać rywala od środka kręgu na zewnątrz
                var away = rival.transform.position - z;
                away.y = 0;
                var aim = rival.transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 0.5f;
                return BotCommand.Go(aim, dash);
            }
            // sprytne boty biegną już do następnego kręgu
            if (nextSpot >= 0 && p.Slot.Index % 2 == 1) return BotCommand.Go(Spots[nextSpot]);
            var jitter = Wander(p, 1.2f, 1f);
            return BotCommand.Go(new Vector3(z.x, 0, z.z) + jitter * 0.5f);
        }
    }
}
