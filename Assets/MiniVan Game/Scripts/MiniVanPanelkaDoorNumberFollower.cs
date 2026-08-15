using UnityEngine;

namespace MiniVanGame
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MiniVanPanelkaDoorNumberFollower : MonoBehaviour
    {
        [SerializeField] private Renderer doorPanel;
        [SerializeField] private Vector3 panelLocalAnchor;
        [SerializeField] private Vector3 panelLocalForward = Vector3.forward;
        [SerializeField] private Vector3 panelLocalUp = Vector3.up;

        public void Configure(
            Renderer panel,
            Vector3 worldPosition,
            Vector3 worldForward,
            Vector3 worldUp)
        {
            doorPanel = panel;
            if (doorPanel == null)
                return;

            Transform panelTransform = doorPanel.transform;
            panelLocalAnchor = panelTransform.InverseTransformPoint(worldPosition);
            panelLocalForward =
                panelTransform.InverseTransformDirection(worldForward).normalized;
            panelLocalUp =
                panelTransform.InverseTransformDirection(worldUp).normalized;
            ApplyPose();
        }

        private void OnEnable()
        {
            ApplyPose();
        }

        private void LateUpdate()
        {
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (doorPanel == null)
                return;

            Transform panelTransform = doorPanel.transform;
            Vector3 forward =
                panelTransform.TransformDirection(panelLocalForward).normalized;
            Vector3 up =
                panelTransform.TransformDirection(panelLocalUp).normalized;
            transform.position = panelTransform.TransformPoint(panelLocalAnchor);
            transform.rotation = Quaternion.LookRotation(forward, up);
        }
    }
}
