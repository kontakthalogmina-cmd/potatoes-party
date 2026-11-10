using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Wspólna baza minigier: arena, spadanie, eliminacje, punktacja, ranking i pomoc dla botów.
    /// Każda minigra dopisuje tylko swoje zasady.
    /// </summary>
    /// <summary>Widok "żywi gracze" bez alokacji (zobacz <see cref="Minigame.Alive"/>).</summary>
    public struct AliveSet : IEnumerable<Potato>
    {
        readonly List<Potato> list;
        public AliveSet(List<Potato> list) { this.list = list; }

        public Enumerator GetEnumerator() { return new Enumerator(list); }
        IEnumerator<Potato> IEnumerable<Potato>.GetEnumerator() { return GetEnumerator(); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }

        public struct Enumerator : IEnumerator<Potato>
        {
            readonly List<Potato> list;
            int index;

            public Enumerator(List<Potato> list) { this.list = list; index = -1; }

            public Potato Current { get { return list[index]; } }
            object System.Collections.IEnumerator.Current { get { return Current; } }

            public bool MoveNext()
            {
                if (list == null) return false;
                while (++index < list.Count)
                {
                    var p = list[index];
                    if (p != null && !p.Eliminated) return true;
                }
                return false;
            }

            public void Reset() { index = -1; }
            public void Dispose() { }
        }
    }

    public abstract class Minigame
    {
        public abstract string Title { get; }
        public abstract string Description { get; }
        public abstract MapId PreferredMap { get; }
        public virtual string Goal { get { return "Ostatni na arenie wygrywa!"; } }
        /// <summary>Krótka wskazówka na ekran ładowania.</summary>
        public virtual string Tip { get { return "Zryw (akcja) odpycha innych ziemniaków."; } }
        public virtual float TimeLimit { get { return 90f; } }
        public virtual bool FallEliminates { get { return true; } }
        public virtual bool EndsWhenOneLeft { get { return true; } }
        public virtual float ArenaRadius { get { return 10f; } }
        public virtual float KillY { get { return -2.5f; } }
        /// <summary>Jak mocno kamera trzyma się środka areny (0 = tylko gracze, 1 = tylko arena).</summary>
        public virtual float CameraPull { get { return 0.1f; } }
        public virtual Vector3 ArenaCenter { get { return Vector3.zero; } }
        public virtual string Controls { get { return "Ruch · skok · akcja = zryw (popchnij innych!)"; } }
        /// <summary>Czy prowadzący gracz nosi koronę (cel dla innych!).</summary>
        public virtual bool ShowLeaderCrown { get { return false; } }

        protected GameManager GM;
        protected MapTheme Map;
        protected Transform Root;
        public List<Potato> Players = new List<Potato>();
        public readonly List<Potato> EliminatedOrder = new List<Potato>();
        public readonly Dictionary<Potato, int> Score = new Dictionary<Potato, int>();
        public float TimeLeft;
        public bool Finished;
        public bool Running;

        readonly Dictionary<Potato, Vector3> botGoal = new Dictionary<Potato, Vector3>();
        readonly Dictionary<Potato, float> botGoalTime = new Dictionary<Potato, float>();

        /// <summary>
        /// Żywi gracze. Struktura z własnym enumeratorem: `foreach (var p in Alive)` nie alokuje
        /// (wcześniej LINQ Where = enumerator + domknięcie na stercie przy każdym wywołaniu, kilka razy na klatkę).
        /// Nadal działa z LINQ (OrderBy, ElementAt, new List(Alive)) tam, gdzie to rzadkie.
        /// </summary>
        public AliveSet Alive { get { return new AliveSet(Players); } }

        public int AliveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Players.Count; i++)
                {
                    var p = Players[i];
                    if (p != null && !p.Eliminated) n++;
                }
                return n;
            }
        }

        public void Setup(GameManager gm, MapTheme map, Transform root)
        {
            GM = gm;
            Map = map;
            Root = root;
            TimeLeft = TimeLimit;
        }

        public void AddPlayers(List<Potato> players)
        {
            Players = players;
            foreach (var p in players)
            {
                Score[p] = 0;
                p.Grip = Map.Grip;
                p.BotBrain = Bot;
                p.CanDash = true;
                p.CanJump = true;
                p.SpeedMultiplier = 1f;
                p.KnockMultiplier = 1f;
                ConfigurePlayer(p);
            }
        }

        public abstract void BuildArena();
        protected virtual void ConfigurePlayer(Potato p) { }

        public virtual Vector3 SpawnPoint(int i, int n)
        {
            float a = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f + Mathf.PI * 0.5f;
            return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * ArenaRadius * 0.55f + Vector3.up * (Potato.HalfHeight + 0.1f);
        }

        public virtual void Begin() { Running = true; }

        public virtual void Tick(float dt)
        {
            if (Finished) return;
            TimeLeft -= dt;

            foreach (var p in Players)
            {
                if (p == null || p.Eliminated) continue;
                if (p.transform.position.y < KillY)
                {
                    SkinFx.OnFall(p, Root);
                    if (FallEliminates) Eliminate(p, true);
                    else Respawn(p);
                }
            }

            if (ShowLeaderCrown) UpdateCrown();

            if (EndsWhenOneLeft && AliveCount <= (Players.Count > 1 ? 1 : 0)) Finish();
            if (TimeLeft <= 0f) { TimeLeft = 0f; Finish(); }
        }

        Transform crown;
        Potato crowned;

        /// <summary>Korona nad głową gracza z najlepszym wynikiem (tylko gdy prowadzi sam).</summary>
        void UpdateCrown()
        {
            if (crown == null)
            {
                crown = Shapes.Empty("Korona lidera", Root, Vector3.up * -50f).transform;
                var gold = Shapes.Hex("#FFC61A");
                Shapes.Prim(PrimitiveType.Cylinder, crown, Vector3.zero, new Vector3(0.55f, 0.12f, 0.55f), gold, false, 0.9f);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2f / 5f;
                    Shapes.Prim(PrimitiveType.Sphere, crown, new Vector3(Mathf.Cos(a) * 0.24f, 0.2f, Mathf.Sin(a) * 0.24f), Vector3.one * 0.14f, gold, false, 0.9f);
                }
                Shapes.Prim(PrimitiveType.Sphere, crown, new Vector3(0, 0.12f, 0.27f), Vector3.one * 0.12f, Shapes.Hex("#E2463F"), false, 0.9f);
            }
            Potato best = null;
            int bestScore = 0;
            bool tie = false;
            foreach (var p in Players)
            {
                if (p == null || p.Eliminated) continue;
                int s = ScoreOf(p);
                if (s > bestScore) { best = p; bestScore = s; tie = false; }
                else if (s == bestScore && s > 0) tie = true;
            }
            if (tie) best = null;
            if (best != crowned && best != null) AudioManager.Play(Sfx.Coin, 0.4f, 0.05f);
            crowned = best;
            if (best == null) { crown.position = Vector3.up * -50f; return; }
            crown.position = best.HeadAnchor.position + Vector3.up * (0.05f + Mathf.Sin(Time.time * 4f) * 0.06f);
            crown.rotation = Quaternion.Euler(0, Time.time * 90f, 0);
        }

        protected void Finish()
        {
            if (Finished) return;
            Finished = true;
            Running = false;
            OnFinish();
        }

        protected virtual void OnFinish() { }

        protected void Respawn(Potato p)
        {
            AudioManager.Play(Sfx.Fall, 0.6f);
            p.Teleport(RespawnPoint(p));
        }

        protected virtual Vector3 RespawnPoint(Potato p)
        {
            var spot = Random.insideUnitCircle * ArenaRadius * 0.3f;
            return ArenaCenter + new Vector3(spot.x, 4f, spot.y);
        }

        public void Eliminate(Potato p, bool fell, string message = null)
        {
            if (p.Eliminated) return;
            p.Eliminated = true;
            EliminatedOrder.Add(p);
            AudioManager.Play(fell ? Sfx.Fall : Sfx.Explosion, 0.8f);
            Feel.Knockout(p);
            p.SetHidden(true);
            GM.Hud.Toast(p.Slot.Name + (message ?? (fell ? " odpada!" : " wylatuje w powietrze!")), p.Color);
            OnEliminated(p);
        }

        protected virtual void OnEliminated(Potato p) { }

        /// <summary>Ranking: grupy remisujących graczy, od najlepszych.</summary>
        public virtual List<List<Potato>> Ranking()
        {
            var result = new List<List<Potato>>();
            var alive = Alive.OrderByDescending(ScoreOf).ToList();
            foreach (var group in alive.GroupBy(ScoreOf).OrderByDescending(g => g.Key))
                result.Add(group.ToList());
            for (int i = EliminatedOrder.Count - 1; i >= 0; i--)
                result.Add(new List<Potato> { EliminatedOrder[i] });
            return result;
        }

        public int ScoreOf(Potato p)
        {
            int s;
            return p != null && Score.TryGetValue(p, out s) ? s : 0;
        }

        public virtual void OnBump(Potato hitter, Potato victim, bool dash) { }

        /// <summary>Krótka informacja na karcie gracza (np. liczba monet).</summary>
        public virtual string StatFor(Potato p) { return p.Eliminated ? "odpadł" : ""; }

        /// <summary>Tekst u góry ekranu (np. czas albo stan bomby).</summary>
        public virtual string HudInfo()
        {
            return ""; // czas pokazuje okrągły licznik w rogu ekranu
        }

        public virtual void Cleanup() { }

        /// <summary>Dodatkowe obiekty, które kamera ma trzymać w kadrze (np. piłka).</summary>
        public virtual IEnumerable<Transform> ExtraCameraTargets() { yield break; }

        // ---------------- Pomoc dla botów ----------------

        public abstract BotCommand Bot(Potato p);

        protected Potato Nearest(Potato from, System.Func<Potato, bool> filter = null)
        {
            Potato best = null;
            float bestD = float.MaxValue;
            foreach (var o in Players)
            {
                if (o == null || o == from || o.Eliminated) continue;
                if (filter != null && !filter(o)) continue;
                float d = (o.transform.position - from.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = o; }
            }
            return best;
        }

        protected static float FlatDist(Vector3 a, Vector3 b)
        {
            a.y = 0; b.y = 0;
            return Vector3.Distance(a, b);
        }

        protected Vector3 Clamp(Vector3 target, float radius)
        {
            var flat = new Vector3(target.x, 0, target.z);
            if (flat.magnitude > radius) flat = flat.normalized * radius;
            return flat;
        }

        /// <summary>Czy przed botem jest dziura (wtedy warto skoczyć)?</summary>
        protected bool GapAhead(Potato p, Vector3 target)
        {
            var dir = target - p.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) return false;
            var probe = p.transform.position + dir.normalized * 1.3f;
            return !Physics.Raycast(probe, Vector3.down, 3f, ~(1 << Potato.Layer), QueryTriggerInteraction.Ignore);
        }

        /// <summary>Losowy cel bota, zmieniany co jakiś czas.</summary>
        protected Vector3 Wander(Potato p, float radius, float every = 1.5f)
        {
            float t;
            if (!botGoalTime.TryGetValue(p, out t) || Time.time > t)
            {
                var c = Random.insideUnitCircle * radius;
                botGoal[p] = new Vector3(c.x, 0, c.y);
                botGoalTime[p] = Time.time + every * Random.Range(0.7f, 1.3f);
            }
            return botGoal[p];
        }

        protected void SetBotGoal(Potato p, Vector3 goal, float hold)
        {
            botGoal[p] = goal;
            botGoalTime[p] = Time.time + hold;
        }

        protected bool TryGetBotGoal(Potato p, out Vector3 goal)
        {
            float t;
            goal = Vector3.zero;
            if (botGoalTime.TryGetValue(p, out t) && Time.time < t)
            {
                goal = botGoal[p];
                return true;
            }
            return false;
        }

        public static Minigame Create(int type)
        {
            switch (type)
            {
                // 4 nowe gry na mapie "Ogród Babci" (stare 12 usunięte po playteście)
                case 0: return new DeepFryGame();
                case 1: return new TiltPanGame();
                case 2: return new MoleGame();
                default: return new TowerGame();
            }
        }

        public const int Count = 4;
    }
}
