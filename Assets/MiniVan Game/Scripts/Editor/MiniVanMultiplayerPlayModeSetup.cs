using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// One-click Multiplayer Play Mode defaults for fast local NGO testing.
    /// </summary>
    public static class MiniVanMultiplayerPlayModeSetup
    {
        private const string MenuPath = "MiniVan/Multiplayer/Apply Fast Local MPPM Settings";

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "MiniVan MPPM",
                    "Exit Play Mode first, then run this again.",
                    "OK");
                return;
            }

            // Keep domain reload (NGO-safe). Skip scene reload for faster Play.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            var opts = EditorSettings.enterPlayModeOptions;
            opts |= EnterPlayModeOptions.DisableSceneReload;
            opts &= ~EnterPlayModeOptions.DisableDomainReload;
            EditorSettings.enterPlayModeOptions = opts;

            try
            {
                ApplyMppmInternals();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MiniVan MPPM] Internal settings skipped: " + ex.Message);
            }

            EditorApplication.ExecuteMenuItem("Window/Multiplayer/Multiplayer Play Mode");

            EditorUtility.DisplayDialog(
                "MiniVan MPPM",
                "Configured for fast local 2-player tests.\n\n" +
                "Already set:\n" +
                "• Mute Players ON\n" +
                "• Launch screens on VP OFF\n" +
                "• Only Player 2 (keep 3/4 off)\n" +
                "• Tags: Main=Host, Player2=Client\n" +
                "• Enter Play Mode: no scene reload\n\n" +
                "How to test:\n" +
                "1) Press Play\n" +
                "2) Editor → Host (direct)\n" +
                "3) Player 2 window → Client → 127.0.0.1:7777\n" +
                "4) Do NOT use Relay for local VP tests",
                "OK");
        }

        private static void ApplyMppmInternals()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .First(a => a.GetName().Name == "UnityEditor.MultiplayerModule");

            var settingsType = asm.GetType("Unity.Multiplayer.PlayMode.Editor.MultiplayerPlayModeSettings");
            settingsType.GetMethod("SetIsMppmActive").Invoke(null, new object[] { true });
            settingsType.GetProperty("MutePlayers").SetValue(null, true);
            settingsType.GetProperty("ShowLaunchScreenOnPlayers").SetValue(null, false);
            settingsType.GetProperty("AssetDatabaseRefreshTimeout").SetValue(null, 120);

            var mppm = asm.GetType("Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode");
            var tagErrorType = asm.GetType("Unity.Multiplayer.PlayMode.Editor.TagError");
            var tags = mppm.GetProperty("PlayerTags").GetValue(null);
            EnsureGlobalTag(tags, tagErrorType, "Host");
            EnsureGlobalTag(tags, tagErrorType, "Client");

            var p1 = mppm.GetProperty("PlayerOne").GetValue(null);
            var p2 = mppm.GetProperty("PlayerTwo").GetValue(null);
            var p3 = mppm.GetProperty("PlayerThree").GetValue(null);
            var p4 = mppm.GetProperty("PlayerFour").GetValue(null);

            EnsurePlayerTag(p1, tagErrorType, "Host");
            EnsurePlayerTag(p2, tagErrorType, "Client");

            var activationErrorType = asm.GetType("Unity.Multiplayer.PlayMode.Editor.ActivationError");
            Activate(p2, activationErrorType);
            DeactivateIfNeeded(p3, activationErrorType);
            DeactivateIfNeeded(p4, activationErrorType);

            Debug.Log("[MiniVan MPPM] Fast local settings applied. Player2=" +
                      p2.GetType().GetProperty("PlayerState").GetValue(p2));
        }

        private static void EnsureGlobalTag(object tags, Type tagErrorType, string tag)
        {
            if ((bool)tags.GetType().GetMethod("Contains").Invoke(tags, new object[] { tag }))
            {
                return;
            }

            object err = Activator.CreateInstance(tagErrorType);
            tags.GetType().GetMethod("Add").Invoke(tags, new object[] { tag, err });
        }

        private static void EnsurePlayerTag(object player, Type tagErrorType, string tag)
        {
            var current = (string[])player.GetType().GetProperty("Tags").GetValue(player);
            if (current != null && current.Contains(tag))
            {
                return;
            }

            object err = Activator.CreateInstance(tagErrorType);
            player.GetType().GetMethod("AddTag").Invoke(player, new object[] { tag, err });
        }

        private static void Activate(object player, Type activationErrorType)
        {
            var state = player.GetType().GetProperty("PlayerState").GetValue(player).ToString();
            if (state.IndexOf("NotLaunched", StringComparison.OrdinalIgnoreCase) < 0 &&
                state.IndexOf("Inactive", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            object err = Activator.CreateInstance(activationErrorType);
            var method = player.GetType().GetMethod("Activate");
            var pars = method.GetParameters();
            object[] args = pars.Length == 2 ? new object[] { err, null } : new object[] { err };
            method.Invoke(player, args);
        }

        private static void DeactivateIfNeeded(object player, Type activationErrorType)
        {
            var state = player.GetType().GetProperty("PlayerState").GetValue(player).ToString();
            if (state.IndexOf("NotLaunched", StringComparison.OrdinalIgnoreCase) >= 0 ||
                state.IndexOf("Inactive", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            object err = Activator.CreateInstance(activationErrorType);
            player.GetType().GetMethod("Deactivate").Invoke(player, new object[] { err });
        }
    }
}
