using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// World pickup for any wearable cosmetic. The worn item id is replicated, so one prefab
    /// covers every hat: the model is rebuilt from the cosmetic catalog on each client.
    /// Purely a trigger volume — it never pushes the minivan or anything else around.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanCosmeticPickup : NetworkBehaviour, IMiniVanGameModeInteractable
    {
        public const string PickupResourcePath = "MiniVan/CosmeticPickup";
        public const string VisualChildName = "CosmeticVisual";

        public MiniVanInventoryItem Item = MiniVanInventoryItem.ZoroBandana;
        public float PickupRadius = 2.2f;
        public float VisualScale = 0.38f;
        public float VisualHeight = 0.22f;
        public float SpinDegreesPerSecond = 32f;
        public float BobAmplitude = 0.04f;

        private readonly NetworkVariable<bool> available = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkItem = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Transform visual;
        private MiniVanInventoryItem visualItem = MiniVanInventoryItem.None;
        private float bobPhase;

        public bool IsAvailable => !IsSpawned || available.Value;

        /// <summary>Server-side spawn of a loose cosmetic in the world.</summary>
        public static MiniVanCosmeticPickup ServerSpawn(MiniVanInventoryItem item, Vector3 position, Quaternion rotation)
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsServer)
            {
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(PickupResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[MiniVan] Cosmetic pickup prefab missing at Resources/" + PickupResourcePath);
                return null;
            }

            GameObject instance = Instantiate(prefab, position, rotation);
            MiniVanCosmeticPickup pickup = instance.GetComponent<MiniVanCosmeticPickup>();
            if (pickup != null)
            {
                pickup.Item = item;
            }

            NetworkObject net = instance.GetComponent<NetworkObject>();
            if (net == null)
            {
                Destroy(instance);
                return null;
            }

            net.Spawn(true);
            return pickup;
        }

        private void Awake()
        {
            ConfigureCollider();
            RefreshVisual(Item);
        }

        private void Update()
        {
            if (visual == null)
            {
                return;
            }

            bobPhase += Time.deltaTime;
            visual.localRotation = Quaternion.Euler(0f, bobPhase * SpinDegreesPerSecond, 0f);
            visual.localPosition = new Vector3(0f, VisualHeight + Mathf.Sin(bobPhase * 2f) * BobAmplitude, 0f);
        }

        public override void OnNetworkSpawn()
        {
            ConfigureCollider();
            networkItem.OnValueChanged += HandleItemChanged;

            if (IsServer)
            {
                networkItem.Value = (int)Item;
            }

            MiniVanInventoryItem replicated = (MiniVanInventoryItem)networkItem.Value;
            RefreshVisual(replicated != MiniVanInventoryItem.None ? replicated : Item);
        }

        public override void OnNetworkDespawn()
        {
            networkItem.OnValueChanged -= HandleItemChanged;
        }

        public bool IsInReach(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= PickupRadius;
        }

        public bool TryClaim()
        {
            if (!IsServer || !available.Value)
            {
                return false;
            }

            available.Value = false;
            return true;
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || !IsAvailable)
            {
                return string.Empty;
            }

            return IsInReach(player.transform.position)
                ? "E - взять: " + MiniVanCosmeticCatalog.GetItemName(CurrentItem())
                : string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player != null)
            {
                player.TryPickupCosmetic(this);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public MiniVanInventoryItem CurrentItem()
        {
            MiniVanInventoryItem replicated = (MiniVanInventoryItem)networkItem.Value;
            return replicated != MiniVanInventoryItem.None ? replicated : Item;
        }

        private void HandleItemChanged(int previousValue, int newValue)
        {
            RefreshVisual((MiniVanInventoryItem)newValue);
        }

        private void RefreshVisual(MiniVanInventoryItem item)
        {
            if (item == MiniVanInventoryItem.None || (visual != null && visualItem == item))
            {
                return;
            }

            Transform existing = transform.Find(VisualChildName);

            // A pickup placed on the map already carries its model as a child: keep that one
            // so level designers can tweak it in the scene.
            if (existing != null && visual == null && item == Item)
            {
                visual = existing;
                visualItem = item;
                return;
            }

            if (existing != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(existing.gameObject);
                }
                else
                {
                    DestroyImmediate(existing.gameObject);
                }
            }

            visualItem = item;
            visual = MiniVanCosmeticVisual.Build(item, transform, VisualChildName);
            visual.localScale = Vector3.one * VisualScale;
        }

        private void ConfigureCollider()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }

            // Trigger only: cosmetics are props, they must not shove the van or the player.
            box.center = new Vector3(0f, 0.25f, 0f);
            box.size = new Vector3(0.5f, 0.5f, 0.5f);
            box.isTrigger = true;
        }
    }
}
