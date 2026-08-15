using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private readonly Dictionary<int, string> panelkaKeyIdsBySlot =
            new Dictionary<int, string>();

        public bool TryPickupPanelkaKey(string keyId)
        {
            return TryPickupApartmentKey(keyId);
        }

        public bool TryPickupApartmentKey(string keyId)
        {
            if (!IsOwner || string.IsNullOrEmpty(keyId))
            {
                return false;
            }

            int selectedSlot = Mathf.Clamp(localSelectedSlot, 0, 3);
            int slot = GetInventorySlot(selectedSlot) == MiniVanInventoryItem.None
                ? selectedSlot
                : FindFirstEmptyInventorySlot();
            if (slot < 0)
            {
                return false;
            }

            panelkaKeyIdsBySlot[slot] = keyId;
            SetInventorySlotNetworked(slot, MiniVanInventoryItem.PanelkaKey);
            localSelectedSlot = slot;
            RequestSelectInventorySlotServerRpc(slot);
            return true;
        }

        public bool TryUseSelectedPanelkaKey(string requiredKeyId)
        {
            return TryUseSelectedApartmentKey(requiredKeyId, true);
        }

        public bool TryUseSelectedApartmentKey(
            string requiredKeyId,
            bool consumeKey)
        {
            if (!IsOwner || string.IsNullOrEmpty(requiredKeyId))
            {
                return false;
            }

            int selectedSlot = Mathf.Clamp(localSelectedSlot, 0, 3);
            string carriedKeyId;
            if (!panelkaKeyIdsBySlot.TryGetValue(selectedSlot, out carriedKeyId) ||
                !string.Equals(carriedKeyId, requiredKeyId, System.StringComparison.Ordinal))
            {
                return false;
            }

            // The inventory NetworkVariable can arrive a frame later on clients. The local
            // key-to-slot map is authoritative for the owning player's interaction.
            if (consumeKey)
            {
                panelkaKeyIdsBySlot.Remove(selectedSlot);
                SetInventorySlotNetworked(selectedSlot, MiniVanInventoryItem.None);
            }
            return true;
        }

private GameObject CreatePanelkaKeyHeldVisual(Transform parent)
        {
            GameObject root = new GameObject("Held Panelka Key");
            root.transform.SetParent(parent, false);

            Material material = CreatePanelkaKeyMaterial();
            CreatePanelkaKeyPart(root.transform, "Shaft",
                new Vector3(0.06f, 0f, 0f),
                new Vector3(0.36f, 0.055f, 0.075f), material);
            CreatePanelkaKeyPart(root.transform, "Bow_Top",
                new Vector3(-0.18f, 0.09f, 0f),
                new Vector3(0.16f, 0.055f, 0.075f), material);
            CreatePanelkaKeyPart(root.transform, "Bow_Bottom",
                new Vector3(-0.18f, -0.09f, 0f),
                new Vector3(0.16f, 0.055f, 0.075f), material);
            CreatePanelkaKeyPart(root.transform, "Bow_Back",
                new Vector3(-0.25f, 0f, 0f),
                new Vector3(0.055f, 0.22f, 0.075f), material);
            CreatePanelkaKeyPart(root.transform, "Tooth_A",
                new Vector3(0.20f, -0.065f, 0f),
                new Vector3(0.07f, 0.13f, 0.075f), material);
            CreatePanelkaKeyPart(root.transform, "Tooth_B",
                new Vector3(0.29f, -0.045f, 0f),
                new Vector3(0.055f, 0.09f, 0.075f), material);

            CreateHeldPanelkaKeyNumberLabel(
                root.transform,
                GetSelectedPanelkaKeyApartmentNumber());
            return root;
        }

private string GetSelectedPanelkaKeyApartmentNumber()
        {
            string keyId;
            if (!panelkaKeyIdsBySlot.TryGetValue(localSelectedSlot, out keyId) ||
                string.IsNullOrEmpty(keyId))
            {
                return string.Empty;
            }

            int separator = keyId.LastIndexOf('-');
            int apartmentNumber;
            if (separator < 0 ||
                separator >= keyId.Length - 1 ||
                !int.TryParse(keyId.Substring(separator + 1), out apartmentNumber))
            {
                return string.Empty;
            }

            return apartmentNumber.ToString();
        }

        private static void CreateHeldPanelkaKeyNumberLabel(
            Transform parent,
            string apartmentNumber)
        {
            if (parent == null || string.IsNullOrEmpty(apartmentNumber))
            {
                return;
            }

            // A TextMesh reads correctly from the side its forward points away from, so each
            // face has to look back into the key, not out of it.
            CreateHeldPanelkaKeyNumberFace(
                parent,
                "Apartment_Number_Front",
                apartmentNumber,
                0.043f,
                Quaternion.Euler(0f, 180f, 0f));
            CreateHeldPanelkaKeyNumberFace(
                parent,
                "Apartment_Number_Back",
                apartmentNumber,
                -0.043f,
                Quaternion.identity);
        }

        private static void CreateHeldPanelkaKeyNumberFace(
            Transform parent,
            string name,
            string apartmentNumber,
            float localZ,
            Quaternion localRotation)
        {
            GameObject label = new GameObject(name);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(0.06f, 0.004f, localZ);
            label.transform.localRotation = localRotation;

            TextMesh text = label.AddComponent<TextMesh>();
            text.text = apartmentNumber;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = 0.012f;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.42f, 0.025f, 0.015f, 1f);

            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            Material depthMaterial = Resources.Load<Material>("Panelka_WorldTextDepth");
            if (renderer != null && depthMaterial != null)
            {
                renderer.sharedMaterial = depthMaterial;
            }
            label.AddComponent<MiniVanPanelkaWorldTextDepth>();
        }


        private static void CreatePanelkaKeyPart(
            Transform parent,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material CreatePanelkaKeyMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = "Panelka Key Yellow (Runtime)";
            Color yellow = new Color(1f, 0.72f, 0.03f);
            material.color = yellow;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", yellow);
            }
            return material;
        }
    }
}
