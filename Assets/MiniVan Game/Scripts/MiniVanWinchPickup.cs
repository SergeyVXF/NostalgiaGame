using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanWinchPickup : NetworkBehaviour, IMiniVanGameModeInteractable
    {
        public float PickupRadius = 2.2f;
        [Range(0f, 1f)] public float Durability01 = 1f;
        public GameObject VisualPrefab;
        public string VisualChildName = "Winch Visual";

        private readonly NetworkVariable<bool> available = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Renderer[] renderers;
        private Collider[] colliders;

        public bool IsAvailable => !IsSpawned || available.Value;

        private void Awake()
        {
            EnsureVisual();
            CacheParts();
            ApplyAvailable(true);
        }

        public override void OnNetworkSpawn()
        {
            EnsureVisual();
            CacheParts();
            available.OnValueChanged += HandleAvailableChanged;
            ApplyAvailable(available.Value);
        }

        public override void OnNetworkDespawn()
        {
            available.OnValueChanged -= HandleAvailableChanged;
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
            ApplyAvailable(false);
            return true;
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || !IsAvailable)
            {
                return string.Empty;
            }

            return IsInReach(player.transform.position) ? "E - take winch" : string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1))
            {
                return;
            }

            if (player != null)
            {
                player.TryPickupWinch(this);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        private void HandleAvailableChanged(bool previousValue, bool newValue)
        {
            ApplyAvailable(newValue);
        }

        private void EnsureVisual()
        {
            string childName = string.IsNullOrWhiteSpace(VisualChildName) ? "Winch Visual" : VisualChildName;
            if (transform.Find(childName) != null)
            {
                return;
            }

            if (VisualPrefab != null)
            {
                GameObject instance = Instantiate(VisualPrefab, transform, false);
                instance.name = childName;
                DisableVisualColliders(instance);
                return;
            }

            Material metal = CreateMaterial(new Color(0.16f, 0.18f, 0.19f, 1f));
            Material rope = CreateMaterial(new Color(0.055f, 0.052f, 0.048f, 1f));
            Material strap = CreateMaterial(new Color(0.72f, 0.15f, 0.08f, 1f));

            GameObject visual = new GameObject(childName);
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            visual.transform.localRotation = Quaternion.identity;

            GameObject spool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spool.name = "Cable Spool";
            spool.transform.SetParent(visual.transform, false);
            spool.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            spool.transform.localScale = new Vector3(0.18f, 0.28f, 0.18f);
            SetMaterial(spool, rope);
            DisableCollider(spool);

            GameObject leftPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPlate.name = "Left Plate";
            leftPlate.transform.SetParent(visual.transform, false);
            leftPlate.transform.localPosition = new Vector3(-0.33f, 0f, 0f);
            leftPlate.transform.localScale = new Vector3(0.055f, 0.44f, 0.44f);
            SetMaterial(leftPlate, metal);
            DisableCollider(leftPlate);

            GameObject rightPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPlate.name = "Right Plate";
            rightPlate.transform.SetParent(visual.transform, false);
            rightPlate.transform.localPosition = new Vector3(0.33f, 0f, 0f);
            rightPlate.transform.localScale = new Vector3(0.055f, 0.44f, 0.44f);
            SetMaterial(rightPlate, metal);
            DisableCollider(rightPlate);

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Red Handle";
            handle.transform.SetParent(visual.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0.31f, 0f);
            handle.transform.localScale = new Vector3(0.72f, 0.065f, 0.11f);
            SetMaterial(handle, strap);
            DisableCollider(handle);

            GameObject hook = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hook.name = "Small Hook";
            hook.transform.SetParent(visual.transform, false);
            hook.transform.localPosition = new Vector3(0.47f, -0.16f, 0f);
            hook.transform.localScale = new Vector3(0.13f, 0.13f, 0.13f);
            SetMaterial(hook, metal);
            DisableCollider(hook);
        }

        private void CacheParts()
        {
            renderers = GetComponentsInChildren<Renderer>(true);

            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = new Vector3(0f, 0.28f, 0f);
                box.size = new Vector3(0.95f, 0.75f, 0.65f);
                box.isTrigger = true;
                colliders = new Collider[] { box };
            }
            else
            {
                colliders = System.Array.Empty<Collider>();
            }
        }

        private void ApplyAvailable(bool isAvailable)
        {
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].enabled = isAvailable;
                    }
                }
            }

            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = isAvailable;
                    }
                }
            }
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            return material;
        }

        private static void SetMaterial(GameObject target, Material material)
        {
            Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void DisableCollider(GameObject target)
        {
            Collider collider = target != null ? target.GetComponent<Collider>() : null;
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private static void DisableVisualColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] visualColliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < visualColliders.Length; i++)
            {
                if (visualColliders[i] != null)
                {
                    visualColliders[i].enabled = false;
                }
            }
        }
    }
}
