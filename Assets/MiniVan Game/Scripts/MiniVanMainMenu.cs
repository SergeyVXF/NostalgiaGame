using System;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MiniVanGame
{
    public class MiniVanMainMenu : MonoBehaviour
    {
        public string GameSceneName = "MiniVan_MVP";
        public string NewGameSceneName = "Game_v01";
        public string Address = "127.0.0.1";
        public string JoinCode = "";

        private const string PlayerNamePrefsKey = "MiniVan.PlayerDisplayName";
        private const string TestMapScene = "MiniVan_MVP";

        private string roomName = "";
        private string playerName = "";
        private bool refreshingRooms;
        private readonly List<Lobby> availableRooms = new List<Lobby>();

        private MiniVanMainMenuUi ui;
        private int selectedMapIndex;
        private readonly string[] mapScenes = new string[2];
        private string lastStatus = "";

        private void Awake()
        {
            mapScenes[0] = NewGameSceneName;
            mapScenes[1] = TestMapScene;
            playerName = PlayerPrefs.GetString(PlayerNamePrefsKey, playerName ?? string.Empty);
            EnsureUi();
            WireUi();
            ShowMain();
        }

        private void OnEnable()
        {
            roomName = "";
            ShowMenuCursor();
            if (ui != null)
            {
                if (ui.MainNameField != null)
                {
                    ui.MainNameField.SetTextWithoutNotify(playerName);
                }

                if (ui.CreateRoomNameField != null)
                {
                    ui.CreateRoomNameField.SetTextWithoutNotify(roomName);
                }
            }
        }

        private void Update()
        {
            ShowMenuCursor();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                MiniVanSettingsUi.TryHandleEscape();
            }

            if (ui == null)
            {
                return;
            }

            string status = MiniVanLaunchState.StatusMessage ?? string.Empty;
            if (status != lastStatus)
            {
                lastStatus = status;
                ui.SetStatus(status);
            }
        }

        private static void ShowMenuCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void EnsureUi()
        {
            ui = FindFirstObjectByType<MiniVanMainMenuUi>();
            if (ui != null)
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(MiniVanMainMenuUi.ResourcesPath);
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab);
                instance.name = "MiniVanMainMenu";
                ui = instance.GetComponent<MiniVanMainMenuUi>();
                return;
            }

            // Editor / missing-prefab fallback.
            ui = MiniVanMainMenuUiBuilder.Build();
        }

        private void WireUi()
        {
            if (ui == null)
            {
                return;
            }

            if (ui.MainNameField != null)
            {
                ui.MainNameField.onValueChanged.AddListener(value =>
                {
                    playerName = value ?? string.Empty;
                    PlayerPrefs.SetString(PlayerNamePrefsKey, playerName);
                    PlayerPrefs.Save();
                });
            }

            if (ui.CreateRoomNameField != null)
            {
                ui.CreateRoomNameField.onValueChanged.AddListener(value => roomName = value ?? string.Empty);
            }

            if (ui.MainCreateButton != null)
            {
                ui.MainCreateButton.onClick.AddListener(ShowCreateRoom);
            }

            if (ui.MainJoinButton != null)
            {
                ui.MainJoinButton.onClick.AddListener(ShowJoinRoom);
            }

            if (ui.MainQuitButton != null)
            {
                ui.MainQuitButton.onClick.AddListener(QuitGame);
            }

            if (ui.MainSettingsButton != null)
            {
                ui.MainSettingsButton.onClick.AddListener(() =>
                {
                    MiniVanSettingsUi.Show();
                });
            }

            if (ui.CreateBackButton != null)
            {
                ui.CreateBackButton.onClick.AddListener(ShowMain);
            }

            if (ui.JoinBackButton != null)
            {
                ui.JoinBackButton.onClick.AddListener(ShowMain);
            }

            if (ui.JoinRefreshButton != null)
            {
                ui.JoinRefreshButton.onClick.AddListener(() =>
                {
                    if (!refreshingRooms)
                    {
                        RefreshRoomsAsync();
                    }
                });
            }

            if (ui.CreateConfirmButton != null)
            {
                ui.CreateConfirmButton.onClick.AddListener(ConfirmCreateRoom);
            }

            if (ui.MapButtons != null)
            {
                for (int i = 0; i < ui.MapButtons.Length; i++)
                {
                    int mapIndex = i;
                    if (ui.MapButtons[i] != null)
                    {
                        ui.MapButtons[i].onClick.AddListener(() => SelectMap(mapIndex));
                    }
                }
            }

            SelectMap(0);
        }

        private void ShowMain()
        {
            ui.ShowPanel(MiniVanMainMenuUi.Panel.Main);
            if (ui.MainNameField != null)
            {
                ui.MainNameField.SetTextWithoutNotify(playerName);
            }

            ui.SetStatus(MiniVanLaunchState.StatusMessage ?? string.Empty);
        }

        private void ShowCreateRoom()
        {
            roomName = "";
            if (ui.CreateRoomNameField != null)
            {
                ui.CreateRoomNameField.SetTextWithoutNotify(roomName);
            }

            SelectMap(selectedMapIndex);
            ui.ShowPanel(MiniVanMainMenuUi.Panel.CreateRoom);
            ui.SetStatus(string.Empty);
        }

        private void ShowJoinRoom()
        {
            ui.ShowPanel(MiniVanMainMenuUi.Panel.JoinRoom);
            RefreshRoomsAsync();
        }

        private void SelectMap(int index)
        {
            selectedMapIndex = Mathf.Clamp(index, 0, mapScenes.Length - 1);
            ui.SetMapSelected(selectedMapIndex);
        }

        private void ConfirmCreateRoom()
        {
            string scene = mapScenes[selectedMapIndex];
            if (string.IsNullOrWhiteSpace(scene))
            {
                scene = NewGameSceneName;
            }

            StartRelayGame(MiniVanLaunchMode.RelayHost, scene);
        }

        private void StartRelayGame(MiniVanLaunchMode mode, string sceneName)
        {
            ShowMenuCursor();
            if (mode == MiniVanLaunchMode.RelayHost && string.IsNullOrWhiteSpace(roomName))
            {
                MiniVanLaunchState.StatusMessage = "Enter a room name.";
                ui.SetStatus(MiniVanLaunchState.StatusMessage);
                return;
            }

            MiniVanLaunchState.PendingMode = mode;
            MiniVanLaunchState.JoinCode = JoinCode;
            MiniVanLaunchState.ActiveMode = MiniVanLaunchMode.None;
            MiniVanLaunchState.LobbyId = "";
            MiniVanLaunchState.RoomName = mode == MiniVanLaunchMode.RelayHost ? roomName.Trim() : "";
            MiniVanLaunchState.SceneName = sceneName;
            MiniVanLaunchState.PendingDisplayName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
            MiniVanLaunchState.PreserveLobbyOnRestart = false;
            MiniVanLaunchState.StatusMessage = "";
            SceneManager.LoadScene(string.IsNullOrWhiteSpace(sceneName) ? GameSceneName : sceneName);
        }

        private async void RefreshRoomsAsync()
        {
            if (refreshingRooms)
            {
                return;
            }

            refreshingRooms = true;
            if (ui.JoinRefreshLabel != null)
            {
                ui.JoinRefreshLabel.text = "Refreshing...";
            }

            MiniVanLaunchState.StatusMessage = "Loading rooms...";
            ui.SetStatus(MiniVanLaunchState.StatusMessage);

            try
            {
                List<Lobby> rooms = await MiniVanNetworkBootstrap.QueryPublicRoomsAsync();
                availableRooms.Clear();
                availableRooms.AddRange(rooms);
                MiniVanLaunchState.StatusMessage = availableRooms.Count == 0
                    ? "No open rooms found."
                    : "";
                RebuildRoomList();
                ui.SetStatus(MiniVanLaunchState.StatusMessage);
            }
            catch (Exception exception)
            {
                MiniVanLaunchState.StatusMessage = "Could not load rooms: " + exception.Message;
                ui.SetStatus(MiniVanLaunchState.StatusMessage);
                Debug.LogException(exception);
            }
            finally
            {
                refreshingRooms = false;
                if (ui.JoinRefreshLabel != null)
                {
                    ui.JoinRefreshLabel.text = "Refresh";
                }
            }
        }

        private void RebuildRoomList()
        {
            if (ui.RoomListContent == null)
            {
                return;
            }

            for (int i = ui.RoomListContent.childCount - 1; i >= 0; i--)
            {
                Destroy(ui.RoomListContent.GetChild(i).gameObject);
            }

            bool empty = availableRooms.Count == 0;
            if (ui.RoomEmptyState != null)
            {
                ui.RoomEmptyState.SetActive(empty && !refreshingRooms);
            }

            if (empty || ui.RoomRowTemplate == null)
            {
                return;
            }

            for (int i = 0; i < availableRooms.Count; i++)
            {
                Lobby lobby = availableRooms[i];
                if (lobby == null)
                {
                    continue;
                }

                GameObject row = Instantiate(ui.RoomRowTemplate, ui.RoomListContent);
                row.SetActive(true);
                row.name = "Room_" + lobby.Id;

                int currentPlayers = Mathf.Max(0, lobby.MaxPlayers - lobby.AvailableSlots);
                Text label = row.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = lobby.Name + "    " + currentPlayers + "/" + lobby.MaxPlayers;
                }

                Button button = row.GetComponent<Button>();
                if (button != null)
                {
                    Lobby captured = lobby;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => JoinSelectedRoom(captured));
                }
            }
        }

        private void JoinSelectedRoom(Lobby lobby)
        {
            if (lobby == null)
            {
                return;
            }

            string sceneName = MiniVanNetworkBootstrap.GetLobbySceneName(lobby, GameSceneName);
            MiniVanLaunchState.PendingMode = MiniVanLaunchMode.RelayClient;
            MiniVanLaunchState.ActiveMode = MiniVanLaunchMode.None;
            MiniVanLaunchState.LobbyId = lobby.Id;
            MiniVanLaunchState.RoomName = lobby.Name;
            MiniVanLaunchState.SceneName = sceneName;
            MiniVanLaunchState.PendingDisplayName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
            MiniVanLaunchState.JoinCode = "";
            MiniVanLaunchState.LastJoinCode = "";
            MiniVanLaunchState.PreserveLobbyOnRestart = false;
            MiniVanLaunchState.StatusMessage = "Joining " + lobby.Name + "...";
            SceneManager.LoadScene(sceneName);
        }

        private void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
