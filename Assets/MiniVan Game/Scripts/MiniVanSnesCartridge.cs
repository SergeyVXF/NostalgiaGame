using System.IO;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    [DisallowMultipleComponent]
    public sealed class MiniVanSnesCartridge : NetworkBehaviour
    {
        public const string DefaultGameName = "Ultimate Mortal Kombat 3 (Europe)";
        public const string DefaultRomFileName = "Ultimate Mortal Kombat 3 (Europe).sfc";

        [Header("ROM")]
        public string GameName = DefaultGameName;
        public string RomFileName = DefaultRomFileName;
        public string GamesSubdirectory = "snes";
        [Tooltip("Absolute or project-relative source used once to seed persistentDataPath.")]
        public string SourceRomProjectRelativePath =
            "Assets/MiniVan Game/Resources/Rooms/Ultimate Mortal Kombat 3 (Europe).sfc";

        [Header("Interaction")]
        public float InteractRadius = 2.4f;
        public Vector3 CarryLocalPosition = new Vector3(0.35f, -0.25f, 0.55f);
        public Vector3 CarryLocalEuler = new Vector3(10f, 160f, 8f);

        [Header("Refs")]
        public Transform VisualRoot;
        public Collider[] Colliders;

        private readonly NetworkVariable<ulong> carriedByClientId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> insertedConsoleId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private MiniVanSnesOutline outline;
        private Rigidbody body;
        private Renderer[] renderers;
        private bool carriedVisualVisible = true;
        private bool localReleasePredictionActive;

        public bool IsCarried => carriedByClientId.Value != ulong.MaxValue;
        public bool IsInserted => insertedConsoleId.Value != ulong.MaxValue;
        public bool IsAvailable => !IsCarried && !IsInserted;
        public ulong CarriedByClientId => carriedByClientId.Value;

        public override void OnNetworkSpawn()
        {
            EnsureRomInPersistentGames();
            carriedByClientId.OnValueChanged += OnCarryChanged;
            insertedConsoleId.OnValueChanged += OnInsertChanged;
            ApplyVisualState();
        }

        public override void OnNetworkDespawn()
        {
            carriedByClientId.OnValueChanged -= OnCarryChanged;
            insertedConsoleId.OnValueChanged -= OnInsertChanged;
        }

        private void Awake()
        {
            outline = GetComponent<MiniVanSnesOutline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<MiniVanSnesOutline>();
            }

            body = MiniVanSnesPhysics.EnsureKinematicBody(gameObject);
            if (VisualRoot == null)
            {
                VisualRoot = transform;
            }

            Colliders = MiniVanSnesPhysics.EnsureTriggerColliders(
                gameObject, new Vector3(0.14f, 0.04f, 0.18f), Vector3.zero);
            MiniVanSnesPhysics.IgnoreVehicleCollisions(Colliders);
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
                else
                {
                    MiniVanSnesPhysics.TickLooseBody(body, Colliders, transform);
                }

                SetCarriedVisualVisible(true);
                return;
            }

            if (!IsCarried)
            {
                SetCarriedVisualVisible(true);
                return;
            }

            MiniVanPlayer carrier = FindCarrier();
            if (carrier != null)
            {
                ApplyCarryPose(carrier);
                bool show = carrier.IsInventoryItemSelectedForWorld(MiniVanInventoryItem.SnesCartridge);
                SetCarriedVisualVisible(show);
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || IsCarried || IsInserted)
            {
                return;
            }

            MiniVanSnesPhysics.TickLooseBody(body, Colliders, transform);
        }

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= InteractRadius;
        }

        public void SetHighlighted(bool value)
        {
            if (outline != null)
            {
                outline.SetHighlighted(value);
            }
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            if (IsInserted)
            {
                return "E - remove cartridge";
            }

            if (IsAvailable)
            {
                return "E - pick up cartridge";
            }

            if (IsCarried && carriedByClientId.Value == player.OwnerClientId)
            {
                return "Q - drop cartridge";
            }

            return null;
        }

        public bool TryPickupServer(MiniVanPlayer player)
        {
            if (!IsServer || player == null || !IsAvailable)
            {
                return false;
            }

            if (!IsInRange(player.transform.position))
            {
                return false;
            }

            transform.SetParent(null, true);
            carriedByClientId.Value = player.OwnerClientId;
            insertedConsoleId.Value = ulong.MaxValue;
            SetPhysicsEnabled(false);
            return true;
        }

        public bool TryDropServer(MiniVanPlayer player, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (!IsServer || player == null || carriedByClientId.Value != player.OwnerClientId)
            {
                return false;
            }

            carriedByClientId.Value = ulong.MaxValue;
            MiniVanSnesPhysics.ApplyDroppedState(body, Colliders, transform, worldPosition, worldRotation);
            return true;
        }

        public void BeginLocalDropPrediction(MiniVanPlayer player, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (player == null || !IsCarried || carriedByClientId.Value != player.OwnerClientId)
            {
                return;
            }

            localReleasePredictionActive = true;
            MiniVanSnesPhysics.ApplyDroppedState(body, Colliders, transform, worldPosition, worldRotation);
            SetCarriedVisualVisible(true);
        }

        public bool TryInsertServer(MiniVanSnesConsole console)
        {
            if (!IsServer || console == null || console.NetworkObject == null || IsInserted)
            {
                return false;
            }

            carriedByClientId.Value = ulong.MaxValue;
            insertedConsoleId.Value = console.NetworkObject.NetworkObjectId;
            SetPhysicsEnabled(false);
            return true;
        }

        public bool TryEjectServer(MiniVanPlayer player)
        {
            if (!IsServer || !IsInserted || player == null)
            {
                return false;
            }

            insertedConsoleId.Value = ulong.MaxValue;
            carriedByClientId.Value = player.OwnerClientId;
            SetPhysicsEnabled(false);
            return true;
        }

        public void SnapToSlot(Transform slot)
        {
            if (slot == null)
            {
                return;
            }

            transform.SetParent(null, true);
            transform.SetPositionAndRotation(slot.position, slot.rotation);
        }

        public static void EnsureRomFile(string gamesSubdirectory, string romFileName, string projectRelativeSource)
        {
            string gamesDir = Path.Combine(Application.persistentDataPath, "Libretro", "games", gamesSubdirectory);
            Directory.CreateDirectory(gamesDir);
            string dest = Path.Combine(gamesDir, romFileName);
            if (File.Exists(dest) && new FileInfo(dest).Length > 1024)
            {
                return;
            }

            string source = ResolveSourcePath(projectRelativeSource);
            if (!string.IsNullOrEmpty(source) && File.Exists(source))
            {
                File.Copy(source, dest, true);
            }
        }

        private void EnsureRomInPersistentGames()
        {
            EnsureRomFile(GamesSubdirectory, RomFileName, SourceRomProjectRelativePath);
        }

        private static string ResolveSourcePath(string projectRelative)
        {
            if (string.IsNullOrWhiteSpace(projectRelative))
            {
                return null;
            }

            if (Path.IsPathRooted(projectRelative) && File.Exists(projectRelative))
            {
                return projectRelative;
            }

            // Editor / standalone: Assets/... under dataPath parent.
            string fromData = Path.GetFullPath(Path.Combine(Application.dataPath, "..", projectRelative));
            if (File.Exists(fromData))
            {
                return fromData;
            }

            string fromAssets = Path.Combine(Application.dataPath, projectRelative.Replace("Assets/", "").Replace("Assets\\", ""));
            return File.Exists(fromAssets) ? fromAssets : null;
        }

        private void OnCarryChanged(ulong previous, ulong current)
        {
            ApplyVisualState();
        }

        private void OnInsertChanged(ulong previous, ulong current)
        {
            ApplyVisualState();
            if (IsServer && previous != ulong.MaxValue && current == ulong.MaxValue)
            {
                // Ejected — notify old console.
                if (NetworkManager.Singleton != null &&
                    NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(previous, out NetworkObject consoleObject))
                {
                    MiniVanSnesConsole console = consoleObject.GetComponent<MiniVanSnesConsole>();
                    console?.NotifyCartridgeRemovedServer();
                }
            }
        }

        private void ApplyVisualState()
        {
            bool heldOrInserted = IsCarried || IsInserted;
            if (heldOrInserted)
            {
                MiniVanSnesPhysics.ApplyCarryState(body, Colliders, carried: true);
                return;
            }

            // Clients sync dropped free body; keep vehicle ignore so van never gets shoved.
            if (body != null && !body.isKinematic)
            {
                MiniVanSnesPhysics.IgnoreVehicleCollisions(Colliders);
                return;
            }

            MiniVanSnesPhysics.ApplyCarryState(body, Colliders, carried: false);
            MiniVanSnesPhysics.IgnoreVehicleCollisions(Colliders);
        }

        private void SetPhysicsEnabled(bool enabled)
        {
            // "enabled" here means free/interactable in the world (not carried / not inserted).
            if (!enabled)
            {
                MiniVanSnesPhysics.ApplyCarryState(body, Colliders, carried: true);
                return;
            }

            MiniVanSnesPhysics.ApplyCarryState(body, Colliders, carried: false);
            MiniVanSnesPhysics.IgnoreVehicleCollisions(Colliders);
        }

        private MiniVanPlayer FindCarrier()
        {
            if (!IsCarried || NetworkManager.Singleton == null)
            {
                return null;
            }

            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(carriedByClientId.Value, out NetworkClient client) &&
                client.PlayerObject != null)
            {
                return client.PlayerObject.GetComponent<MiniVanPlayer>();
            }

            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].OwnerClientId == carriedByClientId.Value)
                {
                    return players[i];
                }
            }

            return null;
        }

        private void ApplyCarryPose(MiniVanPlayer player)
        {
            Transform cam = player.PlayerCamera != null
                ? player.PlayerCamera.transform
                : player.transform;
            Vector3 worldPos = cam.TransformPoint(CarryLocalPosition);
            Quaternion worldRot = cam.rotation * Quaternion.Euler(CarryLocalEuler);
            transform.SetPositionAndRotation(worldPos, worldRot);
        }

        private void SetCarriedVisualVisible(bool visible)
        {
            if (carriedVisualVisible == visible)
            {
                return;
            }

            carriedVisualVisible = visible;
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible;
                }
            }
        }
    }
}
