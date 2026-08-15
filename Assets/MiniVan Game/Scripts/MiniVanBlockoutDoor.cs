using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanBlockoutDoor : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float InteractionReach = 2.2f;

        public Transform DoorPivot;
        public float OpenAngle = 95f;
        public float OpenSpeed = 9f;
        public bool StartsOpen;

        private bool isOpen;
        private Quaternion closedRotation;
        private Quaternion openRotation;

        private void Awake()
        {
            if (DoorPivot == null)
            {
                DoorPivot = transform;
            }

            closedRotation = DoorPivot.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, OpenAngle, 0f);
            isOpen = StartsOpen;

            if (StartsOpen)
            {
                DoorPivot.localRotation = openRotation;
            }
        }

        private void Update()
        {
            if (DoorPivot == null)
            {
                return;
            }

            Quaternion target = isOpen ? openRotation : closedRotation;
            float blend = 1f - Mathf.Exp(-OpenSpeed * Time.deltaTime);
            DoorPivot.localRotation = Quaternion.Slerp(DoorPivot.localRotation, target, blend);
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return string.Empty;
            }

            return isOpen ? "E - close door" : "E - open door";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1))
            {
                return;
            }

            if (player == null || Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return;
            }

            Toggle();
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public void Toggle()
        {
            isOpen = !isOpen;
        }
    }
}
