using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanBridgeCableEnd : MonoBehaviour, IMiniVanGameModeInteractable
    {
        public MiniVanBridgePowerCable Cable;
        public int EndIndex;

        private void Awake()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = false;
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            return Cable != null ? Cable.GetEndPrompt(EndIndex, player) : string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Cable != null)
            {
                Cable.InteractEnd(EndIndex, player);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }
    }
}
