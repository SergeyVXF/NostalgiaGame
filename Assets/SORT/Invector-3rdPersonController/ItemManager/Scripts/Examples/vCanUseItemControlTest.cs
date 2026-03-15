
using System.Collections.Generic;
namespace Invector.vItemManager
{
    using vCharacterController;
    public class vCanUseItemControlTest : vMonoBehaviour
    {
        vThirdPersonController tp;

        private void Start()
        {
            tp = GetComponent<vThirdPersonController>();
            var itemManager = GetComponent<vItemManager>();
            if (itemManager)
            {
                itemManager.canUseItemDelegate -= new vItemManager.CanUseItemDelegate(CanUseItem);
                itemManager.canUseItemDelegate += new vItemManager.CanUseItemDelegate(CanUseItem);
            }

        }

        private void OnDestroy()
        {
            var itemManager = GetComponent<vItemManager>();
            if (itemManager)
                itemManager.canUseItemDelegate -= new vItemManager.CanUseItemDelegate(CanUseItem);
        }

        private void CanUseItem(vItem item, ref List<bool> validateResult)
        {
            if (item.GetItemAttribute(vItemAttributes.Health) != null)
            {
                var valid = tp.currentHealth < tp.maxHealth;
                vHUDController.instance.ShowText(valid ? "Increase health" : "Can't use " + item.name + " because your health is full", 4f);
                if (!valid)
                    validateResult.Add(valid);
            }

        }
    }
}