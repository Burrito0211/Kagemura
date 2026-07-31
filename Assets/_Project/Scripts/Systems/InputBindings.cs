using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kagemura.Systems
{
    /// <summary>
    /// The rebindable half of the input setup: which player actions the settings window is
    /// allowed to offer, how to reach the one binding each of them owns on keyboard/mouse, and
    /// how the player's choices survive a restart.
    ///
    /// Only keyboard/mouse is rebindable. Every action also carries a Gamepad binding, and those
    /// are left alone — a pad has a fixed button layout the player already knows, and offering to
    /// remap it doubles the rows in the window for something nobody asks for on a short game.
    ///
    /// Overrides are stored as the Input System's own override JSON in PlayerPrefs, so this holds
    /// no table of its own that could drift out of step with the .inputactions asset.
    /// </summary>
    public static class InputBindings
    {
        /// <summary>One row in the settings window: an action, and which binding of it to edit.</summary>
        public sealed class Entry
        {
            /// <summary>Action name as it appears in the Player map.</summary>
            public readonly string ActionName;

            /// <summary>Label shown to the player.</summary>
            public readonly string DisplayName;

            /// <summary>Composite part ("left"/"right") for Move, or null for a plain binding.</summary>
            public readonly string CompositePart;

            public Entry(string actionName, string displayName, string compositePart = null)
            {
                ActionName = actionName;
                DisplayName = displayName;
                CompositePart = compositePart;
            }
        }

        public const string PlayerMapName = "Player";

        private const string KeyboardScheme = "Keyboard&Mouse";
        private const string PrefsKey = "Kagemura.InputOverrides.v1";

        /// <summary>
        /// Everything the player character can actually do, in the order the window lists it:
        /// movement first, then the moment-to-moment combat keys, then the two specials.
        ///
        /// Move up/down are deliberately absent — the Move composite binds W/S, but this is a
        /// side-scroller and nothing reads the vertical axis, so listing them would promise a
        /// control that does nothing. If a special ever aims with it, add the two entries here
        /// and the window grows a row on its own.
        /// </summary>
        private static readonly Entry[] _entries =
        {
            new Entry("Move", "Move Left", "left"),
            new Entry("Move", "Move Right", "right"),
            new Entry("Jump", "Jump"),
            new Entry("Attack", "Attack"),
            new Entry("Dodge", "Dodge"),
            new Entry("Parry", "Parry"),
            new Entry("SwitchWeapon", "Switch Weapon"),
            new Entry("SpecialBurst", "Special — Slam"),
            new Entry("SpecialDash", "Special — Dash-Strike"),
        };

        public static IReadOnlyList<Entry> Entries => _entries;

        // --- Lookup ---------------------------------------------------------------------

        /// <summary>
        /// The action behind an entry, or null if the asset isn't loaded. Looked up through the
        /// Player map rather than the asset, so a same-named UI action can never be returned by
        /// mistake.
        /// </summary>
        public static InputAction FindAction(Entry entry)
        {
            if (entry == null) return null;
            var map = InputSystem.actions?.FindActionMap(PlayerMapName, throwIfNotFound: false);
            return map?.FindAction(entry.ActionName);
        }

        /// <summary>
        /// Index of the keyboard/mouse binding this entry edits, or -1 if the action has none.
        ///
        /// Actions carry several bindings — a gamepad one, sometimes a second keyboard alternate
        /// (Move has both WASD and the arrow keys; Attack has both left-click and Enter). The
        /// first keyboard/mouse match is the primary, and the alternates stay put: rebinding
        /// "Move Left" moves A and leaves the left arrow as a fallback, which is what a player
        /// who rebinds one key and not the other expects.
        /// </summary>
        public static int BindingIndex(InputAction action, Entry entry)
        {
            if (action == null || entry == null) return -1;

            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (string.IsNullOrEmpty(binding.groups) || !binding.groups.Contains(KeyboardScheme))
                    continue;

                if (entry.CompositePart != null)
                {
                    if (binding.isPartOfComposite && binding.name == entry.CompositePart) return i;
                }
                else if (!binding.isComposite && !binding.isPartOfComposite)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>What this entry is bound to right now, as a human-readable key name.</summary>
        public static string DisplayString(Entry entry)
        {
            var action = FindAction(entry);
            int index = BindingIndex(action, entry);
            if (index < 0) return "—";

            string display = action.GetBindingDisplayString(index);
            return string.IsNullOrEmpty(display) ? "—" : display;
        }

        /// <summary>The control path currently in effect for this entry, override included.</summary>
        public static string EffectivePath(Entry entry)
        {
            var action = FindAction(entry);
            int index = BindingIndex(action, entry);
            return index < 0 ? null : action.bindings[index].effectivePath;
        }

        /// <summary>
        /// The other entry already using this entry's key, or null if it's free.
        ///
        /// Checked after the rebind has been applied, because the Input System resolves the
        /// control to a path for us — comparing paths is exact, where comparing display names
        /// would treat two different controls that print the same as a clash.
        /// </summary>
        public static Entry FindConflict(Entry entry)
        {
            string path = EffectivePath(entry);
            if (string.IsNullOrEmpty(path)) return null;

            foreach (var other in _entries)
            {
                if (ReferenceEquals(other, entry)) continue;
                if (EffectivePath(other) == path) return other;
            }

            return null;
        }

        // --- Overrides ------------------------------------------------------------------

        /// <summary>
        /// Put a binding back the way it was. Takes the previous override rather than calling
        /// RemoveBindingOverride, which would also throw away a choice the player made in an
        /// earlier session — the distinction matters when a rebind is rejected for a conflict.
        /// </summary>
        public static void RestoreOverride(InputAction action, int bindingIndex, string previousOverride)
        {
            if (action == null || bindingIndex < 0) return;

            if (string.IsNullOrEmpty(previousOverride))
                action.RemoveBindingOverride(bindingIndex);
            else
                action.ApplyBindingOverride(bindingIndex, previousOverride);
        }

        /// <summary>Write every override in the asset to PlayerPrefs.</summary>
        public static void Save()
        {
            var asset = InputSystem.actions;
            if (asset == null) return;

            string json = asset.SaveBindingOverridesAsJson();
            if (string.IsNullOrEmpty(json)) PlayerPrefs.DeleteKey(PrefsKey);
            else PlayerPrefs.SetString(PrefsKey, json);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Re-apply saved overrides. Runs before the first scene loads so the player's keys are
        /// live from the opening frame — it can't wait for the settings window, which only exists
        /// in scenes that happen to carry one.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            var asset = InputSystem.actions;
            if (asset == null) return;

            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json)) asset.LoadBindingOverridesFromJson(json);
        }

        /// <summary>Drop every override and forget them, back to the keys in the asset.</summary>
        public static void ResetAll()
        {
            var asset = InputSystem.actions;
            if (asset != null) asset.RemoveAllBindingOverrides();

            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }
    }
}
