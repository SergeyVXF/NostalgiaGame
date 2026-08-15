using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// World stand that gives the player a flamethrower inventory item, then hides itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanFlamethrowerRack : MonoBehaviour, IMiniVanGameModeInteractable
    {
        public float InteractionReach = 3.2f;

        private bool taken;
        private Renderer[] renderers;
        private Collider[] colliders;

        public bool IsAvailable => !taken;

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || !IsPlayerNear(player) || taken)
            {
                return string.Empty;
            }

            if (player.HasFlamethrowerInInventory())
            {
                return "Flamethrower ready (select FLAME slot, hold LMB)";
            }

            return "E - take flamethrower";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || !IsPlayerNear(player) || taken || player.HasFlamethrowerInInventory())
            {
                return;
            }

            player.RequestTakeFlamethrower(this);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool TryClaim()
        {
            if (taken)
            {
                return false;
            }

            taken = true;
            ApplyTakenVisual();
            return true;
        }

        public void ApplyTakenVisual()
        {
            taken = true;
            CacheParts();
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].enabled = false;
                    }
                }
            }

            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = false;
                    }
                }
            }
        }

        public static MiniVanFlamethrowerRack FindClosestAvailable(Vector3 worldPosition, float maxDistance = 6f)
        {
            MiniVanFlamethrowerRack[] racks = FindObjectsByType<MiniVanFlamethrowerRack>(FindObjectsSortMode.None);
            MiniVanFlamethrowerRack best = null;
            float bestDist = maxDistance;
            for (int i = 0; i < racks.Length; i++)
            {
                MiniVanFlamethrowerRack rack = racks[i];
                if (rack == null || rack.taken)
                {
                    continue;
                }

                float dist = Vector3.Distance(worldPosition, rack.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = rack;
                }
            }

            return best;
        }

        public static void HideNearestAt(Vector3 worldPosition, float maxDistance = 6f)
        {
            MiniVanFlamethrowerRack rack = FindClosestAvailable(worldPosition, maxDistance);
            if (rack == null)
            {
                // Maybe already claimed locally but still visible on this client — force nearest any.
                MiniVanFlamethrowerRack[] racks = FindObjectsByType<MiniVanFlamethrowerRack>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                float bestDist = maxDistance;
                for (int i = 0; i < racks.Length; i++)
                {
                    float dist = Vector3.Distance(worldPosition, racks[i].transform.position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        rack = racks[i];
                    }
                }
            }

            if (rack != null)
            {
                rack.ApplyTakenVisual();
            }
        }

        public void Restore()
        {
            taken = false;
            CacheParts();
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].enabled = true;
                    }
                }
            }

            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = true;
                    }
                }
            }
        }

        public static void RestoreNearestAt(Vector3 worldPosition, float maxDistance = 12f)
        {
            MiniVanFlamethrowerRack[] racks = Object.FindObjectsByType<MiniVanFlamethrowerRack>(FindObjectsSortMode.None);
            MiniVanFlamethrowerRack best = null;
            float bestDist = maxDistance;
            for (int i = 0; i < racks.Length; i++)
            {
                MiniVanFlamethrowerRack rack = racks[i];
                if (rack == null || !rack.taken)
                {
                    continue;
                }

                float dist = Vector3.Distance(worldPosition, rack.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = rack;
                }
            }

            if (best != null)
            {
                best.Restore();
            }
        }

        private bool IsPlayerNear(MiniVanPlayer player)
        {
            Vector3 from = player.PlayerCamera != null
                ? player.PlayerCamera.transform.position
                : player.transform.position;
            return Vector3.Distance(from, transform.position) <= InteractionReach;
        }

        private void Awake()
        {
            EnsureVisual();
            if (GetComponent<Collider>() == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.7f, 0f);
                box.size = new Vector3(1.1f, 1.5f, 1.1f);
            }

            CacheParts();
        }

        private void CacheParts()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        private void EnsureVisual()
        {
            if (transform.Find("RackVisual") != null)
            {
                return;
            }

            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "RackVisual";
            pedestal.transform.SetParent(transform, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            pedestal.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f);
            Object.Destroy(pedestal.GetComponent<Collider>());

            GameObject nozzle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nozzle.name = "NozzleVisual";
            nozzle.transform.SetParent(transform, false);
            nozzle.transform.localPosition = new Vector3(0f, 1.05f, 0.15f);
            nozzle.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            nozzle.transform.localScale = new Vector3(0.18f, 0.18f, 0.7f);
            Object.Destroy(nozzle.GetComponent<Collider>());

            Material mat = CreateMat(new Color(0.15f, 0.15f, 0.16f, 1f));
            Material flameMat = CreateMat(new Color(1f, 0.35f, 0.05f, 1f));
            pedestal.GetComponent<Renderer>().sharedMaterial = mat;
            nozzle.GetComponent<Renderer>().sharedMaterial = flameMat;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.05f, 0.9f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.45f, new Vector3(0.55f, 0.9f, 0.55f));
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.05f, 0.18f);
            Gizmos.color = new Color(1f, 0.4f, 0.05f, 0.18f);
            Gizmos.DrawWireSphere(transform.position, InteractionReach);
        }
#endif

        private static Material CreateMat(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }
    }
}
