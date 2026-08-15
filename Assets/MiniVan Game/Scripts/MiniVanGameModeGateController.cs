using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanGameModeGateKind
    {
        StartLever,
        SaveHorn
    }

    [DisallowMultipleComponent]
    public sealed class MiniVanGameModeGateController : MonoBehaviour
    {
        public int GateId;
        public MiniVanGameModeGateKind Kind;
        public Transform LeftLeaf;
        public Transform RightLeaf;
        public Transform LeverPoint;
        public Vector3 LeftClosedPosition;
        public Vector3 RightClosedPosition;
        public Vector3 LeftOpenPosition;
        public Vector3 RightOpenPosition;
        [Min(0.1f)] public float MoveSeconds = 1.6f;
        [Min(0f)] public float AutoCloseSeconds;
        [Min(2f)] public float HornRadius = 24f;
        [Min(1f)] public float LeverUseRadius = 5.5f;

        private bool targetOpen;
        private float stateBlend;
        private float closeAt = float.PositiveInfinity;

        public bool IsOpen => targetOpen;

        private void Awake()
        {
            ApplyState(0f);
        }

        private void Update()
        {
            if (targetOpen && AutoCloseSeconds > 0f && Time.time >= closeAt)
            {
                CloseLocal();
            }

            float target = targetOpen ? 1f : 0f;
            stateBlend = Mathf.MoveTowards(stateBlend, target,
                Time.deltaTime / Mathf.Max(0.1f, MoveSeconds));
            ApplyState(stateBlend * stateBlend * (3f - 2f * stateBlend));
        }

        public bool CanUseLever(MiniVanPlayer player)
        {
            if (Kind != MiniVanGameModeGateKind.StartLever || player == null)
            {
                return false;
            }

            Vector3 point = LeverPoint != null ? LeverPoint.position : transform.position;
            return Vector3.Distance(player.transform.position, point) <= LeverUseRadius;
        }

        public bool CanRespondToHorn(Vector3 hornPosition)
        {
            return Kind == MiniVanGameModeGateKind.SaveHorn &&
                   Vector3.Distance(transform.position, hornPosition) <= HornRadius;
        }

        public void OpenLocal()
        {
            targetOpen = true;
            closeAt = AutoCloseSeconds > 0f
                ? Time.time + AutoCloseSeconds
                : float.PositiveInfinity;
        }

        public void CloseLocal()
        {
            targetOpen = false;
            closeAt = float.PositiveInfinity;
        }

        public static MiniVanGameModeGateController FindGate(int gateId)
        {
            MiniVanGameModeGateController[] gates =
                FindObjectsByType<MiniVanGameModeGateController>(FindObjectsSortMode.None);
            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i] != null && gates[i].GateId == gateId)
                {
                    return gates[i];
                }
            }
            return null;
        }

        public static MiniVanGameModeGateController FindHornGate(Vector3 hornPosition)
        {
            MiniVanGameModeGateController[] gates =
                FindObjectsByType<MiniVanGameModeGateController>(FindObjectsSortMode.None);
            MiniVanGameModeGateController nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i] == null || !gates[i].CanRespondToHorn(hornPosition))
                {
                    continue;
                }

                float distance = Vector3.Distance(gates[i].transform.position, hornPosition);
                if (distance < nearestDistance)
                {
                    nearest = gates[i];
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        private void ApplyState(float blend)
        {
            if (LeftLeaf != null)
            {
                LeftLeaf.localPosition = Vector3.Lerp(LeftClosedPosition, LeftOpenPosition, blend);
            }
            if (RightLeaf != null)
            {
                RightLeaf.localPosition = Vector3.Lerp(RightClosedPosition, RightOpenPosition, blend);
            }
        }
    }

}
