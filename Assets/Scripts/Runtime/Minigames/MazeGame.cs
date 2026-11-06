using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Labirynt w Ogrodzie: żywopłotowy labirynt, za każdym razem inny.
    /// Wejście na dole, wyjście na górze. Kto pierwszy wybiegnie z labiryntu, wygrywa.
    /// </summary>
    public class MazeGame : RaceGame
    {
        public override string Title { get { return "Labirynt w Ogrodzie"; } }
        public override string Description { get { return "Znajdź drogę przez żywopłotowy labirynt! Wyjście jest na górze. Kolorowe ściany to drzwi: niektóre otwierają skrót, a inne tylko cię odbiją. Za każdym razem labirynt jest inny."; } }
        public override string Tip { get { return "Kolorowe ściany to drzwi. Część z nich otwiera skrót!"; } }
        public override string Goal { get { return "Pierwszy przy wyjściu wygrywa!"; } }
        public override MapId PreferredMap { get { return MapId.Garden; } }
        public override float TimeLimit { get { return 120f; } }
        public override float ArenaRadius { get { return 18f; } }
        public override string Controls { get { return "Ruch · skok · akcja = zryw (szybszy bieg)"; } }
        protected override float Grace { get { return 20f; } }

        const int N = 9;
        const float Cell = 3f;
        const float Wall = 0.5f, WallH = 1.7f;
        float Half { get { return N * Cell * 0.5f; } }
        int Exit { get { return N / 2; } }

        bool[,] north; // ściana między (x,z) a (x,z+1)
        bool[,] east;  // ściana między (x,z) a (x+1,z)
        int[,] dist;   // odległość (w polach) do wyjścia
        int maxDist;

        public override void BuildArena()
        {
            Generate();
            float floorLen = N * Cell + 10f;
            Shapes.Box(Root, new Vector3(0, -0.5f, 0), new Vector3(N * Cell + 2f, 1f, floorLen), Map.Floor, true);
            // ścieżki (jaśniejsze pasy w komórkach)
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                    if ((x + z) % 2 == 0)
                        Shapes.Box(Root, CellCenter(x, z) + Vector3.up * 0.005f, new Vector3(Cell - Wall, 0.01f, Cell - Wall), Map.FloorAlt, false);

            var hedge = Map.Wall;
            var hedgeAlt = Color.Lerp(Map.Wall, Color.black, 0.12f);
            // brzegi
            for (int x = 0; x < N; x++)
            {
                if (x != Exit)
                {
                    HedgeX(x, -Half, hedge);
                    HedgeX(x, Half, hedge);
                }
            }
            for (int z = 0; z < N; z++)
            {
                HedgeZ(-Half, z, hedgeAlt);
                HedgeZ(Half, z, hedgeAlt);
            }
            // środek (część ścian to drzwi-zagadki: prawdziwe otwierają skrót, fałszywe odbijają)
            Color[] doorColors = { Shapes.Hex("#FF6F91"), Shapes.Hex("#8E6CEF"), Shapes.Hex("#2EC4B6"), Shapes.Hex("#FFD23F") };
            int doors = 0;
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    if (z < N - 1 && north[x, z])
                    {
                        var w = HedgeX(x, -Half + (z + 1) * Cell, (x + z) % 3 == 0 ? hedgeAlt : hedge);
                        if (doors < 8 && z > 0 && Random.value < 0.12f) { MakeDoor(w, doorColors[doors % 4]); doors++; }
                    }
                    if (x < N - 1 && east[x, z])
                    {
                        var w = HedgeZ(-Half + (x + 1) * Cell, z, (x + z) % 3 == 1 ? hedgeAlt : hedge);
                        if (doors < 8 && z > 0 && Random.value < 0.12f) { MakeDoor(w, doorColors[doors % 4]); doors++; }
                    }
                }
            // słupki w narożnikach, żeby ściany ładnie się łączyły
            for (int x = 0; x <= N; x++)
                for (int z = 0; z <= N; z++)
                    Shapes.Box(Root, new Vector3(-Half + x * Cell, WallH * 0.5f, -Half + z * Cell), new Vector3(Wall + 0.05f, WallH + 0.05f, Wall + 0.05f), hedge, true);

            // boczne płotki przy strefie startu i mety
            for (int side = -1; side <= 1; side += 2)
            {
                Shapes.Box(Root, new Vector3((Half + 0.6f) * side, 0.5f, -Half - 2.5f), new Vector3(0.4f, 1f, 5f), Map.Accent, true);
                Shapes.Box(Root, new Vector3((Half + 0.6f) * side, 0.5f, Half + 2.5f), new Vector3(0.4f, 1f, 5f), Map.Accent, true);
            }
            Shapes.Box(Root, new Vector3(0, 0.5f, -Half - 5f), new Vector3(N * Cell + 1.6f, 1f, 0.4f), Map.Accent, true);
            Shapes.Box(Root, new Vector3(0, 0.5f, Half + 5f), new Vector3(N * Cell + 1.6f, 1f, 0.4f), Map.Accent, true);

            // brama wyjścia
            var gate = Shapes.Empty("Wyjscie", Root, new Vector3(CellCenter(Exit, N - 1).x, 0, Half)).transform;
            for (int s = -1; s <= 1; s += 2)
                Shapes.Box(gate, new Vector3(Cell * 0.5f * s, 1.6f, 0), new Vector3(0.4f, 3.2f, 0.4f), Map.Accent, false);
            Shapes.Box(gate, new Vector3(0, 3.3f, 0), new Vector3(Cell + 0.6f, 0.4f, 0.4f), Map.Accent, false);
            for (int i = 0; i < 6; i++)
                Shapes.Box(gate, new Vector3(-Cell * 0.5f + 0.25f + i * 0.5f, 0.01f, 0.6f), new Vector3(0.5f, 0.02f, 0.6f), i % 2 == 0 ? Color.white : Color.black, false);
        }

        void MakeDoor(GameObject wall, Color c)
        {
            wall.name = "Drzwi";
            wall.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(c, 0.6f);
            var fd = wall.AddComponent<FakeDoor>();
            fd.Real = Random.value < 0.5f;
            fd.Sink = true;
            // drzwi są trochę niższe od żywopłotu, żeby rzucały się w oczy
            var s = wall.transform.localScale;
            wall.transform.localScale = new Vector3(s.x, WallH * 0.85f, s.z);
            wall.transform.localPosition = new Vector3(wall.transform.localPosition.x, WallH * 0.425f, wall.transform.localPosition.z);
            Shapes.RoundCorners(wall, wall.transform.localScale);
        }

        GameObject HedgeX(int x, float z, Color c)
        {
            return Shapes.Box(Root, new Vector3(-Half + (x + 0.5f) * Cell, WallH * 0.5f, z), new Vector3(Cell, WallH, Wall), c, true);
        }

        GameObject HedgeZ(float x, int z, Color c)
        {
            return Shapes.Box(Root, new Vector3(x, WallH * 0.5f, -Half + (z + 0.5f) * Cell), new Vector3(Wall, WallH, Cell), c, true);
        }

        Vector3 CellCenter(int x, int z)
        {
            return new Vector3(-Half + (x + 0.5f) * Cell, 0, -Half + (z + 0.5f) * Cell);
        }

        bool CellOf(Vector3 pos, out int x, out int z)
        {
            x = Mathf.FloorToInt((pos.x + Half) / Cell);
            z = Mathf.FloorToInt((pos.z + Half) / Cell);
            return x >= 0 && z >= 0 && x < N && z < N;
        }

        // ---------------- Generowanie labiryntu ----------------

        void Generate()
        {
            north = new bool[N, N];
            east = new bool[N, N];
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++) { north[x, z] = true; east[x, z] = true; }

            var rng = new System.Random(Random.Range(0, int.MaxValue));
            var visited = new bool[N, N];
            var stack = new Stack<Vector2Int>();
            var start = new Vector2Int(Exit, 0);
            visited[start.x, start.y] = true;
            stack.Push(start);
            var dirs = new List<Vector2Int>();
            while (stack.Count > 0)
            {
                var c = stack.Peek();
                dirs.Clear();
                foreach (var d in Steps)
                {
                    var n = c + d;
                    if (n.x >= 0 && n.y >= 0 && n.x < N && n.y < N && !visited[n.x, n.y]) dirs.Add(d);
                }
                if (dirs.Count == 0) { stack.Pop(); continue; }
                var step = dirs[rng.Next(dirs.Count)];
                var next = c + step;
                SetWall(c, step, false);
                visited[next.x, next.y] = true;
                stack.Push(next);
            }
            // kilka dodatkowych przejść, żeby było więcej dróg (i mniej ślepych zaułków)
            for (int i = 0; i < N * 2; i++)
            {
                var c = new Vector2Int(rng.Next(N), rng.Next(N));
                var step = Steps[rng.Next(4)];
                var n = c + step;
                if (n.x >= 0 && n.y >= 0 && n.x < N && n.y < N) SetWall(c, step, false);
            }

            // odległości od wyjścia (BFS)
            dist = new int[N, N];
            for (int x = 0; x < N; x++) for (int z = 0; z < N; z++) dist[x, z] = int.MaxValue;
            var q = new Queue<Vector2Int>();
            dist[Exit, N - 1] = 0;
            q.Enqueue(new Vector2Int(Exit, N - 1));
            maxDist = 0;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in Steps)
                {
                    var n = c + d;
                    if (n.x < 0 || n.y < 0 || n.x >= N || n.y >= N || Blocked(c, d)) continue;
                    if (dist[n.x, n.y] != int.MaxValue) continue;
                    dist[n.x, n.y] = dist[c.x, c.y] + 1;
                    maxDist = Mathf.Max(maxDist, dist[n.x, n.y]);
                    q.Enqueue(n);
                }
            }
        }

        static readonly Vector2Int[] Steps = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };

        void SetWall(Vector2Int c, Vector2Int d, bool on)
        {
            if (d.y == 1) north[c.x, c.y] = on;
            else if (d.y == -1) north[c.x, c.y - 1] = on;
            else if (d.x == 1) east[c.x, c.y] = on;
            else east[c.x - 1, c.y] = on;
        }

        bool Blocked(Vector2Int c, Vector2Int d)
        {
            if (d.y == 1) return north[c.x, c.y];
            if (d.y == -1) return north[c.x, c.y - 1];
            if (d.x == 1) return east[c.x, c.y];
            return east[c.x - 1, c.y];
        }

        // ---------------- Zasady ----------------

        protected override void ConfigurePlayer(Potato p)
        {
            // boty znają drogę, więc są trochę wolniejsze, żeby ludzie mieli szansę
            if (p.Slot != null && p.Slot.IsBot) p.SpeedMultiplier = 0.85f;
        }

        public override Vector3 SpawnPoint(int i, int n)
        {
            float x = (i - (n - 1) * 0.5f) * 1.3f;
            return new Vector3(x, Potato.HalfHeight + 0.1f, -Half - 2.5f);
        }

        protected override Vector3 RespawnPoint(Potato p)
        {
            return new Vector3(Random.Range(-2f, 2f), 3f, -Half - 2.5f);
        }

        protected override float Progress(Potato p)
        {
            var pos = p.transform.position;
            if (pos.z > Half) return (maxDist + 2) * Cell;
            int x, z;
            if (!CellOf(pos, out x, out z) || dist[x, z] == int.MaxValue) return 0f;
            return (maxDist + 1 - dist[x, z]) * Cell;
        }

        protected override bool CrossedFinish(Potato p) { return p.transform.position.z > Half + 0.8f; }

        public override string StatFor(Potato p)
        {
            var s = base.StatFor(p);
            if (s != "") return s;
            int x, z;
            if (CellOf(p.transform.position, out x, out z) && dist[x, z] != int.MaxValue) return "do wyjścia: " + (dist[x, z] + 1);
            return "";
        }

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            int x, z;
            if (!CellOf(pos, out x, out z))
            {
                if (pos.z < -Half) return BotCommand.Go(CellCenter(Exit, 0) + Vector3.back * 0.5f);
                return BotCommand.Go(new Vector3(CellCenter(Exit, N - 1).x, 0, Half + 3f));
            }
            if (x == Exit && z == N - 1) return BotCommand.Go(new Vector3(CellCenter(Exit, N - 1).x, 0, Half + 3f));

            // Krok do sąsiada bliżej wyjścia. Czasem bot się "zamyśla" i skręca nie tam (rzadko).
            var c = new Vector2Int(x, z);
            Vector2Int best = Vector2Int.zero;
            int bestD = int.MaxValue;
            foreach (var d in Steps)
            {
                var n = c + d;
                if (n.x < 0 || n.y < 0 || n.x >= N || n.y >= N || Blocked(c, d)) continue;
                if (dist[n.x, n.y] < bestD) { bestD = dist[n.x, n.y]; best = d; }
            }
            Vector3 g;
            if (TryGetBotGoal(p, out g)) return BotCommand.Go(g);
            if (Random.value < 0.004f)
            {
                var d = Steps[Random.Range(0, 4)];
                if (!Blocked(c, d) && (c + d).x >= 0 && (c + d).y >= 0 && (c + d).x < N && (c + d).y < N)
                {
                    SetBotGoal(p, CellCenter(c.x + d.x, c.y + d.y), 1.2f);
                }
            }

            var center = CellCenter(x, z);
            var dir = new Vector3(best.x, 0, best.y);
            var off = pos - center;
            off.y = 0;
            float lateral = Vector3.Dot(off, new Vector3(dir.z, 0, -dir.x));
            if (Mathf.Abs(lateral) > 0.55f) return BotCommand.Go(center + dir * 0.4f);
            return BotCommand.Go(CellCenter(x + best.x, z + best.y));
        }
    }
}
