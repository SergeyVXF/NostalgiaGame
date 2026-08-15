using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Socket at the dam spindle where the valve is inserted. After insertion the
    /// player turns the valve (hold E) to lower the dam gate.
    /// Pattern follows MiniVanCarBatteryReceiver.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanDamValveSocket : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float InteractionReach = 2.8f;

        public MiniVanDamObstacleController Controller;
        public MiniVanDamGate Gate;
        public Transform PlacementPoint;
        public MiniVanDamValve InstalledValve;

        [Header("Turning")]
        public float TurnSecondsToClose = 2.5f;
        public Transform ValveWheelVisual;
        public float WheelTurnDegreesPerSecond = 220f;

        public bool HasValve => InstalledValve != null && InstalledValve.IsInstalled;

        private float turnProgress;
        private bool turning;

        private void Awake()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            if (box.size.sqrMagnitude < 0.01f)
            {
                box.size = new Vector3(1.4f, 1.4f, 1.4f);
                box.center = new Vector3(0f, 0.35f, 0f);
            }
        }

        private void Update()
        {
            UpdateTurning();
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return string.Empty;
            }

            if (!HasValve)
            {
                return MiniVanDamValve.GetCarriedBy(player) != null
                    ? "E - insert valve"
                    : "Valve socket empty";
            }

            if (Gate != null && Gate.IsClosed)
            {
                return "Dam is closed";
            }

            return "Hold E - turn valve";
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

            if (!HasValve)
            {
                MiniVanDamValve carried = MiniVanDamValve.GetCarriedBy(player);
                if (carried != null)
                {
                    AttachValve(carried);
                }

                return;
            }

            // Valve installed: begin turning (hold is handled in Update).
            turning = true;
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        private void UpdateTurning()
        {
            if (!HasValve || Gate == null || Gate.IsClosed)
            {
                turning = false;
                return;
            }

            MiniVanPlayer player = MiniVanPlayer.LocalPlayer;
            bool holding = turning &&
                           player != null &&
                           MiniVanKeyBindings.GetKey(MiniVanKeyAction.Interact) &&
                           Vector3.Distance(player.transform.position, transform.position) <= InteractionReach;
            if (!holding)
            {
                turning = false;
                return;
            }

            turnProgress += Time.deltaTime / Mathf.Max(0.1f, TurnSecondsToClose);
            turnProgress = Mathf.Clamp01(turnProgress);
            RotateWheelVisual();
            Gate.SetCloseProgress(turnProgress);

            // Use a soft threshold so float rounding cannot skip NotifyDamClosed.
            if (turnProgress >= 0.999f)
            {
                turnProgress = 1f;
                turning = false;
                Gate.Close();
            }
        }

        private void RotateWheelVisual()
        {
            Transform wheel = ValveWheelVisual;
            if (wheel == null && InstalledValve != null)
            {
                wheel = InstalledValve.transform;
            }

            if (wheel == null)
            {
                return;
            }

            // Spindle axis is world up; spin the handwheel around it.
            wheel.Rotate(Vector3.up, WheelTurnDegreesPerSecond * Time.deltaTime, Space.World);
        }

        public bool AttachValve(MiniVanDamValve valve)
        {
            if (valve == null || HasValve)
            {
                return false;
            }

            InstalledValve = valve;
            valve.PlaceInto(this);
            if (ValveWheelVisual == null)
            {
                ValveWheelVisual = valve.transform;
            }

            if (Controller != null)
            {
                Controller.NotifyValveInserted();
            }

            return true;
        }

        public void DetachValve(MiniVanDamValve valve)
        {
            if (InstalledValve != valve)
            {
                return;
            }

            InstalledValve = null;
            if (valve != null)
            {
                valve.ClearInstalledSocket(this);
            }
        }
    }
}
