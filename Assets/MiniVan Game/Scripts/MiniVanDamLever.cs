using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Power lever. After the generator is on, both levers must be pulled within 1s
    /// or the first snaps back.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanDamLever : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float InteractionReach = 2.4f;

        public MiniVanDamObstacleController Controller;
        public Transform Handle;
        public Vector3 UpEuler = new Vector3(-32f, 0f, 0f);
        public Vector3 DownEuler = new Vector3(32f, 0f, 0f);

        public bool IsDown { get; private set; }

        private void Awake()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.9f, 1.1f, 0.9f);
            box.center = new Vector3(0f, 0.5f, 0f);
            ApplyVisual();
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return string.Empty;
            }

            if (Controller != null && Controller.LeversPulled)
            {
                return "Levers engaged";
            }

            if (Controller != null && !Controller.GeneratorOn)
            {
                return "Generator must be ON first";
            }

            if (IsDown)
            {
                return "Hurry - pull the other lever!";
            }

            return "E - pull lever";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1))
            {
                return;
            }

            if (player == null || IsDown ||
                Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return;
            }

            if (Controller != null && (!Controller.GeneratorOn || Controller.LeversPulled))
            {
                return;
            }

            IsDown = true;
            ApplyVisual();
            if (Controller != null)
            {
                Controller.NotifyLeverPulled(this);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public void ResetLever()
        {
            IsDown = false;
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (Handle != null)
            {
                Handle.localRotation = Quaternion.Euler(IsDown ? DownEuler : UpEuler);
            }
        }
    }
}
