using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Click-to-bind for active plugins. The plugin list shows the live key inside the description
    // ("Press [Z] ..."); clicking it calls Begin, the next keyboard key pressed becomes the new
    // binding (Esc cancels), and because descriptions read the assigned key every time they are
    // built, the text updates by itself.
    public static class KeybindEditor
    {
        private const string UnboundLabel = "Unbound";
        private const string ListeningLabel = "...";

        // Listening only lasts while the plugin list keeps calling Tick, so closing the list some
        // other way can never leave every hotkey blocked.
        private const uint ExpireTicks = 3;

        private static ModKeybind target;
        private static uint lastTick;
        private static uint suppressUntil;
        private static Keys[] held = Array.Empty<Keys>();
        private static readonly object TextTaker = new object();

        public static bool IsListening
        {
            get
            {
                if (target == null)
                    return false;

                if (Main.GameUpdateCount - lastTick > ExpireTicks)
                {
                    target = null;
                    return false;
                }

                return true;
            }
        }

        // True while listening and for a couple of ticks after, so the very key press that finishes
        // the pick (or Esc) is not also handled as an ordinary hotkey by the rest of the mod.
        public static bool BlocksInput => IsListening || Main.GameUpdateCount <= suppressUntil;

        public static bool IsListeningFor(ModKeybind keybind)
        {
            return keybind != null && IsListening && target == keybind;
        }

        public static void Begin(ModKeybind keybind)
        {
            if (keybind == null || Main.dedServ)
                return;

            target = keybind;
            lastTick = Main.GameUpdateCount;
            // Keys already held when the click lands (Ctrl, a movement key) must not count as the pick.
            held = Main.keyState.GetPressedKeys();
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public static void Cancel()
        {
            target = null;
        }

        // Tells the game something is consuming typed input - same trick as the list's search box -
        // so the inventory/Esc handling and other hotkeys stay quiet while a key is being picked.
        public static void HoldInput()
        {
            if (!IsListening)
                return;

            PlayerInput.WritingText = true;
            Main.CurrentInputTextTakerOverride = TextTaker;
        }

        // Called every tick by the plugin list while it is open.
        public static void Tick()
        {
            if (!IsListening)
                return;

            lastTick = Main.GameUpdateCount;
            HoldInput();

            Keys[] pressed = Main.keyState.GetPressedKeys();
            Keys picked = Keys.None;
            foreach (Keys key in pressed)
            {
                if (key != Keys.None && Array.IndexOf(held, key) < 0)
                {
                    picked = key;
                    break;
                }
            }
            held = pressed;

            if (picked == Keys.None)
                return;

            suppressUntil = Main.GameUpdateCount + 2;

            if (picked == Keys.Escape)
            {
                Cancel();
                SoundEngine.PlaySound(SoundID.MenuClose);
                return;
            }

            Bind(picked);
        }

        private static void Bind(Keys key)
        {
            List<string> keys = GetKeys(target);
            if (keys != null)
            {
                // The list is the live one the game reads, so editing it rebinds immediately.
                keys.Clear();
                keys.Add(key.ToString());
                PlayerInput.Save();
            }

            target = null;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        private static List<string> GetKeys(ModKeybind keybind)
        {
            if (keybind == null || Main.dedServ)
                return null;

            try
            {
                return keybind.GetAssignedKeys();
            }
            catch (Exception)
            {
                // Input profile not ready yet (very early load) - treat as unbound.
                return null;
            }
        }

        // One space-free word, so the description wrapper keeps it in a single piece.
        public static string GetLabel(ModKeybind keybind, out string hex)
        {
            if (IsListeningFor(keybind))
            {
                hex = AugmentTextColors.ActiveHex;
                return ListeningLabel;
            }

            List<string> keys = GetKeys(keybind);
            if (keys == null || keys.Count == 0)
            {
                hex = AugmentTextColors.KeybindUnboundHex;
                return UnboundLabel;
            }

            hex = AugmentTextColors.KeybindHex;
            return string.Join("/", keys.Select(Pretty));
        }

        private static string Pretty(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "?";

            // "D1" is the number row key; show it as the digit.
            if (key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1]))
                return key.Substring(1);

            // Brackets and spaces would break the colour tag the label sits in.
            return key.Replace(" ", "").Replace("[", "").Replace("]", "");
        }

        // Finds the "[label]" button inside an already wrapped description line. Returns where it
        // starts and how long it is (brackets included), so the panel can measure and hit-test it.
        public static bool TryFindLabel(string line, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (string.IsNullOrEmpty(line))
                return false;

            string[] hexes = { AugmentTextColors.KeybindHex, AugmentTextColors.KeybindUnboundHex, AugmentTextColors.ActiveHex };
            foreach (string hex in hexes)
            {
                // The outer [ ] are plain characters sitting right against the colour tag.
                string open = "[[c/" + hex + ":";
                int i = line.IndexOf(open, StringComparison.Ordinal);
                if (i < 0)
                    continue;

                int close = line.IndexOf("]]", i + open.Length, StringComparison.Ordinal);
                if (close < 0)
                    continue;

                start = i;
                length = close + 2 - i;
                return true;
            }

            return false;
        }
    }
}
