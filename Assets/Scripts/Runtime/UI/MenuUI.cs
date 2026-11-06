using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotatoParty
{
    /// <summary>Wszystkie ekrany menu: główne, ustawienia, dołączanie, lobby, pauza, zapowiedź rundy, wyniki.</summary>
    public class MenuUI : MonoBehaviour
    {
        public struct ResultLine
        {
            public int Place;
            public PlayerSlot Slot;
            public int Gained;
            public string Stat;
        }

        GameManager gm;
        Canvas canvas;
        RectTransform root;
        GameObject shade, main, settings, join, lobby, pause, intro, results, final;
        public FortuneWheel Wheel { get; private set; }
        public MapPicker MapPicker { get; private set; }

        // Lobby
        Text lobbyTitle, lobbyCode, lobbyPlayersTitle, lobbyHint, lobbyModeInfo, lobbyGuestNote;
        InputField lobbyName;
        Stepper typeStep, roundsStep;
        readonly List<SlotRow> slotRows = new List<SlotRow>();
        Button addBotButton, startButton;

        // Dołączanie
        RectTransform joinList;
        InputField joinCode;
        Text joinStatus;

        // Zapowiedź / wyniki / finał
        Text introRound, introTitle, introGoal, introDesc, introMap, introControls;
        Text resultsTitle;
        RectTransform resultsList;
        Text finalWinner;
        RectTransform finalList;
        Button finalFirst, mainFirst, pauseFirst;

        class SlotRow
        {
            public Image Bg, Swatch;
            public Text Name, Control;
            public Button Remove;
        }

        public static MenuUI Create(Transform parent, GameManager gm)
        {
            var go = new GameObject("Menu");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<MenuUI>();
            m.gm = gm;
            m.Build();
            return m;
        }

        void Build()
        {
            canvas = UIKit.CreateCanvas("Menu", 20, transform);
            root = UIKit.Fill(UIKit.Rect("Korzen", canvas.transform));

            shade = UIKit.Panel(root, "Przyciemnienie", UIKit.Shade, false).gameObject;
            UIKit.Fill((RectTransform)shade.transform);

            BuildMain();
            BuildSettings();
            BuildJoin();
            BuildLobby();
            BuildPause();
            BuildIntro();
            BuildResults();
            BuildFinal();

            Wheel = FortuneWheel.Create(root);
            MapPicker = MapPicker.Create(root);
            HideAll();
        }

        void HideAll()
        {
            foreach (var g in new[] { shade, main, settings, join, lobby, pause, intro, results, final })
                g.SetActive(false);
        }

        public void HideOverlays()
        {
            if (pause == null) return;
            shade.transform.SetSiblingIndex(0);
            pause.SetActive(false);
            intro.SetActive(false);
            results.SetActive(false);
            final.SetActive(false);
            shade.SetActive(false);
            Wheel.Hide();
            MapPicker.Hide();
        }

        static void Select(Selectable s)
        {
            if (EventSystem.current == null || s == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        GameObject Card(string name, Vector2 size, Color color, out RectTransform content, string header = null)
        {
            var card = UIKit.Panel(root, name, color);
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var sh = card.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.1f, 0.1f, 0.3f, 0.25f);
            sh.effectDistance = new Vector2(0, -12);
            card.gameObject.AddComponent<UIPop>();
            content = UIKit.Rect("Zawartosc", card.transform);
            UIKit.Stretch(content, Vector2.zero, Vector2.one, new Vector2(48, 40), new Vector2(-48, -40));
            UIKit.VStack(content, 16);
            if (header != null)
            {
                var h = UIKit.Label(UIKit.Row(content, "Naglowek", 80).transform, header, 56, UIKit.Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Fill(h.rectTransform);
            }
            return card.gameObject;
        }

        Text RowLabel(RectTransform parent, string text, int size, float height, Color c, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = UIKit.Label(UIKit.Row(parent, "Wiersz", height).transform, text, size, c, align, style);
            UIKit.Fill(t.rectTransform);
            return t;
        }

        Button RowButton(RectTransform parent, string text, Color c, System.Action onClick, float height = 86f, int fontSize = 38)
        {
            var row = UIKit.Row(parent, "Przycisk", height);
            var b = UIKit.Button(row.transform, text, c, onClick, fontSize);
            UIKit.Fill((RectTransform)b.transform);
            return b;
        }

        // =====================================================================
        //  Menu główne
        // =====================================================================

        void BuildMain()
        {
            var rt = UIKit.Rect("Menu glowne", root);
            UIKit.Stretch(rt, new Vector2(0, 0), new Vector2(0.42f, 1), new Vector2(80, 60), new Vector2(0, -60));
            main = rt.gameObject;

            var titleBox = UIKit.Rect("Tytul", rt);
            UIKit.Stretch(titleBox, new Vector2(0, 0.66f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            titleBox.gameObject.AddComponent<UIWobble>();
            var t1 = UIKit.Label(titleBox, "POTATOES", 132, UIKit.Yellow, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(t1.rectTransform, new Vector2(0, 0.45f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            UIKit.Outline(t1, UIKit.Dark, 6f);
            UIKit.Shadow(t1, 9f, 0.35f);
            var t2 = UIKit.Label(titleBox, "PARTY!", 120, UIKit.Orange, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(t2.rectTransform, new Vector2(0, 0.0f), new Vector2(1, 0.55f), Vector2.zero, Vector2.zero);
            UIKit.Outline(t2, UIKit.Dark, 6f);
            UIKit.Shadow(t2, 9f, 0.35f);

            var stack = UIKit.Rect("Przyciski", rt);
            UIKit.Stretch(stack, new Vector2(0.12f, 0.1f), new Vector2(0.88f, 0.58f), Vector2.zero, Vector2.zero);
            UIKit.VStack(stack, 22);
            mainFirst = RowButton(stack, "Dołącz do gry", UIKit.Teal, () => gm.OpenJoin(), 100, 44);
            RowButton(stack, "Stwórz grę", UIKit.Orange, () => gm.CreateLobby(), 100, 44);
            RowButton(stack, "Ustawienia", UIKit.Purple, () => gm.OpenSettings(), 100, 44);
            RowButton(stack, "Wyjdź z gry", UIKit.Red, () => gm.QuitGame(), 100, 44);

        }

        public void ShowMain()
        {
            HideAll();
            main.SetActive(true);
            Select(mainFirst);
        }

        // =====================================================================
        //  Ustawienia
        // =====================================================================

        Stepper masterStep, musicStep, sfxStep, screenStep, qualityStep;

        void BuildSettings()
        {
            RectTransform c;
            settings = Card("Ustawienia", new Vector2(1000, 920), UIKit.Cream, out c, "Ustawienia");
            var vols = new string[11];
            for (int i = 0; i <= 10; i++) vols[i] = (i * 10) + "%";
            masterStep = Stepper.Create(c, "Głośność ogólna", vols, GameSettings.MasterVolume / 10, i => { GameSettings.MasterVolume = i * 10; GameSettings.Apply(); });
            musicStep = Stepper.Create(c, "Muzyka", vols, GameSettings.MusicVolume / 10, i => { GameSettings.MusicVolume = i * 10; GameSettings.Apply(); });
            sfxStep = Stepper.Create(c, "Efekty dźwiękowe", vols, GameSettings.SfxVolume / 10, i => { GameSettings.SfxVolume = i * 10; GameSettings.Apply(); AudioManager.Play(Sfx.Coin); });
            screenStep = Stepper.Create(c, "Tryb ekranu", new[] { "Pełny ekran", "Okno" }, GameSettings.Fullscreen ? 0 : 1, i => { GameSettings.Fullscreen = i == 0; });
            var q = QualitySettings.names;
            if (q == null || q.Length == 0) q = new[] { "Domyślna" };
            qualityStep = Stepper.Create(c, "Jakość grafiki", q, Mathf.Clamp(GameSettings.Quality, 0, q.Length - 1), i => { GameSettings.Quality = i; });

            RowLabel(c, "<b>Sterowanie</b>\n" + InputBridge.SchemeHint(0) + "\n" + InputBridge.SchemeHint(1) + "\nPad: lewa gałka ruch, A skok, X/B akcja · ESC / Start: pauza",
                24, 160, UIKit.Dark);
            RowButton(c, "Zapisz i wróć", UIKit.Green, () => { GameSettings.Save(); gm.CloseSettings(); });
        }

        public void ShowSettings()
        {
            bool fromPause = gm.Paused;
            HideAll();
            if (fromPause)
            {
                shade.SetActive(true);
                shade.transform.SetAsLastSibling();
            }
            settings.transform.SetAsLastSibling();
            masterStep.Set(GameSettings.MasterVolume / 10);
            musicStep.Set(GameSettings.MusicVolume / 10);
            sfxStep.Set(GameSettings.SfxVolume / 10);
            screenStep.Set(GameSettings.Fullscreen ? 0 : 1);
            settings.SetActive(true);
            Select(masterStep.GetComponentInChildren<Button>());
        }

        // =====================================================================
        //  Dołącz do gry
        // =====================================================================

        void BuildJoin()
        {
            RectTransform c;
            join = Card("Dolacz", new Vector2(1240, 1010), UIKit.Cream, out c, "Dołącz do gry");
            RowLabel(c, gm.Lobbies.Description, 24, 40, UIKit.Dark * 0.8f + new Color(0, 0, 0, 0.2f), FontStyle.Italic);

            var header = UIKit.Row(c, "Naglowek listy", 50);
            var lt = UIKit.Label(header.transform, "Otwarte lobby", 34, UIKit.Dark, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Stretch(lt.rectTransform, Vector2.zero, new Vector2(0.7f, 1), Vector2.zero, Vector2.zero);
            var refresh = UIKit.Button(header.transform, "Odśwież", UIKit.Teal, RefreshJoinList, 26);
            UIKit.Stretch((RectTransform)refresh.transform, new Vector2(0.78f, 0), Vector2.one, Vector2.zero, Vector2.zero);

            var listBg = UIKit.Panel(UIKit.Row(c, "Lista", 380).transform, "Tlo listy", new Color(1, 1, 1, 0.6f));
            UIKit.Fill(listBg.rectTransform);
            joinList = UIKit.Rect("Pozycje", listBg.transform);
            UIKit.Stretch(joinList, Vector2.zero, Vector2.one, new Vector2(14, 14), new Vector2(-14, -14));
            UIKit.VStack(joinList, 10);

            RowLabel(c, "Masz kod prywatnego lobby?", 30, 44, UIKit.Dark, FontStyle.Bold, TextAnchor.MiddleLeft);
            var codeRow = UIKit.Row(c, "Kod", 76);
            joinCode = UIKit.Input(codeRow.transform, "Wpisz kod, np. AB12CD", 6, 34);
            UIKit.Stretch(joinCode.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0.6f, 1), Vector2.zero, Vector2.zero);
            joinCode.onValueChanged.AddListener(v => { var u = v.ToUpperInvariant(); if (u != v) joinCode.text = u; });
            var codeBtn = UIKit.Button(codeRow.transform, "Dołącz z kodem", UIKit.Orange, () =>
            {
                var err = gm.JoinLobby(gm.Lobbies.FindByCode(joinCode.text));
                if (err != null) joinStatus.text = err;
            }, 32);
            UIKit.Stretch((RectTransform)codeBtn.transform, new Vector2(0.63f, 0), Vector2.one, Vector2.zero, Vector2.zero);

            joinStatus = RowLabel(c, "", 26, 36, UIKit.Red, FontStyle.Bold);
            RowButton(c, "Wstecz", UIKit.Purple, () => gm.GoToMainMenu(), 80);
        }

        public void ShowJoin()
        {
            HideAll();
            joinStatus.text = "";
            joinCode.text = "";
            RefreshJoinList();
            join.SetActive(true);
        }

        void RefreshJoinList()
        {
            foreach (Transform t in joinList) Destroy(t.gameObject);
            var list = gm.Lobbies.ListOpen();
            if (list.Count == 0)
            {
                RowLabel(joinList, "Nie znaleziono otwartych lobby. Stwórz własną grę!", 28, 60, UIKit.Dark);
                return;
            }
            foreach (var l in list)
            {
                var row = UIKit.Row(joinList, l.Name, 78);
                var bg = UIKit.Panel(row.transform, "Tlo", Color.white);
                UIKit.Fill(bg.rectTransform);
                var name = UIKit.Label(bg.transform, "<b>" + l.Name + "</b>  ·  gospodarz: " + l.HostName, 28, UIKit.Dark, TextAnchor.MiddleLeft);
                UIKit.Stretch(name.rectTransform, new Vector2(0, 0.45f), new Vector2(0.75f, 1), new Vector2(20, 0), Vector2.zero);
                var det = UIKit.Label(bg.transform, l.Players + "/" + l.MaxPlayers + " graczy  ·  " + MatchSettings.RoundOptions[l.RoundOptionIndex] + "  ·  " + MatchSettings.ModeLabel(l.RoundOptionIndex), 22, UIKit.Dark * 0.7f + new Color(0, 0, 0, 0.3f), TextAnchor.MiddleLeft);
                UIKit.Stretch(det.rectTransform, new Vector2(0, 0), new Vector2(0.75f, 0.5f), new Vector2(20, 0), Vector2.zero);
                var info = l;
                var b = UIKit.Button(bg.transform, "Dołącz", UIKit.Green, () =>
                {
                    var err = gm.JoinLobby(info);
                    if (err != null) joinStatus.text = err;
                }, 30);
                UIKit.Stretch((RectTransform)b.transform, new Vector2(0.77f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero);
                b.interactable = l.Players < l.MaxPlayers;
            }
        }

        // =====================================================================
        //  Lobby
        // =====================================================================

        void BuildLobby()
        {
            var rt = UIKit.Fill(UIKit.Rect("Lobby", root));
            lobby = rt.gameObject;

            // Nagłówek
            var head = UIKit.Panel(rt, "Naglowek", UIKit.Cream);
            UIKit.Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(700, 130));
            UIKit.Shadow(head, 6f, 0.3f);
            lobbyTitle = UIKit.Label(head.transform, "", 46, UIKit.Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(lobbyTitle.rectTransform, new Vector2(0, 0.45f), Vector2.one, new Vector2(10, 0), new Vector2(-10, -6));
            lobbyCode = UIKit.Label(head.transform, "", 30, UIKit.Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(lobbyCode.rectTransform, Vector2.zero, new Vector2(1, 0.5f), new Vector2(10, 8), new Vector2(-10, 0));

            // Ustawienia (lewa strona)
            var left = UIKit.Panel(rt, "Ustawienia gry", UIKit.Cream);
            UIKit.Stretch(left.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(36, 290), new Vector2(600, -36));
            UIKit.Shadow(left, 6f, 0.3f);
            var lc = UIKit.Rect("Zawartosc", left.transform);
            UIKit.Stretch(lc, Vector2.zero, Vector2.one, new Vector2(30, 26), new Vector2(-30, -26));
            UIKit.VStack(lc, 14);
            RowLabel(lc, "Ustawienia gry", 40, 60, UIKit.Orange, FontStyle.Bold);

            RowLabel(lc, "Nazwa lobby", 26, 34, UIKit.Dark, FontStyle.Bold, TextAnchor.MiddleLeft);
            var nameRow = UIKit.Row(lc, "Nazwa", 66);
            lobbyName = UIKit.Input(nameRow.transform, "Nazwa lobby", 28, 28);
            UIKit.Fill(lobbyName.GetComponent<RectTransform>());
            lobbyName.onEndEdit.AddListener(v =>
            {
                gm.Settings.LobbyName = string.IsNullOrEmpty(v.Trim()) ? "Ziemniaczana impreza" : v.Trim();
                lobbyName.text = gm.Settings.LobbyName;
                gm.PublishLobby();
                RefreshLobby();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });

            typeStep = Stepper.Create(lc, "Lobby", new[] { "Otwarte", "Prywatne" }, 0, i => { gm.Settings.IsPrivate = i == 1; gm.PublishLobby(); RefreshLobby(); });
            var modes = new string[MatchSettings.RoundOptions.Length];
            for (int i = 0; i < modes.Length; i++) modes[i] = MatchSettings.ModeLabel(i);
            roundsStep = Stepper.Create(lc, "Tryb gry", modes, 0, i => { gm.Settings.RoundOptionIndex = i; gm.PublishLobby(); RefreshLobby(); });
            lobbyModeInfo = RowLabel(lc, "", 24, 60, UIKit.Soft, FontStyle.Normal, TextAnchor.UpperLeft);
            lobbyGuestNote = RowLabel(lc, "", 24, 40, UIKit.Purple, FontStyle.Bold);

            // Gracze (prawa strona)
            var right = UIKit.Panel(rt, "Gracze", UIKit.Cream);
            UIKit.Stretch(right.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-600, 290), new Vector2(-36, -36));
            UIKit.Shadow(right, 6f, 0.3f);
            var rc = UIKit.Rect("Zawartosc", right.transform);
            UIKit.Stretch(rc, Vector2.zero, Vector2.one, new Vector2(30, 26), new Vector2(-30, -26));
            UIKit.VStack(rc, 10);
            lobbyPlayersTitle = RowLabel(rc, "Gracze", 40, 60, UIKit.Orange, FontStyle.Bold);
            for (int i = 0; i < MatchSettings.MaxPlayers; i++) slotRows.Add(MakeSlotRow(rc));
            addBotButton = RowButton(rc, "+ Dodaj bota", UIKit.Teal, () => gm.AddBot(), 72, 32);

            // Dół
            lobbyHint = UIKit.Label(rt, "", 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(lobbyHint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-650, 160), new Vector2(650, 280));
            UIKit.Outline(lobbyHint, UIKit.Dark, 3f);

            var startRow = UIKit.Rect("Start", rt);
            UIKit.Stretch(startRow, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-560, 36), new Vector2(560, 136));
            startButton = UIKit.Button(startRow, "Rozpocznij grę!", UIKit.Green, () => gm.StartMatch(), 48);
            UIKit.Stretch((RectTransform)startButton.transform, Vector2.zero, new Vector2(0.64f, 1), Vector2.zero, Vector2.zero);
            var leave = UIKit.Button(startRow, "Wyjdź z lobby", UIKit.Red, () => gm.LeaveLobby(), 34);
            UIKit.Stretch((RectTransform)leave.transform, new Vector2(0.67f, 0.1f), new Vector2(1, 0.9f), Vector2.zero, Vector2.zero);
        }

        SlotRow MakeSlotRow(RectTransform parent)
        {
            var row = UIKit.Row(parent, "Miejsce", 74);
            var r = new SlotRow();
            r.Bg = UIKit.Panel(row.transform, "Tlo", Color.white);
            UIKit.Fill(r.Bg.rectTransform);
            r.Swatch = UIKit.Panel(r.Bg.transform, "Kolor", Color.gray);
            UIKit.Stretch(r.Swatch.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(10, 10), new Vector2(72, -10));
            r.Name = UIKit.Label(r.Bg.transform, "", 30, UIKit.Dark, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Stretch(r.Name.rectTransform, new Vector2(0, 0.42f), new Vector2(1, 1), new Vector2(88, 0), new Vector2(-80, -4));
            r.Control = UIKit.Label(r.Bg.transform, "", 22, UIKit.Dark * 0.7f + new Color(0, 0, 0, 0.3f), TextAnchor.MiddleLeft);
            UIKit.Stretch(r.Control.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(88, 4), new Vector2(-80, 0));
            r.Remove = UIKit.Button(r.Bg.transform, "X", UIKit.Red, null, 30);
            UIKit.Stretch((RectTransform)r.Remove.transform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-68, 12), new Vector2(-12, -12));
            return r;
        }

        public void HideLobby()
        {
            lobby.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void ShowLobby()
        {
            HideAll();
            lobby.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            RefreshLobby();
        }

        public void RefreshLobby()
        {
            if (!lobby.activeSelf) return;
            var s = gm.Settings;
            lobbyTitle.text = s.LobbyName;
            lobbyCode.text = (s.IsPrivate ? "Lobby prywatne" : "Lobby otwarte") + "  ·  kod: <color=#FF6F91>" + s.LobbyCode + "</color>";
            if (!lobbyName.isFocused) lobbyName.text = s.LobbyName;
            typeStep.Set(s.IsPrivate ? 1 : 0);
            roundsStep.Set(s.RoundOptionIndex);

            bool host = !gm.JoinedAsGuest;
            lobbyName.interactable = host;
            typeStep.SetInteractable(host);
            roundsStep.SetInteractable(host);
            lobbyGuestNote.text = host ? "" : "Ustawienia wybiera gospodarz: " + gm.JoinedHostName;

            lobbyModeInfo.text = (s.RoundOptionIndex == 0 ? "5" : "10") + " losowych minigier bez powtórek.";

            var slots = gm.Slots;
            lobbyPlayersTitle.text = "Gracze (" + slots.Count + "/" + MatchSettings.MaxPlayers + ")";
            for (int i = 0; i < slotRows.Count; i++)
            {
                var r = slotRows[i];
                r.Remove.onClick.RemoveAllListeners();
                if (i < slots.Count)
                {
                    var slot = slots[i];
                    r.Swatch.color = slot.Color;
                    r.Bg.color = Color.white;
                    r.Name.text = slot.Name;
                    r.Control.text = slot.Kind == ControlKind.Keyboard ? InputBridge.SchemeHint(slot.DeviceId) : slot.Kind == ControlKind.Gamepad ? "Pad: gałka ruch, A skok, X akcja" : "Sterowany przez komputer";
                    r.Remove.gameObject.SetActive(true);
                    r.Remove.onClick.AddListener(() => { AudioManager.Play(Sfx.Click); gm.RemoveSlot(slot); });
                }
                else
                {
                    r.Swatch.color = new Color(0.8f, 0.78f, 0.74f);
                    r.Bg.color = new Color(1, 1, 1, 0.45f);
                    r.Name.text = "Wolne miejsce";
                    r.Control.text = "";
                    r.Remove.gameObject.SetActive(false);
                }
            }
            addBotButton.interactable = slots.Count < MatchSettings.MaxPlayers;
            startButton.interactable = slots.Count >= MatchSettings.MinPlayers;
            UIKit.SetButtonText(startButton, slots.Count >= MatchSettings.MinPlayers ? "Rozpocznij grę!" : "Potrzeba 2 graczy");

            string hint = "Dołącz: <color=#FFC93C>SPACJA</color>, <color=#FFC93C>PRAWY CTRL</color> lub <color=#FFC93C>A</color> na padzie";
            lobbyHint.text = hint;
        }

        // =====================================================================
        //  Pauza
        // =====================================================================

        void BuildPause()
        {
            RectTransform c;
            pause = Card("Pauza", new Vector2(720, 600), UIKit.Cream, out c, "Pauza");
            pauseFirst = RowButton(c, "Wznów grę", UIKit.Green, () => gm.SetPaused(false));
            RowButton(c, "Ustawienia", UIKit.Purple, () => gm.OpenSettings());
            RowButton(c, "Wyjdź do menu", UIKit.Red, () => gm.QuitToMenu());
        }

        public void ShowPause()
        {
            settings.SetActive(false);
            shade.SetActive(true);
            shade.transform.SetAsLastSibling();
            pause.SetActive(true);
            pause.transform.SetAsLastSibling();
            Select(pauseFirst);
        }

        public void HidePause()
        {
            pause.SetActive(false);
            settings.SetActive(false);
            shade.transform.SetSiblingIndex(0);
            shade.SetActive(intro.activeSelf || results.activeSelf);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        // =====================================================================
        //  Zapowiedź rundy
        // =====================================================================

        void BuildIntro()
        {
            RectTransform c;
            // Zapowiedź: tylko to, co ważne — numer, nazwa, cel i sterowanie
            intro = Card("Zapowiedz", new Vector2(1060, 400), UIKit.Cream, out c);
            introRound = RowLabel(c, "", 28, 40, UIKit.Purple);
            introTitle = RowLabel(c, "", 84, 110, UIKit.Orange, FontStyle.Bold);
            var titleShadow = introTitle.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = Color.Lerp(UIKit.Orange, Color.black, 0.35f);
            titleShadow.effectDistance = new Vector2(0, -5);
            introGoal = RowLabel(c, "", 36, 56, UIKit.Dark);
            introDesc = RowLabel(c, "", 30, 0, UIKit.Dark);
            introDesc.transform.parent.gameObject.SetActive(false);
            introMap = RowLabel(c, "", 30, 0, UIKit.Teal);
            introMap.transform.parent.gameObject.SetActive(false);
            introControls = RowLabel(c, "", 24, 70, UIKit.Soft);
        }

        public void ShowIntro(int round, int total, Minigame mg, MapTheme map)
        {
            HideOverlays();
            introRound.text = "Minigra " + round + "/" + total + (round == total && total > 1 ? "  ·  FINAŁ ×2" : "") + "  ·  " + map.Name;
            introControls.text = mg.Controls;
            introTitle.text = mg.Title;
            introGoal.text = mg.Goal;
            introDesc.text = mg.Description;
            introMap.text = "Mapa: " + map.Name + " – " + map.Tagline + (map.Grip < 0.6f ? " (ślisko!)" : "");
            shade.SetActive(true);
            intro.SetActive(true);
        }

        public void HideIntro()
        {
            intro.SetActive(false);
            shade.SetActive(false);
        }

        // =====================================================================
        //  Wyniki rundy i finał
        // =====================================================================

        void BuildResults()
        {
            RectTransform c;
            results = Card("Wyniki", new Vector2(1100, 900), UIKit.Cream, out c, "Wyniki minigry");
            resultsTitle = RowLabel(c, "", 34, 50, UIKit.Purple, FontStyle.Bold);
            var listRow = UIKit.Row(c, "Lista", 560);
            resultsList = UIKit.Fill(UIKit.Rect("Pozycje", listRow.transform));
            UIKit.VStack(resultsList, 10);
            RowLabel(c, "SPACJA / A – dalej", 24, 40, UIKit.Soft);
        }

        static readonly Color[] medal = { Shapes.Hex("#FFC61A"), Shapes.Hex("#C9D3DD"), Shapes.Hex("#E39A5B") };

        void ResultRow(RectTransform parent, int place, PlayerSlot slot, string left, string right)
        {
            var row = UIKit.Row(parent, "Pozycja", 80);
            var bg = UIKit.Panel(row.transform, "Tlo", Color.white);
            UIKit.Fill(bg.rectTransform);
            var badge = UIKit.Panel(bg.transform, "Miejsce", place <= 3 ? medal[place - 1] : new Color(0.85f, 0.82f, 0.78f));
            badge.sprite = UIKit.Circle;
            UIKit.Stretch(badge.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(12, 8), new Vector2(76, -8));
            var pl = UIKit.Label(badge.transform, place.ToString(), 34, UIKit.Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Fill(pl.rectTransform);
            var sw = UIKit.Panel(bg.transform, "Kolor", slot.Color);
            UIKit.Stretch(sw.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(90, 14), new Vector2(110, -14));
            var name = UIKit.Label(bg.transform, left, 32, UIKit.Dark, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Stretch(name.rectTransform, Vector2.zero, new Vector2(0.62f, 1), new Vector2(126, 0), Vector2.zero);
            var r = UIKit.Label(bg.transform, right, 30, UIKit.Dark, TextAnchor.MiddleRight, FontStyle.Bold);
            UIKit.Stretch(r.rectTransform, new Vector2(0.55f, 0), Vector2.one, Vector2.zero, new Vector2(-24, 0));
        }

        public void ShowRoundResults(int round, int total, string title, List<ResultLine> lines)
        {
            HideOverlays();
            resultsTitle.text = "Minigra " + round + " z " + total + " · " + title;
            foreach (Transform t in resultsList) Destroy(t.gameObject);
            foreach (var l in lines)
            {
                string stat = string.IsNullOrEmpty(l.Stat) ? "" : "  <size=24>(" + l.Stat + ")</size>";
                ResultRow(resultsList, l.Place, l.Slot, l.Slot.Name + stat,
                    "<color=#2EA043>+" + l.Gained + "</color>   razem: " + l.Slot.Points + " pkt");
            }
            shade.SetActive(true);
            results.SetActive(true);
        }

        public void HideRoundResults()
        {
            results.SetActive(false);
            shade.SetActive(false);
        }

        void BuildFinal()
        {
            var card = UIKit.Panel(root, "Final", UIKit.Cream);
            UIKit.Stretch(card.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-860, -500), new Vector2(-40, 500));
            UIKit.Shadow(card, 10f, 0.3f);
            final = card.gameObject;
            var c = UIKit.Rect("Zawartosc", card.transform);
            UIKit.Stretch(c, Vector2.zero, Vector2.one, new Vector2(40, 36), new Vector2(-40, -36));
            UIKit.VStack(c, 12);
            var h = RowLabel(c, "Koniec gry!", 64, 84, UIKit.Orange, FontStyle.Bold);
            UIKit.Outline(h, UIKit.Dark, 3f);
            finalWinner = RowLabel(c, "", 38, 100, UIKit.Purple, FontStyle.Bold);
            var listRow = UIKit.Row(c, "Lista", 520);
            finalList = UIKit.Fill(UIKit.Rect("Pozycje", listRow.transform));
            UIKit.VStack(finalList, 6);
            finalFirst = RowButton(c, "Zagraj ponownie", UIKit.Green, () => gm.PlayAgain(), 80);
            RowButton(c, "Menu główne", UIKit.Purple, () => gm.GoToMainMenu(), 70, 32);
        }

        public void ShowFinal(List<PlayerSlot> standings, List<PlayerSlot> champs)
        {
            HideAll();
            var names = new List<string>();
            foreach (var ch in champs) names.Add("<color=#" + ColorUtility.ToHtmlStringRGB(ch.Color) + ">" + ch.Name + "</color>");
            finalWinner.text = (champs.Count > 1 ? "Remis! Wygrywają: " : "Wygrywa: ") + string.Join(", ", names.ToArray()) + "!";
            foreach (Transform t in finalList) Destroy(t.gameObject);
            int place = 0, prevPts = int.MinValue, shown = 0;
            foreach (var s in standings)
            {
                shown++;
                if (s.Points != prevPts) place = shown;
                prevPts = s.Points;
                ResultRow(finalList, place, s, s.Name, s.Points + " pkt  ·  " + s.Wins + " wygr.");
            }
            final.SetActive(true);
            Select(finalFirst);
        }
    }
}
