using System;
using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanKeyAction
    {
        MoveForward = 0,
        MoveBack = 1,
        MoveLeft = 2,
        MoveRight = 3,
        Jump = 4,
        Interact = 5,
        Drop = 6,
        Crouch = 7,
        Equipment = 8
    }

    /// <summary>
    /// Central rebindable key map. Persisted in PlayerPrefs, edited from the pause menu.
    /// Gameplay code reads keys through this class instead of hardcoded KeyCodes.
    /// </summary>
    public static class MiniVanKeyBindings
    {
        private const string PrefsPrefix = "MiniVan.Key.";

        private static readonly MiniVanKeyAction[] AllActions =
            (MiniVanKeyAction[])Enum.GetValues(typeof(MiniVanKeyAction));

        private static readonly KeyCode[] Bindings = new KeyCode[AllActions.Length];
        private static readonly KeyCode[] Draft = new KeyCode[AllActions.Length];
        private static bool loaded;
        private static bool editing;

        public static MiniVanKeyAction[] Actions => AllActions;
        public static bool IsEditing => editing;

        public static KeyCode GetDefault(MiniVanKeyAction action)
        {
            switch (action)
            {
                case MiniVanKeyAction.MoveForward: return KeyCode.W;
                case MiniVanKeyAction.MoveBack: return KeyCode.S;
                case MiniVanKeyAction.MoveLeft: return KeyCode.A;
                case MiniVanKeyAction.MoveRight: return KeyCode.D;
                case MiniVanKeyAction.Jump: return KeyCode.Space;
                case MiniVanKeyAction.Interact: return KeyCode.E;
                case MiniVanKeyAction.Drop: return KeyCode.Q;
                case MiniVanKeyAction.Crouch: return KeyCode.LeftControl;
                case MiniVanKeyAction.Equipment: return KeyCode.I;
                default: return KeyCode.None;
            }
        }

        public static string GetLabel(MiniVanKeyAction action)
        {
            switch (action)
            {
                case MiniVanKeyAction.MoveForward: return "Move Forward";
                case MiniVanKeyAction.MoveBack: return "Move Back";
                case MiniVanKeyAction.MoveLeft: return "Move Left";
                case MiniVanKeyAction.MoveRight: return "Move Right";
                case MiniVanKeyAction.Jump: return "Jump";
                case MiniVanKeyAction.Interact: return "Interact / Use";
                case MiniVanKeyAction.Drop: return "Drop / Release";
                case MiniVanKeyAction.Crouch: return "Crouch";
                case MiniVanKeyAction.Equipment: return "Equipment / Inventory";
                default: return action.ToString();
            }
        }

        public static KeyCode Get(MiniVanKeyAction action)
        {
            EnsureLoaded();
            return Bindings[(int)action];
        }

        /// <summary>Value shown in the rebind UI (draft while editing, otherwise committed).</summary>
        public static KeyCode GetEdit(MiniVanKeyAction action)
        {
            EnsureLoaded();
            return editing ? Draft[(int)action] : Bindings[(int)action];
        }

        public static void BeginEdit()
        {
            EnsureLoaded();
            for (int i = 0; i < Bindings.Length; i++)
            {
                Draft[i] = Bindings[i];
            }

            editing = true;
        }

        public static void SetEdit(MiniVanKeyAction action, KeyCode key)
        {
            EnsureLoaded();
            if (!editing)
            {
                BeginEdit();
            }

            // Swap if the key is already used in the draft.
            for (int i = 0; i < Draft.Length; i++)
            {
                if (i != (int)action && Draft[i] == key)
                {
                    Draft[i] = Draft[(int)action];
                }
            }

            Draft[(int)action] = key;
        }

        public static void ApplyEdit()
        {
            if (!editing)
            {
                return;
            }

            for (int i = 0; i < Draft.Length; i++)
            {
                Bindings[i] = Draft[i];
                PlayerPrefs.SetInt(PrefsPrefix + AllActions[i], (int)Bindings[i]);
            }

            PlayerPrefs.Save();
            editing = false;
        }

        public static void DiscardEdit()
        {
            editing = false;
        }

        public static void ResetEditToDefaults()
        {
            if (!editing)
            {
                BeginEdit();
            }

            for (int i = 0; i < AllActions.Length; i++)
            {
                Draft[i] = GetDefault(AllActions[i]);
            }
        }

        public static bool HasPendingEdit()
        {
            if (!editing)
            {
                return false;
            }

            for (int i = 0; i < Bindings.Length; i++)
            {
                if (Draft[i] != Bindings[i])
                {
                    return true;
                }
            }

            return false;
        }

        public static void Set(MiniVanKeyAction action, KeyCode key)
        {
            EnsureLoaded();

            // If the key is taken by another action, swap so nothing ends up unbound.
            for (int i = 0; i < Bindings.Length; i++)
            {
                if (i != (int)action && Bindings[i] == key)
                {
                    Bindings[i] = Bindings[(int)action];
                    PlayerPrefs.SetInt(PrefsPrefix + (MiniVanKeyAction)i, (int)Bindings[i]);
                }
            }

            Bindings[(int)action] = key;
            PlayerPrefs.SetInt(PrefsPrefix + action, (int)key);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            for (int i = 0; i < AllActions.Length; i++)
            {
                Bindings[i] = GetDefault(AllActions[i]);
                PlayerPrefs.SetInt(PrefsPrefix + AllActions[i], (int)Bindings[i]);
            }

            loaded = true;
            editing = false;
            PlayerPrefs.Save();
        }

        public static bool GetKey(MiniVanKeyAction action)
        {
            return Input.GetKey(Get(action));
        }

        public static bool GetKeyDown(MiniVanKeyAction action)
        {
            return Input.GetKeyDown(Get(action));
        }

        public static bool GetKeyUp(MiniVanKeyAction action)
        {
            return Input.GetKeyUp(Get(action));
        }

        /// <summary>-1..1 strafe axis built from the bound movement keys.</summary>
        public static float MoveHorizontal()
        {
            float value = 0f;
            if (GetKey(MiniVanKeyAction.MoveLeft))
            {
                value -= 1f;
            }

            if (GetKey(MiniVanKeyAction.MoveRight))
            {
                value += 1f;
            }

            return value;
        }

        /// <summary>-1..1 forward axis built from the bound movement keys.</summary>
        public static float MoveVertical()
        {
            float value = 0f;
            if (GetKey(MiniVanKeyAction.MoveBack))
            {
                value -= 1f;
            }

            if (GetKey(MiniVanKeyAction.MoveForward))
            {
                value += 1f;
            }

            return value;
        }

        public static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None: return "---";
                case KeyCode.LeftControl: return "L-Ctrl";
                case KeyCode.RightControl: return "R-Ctrl";
                case KeyCode.LeftShift: return "L-Shift";
                case KeyCode.RightShift: return "R-Shift";
                case KeyCode.LeftAlt: return "L-Alt";
                case KeyCode.RightAlt: return "R-Alt";
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                default: return key.ToString();
            }
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            for (int i = 0; i < AllActions.Length; i++)
            {
                int stored = PlayerPrefs.GetInt(PrefsPrefix + AllActions[i], (int)GetDefault(AllActions[i]));
                Bindings[i] = (KeyCode)stored;
            }

            loaded = true;
        }
    }
}
