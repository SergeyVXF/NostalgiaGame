using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// The dam gate / shutter that blocks the side inflow. Slides down while the
    /// valve is being turned. Notifies the controller when fully closed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanDamGate : MonoBehaviour
    {
        public MiniVanDamObstacleController Controller;
        public Transform GateVisual;
        public Vector3 OpenLocalPosition = new Vector3(0f, 14f, 0f);
        public Vector3 ClosedLocalPosition = new Vector3(0f, 1.3f, 0f);
        public float CloseSpeed = 1.6f;

        public bool IsClosed { get; private set; }

        private bool movingToClosed;
        private float closeProgress01;

        private void Awake()
        {
            if (Controller == null)
            {
                Controller = GetComponentInParent<MiniVanDamObstacleController>();
            }

            SnapOpen();
        }

        public void Close()
        {
            SetCloseProgress(1f);
        }

        /// <summary>
        /// Drives the shutter between open and closed while the valve is being turned.
        /// </summary>
        public void SetCloseProgress(float progress01)
        {
            closeProgress01 = Mathf.Clamp01(progress01);
            ApplyVisualProgress(closeProgress01);

            if (closeProgress01 >= 0.999f)
            {
                movingToClosed = false;
                BecomeClosed();
            }
            else
            {
                IsClosed = false;
                movingToClosed = false;
            }
        }

        private void Update()
        {
            if (!movingToClosed || GateVisual == null || IsClosed)
            {
                return;
            }

            GateVisual.localPosition = Vector3.MoveTowards(
                GateVisual.localPosition,
                ClosedLocalPosition,
                CloseSpeed * Time.deltaTime);

            float span = Mathf.Max(0.001f, Mathf.Abs(OpenLocalPosition.y - ClosedLocalPosition.y));
            closeProgress01 = 1f - Mathf.Clamp01(
                Mathf.Abs(GateVisual.localPosition.y - ClosedLocalPosition.y) / span);

            if (Vector3.Distance(GateVisual.localPosition, ClosedLocalPosition) <= 0.001f)
            {
                GateVisual.localPosition = ClosedLocalPosition;
                movingToClosed = false;
                BecomeClosed();
            }
        }

        public void SnapOpen()
        {
            if (GateVisual != null)
            {
                GateVisual.localPosition = OpenLocalPosition;
            }

            IsClosed = false;
            movingToClosed = false;
            closeProgress01 = 0f;
        }

        private void BecomeClosed()
        {
            if (GateVisual != null)
            {
                GateVisual.localPosition = ClosedLocalPosition;
            }

            closeProgress01 = 1f;
            movingToClosed = false;

            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            if (Controller != null)
            {
                Controller.NotifyDamClosed();
            }
        }

        private void ApplyVisualProgress(float progress01)
        {
            if (GateVisual == null)
            {
                return;
            }

            GateVisual.localPosition = Vector3.Lerp(OpenLocalPosition, ClosedLocalPosition, progress01);
        }
    }
}
