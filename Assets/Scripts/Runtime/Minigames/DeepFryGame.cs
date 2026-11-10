using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// <b>Wielkie Smażenie</b> – ekonomia strzępków. Zryw w rywala odrywa z niego masę jako zbieralne strzępki;
    /// zebrane nosisz w wiaderku i wrzucasz do frytkownicy na dnie grządki (stań 0.6 s na pomoście).
    /// Hook: masa = rozmiar. Trafiony chudnie → szybszy, ale lżejszy (dalej lata). Zbierając, rośniesz i zwalniasz.
    /// Co 30 s frytkownica "prycha" olejem: wyrzuca stojących przy niej i wysypuje połowę wiaderka.
    /// </summary>
    public class DeepFryGame : Minigame
    {
        // ---------------- strojenie ----------------
        public static float MassMin = 0.3f, MassMax = 1.4f, MassPerShred = 0.02f;
        public static float LoseMin = 0.06f, LoseMax = 0.14f;
        public static int CarryMax = 20, ComboAt = 10;
        public static float DepositTime = 0.6f, DepositRadius = 3.4f;
        public static float SpitEvery = 30f, SpitWarn = 2f, SpitTime = 3f, SpitRadius = 4.2f;
        public static float WarmupTime = 5f, FinaleTime = 20f, FinaleSpitEvery = 12f;
        public static float GoldEvery = 15f, FinaleGoldEvery = 8f;
        public static int GoldValue = 5;

        public override string Title { get { return "Wielkie Smażenie"; } }
        public override string Description { get { return "Zrywem odrywaj z rywali strzępki, zbieraj je do wiaderka i wrzucaj do frytkownicy na dnie grządki. Uważaj: im mniej masy, tym jesteś szybszy, ale lżejszy – jeden cios i lecisz!"; } }
        public override string Goal { get { return "Najwięcej strzępków we frytkownicy po 2 minutach."; } }
        public override string Tip { get { return "Wrzut 10+ naraz liczy się ×1,5. Poluj na tego z pełnym wiaderkiem."; } }
        public override string Controls { get { return "Ruch · skok · zryw = oderwij strzępki · stój na pomoście = wrzut"; } }
        public override MapId PreferredMap { get { return MapId.Garden; } }
        public override float TimeLimit { get { return 120f; } }
        public override float ArenaRadius { get { return 22f; } }
        public override float KillY { get { return GardenMap.KillY; } }
        public override bool FallEliminates { get { return false; } }
        public override bool EndsWhenOneLeft { get { return false; } }
        public override bool ShowLeaderCrown { get { return true; } }
        public override float CameraPull { get { return 0.08f; } }

        GardenLayout L;
        readonly Dictionary<Potato, float> mass = new Dictionary<Potato, float>();
        readonly Dictionary<Potato, int> carried = new Dictionary<Potato, int>();
        readonly Dictionary<Potato, float> onDeck = new Dictionary<Potato, float>();
        readonly Dictionary<Potato, Transform> buckets = new Dictionary<Potato, Transform>();
        readonly List<Rigidbody> shreds = new List<Rigidbody>();
        readonly List<float> shredAge = new List<float>();
        readonly List<int> shredValue = new List<int>();
        float goldTimer = 6f;

        // ---------------- maszyna stanów (State pattern) ----------------
        public enum Phase { Warmup, Frying, SpitWarn, Spitting, Finale }

        /// <summary>Stan gry: Enter przy wejściu, Tick co klatkę zwraca stan następny (albo siebie).</summary>
        abstract class FryState
        {
            public abstract Phase Id { get; }
            public float Time;
            public virtual void Enter(DeepFryGame g) { }
            public abstract FryState Tick(DeepFryGame g, float dt);
        }

        /// <summary>5 s: frytkownica grzeje się (wrzut zablokowany), na dachu szopy i wieży lądują złote strzępki.</summary>
        sealed class WarmupState : FryState
        {
            public override Phase Id { get { return Phase.Warmup; } }
            public override void Enter(DeepFryGame g) { g.GM.Hud.Toast("Olej się grzeje – złote strzępki na dachu i wieży!", Shapes.Hex("#FFC61A")); g.SpawnGold(true); }
            public override FryState Tick(DeepFryGame g, float dt) { return Time >= WarmupTime ? (FryState)new FryingState() : this; }
        }

        /// <summary>Normalne smażenie: wrzut otwarty, licznik do prychnięcia.</summary>
        sealed class FryingState : FryState
        {
            public override Phase Id { get { return Phase.Frying; } }
            public override void Enter(DeepFryGame g) { g.spitTimer = g.finale ? FinaleSpitEvery : SpitEvery; }
            public override FryState Tick(DeepFryGame g, float dt)
            {
                g.spitTimer -= dt;
                if (!g.finale && g.TimeLeft <= FinaleTime) return new FinaleState();
                return g.spitTimer <= SpitWarn ? (FryState)new SpitWarnState() : this;
            }
        }

        /// <summary>2 s ostrzeżenia: bulgotanie, iskry, toast. Wrzut nadal działa (ryzyko!).</summary>
        sealed class SpitWarnState : FryState
        {
            public override Phase Id { get { return Phase.SpitWarn; } }
            public override void Enter(DeepFryGame g) { g.GM.Hud.Toast("Frytkownica zaraz PRYCHNIE!", Shapes.Hex("#FFB347")); AudioManager.Play(Sfx.Tick, 1f); }
            public override FryState Tick(DeepFryGame g, float dt)
            {
                if (Random.value < dt * 10f) Fx.Sparks(g.Root, g.Fryer + Vector3.up * 1.7f, Shapes.Hex("#FFB347"), 4, 3f);
                return Time >= SpitWarn ? (FryState)new SpittingState() : this;
            }
        }

        /// <summary>3 s prychania: wyrzut z pomostu, wysypanie połowy wiaderka, przypalenie. Wrzut zablokowany.</summary>
        sealed class SpittingState : FryState
        {
            public override Phase Id { get { return Phase.Spitting; } }
            public override void Enter(DeepFryGame g) { Feel.Explosion(g.Fryer, 0.6f); }
            public override FryState Tick(DeepFryGame g, float dt)
            {
                g.SpitBlast(dt);
                return Time >= SpitTime ? (FryState)new FryingState() : this;
            }
        }

        /// <summary>Ostatnie 20 s: wrzut liczy się ×2, prychnięcia co 12 s, złoto co 8 s.</summary>
        sealed class FinaleState : FryState
        {
            public override Phase Id { get { return Phase.Finale; } }
            public override void Enter(DeepFryGame g)
            {
                g.finale = true;
                g.GM.Hud.Toast("FINAŁ! Wrzut ×2, olej prycha co 12 s", Shapes.Hex("#FF5A3C"));
                AudioManager.Play(Sfx.Fanfare, 0.7f);
            }
            public override FryState Tick(DeepFryGame g, float dt) { return new FryingState(); }
        }

        FryState state;
        float spitTimer;
        bool finale;
        public Phase Current { get { return state != null ? state.Id : Phase.Warmup; } }
        bool DepositOpen { get { return state != null && (state.Id == Phase.Frying || state.Id == Phase.SpitWarn); } }

        void Go(FryState next)
        {
            if (next == state) return;
            state = next;
            state.Time = 0f;
            state.Enter(this);
        }

        Vector3 Fryer { get { return L.PitCenter; } }

        public override void BuildArena()
        {
            L = GardenMap.Build(Root, Map, GardenCenter.Fryer);
        }

        public override Vector3 SpawnPoint(int i, int n)
        {
            float a = i / (float)Mathf.Max(1, n) * Mathf.PI * 2f + 0.4f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 11f + Vector3.up * (Potato.HalfHeight + 0.1f);
        }

        protected override Vector3 RespawnPoint(Potato p)
        {
            float a = Random.value * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 12f + Vector3.up * 3f;
        }

        protected override void ConfigurePlayer(Potato p)
        {
            mass[p] = 1f;
            carried[p] = 0;
            onDeck[p] = 0f;
            // wiaderko nad głową: rośnie z liczbą niesionych strzępków
            var b = Shapes.Empty("Wiaderko", p.HeadAnchor, new Vector3(0, 0.25f, 0)).transform;
            Shapes.Prim(PrimitiveType.Cylinder, b, Vector3.zero, new Vector3(0.45f, 0.18f, 0.45f), Shapes.Hex("#C0C7D1"), false, 0.8f);
            var fill = Shapes.Prim(PrimitiveType.Sphere, b, new Vector3(0, 0.12f, 0), new Vector3(0.4f, 0.25f, 0.4f), Shapes.Hex("#FFD966"), false, 0.7f);
            fill.name = "Strzepki";
            b.gameObject.SetActive(false);
            buckets[p] = b;
            Apply(p);
        }

        // ---------------- masa ----------------

        void Apply(Potato p)
        {
            float m = mass[p];
            int c = carried[p];
            p.MoveSpeed = 7.5f * (1.35f - 0.35f * m) * (1f - 0.012f * c);
            p.KnockMultiplier = Mathf.Pow(m, -0.6f);
            if (p.Anim != null) p.Anim.VisualScale = 1.3f * (0.5f + 0.5f * m);
            var b = buckets[p];
            b.gameObject.SetActive(c > 0);
            if (c > 0) b.localScale = Vector3.one * (0.8f + 0.5f * c / (float)CarryMax);
        }

        public override void OnBump(Potato hitter, Potato victim, bool dash)
        {
            if (!dash || !mass.ContainsKey(victim)) return;
            var v = Phys.GetVelocity(hitter.Body); v.y = 0f;
            float power = Mathf.Clamp01(v.magnitude / 20f);
            float lose = Mathf.Min(mass[victim] - MassMin, Mathf.Lerp(LoseMin, LoseMax, power));
            var dir = victim.transform.position - hitter.transform.position; dir.y = 0f;
            int n = Mathf.Clamp(Mathf.RoundToInt(lose / MassPerShred * 0.5f), 0, 7);
            if (lose > 0f) mass[victim] -= lose;
            // ciężkie wiaderko: cios wysypuje ćwierć
            int spill = carried[victim] / 4;
            carried[victim] -= spill;
            Drop(victim.transform.position, dir, n + spill);
            Apply(victim);
            if (n + spill >= 6) GM.Hud.Toast(victim.Slot.Name + " sypie się! (" + (n + spill) + ")", hitter.Color);
        }

        void Drop(Vector3 at, Vector3 dir, int n, int value = 1)
        {
            if (dir.sqrMagnitude < 1e-3f) dir = Random.insideUnitSphere;
            dir.y = 0f; dir.Normalize();
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Strzepek");
                go.transform.SetParent(Root, false);
                go.transform.position = at + Vector3.up * 0.6f + Random.insideUnitSphere * 0.3f;
                go.transform.rotation = Random.rotation;
                go.transform.localScale = Vector3.one * Random.Range(0.28f, 0.38f);
                go.AddComponent<MeshFilter>().sharedMesh = PotatoShreds.Chunk(i);
                bool gold = value > 1;
                if (gold) go.transform.localScale *= 1.8f;
                go.AddComponent<MeshRenderer>().sharedMaterial = gold ? Shapes.Mat(Shapes.Hex("#FFB800"), 0.9f, 1.4f) : Shapes.Mat(Shapes.Hex("#FFD54A"), 0.6f, 0.35f);
                var col = go.AddComponent<SphereCollider>();
                col.radius = 0.5f;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                foreach (var p in Players) if (p != null) Physics.IgnoreCollision(col, p.GetComponent<Collider>(), true);
                float yaw = Random.Range(-60f, 60f);
                var d = Quaternion.Euler(0, yaw, 0) * dir;
                Phys.SetVelocity(rb, gold ? Vector3.up * 2f : d * Random.Range(3f, 7f) + Vector3.up * Random.Range(4f, 7f)); // złoto spada pionowo (zostaje na dachu)
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                shreds.Add(rb);
                shredAge.Add(0f);
                shredValue.Add(value);
            }
        }

        /// <summary>Złote strzępki (×5) na wysokich poziomach mapy: dach szopy, wieża, losowy taras – wymusza wspinaczkę.</summary>
        void SpawnGold(bool all)
        {
            var spots = new List<Vector3> { L.ShedRoof, L.TankTop };
            if (L.Terraces.Count > 0) spots.Add(L.Terraces[Random.Range(0, L.Terraces.Count)]);
            if (!all) spots = new List<Vector3> { spots[Random.Range(0, spots.Count)] };
            foreach (var sp in spots)
            {
                Drop(sp + Vector3.up * 1.2f, Random.insideUnitSphere, 1, GoldValue);
                Fx.Sparkle(Root, sp + Vector3.up * 1.5f, Shapes.Hex("#FFC61A"), 1.4f);
            }
            AudioManager.Play(Sfx.Coin, 0.6f);
        }

        // ---------------- pętla ----------------

        public override void Tick(float dt)
        {
            // spadnięcie: tracisz wiaderko i 40% masy (zanim bazowa klasa przeniesie gracza)
            foreach (var p in Players)
            {
                if (p == null || p.Eliminated || !mass.ContainsKey(p)) continue;
                if (p.transform.position.y < KillY)
                {
                    carried[p] = 0;
                    mass[p] = Mathf.Min(mass[p], 0.6f);
                    Apply(p);
                }
            }
            base.Tick(dt);
            if (Finished || !Running) return;

            if (state == null) Go(new WarmupState());
            state.Time += dt;
            Go(state.Tick(this, dt));
            goldTimer -= dt;
            if (goldTimer <= 0f && Current != Phase.Warmup) { goldTimer = finale ? FinaleGoldEvery : GoldEvery; SpawnGold(false); }

            Collect(dt);
            Deposit(dt);
        }

        void Collect(float dt)
        {
            for (int i = shreds.Count - 1; i >= 0; i--)
            {
                var rb = shreds[i];
                if (rb == null || rb.position.y < KillY) { if (rb != null) Object.Destroy(rb.gameObject); shreds.RemoveAt(i); shredAge.RemoveAt(i); shredValue.RemoveAt(i); continue; }
                shredAge[i] += dt;
                if (shredAge[i] < 0.45f) continue; // świeżo oderwany – nie wraca od razu do ofiary
                foreach (var p in Players)
                {
                    if (p == null || p.Eliminated || !carried.ContainsKey(p) || carried[p] >= CarryMax) continue;
                    if ((p.transform.position - rb.position).sqrMagnitude > 1.3f * 1.3f) continue;
                    int val = shredValue[i];
                    carried[p] = Mathf.Min(CarryMax, carried[p] + val);
                    mass[p] = Mathf.Min(MassMax, mass[p] + MassPerShred * val);
                    if (val > 1) GM.Hud.Toast(p.Slot.Name + " łapie ZŁOTO! +" + val, Shapes.Hex("#FFC61A"));
                    Apply(p);
                    AudioManager.PlayPitched(Sfx.Coin, 0.35f, 1f + carried[p] * 0.03f);
                    Object.Destroy(rb.gameObject);
                    shreds.RemoveAt(i); shredAge.RemoveAt(i); shredValue.RemoveAt(i);
                    break;
                }
            }
        }

        void Deposit(float dt)
        {
            foreach (var p in Players)
            {
                if (p == null || p.Eliminated || !carried.ContainsKey(p)) continue;
                var pos = p.transform.position;
                bool on = FlatDist(pos, Fryer) < DepositRadius && pos.y < Fryer.y + 1.6f && carried[p] > 0 && DepositOpen;
                onDeck[p] = on ? onDeck[p] + dt : 0f;
                if (onDeck[p] < DepositTime) continue;
                int c = carried[p];
                int pts = Mathf.RoundToInt(c * (c >= ComboAt ? 1.5f : 1f) * (finale ? 2f : 1f));
                Score[p] += pts;
                carried[p] = 0;
                onDeck[p] = 0f;
                Apply(p);
                Fx.Splash(Root, Fryer + Vector3.up * 1.7f, Shapes.Hex("#F7C548"), c >= ComboAt ? 1.6f : 0.9f);
                AudioManager.Play(c >= ComboAt ? Sfx.Fanfare : Sfx.Coin, 0.8f);
                GM.Hud.Toast(p.Slot.Name + " smaży " + c + (c >= ComboAt ? " (×1,5!)" : "") + " → +" + pts, p.Color);
                if (c >= ComboAt) Feel.Triumph(Fryer);
            }
        }

        /// <summary>Jedna klatka prychania (stan <see cref="SpittingState"/>).</summary>
        void SpitBlast(float dt)
        {
            if (Random.value < dt * 8f) Fx.Splash(Root, Fryer + Vector3.up * 1.7f + Random.insideUnitSphere * 1.5f, Shapes.Hex("#F7C548"), 0.7f);
            foreach (var p in Players)
            {
                if (p == null || p.Eliminated || !carried.ContainsKey(p)) continue;
                var d = p.transform.position - Fryer; d.y = 0f;
                if (d.magnitude > SpitRadius || p.transform.position.y > Fryer.y + 2.5f) continue;
                p.Knock(d.normalized * 13f + Vector3.up * 9f, 0.6f);
                if (p.Surface != null) p.Surface.Scorch(Fryer, 0.5f);
                int spill = carried[p] / 2;
                carried[p] -= spill;
                Drop(p.transform.position, d, spill);
                Apply(p);
            }
        }

        public override string StatFor(Potato p)
        {
            int c; float m;
            if (!carried.TryGetValue(p, out c) || !mass.TryGetValue(p, out m)) return "";
            return c + " w wiadrze · " + Mathf.RoundToInt(m * 100f) + "% masy";
        }

        public override string HudInfo()
        {
            switch (Current)
            {
                case Phase.Warmup: return "Olej się grzeje… zbieraj złoto na górze!";
                case Phase.Spitting: return "FRYTKOWNICA PRYCHA!";
                case Phase.SpitWarn: return "UWAGA: prychnięcie!";
                default: return (finale ? "FINAŁ ×2 · " : "") + "Prychnięcie za " + Mathf.CeilToInt(Mathf.Max(0f, spitTimer)) + " s";
            }
        }

        // ---------------- boty ----------------

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            int c = carried.ContainsKey(p) ? carried[p] : 0;
            // 1) ucieczka przed prychnięciem
            if ((Current == Phase.SpitWarn || Current == Phase.Spitting || Current == Phase.Warmup) && FlatDist(pos, Fryer) < SpitRadius + 2f)
            {
                var away = pos - Fryer; away.y = 0f;
                return BotCommand.Go(pos + away.normalized * 6f);
            }
            // 2) wrzut: 8+ albo końcówka
            if (c >= 8 || (c > 0 && TimeLeft < 15f)) return BotCommand.Go(Fryer, false, GapAhead(p, Fryer));
            // 3) zbieranie najbliższych strzępków
            Rigidbody best = null; float bestD = 9f * 9f;
            for (int i = 0; i < shreds.Count; i++)
            {
                if (shreds[i] == null || shredAge[i] < 0.45f) continue;
                float d = (shreds[i].position - pos).sqrMagnitude / (shredValue[i] > 1 ? 25f : 1f); // złoto warte drogi na dach
                if (d < bestD) { bestD = d; best = shreds[i]; }
            }
            if (best != null) return BotCommand.Go(best.position);
            // 4) polowanie na pełne wiaderko (albo najbliższego)
            Potato target = null; int most = -1;
            foreach (var o in Players)
            {
                if (o == null || o == p || o.Eliminated || !carried.ContainsKey(o)) continue;
                int oc = carried[o] * 3 - Mathf.RoundToInt(FlatDist(o.transform.position, pos));
                if (oc > most) { most = oc; target = o; }
            }
            if (target == null) return BotCommand.Go(Wander(p, 14f));
            float dist = FlatDist(target.transform.position, pos);
            bool dash = dist < 3.4f && p.DashCooldownLeft <= 0f && Random.value < 0.5f;
            return BotCommand.Go(target.transform.position, dash, Random.value < 0.03f);
        }
    }
}
