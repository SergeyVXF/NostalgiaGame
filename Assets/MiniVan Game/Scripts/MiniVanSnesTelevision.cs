using System;
using System.IO;
using SK.Libretro;
using SK.Libretro.Unity;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanSnesTvSignal : byte
    {
        Off = 0,
        White = 1,
        Game = 2
    }

    /// <summary>
    /// CRT display for a linked SNES console.
    /// Emulator runs only on the server; clients receive streamed frames and send joypad input.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [DisallowMultipleComponent]
    public sealed class MiniVanSnesTelevision : NetworkBehaviour
    {
        public const string PromptPlay = "E - play";
        public const string PromptExit = "E - stop playing";
        public const string PromptPickUp = "E - pick up TV";
        public const string PromptPlace = "E - place  |  scroll - rotate  |  Q - drop";
        public const string FrameMessageName = "MiniVanSnesFrame";

        private const int MaxPlayers = 2;
        private const float StreamInterval = 1f / 15f;
        private const int JpegQuality = 35;

        [Header("Interaction")]
        public float InteractRadius = 2.8f;
        public float PlaceMaxDistance = 5.5f;
        [Tooltip("Camera-local hold pose: bottom-right, small on screen.")]
        public Vector3 CarryLocalPosition = new Vector3(0.42f, -0.32f, 1.15f);
        public Vector3 CarryLocalEuler = new Vector3(-12f, 28f, 6f);
        [Range(0.15f, 1f)]
        public float CarryLocalScale = 0.32f;

        [Header("Libretro")]
        public LibretroInstance Libretro;
        public LibretroInstanceVariable InstanceVariable;
        public string CoreName = "snes9x";
        public string GamesSubdirectory = "snes";

        [Header("Screen")]
        public Renderer ScreenRenderer;
        public Transform LookAnchor;
        public Color ScreenOnBaseColor = Color.white;
        public Color ScreenOffBaseColor = new Color(0.05f, 0.08f, 0.12f, 1f);

        private readonly NetworkVariable<ulong> carriedByClientId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<byte> networkSignal = new NetworkVariable<byte>(
            (byte)MiniVanSnesTvSignal.Off,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> player0ClientId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> player1ClientId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private MiniVanSnesOutline outline;
        private Rigidbody body;
        private Collider[] colliders;
        private MiniVanPlayer localPlayingPlayer;
        private int localPlayPort = -1;
        private Vector3 placedLocalScale = Vector3.one;
        private bool hasPlacedLocalScale;
        private bool localReleasePredictionActive;
        private bool localReleasePhysical;
        private string activeGameName;
        private string activeGamesSubdirectory;
        private string activeRomFileName;
        private string activeRomSourcePath;
        private string runningGameKey;
        private bool emulatorStarting;
        private float nextLinkPollTime;
        private float nextStreamTime;
        private Texture2D whiteTexture;
        private Texture2D remoteFrameTexture;
        private bool screenSamplingEnsured;
        private static int frameHandlerRefCount;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        public bool IsCarried => carriedByClientId.Value != ulong.MaxValue;
        public bool IsAvailable => !IsCarried;
        public ulong CarriedByClientId => carriedByClientId.Value;
        public MiniVanSnesTvSignal Signal => (MiniVanSnesTvSignal)networkSignal.Value;
        public bool HasActivePlaySession => localPlayingPlayer != null;
        public bool IsGameRunningOnServer =>
            IsServer && Libretro != null && (Libretro.Running || emulatorStarting);

        public float GhostRootScaleMultiplier
        {
            get
            {
                float carry = Mathf.Clamp(CarryLocalScale, 0.15f, 1f);
                return carry > 0.001f ? 1f / carry : 1f;
            }
        }

        public override void OnNetworkSpawn()
        {
            carriedByClientId.OnValueChanged += (_, __) => ApplyCarryPhysics();
            networkSignal.OnValueChanged += HandleNetworkSignalChanged;
            MiniVanSnesPhysics.IgnoreVehicleCollisions(colliders);
            ApplyCarryPhysics();
            ApplySignalVisual(Signal);

            if (IsServer)
            {
                MiniVanSnesNetInputProcessor.RegisterWithLibretro();
            }

            RegisterFrameHandler();
            if (NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            networkSignal.OnValueChanged -= HandleNetworkSignalChanged;
            UnregisterFrameHandler();
            if (NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            if (IsServer)
            {
                StopEmulatorServer();
            }
        }

        private void Awake()
        {
            outline = GetComponent<MiniVanSnesOutline>() ?? gameObject.AddComponent<MiniVanSnesOutline>();
            body = MiniVanSnesPhysics.EnsureKinematicBody(gameObject);
            colliders = MiniVanSnesPhysics.EnsureTriggerColliders(
                gameObject, new Vector3(0.85f, 0.95f, 0.7f), new Vector3(0f, 0.45f, 0f));
            MiniVanSnesPhysics.IgnoreVehicleCollisions(colliders);

            if (Libretro == null)
            {
                Libretro = GetComponentInChildren<LibretroInstance>(true);
            }

            if (ScreenRenderer == null && Libretro != null)
            {
                ScreenRenderer = Libretro.Renderer;
            }

            if (ScreenRenderer == null)
            {
                Transform screen = transform.Find("Screen");
                if (screen != null)
                {
                    ScreenRenderer = screen.GetComponent<Renderer>();
                }
            }

            if (LookAnchor == null)
            {
                LookAnchor = ScreenRenderer != null ? ScreenRenderer.transform : transform;
            }

            if (Libretro != null && ScreenRenderer != null)
            {
                Libretro.Renderer = ScreenRenderer;
            }

            EnsureScreenMaterialSamplesTexture();
        }

        private void OnDisable()
        {
            if (localPlayingPlayer != null)
            {
                // Local UI cleanup only — server session cleared via RPC/disconnect.
                MiniVanPlayer p = localPlayingPlayer;
                localPlayingPlayer = null;
                localPlayPort = -1;
                p.NotifySnesTelevisionStopped(this);
            }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (localReleasePredictionActive)
            {
                if (!IsCarried)
                {
                    localReleasePredictionActive = false;
                }
                else if (localReleasePhysical)
                {
                    MiniVanSnesPhysics.TickLooseBody(body, colliders, transform);
                }

                return;
            }

            if (IsCarried)
            {
                MiniVanPlayer carrier = FindCarrier();
                if (carrier != null)
                {
                    ApplyCarryPose(carrier);
                }

                if (IsServer && Signal != MiniVanSnesTvSignal.Off)
                {
                    SetSignalOff();
                }

                return;
            }

            if (IsServer)
            {
                if (Time.time >= nextLinkPollTime)
                {
                    nextLinkPollTime = Time.time + 0.25f;
                    PollLinkedConsoleSignal();
                }

                TickVideoStream();
                RefreshInputEnabledFromPlayers();
            }

            if (!IsServer &&
                !screenSamplingEnsured &&
                Signal == MiniVanSnesTvSignal.Game)
            {
                EnsureScreenMaterialSamplesTexture();
            }
            else if (IsServer &&
                     !screenSamplingEnsured &&
                     Signal == MiniVanSnesTvSignal.Game &&
                     Libretro != null &&
                     Libretro.Running)
            {
                EnsureScreenMaterialSamplesTexture();
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || IsCarried)
            {
                return;
            }

            MiniVanSnesPhysics.TickLooseBody(body, colliders, transform);
        }

        private void PollLinkedConsoleSignal()
        {
            MiniVanSnesConsole console = FindNearestConsoleInLinkRange();
            if (console == null || !console.IsPowered)
            {
                if (Signal != MiniVanSnesTvSignal.Off)
                {
                    SetSignalOff();
                }

                return;
            }

            if (console.IsInsertedCartridge(out MiniVanSnesCartridge cart))
            {
                string key = BuildGameKey(cart.GameName, cart.GamesSubdirectory, cart.RomFileName);
                if (Signal == MiniVanSnesTvSignal.Game &&
                    (Libretro != null && (Libretro.Running || emulatorStarting)) &&
                    string.Equals(runningGameKey, key, StringComparison.Ordinal))
                {
                    return;
                }

                SetSignalGame(cart.GameName, cart.GamesSubdirectory, cart.RomFileName, cart.SourceRomProjectRelativePath);
            }
            else if (Signal != MiniVanSnesTvSignal.White)
            {
                SetSignalWhiteScreen();
            }
        }

        private MiniVanSnesConsole FindNearestConsoleInLinkRange()
        {
            MiniVanSnesConsole[] consoles = MiniVanSceneScan.Get<MiniVanSnesConsole>();
            MiniVanSnesConsole best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < consoles.Length; i++)
            {
                MiniVanSnesConsole console = consoles[i];
                if (console == null || console.IsCarried)
                {
                    continue;
                }

                float linkRadius = console.TelevisionLinkRadius;
                float d = Vector3.Distance(transform.position, console.transform.position);
                if (d <= linkRadius && d < bestDist)
                {
                    bestDist = d;
                    best = console;
                }
            }

            return best;
        }

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= InteractRadius;
        }

        public void SetHighlighted(bool value) => outline?.SetHighlighted(value);

        public bool IsPlayableForClients()
        {
            return IsAvailable && Signal == MiniVanSnesTvSignal.Game;
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            if (localPlayingPlayer == player)
            {
                return PromptExit;
            }

            if (IsCarried && CarriedByClientId == player.OwnerClientId)
            {
                return PromptPlace;
            }

            if (IsPlayableForClients())
            {
                return PromptPlay;
            }

            if (IsAvailable && Signal == MiniVanSnesTvSignal.White)
            {
                return "TV on (no cartridge)";
            }

            if (IsAvailable)
            {
                return PromptPickUp;
            }

            return null;
        }

        public bool TryPickupServer(MiniVanPlayer player)
        {
            if (!IsServer || player == null || !IsAvailable || !IsInRange(player.transform.position))
            {
                return false;
            }

            SetSignalOff();
            transform.SetParent(null, true);
            if (!hasPlacedLocalScale)
            {
                placedLocalScale = transform.localScale;
                hasPlacedLocalScale = true;
            }

            carriedByClientId.Value = player.OwnerClientId;
            ApplyCarryPhysics();
            return true;
        }

        public bool TryDropOrPlaceServer(MiniVanPlayer player, Vector3 position, Quaternion rotation, bool physicalDrop = false)
        {
            if (!IsServer || player == null || CarriedByClientId != player.OwnerClientId)
            {
                return false;
            }

            carriedByClientId.Value = ulong.MaxValue;
            if (physicalDrop)
            {
                MiniVanSnesPhysics.ApplyDroppedState(body, colliders, transform, position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
                MiniVanSnesPhysics.ApplyPlacedState(body, colliders, transform, position);
            }

            MiniVanSnesConsole[] consoles = MiniVanSceneScan.Get<MiniVanSnesConsole>();
            for (int i = 0; i < consoles.Length; i++)
            {
                if (consoles[i] != null && !consoles[i].IsCarried)
                {
                    consoles[i].RefreshLinkedTelevisionSignal();
                }
            }

            return true;
        }

        public void BeginLocalReleasePrediction(MiniVanPlayer player, Vector3 position, Quaternion rotation, bool physicalDrop)
        {
            if (player == null || !IsCarried || CarriedByClientId != player.OwnerClientId)
            {
                return;
            }

            localReleasePredictionActive = true;
            localReleasePhysical = physicalDrop;
            if (physicalDrop)
            {
                MiniVanSnesPhysics.ApplyDroppedState(body, colliders, transform, position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
                MiniVanSnesPhysics.ApplyPlacedState(body, colliders, transform, position);
                if (hasPlacedLocalScale)
                {
                    transform.localScale = placedLocalScale;
                }
            }
        }

        public void SetSignalOff()
        {
            if (!IsServer)
            {
                return;
            }

            ClearAllPlayersServer();
            StopEmulatorServer();
            networkSignal.Value = (byte)MiniVanSnesTvSignal.Off;
            ApplySignalVisual(MiniVanSnesTvSignal.Off);
        }

        public void SetSignalWhiteScreen()
        {
            if (!IsServer)
            {
                return;
            }

            ClearAllPlayersServer();
            StopEmulatorServer();
            networkSignal.Value = (byte)MiniVanSnesTvSignal.White;
            ApplySignalVisual(MiniVanSnesTvSignal.White);
        }

        public void SetSignalGame(string gameName, string gamesSubdirectory, string romFileName, string romSourcePath)
        {
            if (!IsServer)
            {
                return;
            }

            activeGameName = gameName;
            activeGamesSubdirectory = string.IsNullOrEmpty(gamesSubdirectory) ? "snes" : gamesSubdirectory;
            activeRomFileName = romFileName;
            activeRomSourcePath = romSourcePath;
            networkSignal.Value = (byte)MiniVanSnesTvSignal.Game;
            ApplySignalVisual(MiniVanSnesTvSignal.Game);
            StartEmulatorServer();
        }

        public bool ServerTryBeginPlay(MiniVanPlayer player, out int port)
        {
            port = -1;
            if (!IsServer || player == null || !IsAvailable)
            {
                return false;
            }

            if (Signal != MiniVanSnesTvSignal.Game || Libretro == null || !Libretro.Running)
            {
                return false;
            }

            ulong clientId = player.OwnerClientId;
            if (player0ClientId.Value == clientId)
            {
                port = 0;
                return true;
            }

            if (player1ClientId.Value == clientId)
            {
                port = 1;
                return true;
            }

            if (player0ClientId.Value == ulong.MaxValue)
            {
                player0ClientId.Value = clientId;
                port = 0;
            }
            else if (player1ClientId.Value == ulong.MaxValue)
            {
                player1ClientId.Value = clientId;
                port = 1;
            }
            else
            {
                return false;
            }

            MiniVanSnesNetInputProcessor.Instance.ClearPort(port);
            RefreshInputEnabledFromPlayers();
            return true;
        }

        public void ServerEndPlay(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            if (player0ClientId.Value == clientId)
            {
                player0ClientId.Value = ulong.MaxValue;
                MiniVanSnesNetInputProcessor.Instance.ClearPort(0);
            }

            if (player1ClientId.Value == clientId)
            {
                player1ClientId.Value = ulong.MaxValue;
                MiniVanSnesNetInputProcessor.Instance.ClearPort(1);
            }

            RefreshInputEnabledFromPlayers();
        }

        public void ServerSetJoypad(ulong clientId, ushort mask)
        {
            if (!IsServer)
            {
                return;
            }

            int port = GetPortForClient(clientId);
            if (port < 0)
            {
                return;
            }

            MiniVanSnesNetInputProcessor.Instance.SetJoypadMask(port, mask);
        }

        public void NotifyLocalPlayStarted(MiniVanPlayer player, int port)
        {
            localPlayingPlayer = player;
            localPlayPort = port;
            if (InstanceVariable != null && IsServer && Libretro != null)
            {
                InstanceVariable.Current = Libretro;
            }
        }

        public void NotifyLocalPlayEnded(MiniVanPlayer player)
        {
            if (localPlayingPlayer != player)
            {
                return;
            }

            localPlayingPlayer = null;
            localPlayPort = -1;
            if (InstanceVariable != null && InstanceVariable.Current == Libretro)
            {
                InstanceVariable.Current = null;
            }
        }

        // Kept for older call sites; networked play uses ServerTryBeginPlay instead.
        public bool TryBeginPlay(MiniVanPlayer player) => false;

        public void EndPlaySession(MiniVanPlayer player)
        {
            if (player == null)
            {
                return;
            }

            NotifyLocalPlayEnded(player);
            player.NotifySnesTelevisionStopped(this);
        }

        private void HandleNetworkSignalChanged(byte previous, byte current)
        {
            ApplySignalVisual((MiniVanSnesTvSignal)current);
        }

        private void ApplySignalVisual(MiniVanSnesTvSignal signal)
        {
            switch (signal)
            {
                case MiniVanSnesTvSignal.White:
                    ApplyWhiteScreenLocal();
                    break;
                case MiniVanSnesTvSignal.Game:
                    SetScreenBaseColor(ScreenOnBaseColor);
                    EnsureScreenMaterialSamplesTexture();
                    break;
                default:
                    SetScreenBaseColor(ScreenOffBaseColor);
                    break;
            }
        }

        private void StartEmulatorServer()
        {
            if (!IsServer || Libretro == null || string.IsNullOrEmpty(activeGameName))
            {
                return;
            }

            MiniVanSnesNetInputProcessor.RegisterWithLibretro();

            string gameKey = BuildGameKey(activeGameName, activeGamesSubdirectory, activeRomFileName);
            if (Libretro.Running || emulatorStarting)
            {
                runningGameKey = gameKey;
                SetScreenBaseColor(ScreenOnBaseColor);
                return;
            }

            MiniVanSnesCartridge.EnsureRomFile(
                activeGamesSubdirectory,
                string.IsNullOrEmpty(activeRomFileName) ? activeGameName + ".sfc" : activeRomFileName,
                activeRomSourcePath);

            string gamesDir = Path.Combine(Application.persistentDataPath, "Libretro", "games", activeGamesSubdirectory)
                .Replace('\\', '/');

            if (Libretro.Settings == null)
            {
                Libretro.Settings = new InstanceSettings();
            }

            Libretro.Settings.ShaderTextureName = "_BaseMap";
            Libretro.Settings.LeftStickBehaviour = LeftStickBehaviour.AnalogAndDigital;
            if (ScreenRenderer != null)
            {
                Libretro.Renderer = ScreenRenderer;
            }

            EnsureScreenMaterialSamplesTexture();
            screenSamplingEnsured = false;

            emulatorStarting = true;
            runningGameKey = gameKey;
            Libretro.Initialize(CoreName, gamesDir, activeGameName);
            Libretro.OnInstanceStarted = () =>
            {
                emulatorStarting = false;
                RefreshInputEnabledFromPlayers();
                SetScreenBaseColor(ScreenOnBaseColor);
                EnsureScreenMaterialSamplesTexture();
            };
            Libretro.OnInstanceStopped = () =>
            {
                emulatorStarting = false;
                if (string.Equals(runningGameKey, gameKey, StringComparison.Ordinal))
                {
                    runningGameKey = null;
                }
            };
            Libretro.StartContent();
            SetScreenBaseColor(ScreenOnBaseColor);
        }

        private void StopEmulatorServer()
        {
            if (!IsServer)
            {
                return;
            }

            emulatorStarting = false;
            runningGameKey = null;
            SetLibretroInputEnabled(false);
            MiniVanSnesNetInputProcessor.Instance.ClearAll();

            if (Libretro != null && Libretro.Running)
            {
                Libretro.StopContent();
            }

            if (InstanceVariable != null && InstanceVariable.Current == Libretro)
            {
                InstanceVariable.Current = null;
            }
        }

        private void RefreshInputEnabledFromPlayers()
        {
            if (!IsServer)
            {
                return;
            }

            bool any = player0ClientId.Value != ulong.MaxValue || player1ClientId.Value != ulong.MaxValue;
            SetLibretroInputEnabled(any);
        }

        private void SetLibretroInputEnabled(bool enabled)
        {
            if (Libretro == null || !Libretro.Running)
            {
                return;
            }

            try
            {
                Libretro.InputEnabled = enabled;
            }
            catch (Exception)
            {
            }
        }

        private void ClearAllPlayersServer()
        {
            player0ClientId.Value = ulong.MaxValue;
            player1ClientId.Value = ulong.MaxValue;
            MiniVanSnesNetInputProcessor.Instance.ClearAll();
            KickLocalPlayersClientRpc();
        }

        private int GetPortForClient(ulong clientId)
        {
            if (player0ClientId.Value == clientId)
            {
                return 0;
            }

            if (player1ClientId.Value == clientId)
            {
                return 1;
            }

            return -1;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            ServerEndPlay(clientId);
        }

        private void TickVideoStream()
        {
            if (Signal != MiniVanSnesTvSignal.Game || Libretro == null || !Libretro.Running)
            {
                return;
            }

            if (NetworkManager == null || NetworkManager.ConnectedClientsList.Count <= 1)
            {
                return;
            }

            if (Time.unscaledTime < nextStreamTime)
            {
                return;
            }

            nextStreamTime = Time.unscaledTime + StreamInterval;

            Texture2D frame = ResolveServerFrameTexture();
            if (frame == null)
            {
                return;
            }

            byte[] jpg;
            try
            {
                jpg = frame.EncodeToJPG(JpegQuality);
            }
            catch (Exception)
            {
                return;
            }

            if (jpg == null || jpg.Length == 0)
            {
                return;
            }

            ulong objectId = NetworkObjectId;
            int size = sizeof(ulong) + sizeof(int) + jpg.Length;
            using FastBufferWriter writer = new FastBufferWriter(size, Allocator.Temp, size * 2);
            writer.WriteValueSafe(objectId);
            writer.WriteValueSafe(jpg.Length);
            writer.WriteBytesSafe(jpg);

            var clients = NetworkManager.ConnectedClientsIds;
            for (int i = 0; i < clients.Count; i++)
            {
                ulong clientId = clients[i];
                if (clientId == NetworkManager.ServerClientId || clientId == NetworkManager.LocalClientId)
                {
                    continue;
                }

                NetworkManager.CustomMessagingManager.SendNamedMessage(
                    FrameMessageName,
                    clientId,
                    writer,
                    NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private Texture2D ResolveServerFrameTexture()
        {
            if (ScreenRenderer == null)
            {
                return null;
            }

            Material material = ScreenRenderer.material;
            if (material == null)
            {
                return null;
            }

            Texture tex = material.HasProperty(BaseMapId) ? material.GetTexture(BaseMapId) : null;
            tex ??= material.mainTexture;
            return tex as Texture2D;
        }

        private void RegisterFrameHandler()
        {
            if (NetworkManager == null || NetworkManager.CustomMessagingManager == null)
            {
                return;
            }

            if (frameHandlerRefCount == 0)
            {
                NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(FrameMessageName, HandleFrameMessageStatic);
            }

            frameHandlerRefCount++;
        }

        private void UnregisterFrameHandler()
        {
            if (NetworkManager == null || NetworkManager.CustomMessagingManager == null)
            {
                return;
            }

            frameHandlerRefCount = Mathf.Max(0, frameHandlerRefCount - 1);
            if (frameHandlerRefCount == 0)
            {
                NetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(FrameMessageName);
            }
        }

        private static void HandleFrameMessageStatic(ulong senderId, FastBufferReader reader)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out ulong objectId);
            reader.ReadValueSafe(out int length);
            if (length <= 0 || length > 512 * 1024)
            {
                return;
            }

            byte[] jpg = new byte[length];
            reader.ReadBytesSafe(ref jpg, length);

            if (NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
            {
                return;
            }

            MiniVanSnesTelevision tv = netObj.GetComponent<MiniVanSnesTelevision>();
            tv?.ApplyRemoteFrame(jpg);
        }

        private void ApplyRemoteFrame(byte[] jpg)
        {
            if (jpg == null || jpg.Length == 0 || ScreenRenderer == null)
            {
                return;
            }

            if (remoteFrameTexture == null)
            {
                remoteFrameTexture = new Texture2D(2, 2, TextureFormat.RGB24, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "SnesRemoteFrame"
                };
            }

            if (!remoteFrameTexture.LoadImage(jpg, false))
            {
                return;
            }

            remoteFrameTexture.filterMode = FilterMode.Point;
            Material material = ScreenRenderer.material;
            ApplyScreenTexture(material, remoteFrameTexture);
            SetScreenBaseColor(ScreenOnBaseColor);
            screenSamplingEnsured = true;
        }

        [ClientRpc]
        private void KickLocalPlayersClientRpc()
        {
            if (localPlayingPlayer != null)
            {
                MiniVanPlayer p = localPlayingPlayer;
                NotifyLocalPlayEnded(p);
                p.NotifySnesTelevisionStopped(this);
            }
        }

        private static string BuildGameKey(string gameName, string subdirectory, string romFileName)
        {
            return (subdirectory ?? string.Empty) + "|" + (gameName ?? string.Empty) + "|" + (romFileName ?? string.Empty);
        }

        private void ApplyWhiteScreenLocal()
        {
            if (ScreenRenderer == null)
            {
                return;
            }

            Material material = Application.isPlaying ? ScreenRenderer.material : ScreenRenderer.sharedMaterial;
            if (material == null)
            {
                return;
            }

            if (whiteTexture == null)
            {
                whiteTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                whiteTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                whiteTexture.Apply();
            }

            ApplyScreenTexture(material, whiteTexture);
            SetScreenBaseColor(Color.white);
        }

        private void EnsureScreenMaterialSamplesTexture()
        {
            if (ScreenRenderer == null)
            {
                return;
            }

            Material material = Application.isPlaying ? ScreenRenderer.material : ScreenRenderer.sharedMaterial;
            if (material == null)
            {
                return;
            }

            if (material.shader == null || material.shader.name != "MiniVanGame/SnesScreen")
            {
                Shader screenShader = Shader.Find("MiniVanGame/SnesScreen");
                if (screenShader != null)
                {
                    material.shader = screenShader;
                }
            }

            Texture tex = null;
            if (material.HasProperty(BaseMapId))
            {
                tex = material.GetTexture(BaseMapId);
            }

            if (tex == null && material.HasProperty(MainTexId))
            {
                tex = material.GetTexture(MainTexId);
            }

            tex ??= material.mainTexture;

            if (tex != null)
            {
                ApplyScreenTexture(material, tex);
                screenSamplingEnsured = true;
            }
            else if (!screenSamplingEnsured)
            {
                material.EnableKeyword("_BASEMAP");
            }
        }

        private static void ApplyScreenTexture(Material material, Texture texture)
        {
            if (material == null || texture == null)
            {
                return;
            }

            if (material.HasProperty(BaseMapId))
            {
                material.SetTexture(BaseMapId, texture);
            }

            if (material.HasProperty(MainTexId))
            {
                material.SetTexture(MainTexId, texture);
            }

            material.mainTexture = texture;
            material.EnableKeyword("_BASEMAP");
        }

        private void SetScreenBaseColor(Color color)
        {
            if (ScreenRenderer == null)
            {
                return;
            }

            Material material = Application.isPlaying ? ScreenRenderer.material : ScreenRenderer.sharedMaterial;
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, color);
            }

            material.color = color;
        }

        private void ApplyCarryPhysics()
        {
            if (IsCarried)
            {
                MiniVanSnesPhysics.ApplyCarryState(body, colliders, carried: true);
                return;
            }

            if (hasPlacedLocalScale)
            {
                transform.localScale = placedLocalScale;
            }

            if (body != null && !body.isKinematic)
            {
                MiniVanSnesPhysics.IgnoreVehicleCollisions(colliders);
                return;
            }

            MiniVanSnesPhysics.ApplyCarryState(body, colliders, carried: false);
            MiniVanSnesPhysics.IgnoreVehicleCollisions(colliders);
        }

        private MiniVanPlayer FindCarrier()
        {
            if (!IsCarried)
            {
                return null;
            }

            MiniVanPlayer[] players = MiniVanSceneScan.Get<MiniVanPlayer>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].OwnerClientId == CarriedByClientId)
                {
                    return players[i];
                }
            }

            return null;
        }

        private void ApplyCarryPose(MiniVanPlayer player)
        {
            Transform cam = player.PlayerCamera != null ? player.PlayerCamera.transform : player.transform;
            transform.SetPositionAndRotation(
                cam.TransformPoint(CarryLocalPosition),
                cam.rotation * Quaternion.Euler(CarryLocalEuler));

            if (!hasPlacedLocalScale)
            {
                placedLocalScale = transform.localScale;
                hasPlacedLocalScale = true;
            }

            float s = Mathf.Clamp(CarryLocalScale, 0.15f, 1f);
            transform.localScale = placedLocalScale * s;
        }
    }
}
