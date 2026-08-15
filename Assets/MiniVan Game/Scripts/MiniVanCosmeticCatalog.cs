using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Single source of truth for wearable cosmetics: which slot an item goes to,
    /// how it is labelled and which model it builds.
    /// </summary>
    public static class MiniVanCosmeticCatalog
    {
        public const int SlotCount = 4;

        public static bool IsCosmetic(MiniVanInventoryItem item)
        {
            return TryGetSlot(item, out _);
        }

        public static bool TryGetSlot(MiniVanInventoryItem item, out MiniVanEquipmentSlot slot)
        {
            switch (item)
            {
                case MiniVanInventoryItem.TestHat:
                case MiniVanInventoryItem.ZoroBandana:
                case MiniVanInventoryItem.StrawHat:
                case MiniVanInventoryItem.ChopperHat:
                case MiniVanInventoryItem.AshCap:
                case MiniVanInventoryItem.NarutoHeadband:
                case MiniVanInventoryItem.LawHat:
                case MiniVanInventoryItem.GokuHair:
                case MiniVanInventoryItem.SuperSaiyanHair:
                case MiniVanInventoryItem.MarioCap:
                case MiniVanInventoryItem.VikingHelmet:
                case MiniVanInventoryItem.PirateTricorn:
                    slot = MiniVanEquipmentSlot.Head;
                    return true;
                default:
                    slot = MiniVanEquipmentSlot.Head;
                    return false;
            }
        }

        public static bool CanEquip(MiniVanInventoryItem item, MiniVanEquipmentSlot slot)
        {
            return TryGetSlot(item, out MiniVanEquipmentSlot itemSlot) && itemSlot == slot;
        }

        public static string GetItemName(MiniVanInventoryItem item)
        {
            switch (item)
            {
                case MiniVanInventoryItem.TestHat: return "Тестовая шляпа";
                case MiniVanInventoryItem.ZoroBandana: return "Бандана Зоро";
                case MiniVanInventoryItem.StrawHat: return "Соломенная шляпа";
                case MiniVanInventoryItem.ChopperHat: return "Шляпа Чоппера";
                case MiniVanInventoryItem.AshCap: return "Кепка Эша";
                case MiniVanInventoryItem.NarutoHeadband: return "Повязка Наруто";
                case MiniVanInventoryItem.LawHat: return "Шапка Ло";
                case MiniVanInventoryItem.GokuHair: return "Причёска Гоку";
                case MiniVanInventoryItem.SuperSaiyanHair: return "Причёска Супер Сайяна";
                case MiniVanInventoryItem.MarioCap: return "Кепка Марио";
                case MiniVanInventoryItem.VikingHelmet: return "Шлем викинга";
                case MiniVanInventoryItem.PirateTricorn: return "Пиратская треуголка";
                default: return item.ToString();
            }
        }

        public static string GetSlotName(MiniVanEquipmentSlot slot)
        {
            switch (slot)
            {
                case MiniVanEquipmentSlot.Head: return "Голова";
                case MiniVanEquipmentSlot.Cloak: return "Плащ";
                case MiniVanEquipmentSlot.Boots: return "Ботинки";
                case MiniVanEquipmentSlot.Belt: return "Пояс";
                default: return slot.ToString();
            }
        }

        /// <summary>
        /// Resources path of the editable model prefab. When it exists it wins over the
        /// procedural builder, so artists can tweak the mesh in the editor.
        /// </summary>
        public static string GetModelResource(MiniVanInventoryItem item)
        {
            if (!IsCosmetic(item) || item == MiniVanInventoryItem.TestHat)
            {
                return string.Empty;
            }

            return "MiniVan/Cosmetics/" + item;
        }

        /// <summary>Resources path of the transparent silhouette shown on an empty slot.</summary>
        public static string GetSlotIconResource(MiniVanEquipmentSlot slot)
        {
            switch (slot)
            {
                case MiniVanEquipmentSlot.Head: return "EquipmentUI/icon_head";
                case MiniVanEquipmentSlot.Cloak: return "EquipmentUI/icon_cloak";
                case MiniVanEquipmentSlot.Boots: return "EquipmentUI/icon_boots";
                case MiniVanEquipmentSlot.Belt: return "EquipmentUI/icon_belt";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Layer for cosmetics worn by the local player. It is dropped from that player's own
        /// camera mask, so a wide brim never covers the first person view while everyone else
        /// still sees the hat on the character.
        /// </summary>
        public const string OwnerHiddenLayerName = "PlayerCosmetic";

        private static int ownerHiddenLayer = -2;

        public static int OwnerHiddenLayer
        {
            get
            {
                if (ownerHiddenLayer == -2)
                {
                    ownerHiddenLayer = LayerMask.NameToLayer(OwnerHiddenLayerName);
                }

                return ownerHiddenLayer;
            }
        }

        /// <summary>Unscaled radius of the new player head (half-capsule).</summary>
        public const float PlayerHeadRadius = 0.20f;

        /// <summary>Unscaled cylinder height under the head dome.</summary>
        public const float PlayerHeadCylinderHeight = 0.28f;

        /// <summary>Hats were authored for the old capsule head.</summary>
        public static float GetHeadHatScale()
        {
            return PlayerHeadRadius / MiniVanHatLibrary.HeadRadius;
        }

        public static float GetHeadHatScale(MiniVanInventoryItem item)
        {
            float scale = GetHeadHatScale();
            if (item == MiniVanInventoryItem.ZoroBandana ||
                item == MiniVanInventoryItem.NarutoHeadband)
            {
                scale += 0.02f;
            }

            return scale;
        }

        /// <summary>
        /// Hat origin in Head-bone space so the authored head sphere lines up with the new dome.
        /// </summary>
        public static Vector3 GetHeadHatLocalPosition()
        {
            float scale = GetHeadHatScale();
            return new Vector3(0f, PlayerHeadCylinderHeight - MiniVanHatLibrary.HeadCenter.y * scale, 0f);
        }

        /// <summary>Where the model attaches on the player capsule (non-head slots).</summary>
        public static Vector3 GetAttachOffset(MiniVanEquipmentSlot slot)
        {
            switch (slot)
            {
                case MiniVanEquipmentSlot.Head: return new Vector3(0f, 0.88f, 0f);
                case MiniVanEquipmentSlot.Cloak: return new Vector3(0f, 0.25f, -0.18f);
                case MiniVanEquipmentSlot.Boots: return new Vector3(0f, -0.78f, 0.02f);
                case MiniVanEquipmentSlot.Belt: return new Vector3(0f, 0.02f, 0f);
                default: return Vector3.zero;
            }
        }

        public static void AttachToWearer(
            Transform visual,
            MiniVanEquipmentSlot slot,
            Transform wearerRoot,
            Transform headBone,
            MiniVanInventoryItem item = MiniVanInventoryItem.None)
        {
            if (visual == null || wearerRoot == null)
            {
                return;
            }

            bool onHead = slot == MiniVanEquipmentSlot.Head && headBone != null;
            visual.SetParent(onHead ? headBone : wearerRoot, false);
            visual.localRotation = Quaternion.identity;
            visual.localScale = onHead ? Vector3.one * GetHeadHatScale(item) : Vector3.one;
            visual.localPosition = onHead ? GetHeadHatLocalPosition() : GetAttachOffset(slot);
        }
    }
}
