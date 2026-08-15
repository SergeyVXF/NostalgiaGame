using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class MiniVanHotPotatoDummy : NetworkBehaviour
    {
        public float ReturnDelay = 0.85f;
        public float PlayerSearchRadius = 45f;
        public Vector3 HoldOffset = new Vector3(0.42f, 1.18f, 0.34f);

        private readonly NetworkVariable<bool> poopMode = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private MiniVanHotPotatoBomb heldBomb;
        private float nextReturnTime;
        private GameObject visualRoot;
        private GameObject poopVisual;

        private void Awake()
        {
            EnsureVisual();
            ConfigureCollider();
        }

        public override void OnNetworkSpawn()
        {
            poopMode.OnValueChanged += HandlePoopModeChanged;
            ApplyPoopMode(poopMode.Value);
        }

        public override void OnNetworkDespawn()
        {
            poopMode.OnValueChanged -= HandlePoopModeChanged;
        }

        private void Update()
        {
            if (!IsServer || heldBomb == null || Time.time < nextReturnTime)
            {
                return;
            }

            MiniVanPlayer target = FindNearestPlayer();
            if (target != null)
            {
                heldBomb.ServerThrowFromDummy(this, target);
            }
        }

        public void ServerAttachBomb(MiniVanHotPotatoBomb bomb)
        {
            if (!IsServer || bomb == null)
            {
                return;
            }

            heldBomb = bomb;
            nextReturnTime = Time.time + Mathf.Max(0.05f, ReturnDelay);
        }

        public void ServerDetachBomb(MiniVanHotPotatoBomb bomb)
        {
            if (!IsServer || heldBomb != bomb)
            {
                return;
            }

            heldBomb = null;
        }

        public void ServerExplodeAsPoop(float seconds)
        {
            if (!IsServer)
            {
                return;
            }

            heldBomb = null;
            poopMode.Value = true;
            CancelInvoke(nameof(ServerEndPoopMode));
            Invoke(nameof(ServerEndPoopMode), Mathf.Max(0.1f, seconds));
        }

        public void GetBombCarryPose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            position = transform.position + transform.right * HoldOffset.x + Vector3.up * HoldOffset.y + forward * HoldOffset.z;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private void ServerEndPoopMode()
        {
            if (IsServer)
            {
                poopMode.Value = false;
            }
        }

        private MiniVanPlayer FindNearestPlayer()
        {
            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            MiniVanPlayer best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, players[i].transform.position);
                if (distance <= PlayerSearchRadius && distance < bestDistance)
                {
                    best = players[i];
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void HandlePoopModeChanged(bool previousValue, bool newValue)
        {
            ApplyPoopMode(newValue);
        }

        private void ApplyPoopMode(bool active)
        {
            EnsureVisual();
            if (visualRoot != null)
            {
                visualRoot.SetActive(!active);
            }

            EnsurePoopVisual();
            if (poopVisual != null)
            {
                poopVisual.SetActive(active);
            }
        }

        private void EnsureVisual()
        {
            if (visualRoot != null)
            {
                return;
            }

            visualRoot = new GameObject("Hot Potato Dummy Visual");
            visualRoot.transform.SetParent(transform, false);
            Material bodyMaterial = CreateMaterial(new Color(0.26f, 0.62f, 0.95f, 1f));
            Material faceMaterial = CreateMaterial(new Color(0.05f, 0.08f, 0.12f, 1f));

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Dummy Body";
            body.transform.SetParent(visualRoot.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.62f, 0.9f, 0.62f);
            SetMaterial(body, bodyMaterial);
            DisableCollider(body);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Dummy Head";
            head.transform.SetParent(visualRoot.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.92f, 0f);
            head.transform.localScale = new Vector3(0.56f, 0.42f, 0.5f);
            SetMaterial(head, bodyMaterial);
            DisableCollider(head);

            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "Dummy Face";
            face.transform.SetParent(visualRoot.transform, false);
            face.transform.localPosition = new Vector3(0f, 1.92f, 0.255f);
            face.transform.localScale = new Vector3(0.36f, 0.08f, 0.02f);
            SetMaterial(face, faceMaterial);
            DisableCollider(face);
        }

        private void EnsurePoopVisual()
        {
            if (poopVisual != null)
            {
                return;
            }

            poopVisual = new GameObject("Dummy Poop Visual");
            poopVisual.transform.SetParent(transform, false);
            Material material = CreateMaterial(new Color(0.36f, 0.19f, 0.08f, 1f));
            AddPoopSphere("Poop Base", new Vector3(0f, 0.28f, 0f), new Vector3(0.78f, 0.28f, 0.78f), material);
            AddPoopSphere("Poop Middle", new Vector3(0f, 0.52f, 0f), new Vector3(0.55f, 0.24f, 0.55f), material);
            AddPoopSphere("Poop Tip", new Vector3(0f, 0.72f, 0f), new Vector3(0.32f, 0.2f, 0.32f), material);
            poopVisual.SetActive(false);
        }

        private void AddPoopSphere(string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(poopVisual.transform, false);
            sphere.transform.localPosition = localPosition;
            sphere.transform.localScale = localScale;
            SetMaterial(sphere, material);
            DisableCollider(sphere);
        }

        private void ConfigureCollider()
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                return;
            }

            capsule.height = 2.1f;
            capsule.radius = 0.42f;
            capsule.center = new Vector3(0f, 1.05f, 0f);
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
    }
}
