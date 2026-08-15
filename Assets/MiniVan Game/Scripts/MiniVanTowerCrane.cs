using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Drives the tower crane. The cab levers feed continuous input here: while a
    /// lever is held the axis moves, the moment it is released the axis stops
    /// where it is.
    ///
    /// Anything standing on the slewing deck - the operator included - is carried
    /// around with it, otherwise the crane would turn out from under the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanTowerCrane : MonoBehaviour
    {
        [Header("Rig")]
        public Transform Slew;
        public Transform Trolley;
        public Transform Rope;
        public Transform Magnet;

        [Header("Travel limits")]
        public float TrolleyNear = 3.0f;      // local Z on the slew
        public float TrolleyFar = 13.5f;
        public float RopeRestLength = 6.2f;   // authored rope length at scale 1
        public float HoistDown = 5.5f;        // how far below rest it can be paid out
        public float HoistUp = 3.6f;          // how far above rest it can be pulled

        [Header("Speeds")]
        public float SlewSpeed = 16f;         // degrees per second at full lever
        public float TrolleySpeed = 1.8f;     // metres per second
        public float HoistSpeed = 1.8f;       // metres per second

        [Header("Carrying the operator")]
        [Tooltip("Radius around the mast, in crane-local metres, inside which a player rides along.")]
        public float CarryRadius = 4.5f;
        public float CarryBelow = 2.5f;
        public float CarryAbove = 4.0f;

        public bool MagnetOn { get; private set; }

        private float slewInput;              // -1..1
        private float trolleyInput;
        private float hoistInput;

        private float trolleyPos;
        private float hoist;                  // 0 = rest, + = lowered
        private Vector3 magnetRest;
        private Vector3 ropeRestScale;
        private bool captured;

        private MiniVanPlayer player;
        private float playerSearchTimer;

        public void SetSlewInput(float v) { slewInput = Mathf.Clamp(v, -1f, 1f); }
        public void SetTrolleyInput(float v) { trolleyInput = Mathf.Clamp(v, -1f, 1f); }
        public void SetHoistInput(float v) { hoistInput = Mathf.Clamp(v, -1f, 1f); }
        public void ToggleMagnet() { MagnetOn = !MagnetOn; }

        private void Awake()
        {
            Capture();
        }

        private void Capture()
        {
            if (captured)
                return;
            if (Magnet != null) magnetRest = Magnet.localPosition;
            if (Rope != null) ropeRestScale = Rope.localScale;
            if (Trolley != null) trolleyPos = Trolley.localPosition.z;
            captured = true;
        }

        private void Update()
        {
            Capture();
            float dt = Time.deltaTime;

            if (Slew != null && Mathf.Abs(slewInput) > 0.001f)
            {
                float delta = slewInput * SlewSpeed * dt;
                Slew.localRotation = Slew.localRotation * Quaternion.Euler(0f, delta, 0f);
                CarryRider(delta);
            }

            if (Trolley != null && Mathf.Abs(trolleyInput) > 0.001f)
            {
                trolleyPos = Mathf.Clamp(
                    trolleyPos + trolleyInput * TrolleySpeed * dt, TrolleyNear, TrolleyFar);
                Vector3 p = Trolley.localPosition;
                p.z = trolleyPos;
                Trolley.localPosition = p;
            }

            if (Mathf.Abs(hoistInput) > 0.001f)
            {
                hoist = Mathf.Clamp(hoist + hoistInput * HoistSpeed * dt, -HoistUp, HoistDown);
                ApplyHoist();
            }
        }

        private void ApplyHoist()
        {
            if (Magnet != null)
                Magnet.localPosition = magnetRest + Vector3.down * hoist;

            if (Rope != null && RopeRestLength > 0.01f)
            {
                Vector3 s = ropeRestScale;
                s.y = ropeRestScale.y * Mathf.Max(0.02f, (RopeRestLength + hoist) / RopeRestLength);
                Rope.localScale = s;
            }
        }

        /// <summary>Spin whoever is up in the cab around the mast with the crane.</summary>
        private void CarryRider(float degrees)
        {
            playerSearchTimer -= Time.deltaTime;
            if (player == null && playerSearchTimer <= 0f)
            {
                player = FindFirstObjectByType<MiniVanPlayer>();
                playerSearchTimer = 1f;
            }
            if (player == null || Slew == null)
                return;

            float scale = Mathf.Abs(transform.lossyScale.x);
            Vector3 pivot = Slew.position;
            Vector3 offset = player.transform.position - pivot;

            float horizontal = new Vector2(offset.x, offset.z).magnitude;
            if (horizontal > CarryRadius * scale)
                return;
            if (offset.y < -CarryBelow * scale || offset.y > CarryAbove * scale)
                return;

            Quaternion spin = Quaternion.AngleAxis(degrees, Vector3.up);
            Vector3 target = pivot + spin * offset;

            // a CharacterController fights direct position writes, so step around it
            CharacterController cc = player.CharacterController;
            bool toggled = cc != null && cc.enabled;
            if (toggled) cc.enabled = false;
            player.transform.position = target;
            player.transform.rotation = spin * player.transform.rotation;
            if (toggled) cc.enabled = true;
        }
    }
}
