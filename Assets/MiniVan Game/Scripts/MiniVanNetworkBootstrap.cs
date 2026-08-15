using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanLaunchMode
    {
        None,
        Host,
        Client,
        Server,
        RelayHost,
        RelayClient
    }

    public static class MiniVanLaunchState
    {
        public static MiniVanLaunchMode PendingMode = MiniVanLaunchMode.None;
        public static MiniVanLaunchMode ActiveMode = MiniVanLaunchMode.None;
        public static string Address = "127.0.0.1";
        public static string JoinCode = "";
        public static string LastJoinCode = "";
        public static string LobbyId = "";
        public static string RoomName = "";
        public static string SceneName = "";
        public static string PendingDisplayName = "";
        public static bool PreserveLobbyOnRestart;
        public static string StatusMessage = "";

        public static void ClearLobby()
        {
            LobbyId = "";
            RoomName = "";
            SceneName = "";
            JoinCode = "";
            LastJoinCode = "";
            PreserveLobbyOnRestart = false;
        }
    }

    public class MiniVanNetworkBootstrap : MonoBehaviour
    {
        public const string LobbyGameKey = "Game";
        public const string LobbySceneKey = "Scene";
        public const string LobbyRelayCodeKey = "RelayCode";
        public const string LobbyRelayRevisionKey = "RelayRevision";

        public UnityTransport Transport;
        public string Address = "127.0.0.1";
        public ushort Port = 7777;
        public int MaxRelayConnections = 3;
        public GameObject[] ExtraNetworkPrefabs;

        private bool isStartingNetwork;
        private bool heartbeatPending;
        private float nextHeartbeatTime;

        private void Awake()
        {
            if (Transport == null)
            {
                Transport = FindFirstObjectByType<UnityTransport>();
            }
        }

        private async void Start()
        {
            if (MiniVanLaunchState.PendingMode == MiniVanLaunchMode.None)
            {
                return;
            }

            Address = MiniVanLaunchState.Address;

            NetworkManager network = NetworkManager.Singleton;

            if (network == null || network.IsListening)
            {
                MiniVanLaunchState.PendingMode = MiniVanLaunchMode.None;
                return;
            }

            MiniVanLaunchMode launchMode = MiniVanLaunchState.PendingMode;
            MiniVanLaunchState.PendingMode = MiniVanLaunchMode.None;
            RegisterExtraNetworkPrefabs(network);
            isStartingNetwork = true;

            try
            {
                switch (launchMode)
                {
                    case MiniVanLaunchMode.Host:
                        ConfigureDirectTransport();
                        network.StartHost();
                        MiniVanLaunchState.ActiveMode = MiniVanLaunchMode.Host;
                        MiniVanLaunchState.StatusMessage = "Direct host started.";
                        break;
                    case MiniVanLaunchMode.Client:
                        ConfigureDirectTransport();
                        network.StartClient();
                        MiniVanLaunchState.ActiveMode = MiniVanLaunchMode.Client;
                        MiniVanLaunchState.StatusMessage = "Connecting directly...";
                        break;
                    case MiniVanLaunchMode.Server:
                        ConfigureDirectTransport();
                        network.StartServer();
                        MiniVanLaunchState.ActiveMode = MiniVanLaunchMode.Server;
                        MiniVanLaunchState.StatusMessage = "Direct server started.";
                        break;
                    case MiniVanLaunchMode.RelayHost:
                        await StartRelayHostAsync(network);
                        break;
                    case MiniVanLaunchMode.RelayClient:
                        await StartRelayClientAsync(network);
                        break;
                }
            }
            catch (System.Exception exception)
            {
                MiniVanLaunchState.StatusMessage = "Online start failed: " + exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                isStartingNetwork = false;
            }
        }

        private void Update()
        {
            if (MiniVanLaunchState.ActiveMode != MiniVanLaunchMode.RelayHost ||
                string.IsNullOrWhiteSpace(MiniVanLaunchState.LobbyId) || heartbeatPending ||
                Time.unscaledTime < nextHeartbeatTime)
            {
                return;
            }

            nextHeartbeatTime = Time.unscaledTime + 15f;
            SendLobbyHeartbeatAsync();
        }

private void OnGUI()
        {
        }

        private void RegisterExtraNetworkPrefabs(NetworkManager network)
        {
            if (network == null || network.NetworkConfig == null || ExtraNetworkPrefabs == null)
            {
                return;
            }

            for (int i = 0; i < ExtraNetworkPrefabs.Length; i++)
            {
                GameObject prefab = ExtraNetworkPrefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                bool alreadyRegistered = false;
                var prefabsList = network.NetworkConfig.Prefabs;
                if (prefabsList != null)
                {
                    for (int j = 0; j < prefabsList.Prefabs.Count; j++)
                    {
                        if (prefabsList.Prefabs[j] != null && prefabsList.Prefabs[j].Prefab == prefab)
                        {
                            alreadyRegistered = true;
                            break;
                        }
                    }
                }

                if (!alreadyRegistered)
                {
                    network.AddNetworkPrefab(prefab);
                }
            }
        }


        private void ConfigureDirectTransport()
        {
            if (Transport == null)
            {
                return;
            }

            Transport.SetConnectionData(Address, Port);
        }

        private async System.Threading.Tasks.Task StartRelayHostAsync(NetworkManager network)
        {
            MiniVanLaunchState.StatusMessage = "Creating room...";
            await EnsureUnityServicesAsync();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxRelayConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Lobby lobby = null;
            bool reuseLobby = MiniVanLaunchState.PreserveLobbyOnRestart &&
                              !string.IsNullOrWhiteSpace(MiniVanLaunchState.LobbyId);
            if (reuseLobby)
            {
                try
                {
                    lobby = await LobbyService.Instance.GetLobbyAsync(MiniVanLaunchState.LobbyId);
                    int revision = ReadRelayRevision(lobby) + 1;
                    lobby = await LobbyService.Instance.UpdateLobbyAsync(lobby.Id, new UpdateLobbyOptions
                    {
                        Data = BuildLobbyData(joinCode, MiniVanLaunchState.SceneName, revision)
                    });
                }
                catch (LobbyServiceException exception)
                {
                    Debug.LogWarning("Could not reuse lobby, creating a replacement: " + exception.Message);
                    MiniVanLaunchState.LobbyId = "";
                    lobby = null;
                }
            }

            if (lobby == null)
            {
                string roomName = NormalizeRoomName(MiniVanLaunchState.RoomName);
                lobby = await LobbyService.Instance.CreateLobbyAsync(roomName, MaxRelayConnections + 1,
                    new CreateLobbyOptions
                    {
                        IsPrivate = false,
                        Data = BuildLobbyData(joinCode, MiniVanLaunchState.SceneName, 1)
                    });
            }

            Transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
            MiniVanLaunchState.LobbyId = lobby.Id;
            MiniVanLaunchState.RoomName = lobby.Name;
            MiniVanLaunchState.LastJoinCode = joinCode;
            MiniVanLaunchState.StatusMessage = "Room " + lobby.Name + " is ready.";
            if (!network.StartHost())
            {
                throw new InvalidOperationException("Netcode could not start the Relay host.");
            }
            MiniVanLaunchState.ActiveMode = MiniVanLaunchMode.RelayHost;
            MiniVanLaunchState.PreserveLobbyOnRestart = false;
            nextHeartbeatTime = Time.unscaledTime + 10f;
        }

        private async System.Threading.Tasks.Task StartRelayClientAsync(NetworkManager network)
        {
            await EnsureUnityServicesAsync();
            string joinCode = "";
            Lobby lobby = null;

            if (!string.IsNullOrWhiteSpace(MiniVanLaunchState.LobbyId))
            {
                MiniVanLaunchState.StatusMessage = "Joining " + MiniVanLaunchState.RoomName + "...";
                lobby = MiniVanLaunchState.PreserveLobbyOnRestart
                    ? await LobbyService.Instance.GetLobbyAsync(MiniVanLaunchState.LobbyId)
                    : await LobbyService.Instance.JoinLobbyByIdAsync(MiniVanLaunchState.LobbyId);
                joinCode = ReadLobbyData(lobby, LobbyRelayCodeKey);
                MiniVanLaunchState.RoomName = lobby.Name;
            }
            else
            {
                joinCode = MiniVanLaunchState.JoinCode.Trim().ToUpperInvariant();
            }

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                throw new InvalidOperationException("The selected room has no active Relay session.");
            }

            MiniVanLaunchState.StatusMessage = "Joining room...";
            JoinAllocation allocation;
            try
            {
                allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            }
            catch (Exception) when (MiniVanLaunchState.PreserveLobbyOnRestart && lobby != null)
            {
                allocation = await WaitForRestartedRelayAsync(lobby.Id, joinCode);
                lobby = await LobbyService.Instance.GetLobbyAsync(lobby.Id);
                joinCode = ReadLobbyData(lobby, LobbyRelayCodeKey);
            }

            Transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
            MiniVanLaunchState.LastJoinCode = joinCode;
            MiniVanLaunchState.StatusMessage = "Connecting to " +
                                               (string.IsNullOrWhiteSpace(MiniVanLaunchState.RoomName)
                                                   ? "room"
                                                   : MiniVanLaunchState.RoomName) + "...";
            if (!network.StartClient())
            {
                throw new InvalidOperationException("Netcode could not start the Relay client.");
            }
            MiniVanLaunchState.ActiveMode = MiniVanLaunchMode.RelayClient;
            MiniVanLaunchState.PreserveLobbyOnRestart = false;
        }

        public static async Task EnsureUnityServicesAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        public static async Task<List<Lobby>> QueryPublicRoomsAsync()
        {
            await EnsureUnityServicesAsync();
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Count = 30,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(field: QueryFilter.FieldOptions.AvailableSlots,
                        value: "0", op: QueryFilter.OpOptions.GT),
                    new QueryFilter(field: QueryFilter.FieldOptions.S1,
                        value: "MiniVanGame", op: QueryFilter.OpOptions.EQ)
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            });
            return response.Results ?? new List<Lobby>();
        }

        public static string GetLobbySceneName(Lobby lobby, string fallback)
        {
            string sceneName = ReadLobbyData(lobby, LobbySceneKey);
            return string.IsNullOrWhiteSpace(sceneName) ? fallback : sceneName;
        }

        public static async Task LeaveCurrentLobbyAsync()
        {
            string lobbyId = MiniVanLaunchState.LobbyId;
            MiniVanLaunchMode mode = MiniVanLaunchState.ActiveMode;
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                MiniVanLaunchState.ClearLobby();
                return;
            }

            try
            {
                await EnsureUnityServicesAsync();
                if (mode == MiniVanLaunchMode.RelayHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                }
                else if (AuthenticationService.Instance.IsSignedIn)
                {
                    await LobbyService.Instance.RemovePlayerAsync(lobbyId,
                        AuthenticationService.Instance.PlayerId);
                }
            }
            catch (LobbyServiceException exception)
            {
                Debug.LogWarning("Lobby cleanup failed: " + exception.Message);
            }
            finally
            {
                MiniVanLaunchState.ClearLobby();
            }
        }

        private static Dictionary<string, DataObject> BuildLobbyData(string relayCode,
            string sceneName, int revision)
        {
            return new Dictionary<string, DataObject>
            {
                [LobbyGameKey] = new DataObject(DataObject.VisibilityOptions.Public, "MiniVanGame",
                    DataObject.IndexOptions.S1),
                [LobbySceneKey] = new DataObject(DataObject.VisibilityOptions.Public,
                    string.IsNullOrWhiteSpace(sceneName) ? "MiniVan_MVP" : sceneName,
                    DataObject.IndexOptions.S2),
                [LobbyRelayCodeKey] = new DataObject(DataObject.VisibilityOptions.Member, relayCode),
                [LobbyRelayRevisionKey] = new DataObject(DataObject.VisibilityOptions.Member,
                    revision.ToString())
            };
        }

        private static string ReadLobbyData(Lobby lobby, string key)
        {
            if (lobby == null || lobby.Data == null || !lobby.Data.TryGetValue(key, out DataObject value))
            {
                return "";
            }

            return value?.Value ?? "";
        }

        private static int ReadRelayRevision(Lobby lobby)
        {
            return int.TryParse(ReadLobbyData(lobby, LobbyRelayRevisionKey), out int revision)
                ? revision
                : 0;
        }

        private static string NormalizeRoomName(string requestedName)
        {
            if (string.IsNullOrWhiteSpace(requestedName))
            {
                throw new InvalidOperationException("A room name is required.");
            }

            string roomName = requestedName.Trim();
            return roomName.Length <= 32 ? roomName : roomName.Substring(0, 32);
        }

        private static async Task<JoinAllocation> WaitForRestartedRelayAsync(string lobbyId,
            string previousJoinCode)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(25);
            Exception lastException = null;
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(750);
                Lobby refreshed = await LobbyService.Instance.GetLobbyAsync(lobbyId);
                string refreshedCode = ReadLobbyData(refreshed, LobbyRelayCodeKey);
                if (string.IsNullOrWhiteSpace(refreshedCode) || refreshedCode == previousJoinCode)
                {
                    continue;
                }

                try
                {
                    return await RelayService.Instance.JoinAllocationAsync(refreshedCode);
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }
            }

            throw new TimeoutException("The host did not restore the room in time.", lastException);
        }

        private async void SendLobbyHeartbeatAsync()
        {
            heartbeatPending = true;
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(MiniVanLaunchState.LobbyId);
            }
            catch (LobbyServiceException exception)
            {
                Debug.LogWarning("Lobby heartbeat failed: " + exception.Message);
            }
            finally
            {
                heartbeatPending = false;
            }
        }

        private async void DisconnectAndLeaveLobby()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network != null && network.IsListening)
            {
                network.Shutdown();
            }

            await LeaveCurrentLobbyAsync();
            MiniVanLaunchState.ActiveMode = MiniVanLaunchMode.None;
        }
    }
}
