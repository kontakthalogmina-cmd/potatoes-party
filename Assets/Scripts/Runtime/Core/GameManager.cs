using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotatoParty
{
    public enum GameState { MainMenu, Settings, Join, Lobby, MapPick, Loading, Intro, Playing, RoundResults, Final }

    /// <summary>
    /// Serce gry: menu, lobby, kolejne rundy, wybór mapy, minigry i punktacja.
    /// Tworzy się samo po wciśnięciu Play (patrz <see cref="Bootstrap"/>).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public MatchSettings Settings = new MatchSettings();
        public readonly List<PlayerSlot> Slots = new List<PlayerSlot>();
        public ILobbyService Lobbies { get; private set; }
        public bool JoinedAsGuest;
        public string JoinedHostName;

        public CinematicCamera Camera { get; private set; }
        public Light Sun { get; private set; }
        public MenuUI Menu { get; private set; }
        public Hud Hud { get; private set; }
        public LoadingScreen Loading { get; private set; }

        public GameState State { get; private set; }
        public bool Paused { get; private set; }
        public bool InMatch { get; private set; }
        public int Round { get; private set; }
        public Minigame Current { get; private set; }
        public readonly List<Potato> Potatoes = new List<Potato>();

        Transform world;
        Coroutine match;
        readonly List<int> bag = new List<int>();
        int lastMinigame = -1;
        readonly Dictionary<Potato, Vector3> wander = new Dictionary<Potato, Vector3>();
        readonly Dictionary<Potato, float> wanderTime = new Dictionary<Potato, float>();
        List<WardrobePad> pads = new List<WardrobePad>();
        readonly Dictionary<PlayerSlot, int> onPad = new Dictionary<PlayerSlot, int>();

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("Potatoes Party");
            DontDestroyOnLoad(go);
            go.AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 120;
            Lobbies = new LocalLobbyService(); // tu, a nie w polu: Random nie działa w konstruktorze MonoBehaviour

            // Usuń domyślną kamerę/światło ze sceny, jeśli jakieś są — gra ma własne.
            foreach (var cam in UnityEngine.Camera.allCameras)
                if (cam.GetComponentInParent<CinematicCamera>() == null) Destroy(cam.gameObject);

            AudioManager.Create(transform);
            GameSettings.Load();

            TimeDirector.Ensure();
            Time.fixedDeltaTime = TimeDirector.BaseFixedDelta;
            Camera = CinematicCamera.Create();
            Camera.transform.SetParent(transform, false);

            var sunGo = new GameObject("Slonce");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.6f;
            RenderSettings.sun = Sun;
            Lighting.Setup(Camera.Cam, Sun);
            Fx.Camera = Camera;
            MenuDirector.Ensure(transform);

            UIKit.EnsureEventSystem();
            Debug.Log("[Potatoes Party] Czcionki: " + UIKit.TitleFont.name + " / " + UIKit.Font.name);
            Hud = Hud.Create(transform);
            Menu = MenuUI.Create(transform, this);
            Loading = LoadingScreen.Create(transform);
        }

        void Start()
        {
            GoToMainMenu();
        }

        // =====================================================================
        //  Świat
        // =====================================================================

        void ClearWorld()
        {
            Potatoes.Clear();
            pads.Clear();
            onPad.Clear();
            wander.Clear();
            wanderTime.Clear();
            Camera.Targets.Clear();
            Vfx.ClearAll();
            PotatoShreds.Clear();
            menuStage = false;
            if (MenuDirector.Instance != null) MenuDirector.Instance.Release(true);
            if (world != null) Destroy(world.gameObject);
            world = new GameObject("Swiat").transform;
            world.SetParent(transform, false);
        }

        /// <summary>Tryb pokazowy (menu, lobby, podium): niska kamera z wolnym ruchem i płytka głębia ostrości.</summary>
        void Showcase(bool on)
        {
            Camera.Showcase = on;
            PostFx.SetShowcase(on);
        }

        void BuildStage(MapTheme theme, float radius, bool wardrobe = false)
        {
            ClearWorld();
            SkinFx.SetMap(theme);
            MapBuilder.ApplyAtmosphere(theme, Camera.Cam, Sun);
            MapBuilder.BuildEnvironment(theme, world, radius);
            MapBuilder.RoundArena(world, radius, theme);
            MapBuilder.WallRing(world, radius, theme);
            if (radius >= MapBuilder.HubRadius - 0.01f) pads = MapBuilder.BuildHub(world, theme, wardrobe);
            Camera.ArenaCenter = Vector3.zero;
            Camera.ArenaRadius = radius;
            Lighting.RefreshReflections(world);
        }

        Potato Spawn(PlayerSlot slot, Vector3 pos)
        {
            var p = Potato.Create(slot, world, pos);
            p.FaceTowards(Vector3.back);
            Potatoes.Add(p);
            Camera.Targets.Add(p.transform);
            return p;
        }

        BotCommand IdleWander(Potato p)
        {
            float t;
            if (!wanderTime.TryGetValue(p, out t) || Time.time > t)
            {
                var c = Random.insideUnitCircle * Mathf.Min(Camera.ArenaRadius, 9f) * 0.75f;
                wander[p] = new Vector3(c.x, 0, c.y);
                wanderTime[p] = Time.time + Random.Range(1.5f, 4f);
            }
            bool hop = Random.value < 0.04f;
            return BotCommand.Go(wander[p], false, hop);
        }

        // =====================================================================
        //  Menu
        // =====================================================================

        /// <summary>Świat menu i lobby (plac z garderobą) już stoi – przejścia menu ↔ lobby bez przebudowy, samym najazdem kamery.</summary>
        bool menuStage;

        public void GoToMainMenu()
        {
            StopMatch();
            if (!JoinedAsGuest && !string.IsNullOrEmpty(Settings.LobbyCode)) Lobbies.Unpublish(Settings.LobbyCode);
            Slots.Clear();
            bool reuse = menuStage && world != null;
            State = GameState.MainMenu;
            if (reuse)
            {
                foreach (var p in Potatoes) if (p != null) Destroy(p.gameObject);
                Potatoes.Clear();
                Camera.Targets.Clear();
            }
            else
            {
                BuildStage(MapTheme.Get(MapId.Farm), MapBuilder.HubRadius, true);
                menuStage = true;
            }
            Showcase(true);
            Camera.ScreenOffset = Vector3.zero;
            Camera.MinDistance = 13f;
            Camera.FollowArenaPull = 0.5f;
            Camera.MinSpread = 12f;
            // 6 ziemniaków na "ringu" przed kamerą – przepychają się zrywami (mózg: MenuDirector.Brawl)
            for (int i = 0; i < 6; i++)
            {
                var slot = new PlayerSlot { Index = i, Name = PlayerColors.BotNames[i % PlayerColors.BotNames.Length], Color = PlayerColors.All[i % PlayerColors.All.Length], Kind = ControlKind.Bot, Skin = (i * 2 + Random.Range(0, 2)) % PotatoSkins.Count };
                float a = i / 6f * Mathf.PI * 2f;
                var p = Spawn(slot, MenuDirector.BrawlCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2f + Vector3.up);
                p.Frozen = false;
            }
            Camera.SnapNow();
            Hud.Hide();
            AudioManager.Instance.PlayMusic(Synth.MenuSong);
            MenuDirector.Instance.Enter(Potatoes, !reuse);
            Menu.ShowMain();
        }

        public void OpenSettings()
        {
            if (MenuDirector.Instance != null && MenuDirector.Instance.Active) MenuDirector.Instance.Shot(MenuShot.Settings);
            Menu.ShowSettings();
        }

        public void CloseSettings()
        {
            if (Paused) Menu.ShowPause();
            else
            {
                if (MenuDirector.Instance != null && MenuDirector.Instance.Active) MenuDirector.Instance.Shot(MenuShot.Title);
                Menu.ShowMain();
            }
        }

        public void OpenJoin()
        {
            State = GameState.Join;
            if (MenuDirector.Instance != null && MenuDirector.Instance.Active) MenuDirector.Instance.Shot(MenuShot.Play);
            Menu.ShowJoin();
        }

        public void CreateLobby()
        {
            Settings = new MatchSettings { LobbyCode = MatchSettings.NewCode() };
            JoinedAsGuest = false;
            JoinedHostName = null;
            Slots.Clear();
            AddSlot(ControlKind.Keyboard, 0); // gospodarz od razu gra na klawiaturze (WASD)
            EnterLobby();
        }

        /// <summary>Dołącza do lobby z listy albo po kodzie. Zwraca komunikat błędu albo null.</summary>
        public string JoinLobby(LobbyInfo info)
        {
            if (info == null) return "Nie znaleziono lobby o takim kodzie.";
            if (info.Players >= info.MaxPlayers) return "To lobby jest pełne.";
            Settings = new MatchSettings
            {
                LobbyName = info.Name,
                LobbyCode = info.Code,
                IsPrivate = info.IsPrivate,
                RoundOptionIndex = info.RoundOptionIndex,
                MapMode = info.MapMode,
            };
            JoinedAsGuest = info.HostedByBots;
            JoinedHostName = info.HostName;
            Slots.Clear();
            if (info.HostedByBots)
                for (int i = 0; i < Mathf.Min(info.Players, MatchSettings.MaxPlayers - 1); i++) AddBot(false);
            AddSlot(ControlKind.Keyboard, 0);
            EnterLobby();
            return null;
        }

        void EnterLobby()
        {
            StopMatch();
            State = GameState.Lobby;
            if (!(menuStage && world != null))
            {
                BuildStage(MapTheme.Get(MapId.Farm), MapBuilder.HubRadius, true);
                menuStage = true;
            }
            // z menu: ten sam plac, kamera gry przejmuje kadr płynnym najazdem (bez ładowania)
            if (MenuDirector.Instance != null) MenuDirector.Instance.Release();
            Showcase(true);
            Camera.ScreenOffset = Vector3.zero;
            Camera.MinDistance = 13f;
            Camera.FollowArenaPull = 0.5f;
            Camera.MinSpread = 12f;
            foreach (var s in Slots) s.Points = 0;
            RebuildLobbyPotatoes();
            Camera.SnapNow();
            Hud.Hide();
            AudioManager.Instance.PlayMusic(Synth.MenuSong);
            PublishLobby();
            Menu.ShowLobby();
        }

        public void PublishLobby()
        {
            if (JoinedAsGuest || State != GameState.Lobby) return;
            Lobbies.Publish(new LobbyInfo
            {
                Name = Settings.LobbyName,
                Code = Settings.LobbyCode,
                IsPrivate = Settings.IsPrivate,
                Players = Slots.Count,
                RoundOptionIndex = Settings.RoundOptionIndex,
                MapMode = Settings.MapMode,
                HostName = Slots.Count > 0 ? Slots[0].Name : "Gospodarz",
            });
        }

        void RebuildLobbyPotatoes()
        {
            foreach (var p in Potatoes) if (p != null) Destroy(p.gameObject);
            Potatoes.Clear();
            Camera.Targets.Clear();
            for (int i = 0; i < Slots.Count; i++)
            {
                var p = Spawn(Slots[i], new Vector3(-5f + i * 2f, 1f, -2.5f));
                p.Frozen = false;
                if (Slots[i].IsBot) p.BotBrain = IdleWander;
            }
        }

        int FreeColor()
        {
            for (int c = 0; c < PlayerColors.All.Length; c++)
                if (!Slots.Any(s => s.Index == c)) return c;
            return -1;
        }

        PlayerSlot AddSlot(ControlKind kind, int device)
        {
            if (Slots.Count >= MatchSettings.MaxPlayers) return null;
            int c = FreeColor();
            if (c < 0) return null;
            var slot = new PlayerSlot { Index = c, Color = PlayerColors.All[c], Kind = kind, DeviceId = device };
            slot.Skin = kind == ControlKind.Bot ? RandomFreeSkin() : PotatoSkins.Saved(c);
            if (kind == ControlKind.Bot)
            {
                var used = Slots.Select(s => s.Name).ToList();
                var name = PlayerColors.BotNames.FirstOrDefault(n => !used.Contains(n));
                slot.Name = name ?? "Bot";
            }
            else slot.Name = "Gracz " + (c + 1);
            Slots.Add(slot);
            Slots.Sort((a, b) => a.Index.CompareTo(b.Index));
            return slot;
        }

        int RandomFreeSkin()
        {
            var free = new List<int>();
            for (int i = 0; i < PotatoSkins.Count; i++) if (!Slots.Any(s => s.Skin == i)) free.Add(i);
            return free.Count > 0 ? free[Random.Range(0, free.Count)] : Random.Range(0, PotatoSkins.Count);
        }

        /// <summary>Strzałki w karcie gracza w lobby: następna/poprzednia skórka.</summary>
        public void CycleSkin(PlayerSlot s, int dir)
        {
            SetSkin(s, (s.Skin + dir + PotatoSkins.Count) % PotatoSkins.Count);
        }

        /// <summary>Zmienia skórkę gracza; w lobby od razu przebiera jego ziemniaka (z efektem).</summary>
        public void SetSkin(PlayerSlot s, int skin)
        {
            if (s == null) return;
            skin = Mathf.Clamp(skin, 0, PotatoSkins.Count - 1);
            if (s.Skin == skin) return;
            s.Skin = skin;
            if (!s.IsBot) PotatoSkins.Save(s.Index, skin);
            var old = Potatoes.FirstOrDefault(p => p != null && p.Slot == s);
            if (old != null && State == GameState.Lobby)
            {
                var pos = old.transform.position;
                var facing = old.Facing;
                Potatoes.Remove(old);
                Camera.Targets.Remove(old.transform);
                wander.Remove(old);
                wanderTime.Remove(old);
                Destroy(old.gameObject);
                var p = Spawn(s, pos);
                p.FaceTowards(facing);
                p.Frozen = false;
                if (s.IsBot) p.BotBrain = IdleWander;
                p.Knock(Vector3.up * 5f, 0.1f);
                Fx.Sparkle(world, pos + Vector3.up * 0.6f, Color.Lerp(s.Color, Color.white, 0.3f), 1.4f);
                Fx.Mist(world, pos, Color.white, 1f);
            }
            AudioManager.Play(Sfx.Coin, 0.8f);
            if (State == GameState.Lobby)
            {
                Menu.RefreshLobby();
                Hud.Toast(s.Name + ": skórka " + PotatoSkins.Get(skin).Name + "!", s.Color);
            }
        }

        /// <summary>Garderoba w lobby: wejście na podest zakłada skórkę z manekina obok.</summary>
        void UpdateWardrobe()
        {
            if (pads.Count == 0) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            foreach (var pad in pads)
            {
                if (pad.Glow == null) continue;
                pad.Glow.localScale = new Vector3(1.45f, 0.08f, 1.45f) * (1f + pulse * 0.04f);
            }
            foreach (var p in Potatoes.ToList())
            {
                if (p == null || p.Slot == null || p.Slot.IsBot) continue;
                int inside = -1;
                foreach (var pad in pads)
                {
                    var d = p.transform.position - pad.Position;
                    d.y = 0;
                    if (d.magnitude < pad.Radius && p.transform.position.y < 2f) { inside = pad.Skin; break; }
                }
                int last;
                onPad.TryGetValue(p.Slot, out last);
                if (inside >= 0 && inside != last - 1 && inside != p.Slot.Skin) SetSkin(p.Slot, inside);
                onPad[p.Slot] = inside + 1; // 0 = poza podestem
            }
        }

        public void AddBot() { AddBot(true); }

        void AddBot(bool refresh)
        {
            var s = AddSlot(ControlKind.Bot, 0);
            if (s == null) { if (refresh) Hud.Toast("Lobby jest pełne (max 6 graczy).", UIKit.Red); return; }
            if (refresh) LobbyChanged(s);
        }

        void AddHuman(ControlKind kind, int device)
        {
            var s = AddSlot(kind, device);
            if (s == null) { Hud.Toast("Lobby jest pełne (max 6 graczy).", UIKit.Red); return; }
            AudioManager.Play(Sfx.Join, 1f);
            LobbyChanged(s);
        }

        public void RemoveSlot(PlayerSlot s)
        {
            Slots.Remove(s);
            LobbyChanged(null);
        }

        void LobbyChanged(PlayerSlot added)
        {
            if (State != GameState.Lobby) return;
            RebuildLobbyPotatoes();
            PublishLobby();
            Menu.RefreshLobby();
            if (added != null) Hud.Toast(added.Name + " dołącza!", added.Color);
        }

        void DetectJoins()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null)
            {
                if (es.currentSelectedGameObject.GetComponent<InputField>() != null) return; // ktoś wpisuje nazwę lobby
                // Spacja/Enter nie powinny "klikać" ostatnio wybranego przycisku
                es.SetSelectedGameObject(null);
            }

            for (int k = 0; k < InputBridge.KeyboardSchemes; k++)
            {
                if (Slots.Any(s => s.Kind == ControlKind.Keyboard && s.DeviceId == k)) continue;
                if (InputBridge.KeyboardJumpDown(k) || InputBridge.KeyboardActionDown(k)) AddHuman(ControlKind.Keyboard, k);
            }
            foreach (var id in InputBridge.GamepadIds())
            {
                if (Slots.Any(s => s.Kind == ControlKind.Gamepad && s.DeviceId == id)) continue;
                if (InputBridge.GamepadJumpDown(id) || InputBridge.GamepadActionDown(id)) AddHuman(ControlKind.Gamepad, id);
            }
        }

        public void LeaveLobby()
        {
            GoToMainMenu();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // =====================================================================
        //  Rozgrywka
        // =====================================================================

        public void StartMatch()
        {
            if (InMatch || State != GameState.Lobby) return;
            if (Slots.Count < MatchSettings.MinPlayers)
            {
                Hud.Toast("Potrzeba co najmniej 2 graczy. Dołącz z pada/klawiatury albo dodaj bota.", UIKit.Red);
                return;
            }
            foreach (var s in Slots) { s.Points = 0; s.Wins = 0; }
            Round = 0;
            bag.Clear();
            InMatch = true;
            Menu.HideLobby();
            Lobbies.Unpublish(Settings.LobbyCode);
            match = StartCoroutine(MatchRoutine());
        }

        void StopMatch()
        {
            if (match != null) StopCoroutine(match);
            match = null;
            if (Current != null) Current.Cleanup();
            Current = null;
            InMatch = false;
            Paused = false;
            TimeDirector.ResetAll();
            Rumble.StopAll();
            Menu.HideOverlays();
            Loading.Hide();
        }

        /// <summary>Losuje kolejność minigier na cały mecz: bez powtórek, ze wszystkich 12.</summary>
        void BuildPlaylist(int count)
        {
            bag.Clear();
            for (int i = 0; i < Minigame.Count; i++) bag.Add(i);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int t = bag[i]; bag[i] = bag[j]; bag[j] = t;
            }
            if (bag.Count > 1 && bag[0] == lastMinigame) { int t = bag[0]; bag[0] = bag[1]; bag[1] = t; }
            if (bag.Count > count) bag.RemoveRange(count, bag.Count - count);
        }

        IEnumerator MatchRoutine()
        {
            int total = Mathf.Min(Settings.Rounds, Minigame.Count);
            BuildPlaylist(total);
            for (Round = 1; Round <= total; Round++)
            {
                lastMinigame = bag[Round - 1];
                var mg = Minigame.Create(lastMinigame);

                // ---- Ekran ładowania i budowa rundy (każda minigra ma swoją mapę) ----
                var theme = MapTheme.Get(mg.PreferredMap);
                State = GameState.Loading;
                Hud.Hide();
                Menu.HideOverlays();
                yield return Loading.Run(Round, total, mg, theme, () => BuildRound(mg, theme));
                AudioManager.Instance.PlayMusic(Synth.GameSong);

                // ---- Zapowiedź i odliczanie ----
                State = GameState.Intro;
                Menu.ShowIntro(Round, total, mg, theme);
                yield return WaitOrConfirm(4.5f, 1.2f);
                Menu.HideIntro();
                Hud.Show(mg);
                for (int c = 3; c >= 1; c--)
                {
                    Hud.Countdown(c.ToString());
                    AudioManager.Play(Sfx.Beep, 1f, 0f);
                    yield return new WaitForSeconds(0.75f);
                }
                Hud.Countdown("START!");
                AudioManager.Play(Sfx.Go, 1f, 0f);
                foreach (var p in Potatoes) p.Frozen = false;
                mg.Begin();
                State = GameState.Playing;

                // ---- Gra ----
                while (!mg.Finished)
                {
                    mg.Tick(Time.deltaTime);
                    yield return null;
                }

                foreach (var p in Potatoes) p.Frozen = true;
                Hud.Countdown("KONIEC!");
                AudioManager.Play(Sfx.Fanfare, 0.9f, 0f);
                var ranking = mg.Ranking();
                var winners = ranking.Count > 0 ? ranking[0] : new List<Potato>();
                Camera.Targets.Clear();
                foreach (var w in winners)
                {
                    Camera.Targets.Add(w.transform);
                    if (w != null) Effects.Confetti(w.transform.parent, w.transform.position + Vector3.up, 140);
                }
                yield return new WaitForSeconds(1.8f);

                // ---- Punkty ----
                int[] pointsByPlace = { 3, 2, 1 };
                var lines = new List<MenuUI.ResultLine>();
                int place = 0;
                foreach (var group in ranking)
                {
                    int pts = place < pointsByPlace.Length ? pointsByPlace[place] : 0;
                    if (Round == total && total > 1) pts *= 2; // finał: podwójne punkty
                    foreach (var p in group)
                    {
                        p.Slot.Points += pts;
                        if (place == 0) p.Slot.Wins++;
                        lines.Add(new MenuUI.ResultLine { Place = place + 1, Slot = p.Slot, Gained = pts, Stat = mg.StatFor(p) });
                    }
                    place += group.Count;
                }
                State = GameState.RoundResults;
                Hud.Hide();
                AudioManager.Instance.PlayMusic(Synth.ResultsSong);
                Menu.ShowRoundResults(Round, total, mg.Title, lines);
                yield return WaitOrConfirm(6f, 1.5f);
                Menu.HideRoundResults();
                mg.Cleanup();
                Current = null;
            }

            // ---- Koniec meczu ----
            State = GameState.Final;
            InMatch = false;
            var standings = Slots.OrderByDescending(s => s.Points).ThenByDescending(s => s.Wins).ToList();
            int best = standings[0].Points;
            var champs = standings.Where(s => s.Points == best).ToList();
            ShowChampions(champs);
            AudioManager.Play(Sfx.Fanfare, 1f, 0f);
            Menu.ShowFinal(standings, champs);
            match = null;
        }

        IEnumerator WaitOrConfirm(float seconds, float minSeconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                if (t > minSeconds && !Paused && InputBridge.AnyConfirmDown()) yield break;
                yield return null;
            }
        }

        void BuildRound(Minigame mg, MapTheme theme)
        {
            ClearWorld();
            SkinFx.SetMap(theme);
            Showcase(false);
            MapBuilder.ApplyAtmosphere(theme, Camera.Cam, Sun);
            MapBuilder.BuildEnvironment(theme, world, mg.ArenaRadius);
            mg.Setup(this, theme, world);
            mg.BuildArena();
            for (int i = 0; i < Slots.Count; i++)
            {
                var p = Spawn(Slots[i], mg.SpawnPoint(i, Slots.Count));
                p.FaceTowards(-p.transform.position);
                p.Frozen = true;
            }
            mg.AddPlayers(new List<Potato>(Potatoes));
            Current = mg;
            foreach (var t in mg.ExtraCameraTargets()) Camera.Targets.Add(t);
            Camera.ArenaCenter = mg.ArenaCenter;
            Camera.ArenaRadius = mg.ArenaRadius;
            Camera.ScreenOffset = Vector3.zero;
            Camera.MinDistance = 13f;
            Camera.FollowArenaPull = mg.CameraPull;
            Camera.MinSpread = 8f;
            Camera.SnapNow();
            Lighting.RefreshReflections(world);
        }

        void ShowChampions(List<PlayerSlot> champs)
        {
            BuildStage(MapTheme.Get(MapId.Farm), MapBuilder.HubRadius);
            Showcase(true);
            // Podium
            MapBuilder.Disc(world, new Vector3(0, 0.4f, 0), 2.5f, 0.8f, UIKit.Yellow);
            for (int i = 0; i < champs.Count; i++)
            {
                var p = Spawn(champs[i], new Vector3((i - (champs.Count - 1) * 0.5f) * 1.6f, 1.6f, 0));
                p.Frozen = false;
                p.BotBrain = c => BotCommand.Go(c.transform.position, false, Random.value < 0.25f);
            }
            // Pozostali kibicują dookoła
            int k = 0;
            foreach (var s in Slots)
            {
                if (champs.Contains(s)) continue;
                float a = Mathf.PI * (1.15f + k * 0.18f);
                var p = Spawn(s, new Vector3(Mathf.Cos(a) * 5f, 1f, Mathf.Sin(a) * 5f + 1f));
                p.FaceTowards(-p.transform.position);
                p.Frozen = true;
                k++;
            }
            Camera.Targets.Clear();
            Camera.ScreenOffset = new Vector3(0, 0, -2f);
            Camera.MinDistance = 11f;
            Camera.FollowArenaPull = 1f;
            Camera.MinSpread = 7f;
            Camera.SnapNow();
            // Ludzie też mogą skakać z radości
            foreach (var p in Potatoes) if (!p.Slot.IsBot) p.Frozen = false;
            Effects.Confetti(world, new Vector3(0, 14f, 0), 70, true, 7f);
        }

        public void PlayAgain()
        {
            EnterLobby();
        }

        // =====================================================================
        //  Pauza i zdarzenia
        // =====================================================================

        void Update()
        {
            if (State == GameState.Lobby && !Paused) { DetectJoins(); UpdateWardrobe(); }

            if (InMatch && (State == GameState.Playing || State == GameState.Intro || State == GameState.RoundResults) && InputBridge.PauseDown())
                SetPaused(!Paused);

            if (InMatch && Current != null) Hud.UpdateHud(Current, Potatoes);
            else if (State == GameState.Lobby || State == GameState.Final) Hud.UpdateLabels(Potatoes);
            else Hud.HideLabels();
        }

        public void SetPaused(bool on)
        {
            Paused = on;
            TimeDirector.SetPaused(on);
            if (on) Rumble.StopAll();
            if (on) Menu.ShowPause();
            else Menu.HidePause();
        }

        public void QuitToMenu()
        {
            GoToMainMenu();
        }

        public void OnPotatoBump(Potato a, Potato b, bool dash)
        {
            if (Current != null && Current.Running) Current.OnBump(a, b, dash);
        }
    }
}
