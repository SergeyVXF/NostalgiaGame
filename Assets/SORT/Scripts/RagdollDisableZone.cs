using UnityEngine;
using Invector.vCharacterController;

public class RagdollDisableZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Transform root = other.transform.root;
            var ragdoll = root.GetComponent<vRagdoll>();
            if (ragdoll != null)
            {
                Debug.Log($"[RagdollDisableZone] Отключаю компонент vRagdoll (галочка) у: {root.name}");
                ragdoll.enabled = false;
            }
            else
            {
                Debug.Log("[RagdollDisableZone] vRagdoll не найден у: " + root.name);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Transform root = other.transform.root;
            var ragdoll = root.GetComponent<vRagdoll>();
            if (ragdoll != null)
            {
                Debug.Log($"[RagdollDisableZone] Включаю компонент vRagdoll (галочка) у: {root.name}");
                ragdoll.enabled = true;
            }
            else
            {
                Debug.Log("[RagdollDisableZone] vRagdoll не найден у: " + root.name);
            }
        }
    }
} 