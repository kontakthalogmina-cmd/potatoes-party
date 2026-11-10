using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PotatoParty
{
    /// <summary>
    /// Jedno miejsce, które czyta klawiaturę i pady. Działa zarówno z nowym Input System,
    /// jak i ze starym Input Managerem (wtedy obsługiwane są tylko klawiatury).
    /// </summary>
    public static class InputBridge
    {
        public const int KeyboardSchemes = 2;

        public static string SchemeName(int scheme)
        {
            return scheme == 0 ? "Klawiatura WASD" : "Klawiatura strzałki";
        }

        public static string SchemeHint(int scheme)
        {
            return scheme == 0
                ? "WASD ruch, SPACJA skok, LEWY SHIFT akcja"
                : "Strzałki ruch, PRAWY CTRL skok, PRAWY SHIFT akcja";
        }

#if ENABLE_INPUT_SYSTEM
        static bool Pressed(Key k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[k].isPressed;
        }

        static bool Down(Key k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[k].wasPressedThisFrame;
        }

        public static Vector2 KeyboardMove(int scheme)
        {
            float x = 0, y = 0;
            if (scheme == 0)
            {
                if (Pressed(Key.A)) x -= 1;
                if (Pressed(Key.D)) x += 1;
                if (Pressed(Key.S)) y -= 1;
                if (Pressed(Key.W)) y += 1;
            }
            else
            {
                if (Pressed(Key.LeftArrow)) x -= 1;
                if (Pressed(Key.RightArrow)) x += 1;
                if (Pressed(Key.DownArrow)) y -= 1;
                if (Pressed(Key.UpArrow)) y += 1;
            }
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        public static bool KeyboardJumpDown(int scheme)
        {
            return scheme == 0 ? Down(Key.Space) : (Down(Key.RightCtrl) || Down(Key.Numpad0));
        }

        /// <summary>Skok przytrzymany (wysokość skoku zależy od czasu przytrzymania).</summary>
        public static bool KeyboardJumpHeld(int scheme)
        {
            return scheme == 0 ? Pressed(Key.Space) : (Pressed(Key.RightCtrl) || Pressed(Key.Numpad0));
        }

        public static bool KeyboardActionDown(int scheme)
        {
            return scheme == 0 ? (Down(Key.LeftShift) || Down(Key.E)) : (Down(Key.RightShift) || Down(Key.Enter) || Down(Key.NumpadEnter));
        }

        public static bool PauseDown()
        {
            if (Down(Key.Escape) || Down(Key.P)) return true;
            foreach (var pad in Gamepad.all)
                if (pad.startButton.wasPressedThisFrame) return true;
            return false;
        }

        public static bool AnyConfirmDown()
        {
            if (Down(Key.Space) || Down(Key.Enter)) return true;
            foreach (var pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame) return true;
            return false;
        }

        static readonly List<int> padIds = new List<int>();

        public static List<int> GamepadIds()
        {
            padIds.Clear();
            foreach (var pad in Gamepad.all) padIds.Add(pad.deviceId);
            return padIds;
        }

        static Gamepad Pad(int id)
        {
            foreach (var pad in Gamepad.all)
                if (pad.deviceId == id) return pad;
            return null;
        }

        public static bool GamepadConnected(int id) { return Pad(id) != null; }

        public static Vector2 GamepadMove(int id)
        {
            var pad = Pad(id);
            if (pad == null) return Vector2.zero;
            Vector2 v = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
            if (v.magnitude < 0.2f) return Vector2.zero;
            return Vector2.ClampMagnitude(v, 1f);
        }

        public static bool GamepadJumpDown(int id)
        {
            var pad = Pad(id);
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        public static bool GamepadJumpHeld(int id)
        {
            var pad = Pad(id);
            return pad != null && pad.buttonSouth.isPressed;
        }

        public static bool GamepadActionDown(int id)
        {
            var pad = Pad(id);
            return pad != null && (pad.buttonWest.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame);
        }

        public static string GamepadName(int id, int index)
        {
            return "Pad " + (index + 1);
        }
#else
        public static Vector2 KeyboardMove(int scheme)
        {
            float x = 0, y = 0;
            if (scheme == 0)
            {
                if (Input.GetKey(KeyCode.A)) x -= 1;
                if (Input.GetKey(KeyCode.D)) x += 1;
                if (Input.GetKey(KeyCode.S)) y -= 1;
                if (Input.GetKey(KeyCode.W)) y += 1;
            }
            else
            {
                if (Input.GetKey(KeyCode.LeftArrow)) x -= 1;
                if (Input.GetKey(KeyCode.RightArrow)) x += 1;
                if (Input.GetKey(KeyCode.DownArrow)) y -= 1;
                if (Input.GetKey(KeyCode.UpArrow)) y += 1;
            }
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        public static bool KeyboardJumpDown(int scheme)
        {
            return scheme == 0 ? Input.GetKeyDown(KeyCode.Space) : (Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.Keypad0));
        }

        public static bool KeyboardJumpHeld(int scheme)
        {
            return scheme == 0 ? Input.GetKey(KeyCode.Space) : (Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.Keypad0));
        }

        public static bool KeyboardActionDown(int scheme)
        {
            return scheme == 0
                ? (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.E))
                : (Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter));
        }

        public static bool PauseDown() { return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P); }
        public static bool AnyConfirmDown() { return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return); }

        // Stary Input Manager nie rozróżnia padów bez ręcznej konfiguracji osi,
        // dlatego pady działają tylko z nowym Input System (domyślnie włączonym w tym projekcie).
        static readonly List<int> padIds = new List<int>();
        public static List<int> GamepadIds() { padIds.Clear(); return padIds; }
        public static bool GamepadConnected(int id) { return false; }
        public static Vector2 GamepadMove(int id) { return Vector2.zero; }
        public static bool GamepadJumpDown(int id) { return false; }
        public static bool GamepadJumpHeld(int id) { return false; }
        public static bool GamepadActionDown(int id) { return false; }
        public static string GamepadName(int id, int index) { return "Pad " + (index + 1); }
#endif
    }
}
