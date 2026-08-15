using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private readonly NetworkVariable<FixedString64Bytes> networkDisplayName = new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkAvatarIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private MiniVanHudUi hudUi;
        private bool identitySubmitted;
        private Material playerBodyColorMaterial;

        public string DisplayName
        {
            get
            {
                string name = networkDisplayName.Value.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    return "Player " + (OwnerClientId + 1);
                }

                return name;
            }
        }

        public int AvatarIndex => networkAvatarIndex.Value;
        public int NetworkHealth => networkHealth.Value;
        public int LocalSelectedSlotForHud => localSelectedSlot;
        public float WinchDurability01ForHud => Mathf.Clamp01(networkWinchDurability.Value);

        public MiniVanInventoryItem GetInventorySlotPublic(int slotIndex)
        {
            return GetInventorySlot(slotIndex);
        }

        public static string GetInventoryLabelPublic(MiniVanInventoryItem item)
        {
            return GetInventoryLabel(item);
        }

        public bool IsDamWaterHeadSubmergedForHud()
        {
            return IsDamWaterHeadSubmerged();
        }

        public float DamWaterOxygen01ForHud()
        {
            return Mathf.Clamp01(damWaterOxygenRemaining / DamWaterOxygenSeconds);
        }

        public MiniVanVehicle ResolveHudVehicleForHud()
        {
            return ResolveHudVehicle();
        }

        public string PingHudTextForHud()
        {
            float ping = NetworkManager.Singleton != null &&
                         (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                ? 0f
                : smoothedPingMs;
            string state = ping >= BadPingMs ? "HIGH" : ping >= GoodPingMs ? "MID" : "OK";
            return "Ping: " + Mathf.RoundToInt(ping) + " ms  " + state;
        }

        private void InitializeHudIdentityOnServerSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            if (networkAvatarIndex.Value < 0)
            {
                networkAvatarIndex.Value = ServerPickUniqueAvatarIndex();
            }

            ServerApplyAvatarBodyColor(networkAvatarIndex.Value);

            if (networkDisplayName.Value.IsEmpty)
            {
                networkDisplayName.Value = "Player";
            }
        }

        private void InitializeHudOnNetworkSpawn()
        {
            networkAvatarIndex.OnValueChanged += HandleAvatarIndexChanged;
            networkHatColor.OnValueChanged += HandleHatColorChanged;
            ApplyPlayerBodyColorFromNetwork();

            if (!IsOwner)
            {
                return;
            }

            EnsureHudUi();
            if (!identitySubmitted)
            {
                identitySubmitted = true;
                string pending = MiniVanLaunchState.PendingDisplayName;
                if (string.IsNullOrWhiteSpace(pending))
                {
                    pending = "Player";
                }

                RequestSetPlayerIdentityServerRpc(pending);
            }
        }

        private void ShutdownHud()
        {
            networkAvatarIndex.OnValueChanged -= HandleAvatarIndexChanged;
            networkHatColor.OnValueChanged -= HandleHatColorChanged;

            if (hudUi != null)
            {
                Destroy(hudUi.gameObject);
                hudUi = null;
            }

            identitySubmitted = false;
            playerBodyColorMaterial = null;
        }

        private void HandleAvatarIndexChanged(int previous, int current)
        {
            if (IsServer)
            {
                ServerApplyAvatarBodyColor(current);
            }

            ApplyPlayerBodyColorFromNetwork();
        }

        private void HandleHatColorChanged(Vector3 previous, Vector3 current)
        {
            ApplyPlayerBodyColorFromNetwork();
        }

        private void ServerApplyAvatarBodyColor(int avatarIndex)
        {
            if (!IsServer || avatarIndex < 0)
            {
                return;
            }

            networkHatColor.Value = MiniVanAvatarCatalog.GetBodyColorVector(avatarIndex);
        }

        private void ApplyPlayerBodyColorFromNetwork()
        {
            if (IsFacePainting())
            {
                return;
            }

            Color color = networkAvatarIndex.Value >= 0
                ? MiniVanAvatarCatalog.GetBodyColor(networkAvatarIndex.Value)
                : VectorToColor(networkHatColor.Value);

            ApplyPlayerSkinColor();
            if (playerVisualReady)
            {
                return;
            }

            Renderer bodyRenderer = GetComponent<Renderer>();
            if (bodyRenderer == null)
            {
                return;
            }

            // Keep a dedicated instance so we don't mutate the shared prefab material.
            if (playerBodyColorMaterial == null || bodyRenderer.sharedMaterial != playerBodyColorMaterial)
            {
                playerBodyColorMaterial = bodyRenderer.material;
            }

            if (playerBodyColorMaterial.HasProperty("_BaseColor"))
            {
                playerBodyColorMaterial.SetColor("_BaseColor", color);
            }

            playerBodyColorMaterial.color = color;
        }

        private void UpdateHudUi()
        {
            if (!IsOwner)
            {
                return;
            }

            EnsureHudUi();
            if (hudUi != null)
            {
                bool hideForEquipment = equipmentWindowOpen;
                hudUi.SetVisible(!hideForEquipment);
                if (!hideForEquipment)
                {
                    hudUi.Refresh();
                }
            }
        }

        private bool HasCanvasHud()
        {
            return hudUi != null;
        }

        private void EnsureHudUi()
        {
            if (hudUi != null)
            {
                return;
            }

            MiniVanHudUi prefab = Resources.Load<MiniVanHudUi>(MiniVanHudUi.ResourcesPath);
            if (prefab == null)
            {
                Debug.LogWarning("[MiniVanHUD] Missing prefab Resources/" + MiniVanHudUi.ResourcesPath);
                return;
            }

            hudUi = Instantiate(prefab);
            hudUi.name = "MiniVanHUD";
            hudUi.Bind(this);
            DontDestroyOnLoad(hudUi.gameObject);
        }

        [ServerRpc]
        private void RequestSetPlayerIdentityServerRpc(string requestedName, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            string cleaned = string.IsNullOrWhiteSpace(requestedName) ? "Player" : requestedName.Trim();
            if (cleaned.Length > 48)
            {
                cleaned = cleaned.Substring(0, 48);
            }

            networkDisplayName.Value = cleaned;
            if (networkAvatarIndex.Value < 0)
            {
                networkAvatarIndex.Value = ServerPickUniqueAvatarIndex();
            }

            ServerApplyAvatarBodyColor(networkAvatarIndex.Value);
        }

        private static int ServerPickUniqueAvatarIndex()
        {
            bool[] used = new bool[MiniVanAvatarCatalog.AvatarCount];
            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || !player.IsSpawned)
                {
                    continue;
                }

                int index = player.networkAvatarIndex.Value;
                if (index >= 0 && index < used.Length)
                {
                    used[index] = true;
                }
            }

            int freeCount = 0;
            for (int i = 0; i < used.Length; i++)
            {
                if (!used[i])
                {
                    freeCount++;
                }
            }

            if (freeCount <= 0)
            {
                return Random.Range(0, MiniVanAvatarCatalog.AvatarCount);
            }

            int pick = Random.Range(0, freeCount);
            for (int i = 0; i < used.Length; i++)
            {
                if (used[i])
                {
                    continue;
                }

                if (pick == 0)
                {
                    return i;
                }

                pick--;
            }

            return 0;
        }
    }
}
