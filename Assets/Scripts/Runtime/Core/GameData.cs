using UnityEngine;

namespace PotatoParty
{
    public enum MapId { Farm, Orchard, Beach, Ice, Kitchen, Volcano, Canteen, Stadium, Playground, Forest, Cornfield, Garden }

    public enum MapSelectMode { MatchMinigame = 0, PlayerChoice = 1, FortuneWheel = 2 }

    public enum ControlKind { Keyboard, Gamepad, Bot }

    /// <summary>Jeden gracz w lobby: kolor, imię, czym steruje i ile ma punktów.</summary>
    public class PlayerSlot
    {
        public int Index;
        public string Name;
        public Color Color;
        public ControlKind Kind;
        public int DeviceId; // numer schematu klawiatury albo id pada
        public int Points;
        public int Wins;
        /// <summary>Indeks skórki z <see cref="PotatoSkins.All"/>.</summary>
        public int Skin;

        public PotatoSkin SkinData { get { return PotatoSkins.Get(Skin); } }

        public bool IsBot { get { return Kind == ControlKind.Bot; } }

        public string ControlLabel
        {
            get
            {
                switch (Kind)
                {
                    case ControlKind.Keyboard: return InputBridge.SchemeName(DeviceId);
                    case ControlKind.Gamepad: return "Pad";
                    default: return "Bot";
                }
            }
        }

        public Vector2 ReadMove()
        {
            if (Kind == ControlKind.Keyboard) return InputBridge.KeyboardMove(DeviceId);
            if (Kind == ControlKind.Gamepad) return InputBridge.GamepadMove(DeviceId);
            return Vector2.zero;
        }

        public bool ReadJump()
        {
            if (Kind == ControlKind.Keyboard) return InputBridge.KeyboardJumpDown(DeviceId);
            if (Kind == ControlKind.Gamepad) return InputBridge.GamepadJumpDown(DeviceId);
            return false;
        }

        /// <summary>Czy przycisk skoku jest wciąż trzymany (zmienna wysokość skoku).</summary>
        public bool ReadJumpHeld()
        {
            if (Kind == ControlKind.Keyboard) return InputBridge.KeyboardJumpHeld(DeviceId);
            if (Kind == ControlKind.Gamepad) return InputBridge.GamepadJumpHeld(DeviceId);
            return false;
        }

        public bool ReadAction()
        {
            if (Kind == ControlKind.Keyboard) return InputBridge.KeyboardActionDown(DeviceId);
            if (Kind == ControlKind.Gamepad) return InputBridge.GamepadActionDown(DeviceId);
            return false;
        }
    }

    /// <summary>Ustawienia jednej rozgrywki wybierane w lobby.</summary>
    public class MatchSettings
    {
        public static readonly int[] RoundOptions = { 5, 10 };
        public static readonly string[] ModeNames = { "Szybka impreza", "Wielka impreza" };
        public const int MaxPlayers = 6;
        public const int MinPlayers = 2;

        public string LobbyName = "Ziemniaczana impreza";
        public string LobbyCode = "";
        public bool IsPrivate;
        public int RoundOptionIndex = 0;
        public MapSelectMode MapMode = MapSelectMode.MatchMinigame;

        public int Rounds { get { return RoundOptions[Mathf.Clamp(RoundOptionIndex, 0, RoundOptions.Length - 1)]; } }

        public static string ModeLabel(int index)
        {
            index = Mathf.Clamp(index, 0, RoundOptions.Length - 1);
            return ModeNames[index] + " (" + RoundOptions[index] + " minigier)";
        }

        public static string MapModeName(MapSelectMode m)
        {
            switch (m)
            {
                case MapSelectMode.PlayerChoice: return "Wybór gracza";
                case MapSelectMode.FortuneWheel: return "Koło fortuny";
                default: return "Dopasowana do minigry";
            }
        }

        public static string NewCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var c = new char[6];
            for (int i = 0; i < c.Length; i++) c[i] = chars[Random.Range(0, chars.Length)];
            return new string(c);
        }
    }

    public static class PlayerColors
    {
        public static readonly Color[] All =
        {
            new Color(0.95f, 0.30f, 0.30f), // czerwony
            new Color(0.25f, 0.55f, 0.98f), // niebieski
            new Color(0.35f, 0.82f, 0.35f), // zielony
            new Color(1.00f, 0.82f, 0.20f), // żółty
            new Color(0.70f, 0.40f, 0.95f), // fioletowy
            new Color(1.00f, 0.55f, 0.15f), // pomarańczowy
        };

        public static readonly string[] Names = { "Czerwony", "Niebieski", "Zielony", "Żółty", "Fioletowy", "Pomarańczowy" };

        public static readonly string[] BotNames = { "Frytek", "Pyra", "Kartofel", "Purée", "Placuszek", "Bulwa", "Chipsik", "Kopytko" };
    }

    /// <summary>Ustawienia gry zapisywane między uruchomieniami.</summary>
    public static class GameSettings
    {
        public static int MasterVolume = 80;
        public static int MusicVolume = 70;
        public static int SfxVolume = 80;
        public static bool Fullscreen = true;
        public static int Quality = -1;

        public static void Load()
        {
            MasterVolume = PlayerPrefs.GetInt("pp_master", 80);
            MusicVolume = PlayerPrefs.GetInt("pp_music", 70);
            SfxVolume = PlayerPrefs.GetInt("pp_sfx", 80);
            Fullscreen = PlayerPrefs.GetInt("pp_fullscreen", 1) == 1;
            Quality = PlayerPrefs.GetInt("pp_quality", QualitySettings.GetQualityLevel());
            Apply();
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("pp_master", MasterVolume);
            PlayerPrefs.SetInt("pp_music", MusicVolume);
            PlayerPrefs.SetInt("pp_sfx", SfxVolume);
            PlayerPrefs.SetInt("pp_fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("pp_quality", Quality);
            PlayerPrefs.Save();
            Apply();
        }

        public static void Apply()
        {
            AudioListener.volume = MasterVolume / 100f;
            if (!Application.isEditor) Screen.fullScreen = Fullscreen;
            if (Quality >= 0 && Quality < QualitySettings.names.Length && Quality != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(Quality, true);
            PostFx.ApplyQuality(QualitySettings.GetQualityLevel());
            if (AudioManager.Instance != null) AudioManager.Instance.ApplyVolumes();
        }
    }
}
