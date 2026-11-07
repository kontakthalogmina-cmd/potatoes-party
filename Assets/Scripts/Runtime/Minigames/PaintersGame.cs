using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Malarze: każdy ziemniak maluje pola, po których chodzi, na swój kolor.
    /// Można zamalować pola innych. Po czasie wygrywa ten, kto ma najwięcej pól.
    /// </summary>
    public class PaintersGame : Minigame
    {
        public override string Title { get { return "Malarze"; } }
        public override string Description { get { return "Biegaj po placu zabaw i maluj pola na swój kolor. Zbieraj bomby z farbą (malują duży kawałek naraz), zamalowuj pola rywali i odpychaj ich zrywem! Lider nosi koronę."; } }
        public override string Tip { get { return "Bomba z farbą maluje duży kawałek planszy naraz. Biegnij po nią!"; } }
        public override string Goal { get { return "Najwięcej pomalowanych pól wygrywa!"; } }
        public override MapId PreferredMap { get { return MapId.Playground; } }
        public override float TimeLimit { get { return 45f; } }
        public override float ArenaRadius { get { return 9f; } }
        public override bool FallEliminates { get { return false; } }
        public override bool EndsWhenOneLeft { get { return false; } }
        public override string Controls { get { return "Ruch · skok · akcja = zryw (odepchnij malarza!)"; } }
        public override bool ShowLeaderCrown { get { return true; } }

        const int Grid = 12;
        const float Size = 1.5f;
        float Half { get { return Grid * Size * 0.5f; } }

        Renderer[,] tiles;
        Potato[,] owner;
        Transform paintBomb;
        float bombTimer = 5f;

        public override void BuildArena()
        {
            tiles = new Renderer[Grid, Grid];
            owner = new Potato[Grid, Grid];
            Shapes.Box(Root, new Vector3(0, -0.5f, 0), new Vector3(Half * 2f, 1f, Half * 2f), Map.Wall, true);
            for (int x = 0; x < Grid; x++)
                for (int z = 0; z < Grid; z++)
                {
                    var t = Shapes.Box(Root, TileCenter(x, z) + Vector3.up * 0.01f, new Vector3(Size - 0.08f, 0.02f, Size - 0.08f), (x + z) % 2 == 0 ? Map.Floor : Map.FloorAlt, false);
                    t.name = "Pole";
                    tiles[x, z] = t.GetComponent<Renderer>();
                }
            // niski murek, żeby nikt nie wypadł
            for (int s = 0; s < 4; s++)
            {
                var rot = Quaternion.Euler(0, s * 90f, 0);
                var w = Shapes.Box(Root, rot * new Vector3(0, 0.5f, Half + 0.3f), new Vector3(Half * 2f + 1.2f, 1f, 0.6f), s % 2 == 0 ? Map.Accent : Map.Wall, true);
                w.transform.localRotation = rot;
            }
        }

        Vector3 TileCenter(int x, int z)
        {
            return new Vector3(-Half + Size * (x + 0.5f), 0, -Half + Size * (z + 0.5f));
        }

        bool TileAt(Vector3 pos, out int x, out int z)
        {
            x = Mathf.FloorToInt((pos.x + Half) / Size);
            z = Mathf.FloorToInt((pos.z + Half) / Size);
            return x >= 0 && z >= 0 && x < Grid && z < Grid;
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
                if (!p.Grounded) continue;
                int x, z;
                if (!TileAt(p.transform.position, out x, out z)) continue;
                if (owner[x, z] != p && p.Surface != null)
                {
                    // malując podłogę, ziemniak brudzi sobie stopy własną farbą
                    p.Surface.SplashMud(p.transform.position + Vector3.down * 2f, 0.03f, Color.Lerp(p.Color, Color.white, 0.15f));
                }
                Paint(p, x, z);
            }

            // bomby z farbą
            bombTimer -= dt;
            if (paintBomb == null && bombTimer <= 0f)
            {
                var pos = TileCenter(Random.Range(1, Grid - 1), Random.Range(1, Grid - 1));
                paintBomb = Shapes.Empty("Bomba z farba", Root, pos + Vector3.up * 0.7f).transform;
                Shapes.Prim(PrimitiveType.Sphere, paintBomb, Vector3.zero, Vector3.one * 0.8f, Shapes.Hex("#8E6CEF"), false, 0.8f);
                Shapes.Prim(PrimitiveType.Sphere, paintBomb, new Vector3(0.18f, 0.2f, -0.2f), Vector3.one * 0.25f, Shapes.Hex("#FF6F91"), false, 0.8f);
                Shapes.Prim(PrimitiveType.Sphere, paintBomb, new Vector3(-0.2f, 0.15f, 0.15f), Vector3.one * 0.22f, Shapes.Hex("#2EC4B6"), false, 0.8f);
                paintBomb.gameObject.AddComponent<Spinner>();
                paintBomb.gameObject.AddComponent<Bobber>();
            }
            if (paintBomb != null)
            {
                foreach (var p in Alive)
                {
                    if (FlatDist(p.transform.position, paintBomb.position) > 1f) continue;
                    int cx, cz;
                    TileAt(paintBomb.position, out cx, out cz);
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dz = -2; dz <= 2; dz++)
                        {
                            int x = cx + dx, z = cz + dz;
                            if (x >= 0 && z >= 0 && x < Grid && z < Grid && dx * dx + dz * dz <= 5) Paint(p, x, z);
                        }
                    Fx.Splash(Root, paintBomb.position, p.Color, 1.8f, 50);
                    Fx.Sparkle(Root, paintBomb.position, p.Color, 1.5f);
                    foreach (var o in Alive)
                    {
                        if (o.Surface == null) continue;
                        float dist = FlatDist(o.transform.position, paintBomb.position);
                        if (dist < 4.5f) o.Surface.SplashMud(paintBomb.position, Mathf.Lerp(0.8f, 0.2f, dist / 4.5f), Color.Lerp(p.Color, Color.white, 0.15f));
                    }
                    AudioManager.Play(Sfx.Explosion, 0.6f, 0.2f);
                    GM.Hud.Toast(p.Slot.Name + " rozlewa farbę!", p.Color);
                    Object.Destroy(paintBomb.gameObject);
                    paintBomb = null;
                    bombTimer = Random.Range(5f, 8f);
                    break;
                }
            }
        }

        void Paint(Potato p, int x, int z)
        {
            var old = owner[x, z];
            if (old == p) return;
            if (old != null) Score[old] = ScoreOf(old) - 1;
            owner[x, z] = p;
            Score[p] = ScoreOf(p) + 1;
            tiles[x, z].sharedMaterial = Shapes.Mat(Color.Lerp(p.Color, Color.white, 0.15f), 0.4f);
        }

        public override string StatFor(Potato p) { return ScoreOf(p) + " pól"; }

        public override string HudInfo()
        {
            Potato best = null;
            foreach (var p in Players) if (p != null && (best == null || ScoreOf(p) > ScoreOf(best))) best = p;
            return base.HudInfo() + (best != null && ScoreOf(best) > 0 ? "   ·   Prowadzi: " + best.Slot.Name : "");
        }

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            if (paintBomb != null && FlatDist(pos, paintBomb.position) < 6f) return BotCommand.Go(paintBomb.position);
            Vector3 goal;
            int cx, cz;
            bool onOwn = TileAt(pos, out cx, out cz) && owner[cx, cz] == p;
            if (!TryGetBotGoal(p, out goal) || (onOwn && FlatDist(pos, goal) < 0.6f))
            {
                // Najbliższe pole, które nie jest moje (z odrobiną losowości)
                float bestD = float.MaxValue;
                goal = Vector3.zero;
                for (int i = 0; i < 25; i++)
                {
                    int x = Random.Range(0, Grid), z = Random.Range(0, Grid);
                    if (owner[x, z] == p) continue;
                    var c = TileCenter(x, z);
                    float d = FlatDist(pos, c) - (owner[x, z] != null ? 1.5f : 0f);
                    if (d < bestD) { bestD = d; goal = c; }
                }
                SetBotGoal(p, goal, 1.5f);
            }
            var rival = Nearest(p);
            bool dash = rival != null && FlatDist(pos, rival.transform.position) < 2f && p.DashCooldownLeft <= 0f && Random.value < 0.08f;
            if (dash) return BotCommand.Go(rival.transform.position, true);
            return BotCommand.Go(goal);
        }
    }
}
