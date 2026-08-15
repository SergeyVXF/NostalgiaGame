using Unity.Netcode;
using System.Collections;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniVanGame
{
    public class MiniVanPauseMenu : MonoBehaviour
    {
        private enum PauseScreen
        {
            Main = 0,
            Settings = 1,
            Keys = 2
        }

        public static bool IsOpen { get; private set; }

        public string MenuSceneName = "MiniVan_Menu";

        private PauseScreen screen = PauseScreen.Main;
        private int rebindIndex = -1;
        private Vector2 keysScroll;
        private string keysStatusMessage = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MiniVan_Menu" || FindFirstObjectByType<MiniVanPauseMenu>() != null)
            {
                return;
            }

            new GameObject("MiniVan Pause Menu").AddComponent<MiniVanPauseMenu>();
        }

        private void Update()
        {
            if (MiniVanGameOverScreen.IsGameOverActive)
            {
                if (IsOpen)
                {
                    SetOpen(false);
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (MiniVanSettingsUi.TryHandleEscape())
                {
                    return;
                }

                MiniVanPlayer localPlayer = MiniVanPlayer.LocalPlayer;
                if (!IsOpen && localPlayer != null && localPlayer.IsEquipmentWindowOpen)
                {
                    // Escape closes the equipment window first, like other in-game screens.
                    localPlayer.SetEquipmentWindowOpen(false);
                    return;
                }

                if (IsOpen && rebindIndex >= 0)
                {
                    // Escape cancels an active key capture, not the menu.
                    rebindIndex = -1;
                }
                else if (IsOpen && screen == PauseScreen.Keys)
                {
                    LeaveKeysScreen(apply: false);
                    screen = PauseScreen.Settings;
                }
                else if (IsOpen && screen == PauseScreen.Settings)
                {
                    screen = PauseScreen.Main;
                }
                else
                {
                    SetOpen(!IsOpen);
                }
            }
        }

        private void OnDisable()
        {
            SetOpen(false);
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }

            // Shared uGUI settings overlay owns its own chrome — don't stack OnGUI on top.
            if (MiniVanSettingsUi.IsOpen)
            {
                return;
            }

            if (screen == PauseScreen.Keys)
            {
                HandleRebindCapture();
            }

            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);

            if (screen == PauseScreen.Keys)
            {
                DrawKeysScreen();
                return;
            }

            if (screen == PauseScreen.Settings)
            {
                // Legacy path kept unused; Settings opens uGUI overlay.
                screen = PauseScreen.Main;
            }

            const float width = 360f;
            const float height = 320f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Space(18f);
            GUILayout.Label("Pause");
            GUILayout.Space(16f);

            if (GUILayout.Button("Settings", GUILayout.Height(44f)))
            {
                rebindIndex = -1;
                keysStatusMessage = string.Empty;
                MiniVanSettingsUi.Show();
            }

            if (GUILayout.Button("Restart", GUILayout.Height(44f)))
            {
                RestartGame();
            }

            if (GUILayout.Button("Exit to Menu", GUILayout.Height(44f)))
            {
                ExitToMenu();
            }

            if (GUILayout.Button("Quit Game", GUILayout.Height(44f)))
            {
                QuitGame();
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("Resume", GUILayout.Height(34f)))
            {
                SetOpen(false);
            }

            GUILayout.EndArea();
        }

        private void DrawSettingsScreen()
        {
            const float width = 360f;
            const float height = 220f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Space(18f);
            GUILayout.Label("Settings");
            GUILayout.Space(16f);

            if (GUILayout.Button("Keys", GUILayout.Height(44f)))
            {
                OpenKeysScreen();
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("Back", GUILayout.Height(34f)))
            {
                screen = PauseScreen.Main;
            }

            GUILayout.EndArea();
        }

        private void OpenKeysScreen()
        {
            MiniVanKeyBindings.BeginEdit();
            screen = PauseScreen.Keys;
            rebindIndex = -1;
            keysStatusMessage = string.Empty;
        }

        private void LeaveKeysScreen(bool apply)
        {
            rebindIndex = -1;
            if (apply)
            {
                MiniVanKeyBindings.ApplyEdit();
                keysStatusMessage = "Applied.";
            }
            else
            {
                MiniVanKeyBindings.DiscardEdit();
                keysStatusMessage = string.Empty;
            }
        }

        private void HandleRebindCapture()
        {
            if (rebindIndex < 0)
            {
                return;
            }

            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown || current.keyCode == KeyCode.None)
            {
                return;
            }

            // Escape cancel is handled in Update; swallow it here so the menu doesn't react twice.
            if (current.keyCode == KeyCode.Escape)
            {
                current.Use();
                return;
            }

            MiniVanKeyBindings.SetEdit(MiniVanKeyBindings.Actions[rebindIndex], current.keyCode);
            rebindIndex = -1;
            keysStatusMessage = string.Empty;
            current.Use();
        }

        private void DrawKeysScreen()
        {
            const float width = 440f;
            const float height = 480f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Space(14f);
            GUILayout.Label("Key Bindings");
            GUILayout.Space(6f);
            GUILayout.Label(rebindIndex >= 0
                ? "Press a key... (Esc - cancel)"
                : "Click a binding to change it. Press Apply to save.");
            if (!string.IsNullOrEmpty(keysStatusMessage))
            {
                GUILayout.Label(keysStatusMessage);
            }

            GUILayout.Space(8f);

            keysScroll = GUILayout.BeginScrollView(keysScroll);
            MiniVanKeyAction[] actions = MiniVanKeyBindings.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(MiniVanKeyBindings.GetLabel(actions[i]), GUILayout.Width(220f));

                string keyText = rebindIndex == i
                    ? "< press key >"
                    : MiniVanKeyBindings.KeyName(MiniVanKeyBindings.GetEdit(actions[i]));

                GUI.enabled = rebindIndex < 0 || rebindIndex == i;
                if (GUILayout.Button(keyText, GUILayout.Height(30f)))
                {
                    rebindIndex = rebindIndex == i ? -1 : i;
                    keysStatusMessage = string.Empty;
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Default", GUILayout.Height(34f)))
            {
                MiniVanKeyBindings.ResetEditToDefaults();
                rebindIndex = -1;
                keysStatusMessage = "Defaults loaded — press Apply to save.";
            }

            if (GUILayout.Button("Apply", GUILayout.Height(34f)))
            {
                MiniVanKeyBindings.ApplyEdit();
                rebindIndex = -1;
                keysStatusMessage = "Applied.";
                // Keep editing session open with fresh draft matching saved values.
                MiniVanKeyBindings.BeginEdit();
            }

            if (GUILayout.Button("Back", GUILayout.Height(34f)))
            {
                LeaveKeysScreen(apply: false);
                screen = PauseScreen.Settings;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void SetOpen(bool open)
        {
            if (!open)
            {
                LeaveKeysScreen(apply: false);
                MiniVanSettingsUi.HideIfOpen();
            }

            IsOpen = open;
            screen = PauseScreen.Main;
            rebindIndex = -1;
            keysStatusMessage = string.Empty;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

private void RestartGame()
        {
            MiniVanLaunchMode restartMode = MiniVanLaunchState.ActiveMode;
            NetworkManager network = NetworkManager.Singleton;
            if (restartMode == MiniVanLaunchMode.None && network != null && network.IsListening)
            {
                restartMode = network.IsHost ? MiniVanLaunchMode.Host : network.IsServer ? MiniVanLaunchMode.Server : MiniVanLaunchMode.Client;
            }

            if (restartMode == MiniVanLaunchMode.RelayHost && network != null && network.IsHost &&
                network.IsListening && network.NetworkConfig.EnableSceneManagement)
            {
                MiniVanLaunchState.StatusMessage = "Restarting room " + MiniVanLaunchState.RoomName + "...";
                SetOpen(false);
                network.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
                return;
            }

            MiniVanLaunchState.PendingMode = restartMode;
            MiniVanLaunchState.PreserveLobbyOnRestart =
                (restartMode == MiniVanLaunchMode.RelayHost || restartMode == MiniVanLaunchMode.RelayClient) &&
                !string.IsNullOrWhiteSpace(MiniVanLaunchState.LobbyId);
            MiniVanLaunchState.StatusMessage = "Restarting...";
            SetOpen(false);
            StartCoroutine(ShutdownNetworkThenLoad(SceneManager.GetActiveScene().name, false, false));
        }

private void ExitToMenu()
        {
            MiniVanLaunchState.PendingMode = MiniVanLaunchMode.None;
            MiniVanLaunchState.StatusMessage = "";
            SetOpen(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            StartCoroutine(ShutdownNetworkThenLoad(MenuSceneName, true, true));
        }

        private IEnumerator ShutdownNetworkThenLoad(string sceneName, bool unlockCursor, bool leaveLobby)
        {
            Task lobbyCleanup = leaveLobby ? MiniVanNetworkBootstrap.LeaveCurrentLobbyAsync() : null;
            NetworkManager network = NetworkManager.Singleton;
            if (network != null && network.IsListening)
            {
                network.Shutdown();
                float timeout = Time.realtimeSinceStartup + 2f;
                while (network != null && network.IsListening && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }
            }

            if (lobbyCleanup != null)
            {
                float cleanupTimeout = Time.realtimeSinceStartup + 4f;
                while (!lobbyCleanup.IsCompleted && Time.realtimeSinceStartup < cleanupTimeout)
                {
                    yield return null;
                }
            }

            if (leaveLobby)
            {
                MiniVanLaunchState.ActiveMode = MiniVanLaunchMode.None;
                MiniVanLaunchState.ClearLobby();
            }

            if (unlockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            SceneManager.LoadScene(sceneName);
        }

private void QuitGame()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network != null && network.IsListening)
            {
                network.Shutdown();
            }

            _ = MiniVanNetworkBootstrap.LeaveCurrentLobbyAsync();
            MiniVanLaunchState.PendingMode = MiniVanLaunchMode.None;
            MiniVanLaunchState.ActiveMode = MiniVanLaunchMode.None;
            MiniVanLaunchState.StatusMessage = "";
            Application.Quit();
        }
    }
}
