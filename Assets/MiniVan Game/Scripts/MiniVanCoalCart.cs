using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Coal cart trailer with WheelColliders. Hitch pole is visual/flexible;
    /// physics hitch uses a fixed mount so the cart does not jitter or tip.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MiniVanCoalCart : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private struct CartWheel
        {
            public Transform Visual;
            public WheelCollider Collider;
            public Quaternion VisualRotationOffset;
        }

        [Header("Hitch")]
        public Transform HitchPoint;
        public Transform HitchPivot;
        public Transform HitchPhysicsAnchor;
        public float HitchAttachDistance = 1.65f;
        public float HitchSnapDistance = 1.1f;
        public float InteractionReach = 5.5f;
        public float HitchPoleAimSpeed = 12f;
        public float HitchPoleRestPitch = -35.27f;

        [Header("Physics")]
        public float Mass = 160f;
        public float LinearDamping = 0.55f;
        public float AngularDamping = 0.8f;
        public float PlayerPushAcceleration = 8f;
        public float PlayerPushMaxSpeed = 1.35f;

        [Header("Wheels")]
        public float WheelRadius = 0.48f;
        public float WheelSuspensionDistance = 0.18f;
        public float WheelSpring = 35000f;
        public float WheelDamper = 3200f;
        public float WheelForwardStiffness = 2.2f;
        public float WheelSidewaysStiffness = 5.5f;
        public float IdleBrakeTorque = 55f;
        public float RollingBrakeTorque = 18f;
        public float LateralGripBleed = 14f;
        public float PlayerPushContactGap = 0.28f;

        [Header("Hitched Towing")]
        public float HitchedMass = 220f;
        public float HitchedLinearDamping = 0.15f;
        public float HitchedAngularDamping = 0.9f;
        public float HitchYawLimit = 55f;
        public float HitchPitchLimit = 25f;
        public float HitchRollLimit = 18f;
        public float HitchYawDamper = 90f;

        public bool IsHitched { get; private set; }

        private Rigidbody body;
        private ConfigurableJoint hitchJoint;
        private MiniVanTowHook hitchedHook;
        private Rigidbody hitchedVehicleBody;
        private CartWheel[] wheels;
        private Quaternion hitchPivotRestLocalRotation = Quaternion.identity;
        private bool hitchHierarchyReady;
        private BoxCollider bodyPushCollider;
        private float lastPushTime = -999f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            EnsureFlexibleHitch();
            EnsureWheels();
            EnsureBodyPushCollider();
            ConfigureFreeBody();
        }

        private void FixedUpdate()
        {
            if (!IsPhysicsAuthority())
            {
                SyncWheelVisuals();
                return;
            }

            EnsureWheels();
            TryProximityPlayerPush();
            TickWheels();
            ApplyGroundGrip();

            if (IsHitched)
            {
                StabilizeHitch();
            }
            else
            {
                UpdateUnhitchedHitchPole();
            }

            SyncWheelVisuals();
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || !IsPlayerNear(player))
            {
                return string.Empty;
            }

            if (IsHitched)
            {
                return "E - unhitch coal cart";
            }

            MiniVanTowHook hook = FindHookNearHitch();
            if (hook != null)
            {
                return "E - hitch coal cart";
            }

            return "Coal cart - back the van to hitch";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1) || player == null || !IsPlayerNear(player))
            {
                return;
            }

            if (!IsPhysicsAuthority())
            {
                return;
            }

            if (IsHitched)
            {
                Unhitch();
                return;
            }

            TryHitch();
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public void ReceiveControllerPush(MiniVanPlayer player, Vector3 moveDirection, Vector3 hitPoint)
        {
            if (IsHitched || player == null || body == null || !IsPhysicsAuthority())
            {
                return;
            }

            Vector3 dir = Vector3.ProjectOnPlane(moveDirection, Vector3.up);
            if (dir.sqrMagnitude < 0.01f)
            {
                Vector3 fromPlayer = hitPoint - player.transform.position;
                fromPlayer.y = 0f;
                dir = fromPlayer;
            }

            if (dir.sqrMagnitude < 0.01f)
            {
                return;
            }

            ApplyPushForce(dir.normalized);
        }

        public bool TryHitch()
        {
            if (IsHitched)
            {
                return false;
            }

            MiniVanTowHook hook = FindHookNearHitch();
            if (hook == null)
            {
                return false;
            }

            Rigidbody vehicleBody = hook.GetComponentInParent<Rigidbody>();
            if (vehicleBody == null)
            {
                return false;
            }

            EnsureFlexibleHitch();
            Vector3 hitchPos = GetPhysicsHitchWorldPosition();
            Vector3 hookPos = hook.AnchorPosition;
            if (Vector3.Distance(hitchPos, hookPos) > HitchAttachDistance)
            {
                return false;
            }

            // Yaw only on the ground plane — wheels keep the cart upright.
            Vector3 planarToHook = Vector3.ProjectOnPlane(hookPos - body.position, Vector3.up);
            if (planarToHook.sqrMagnitude > 0.01f)
            {
                float yaw = Quaternion.LookRotation(planarToHook.normalized, Vector3.up).eulerAngles.y;
                Quaternion uprightYaw = Quaternion.Euler(0f, yaw, 0f);
                body.MoveRotation(uprightYaw);
                transform.rotation = uprightYaw;
            }

            AimHitchPoleAt(hookPos, 1f);

            hitchPos = GetPhysicsHitchWorldPosition();
            Vector3 planarDelta = hookPos - hitchPos;
            planarDelta.y = 0f;
            if (planarDelta.magnitude <= HitchSnapDistance && planarDelta.sqrMagnitude > 0.0001f)
            {
                body.position += planarDelta;
                transform.position = body.position;
                AimHitchPoleAt(hookPos, 1f);
            }

            if (hitchJoint != null)
            {
                Destroy(hitchJoint);
            }

            hitchJoint = gameObject.AddComponent<ConfigurableJoint>();
            hitchJoint.connectedBody = vehicleBody;
            hitchJoint.autoConfigureConnectedAnchor = false;
            hitchJoint.anchor = transform.InverseTransformPoint(GetPhysicsHitchWorldPosition());
            hitchJoint.connectedAnchor = vehicleBody.transform.InverseTransformPoint(hook.AnchorPosition);

            hitchJoint.axis = Vector3.up;
            hitchJoint.secondaryAxis = Vector3.right;

            // Ball hitch: position locked, angles free enough for the tongue; wheels own pitch/roll.
            hitchJoint.xMotion = ConfigurableJointMotion.Locked;
            hitchJoint.yMotion = ConfigurableJointMotion.Locked;
            hitchJoint.zMotion = ConfigurableJointMotion.Locked;
            hitchJoint.angularXMotion = ConfigurableJointMotion.Limited;
            hitchJoint.angularYMotion = ConfigurableJointMotion.Limited;
            hitchJoint.angularZMotion = ConfigurableJointMotion.Limited;

            SoftJointLimitSpring limitSpring = new SoftJointLimitSpring
            {
                spring = 0f,
                damper = HitchYawDamper
            };
            hitchJoint.angularYZLimitSpring = limitSpring;
            hitchJoint.angularXLimitSpring = limitSpring;

            hitchJoint.lowAngularXLimit = new SoftJointLimit { limit = -HitchYawLimit };
            hitchJoint.highAngularXLimit = new SoftJointLimit { limit = HitchYawLimit };
            hitchJoint.angularYLimit = new SoftJointLimit { limit = HitchPitchLimit };
            hitchJoint.angularZLimit = new SoftJointLimit { limit = HitchRollLimit };

            hitchJoint.rotationDriveMode = RotationDriveMode.Slerp;
            hitchJoint.slerpDrive = new JointDrive
            {
                positionSpring = 0f,
                positionDamper = HitchYawDamper,
                maximumForce = 25000f
            };

            // Projection causes hitch jitter with WheelColliders — rely on the joint solver.
            hitchJoint.projectionMode = JointProjectionMode.None;
            hitchJoint.enableCollision = false;

            hitchedHook = hook;
            hitchedVehicleBody = vehicleBody;
            ConfigureHitchedBody();
            IsHitched = true;
            return true;
        }

        public void Unhitch()
        {
            if (!IsHitched)
            {
                return;
            }

            if (hitchJoint != null)
            {
                Destroy(hitchJoint);
                hitchJoint = null;
            }

            hitchedHook = null;
            hitchedVehicleBody = null;
            IsHitched = false;
            ConfigureFreeBody();
        }

        private void StabilizeHitch()
        {
            if (hitchJoint == null || hitchedHook == null || body == null || hitchedVehicleBody == null)
            {
                Unhitch();
                return;
            }

            // Visual tongue only — do NOT rewrite joint anchors every frame (that shakes the cart).
            AimHitchPoleAt(hitchedHook.AnchorPosition, Time.fixedDeltaTime * HitchPoleAimSpeed);
        }

        private void UpdateUnhitchedHitchPole()
        {
            MiniVanTowHook nearHook = FindHookNearHitch();
            if (nearHook != null &&
                Vector3.Distance(GetPhysicsHitchWorldPosition(), nearHook.AnchorPosition) <= HitchAttachDistance)
            {
                AimHitchPoleAt(nearHook.AnchorPosition, Time.fixedDeltaTime * HitchPoleAimSpeed);
            }
            else
            {
                RestoreHitchPoleRest(Time.fixedDeltaTime);
            }
        }

        private void ApplyPushForce(Vector3 pushDir)
        {
            pushDir = Vector3.ProjectOnPlane(pushDir, Vector3.up);
            if (pushDir.sqrMagnitude < 0.0001f || body == null)
            {
                return;
            }

            pushDir.Normalize();
            body.WakeUp();
            lastPushTime = Time.time;
            SetWheelBrakes(0f);

            // Drive planar speed directly — WheelCollider brakes/friction eat weak AddForce.
            float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            Vector3 velocity = body.linearVelocity;
            Vector3 planar = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float along = Vector3.Dot(planar, pushDir);
            float targetAlong = Mathf.MoveTowards(along, PlayerPushMaxSpeed, PlayerPushAcceleration * dt);
            Vector3 lateral = planar - pushDir * along;
            lateral *= 0.9f;
            Vector3 newPlanar = lateral + pushDir * targetAlong;
            body.linearVelocity = new Vector3(newPlanar.x, velocity.y, newPlanar.z);
        }

        private void TryProximityPlayerPush()
        {
            if (IsHitched || body == null)
            {
                return;
            }

            MiniVanPlayer player = MiniVanPlayer.LocalPlayer;
            if (player == null || !player.IsOwner)
            {
                return;
            }

            Vector3 playerPos = player.CharacterController != null
                ? player.CharacterController.bounds.center
                : player.transform.position;

            Collider probeCollider = bodyPushCollider != null ? bodyPushCollider : null;
            Vector3 closest = probeCollider != null
                ? probeCollider.ClosestPoint(playerPos)
                : body.worldCenterOfMass;

            Vector3 toCart = closest - playerPos;
            toCart.y = 0f;
            float gap = toCart.magnitude;
            float playerRadius = player.CharacterController != null ? player.CharacterController.radius : 0.35f;
            float surfaceGap = Mathf.Max(0f, gap - playerRadius);
            if (surfaceGap > PlayerPushContactGap)
            {
                return;
            }

            Vector3 walkIntent = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
            float vertical = MiniVanKeyBindings.MoveVertical();
            float horizontal = MiniVanKeyBindings.MoveHorizontal();
            if (Mathf.Abs(vertical) > 0.01f || Mathf.Abs(horizontal) > 0.01f)
            {
                walkIntent = player.transform.TransformDirection(new Vector3(horizontal, 0f, vertical));
                walkIntent = Vector3.ProjectOnPlane(walkIntent, Vector3.up);
            }

            if (walkIntent.sqrMagnitude < 0.01f)
            {
                return;
            }

            walkIntent.Normalize();
            Vector3 intoCart = toCart.sqrMagnitude > 0.0001f ? toCart.normalized : walkIntent;
            if (Vector3.Dot(walkIntent, intoCart) < 0.15f)
            {
                return;
            }

            ApplyPushForce(intoCart);
        }

        private void TickWheels()
        {
            if (wheels == null)
            {
                return;
            }

            bool recentlyPushed = !IsHitched && Time.time - lastPushTime <= 0.35f;
            float brakeTorque;
            if (IsHitched)
            {
                brakeTorque = RollingBrakeTorque * 0.15f;
            }
            else if (recentlyPushed)
            {
                brakeTorque = 0f;
            }
            else
            {
                float speed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
                brakeTorque = IdleBrakeTorque * Mathf.Clamp01(0.35f + speed);
            }

            SetWheelBrakes(brakeTorque);
            for (int i = 0; i < wheels.Length; i++)
            {
                WheelCollider wheel = wheels[i].Collider;
                if (wheel == null)
                {
                    continue;
                }

                wheel.motorTorque = 0.0001f;
                wheel.steerAngle = 0f;
            }
        }

        private void SetWheelBrakes(float brakeTorque)
        {
            if (wheels == null)
            {
                return;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i].Collider != null)
                {
                    wheels[i].Collider.brakeTorque = brakeTorque;
                }
            }
        }

        private void ApplyGroundGrip()
        {
            if (body == null || IsHitched)
            {
                return;
            }

            // Kill sideways skate relative to the cart facing.
            Vector3 velocity = body.linearVelocity;
            Vector3 local = transform.InverseTransformDirection(velocity);
            float bleed = 1f - Mathf.Clamp01(LateralGripBleed * Time.fixedDeltaTime);
            local.x *= bleed;
            Vector3 damped = transform.TransformDirection(local);
            body.linearVelocity = new Vector3(damped.x, velocity.y, damped.z);
        }

        private void SyncWheelVisuals()
        {
            if (wheels == null)
            {
                return;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                CartWheel wheel = wheels[i];
                if (wheel.Collider == null || wheel.Visual == null)
                {
                    continue;
                }

                wheel.Collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
                wheel.Visual.position = position;
                wheel.Visual.rotation = rotation * wheel.VisualRotationOffset;
            }
        }

        private void EnsureWheels()
        {
            if (wheels != null && wheels.Length == 4)
            {
                bool allValid = true;
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i].Collider == null || wheels[i].Visual == null)
                    {
                        allValid = false;
                        break;
                    }
                }

                if (allValid)
                {
                    return;
                }
            }

            string[] names = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            wheels = new CartWheel[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Transform visual = transform.Find(names[i]);
                if (visual == null)
                {
                    continue;
                }

                // Strip any mesh colliders on decorative wheels — WheelCollider owns contact.
                Collider[] meshCols = visual.GetComponents<Collider>();
                for (int c = 0; c < meshCols.Length; c++)
                {
                    meshCols[c].enabled = false;
                }

                string colliderName = "WheelCollider_" + names[i];
                Transform existing = transform.Find(colliderName);
                WheelCollider wheelCollider;
                if (existing != null)
                {
                    wheelCollider = existing.GetComponent<WheelCollider>();
                    if (wheelCollider == null)
                    {
                        wheelCollider = existing.gameObject.AddComponent<WheelCollider>();
                    }
                }
                else
                {
                    GameObject wheelObject = new GameObject(colliderName);
                    wheelObject.transform.SetParent(transform, false);
                    Vector3 local = visual.localPosition;
                    local.y = WheelRadius;
                    wheelObject.transform.localPosition = local;
                    wheelObject.transform.localRotation = Quaternion.identity;
                    wheelCollider = wheelObject.AddComponent<WheelCollider>();
                }

                ConfigureWheelCollider(wheelCollider);
                wheels[i] = new CartWheel
                {
                    Visual = visual,
                    Collider = wheelCollider,
                    VisualRotationOffset = Quaternion.Euler(0f, 0f, 90f)
                };
            }

            // Bed/side colliders must not scrape the ground and fight the wheels.
            RaiseBodyCollidersAboveWheels();
        }

        private void ConfigureWheelCollider(WheelCollider wheelCollider)
        {
            wheelCollider.radius = WheelRadius;
            wheelCollider.suspensionDistance = WheelSuspensionDistance;
            wheelCollider.mass = 28f;
            wheelCollider.forceAppPointDistance = 0.1f;
            wheelCollider.center = Vector3.zero;
            wheelCollider.ConfigureVehicleSubsteps(5f, 8, 10);

            JointSpring spring = wheelCollider.suspensionSpring;
            spring.spring = WheelSpring;
            spring.damper = WheelDamper;
            spring.targetPosition = 0.5f;
            wheelCollider.suspensionSpring = spring;

            WheelFrictionCurve forward = wheelCollider.forwardFriction;
            forward.extremumSlip = 0.4f;
            forward.extremumValue = 1f;
            forward.asymptoteSlip = 0.8f;
            forward.asymptoteValue = 0.65f;
            forward.stiffness = WheelForwardStiffness;
            wheelCollider.forwardFriction = forward;

            WheelFrictionCurve sideways = wheelCollider.sidewaysFriction;
            sideways.extremumSlip = 0.3f;
            sideways.extremumValue = 1f;
            sideways.asymptoteSlip = 0.65f;
            sideways.asymptoteValue = 0.55f;
            sideways.stiffness = WheelSidewaysStiffness;
            wheelCollider.sidewaysFriction = sideways;
        }

        private void RaiseBodyCollidersAboveWheels()
        {
            float minLocalBottom = WheelRadius + 0.05f;
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                Collider col = cols[i];
                if (col == null || col is WheelCollider || col.isTrigger)
                {
                    continue;
                }

                if (col.transform.name.StartsWith("Wheel_", System.StringComparison.Ordinal) ||
                    col.transform.name.StartsWith("WheelCollider_", System.StringComparison.Ordinal))
                {
                    col.enabled = false;
                    continue;
                }

                if (col is not BoxCollider box || col.transform == transform)
                {
                    continue;
                }

                // Local-space bottom of this box relative to the cart root.
                Vector3 localCenter = transform.InverseTransformPoint(box.bounds.center);
                Vector3 localExtents = transform.InverseTransformVector(box.bounds.extents);
                localExtents = new Vector3(Mathf.Abs(localExtents.x), Mathf.Abs(localExtents.y), Mathf.Abs(localExtents.z));
                float localBottom = localCenter.y - localExtents.y;
                if (localBottom >= minLocalBottom)
                {
                    continue;
                }

                float lift = minLocalBottom - localBottom;
                Vector3 center = box.center;
                float scaleY = Mathf.Max(0.01f, Mathf.Abs(box.transform.lossyScale.y));
                center.y += lift / scaleY;
                box.center = center;
            }
        }

        private void EnsureBodyPushCollider()
        {
            if (bodyPushCollider != null)
            {
                return;
            }

            Transform bed = transform.Find("Cart_Bed");
            if (bed != null)
            {
                bodyPushCollider = bed.GetComponent<BoxCollider>();
                if (bodyPushCollider != null)
                {
                    bodyPushCollider.enabled = true;
                    return;
                }
            }

            bodyPushCollider = gameObject.GetComponent<BoxCollider>();
            if (bodyPushCollider == null)
            {
                bodyPushCollider = gameObject.AddComponent<BoxCollider>();
                bodyPushCollider.size = new Vector3(3.6f, 0.9f, 5.2f);
                bodyPushCollider.center = new Vector3(0f, 0.85f, 0f);
            }
        }

        private void AimHitchPoleAt(Vector3 worldTarget, float blend)
        {
            if (HitchPivot == null)
            {
                return;
            }

            Vector3 from = HitchPivot.position;
            Vector3 dir = worldTarget - from;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion desiredWorld = Quaternion.LookRotation(dir.normalized, transform.up);
            if (blend >= 0.999f)
            {
                HitchPivot.rotation = desiredWorld;
                return;
            }

            float t = 1f - Mathf.Exp(-Mathf.Max(0f, blend));
            HitchPivot.rotation = Quaternion.Slerp(HitchPivot.rotation, desiredWorld, t);
        }

        private void RestoreHitchPoleRest(float dt)
        {
            if (HitchPivot == null || !hitchHierarchyReady)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-HitchPoleAimSpeed * 0.45f * dt);
            HitchPivot.localRotation = Quaternion.Slerp(HitchPivot.localRotation, hitchPivotRestLocalRotation, t);
        }

        private Vector3 GetPhysicsHitchWorldPosition()
        {
            EnsureFlexibleHitch();
            if (HitchPhysicsAnchor != null)
            {
                return HitchPhysicsAnchor.position;
            }

            return HitchPoint != null ? HitchPoint.position : transform.position;
        }

        private MiniVanTowHook FindHookNearHitch()
        {
            EnsureFlexibleHitch();
            return MiniVanTowHook.FindNearest(GetPhysicsHitchWorldPosition(), 0.35f);
        }

        private bool IsPlayerNear(MiniVanPlayer player)
        {
            EnsureFlexibleHitch();
            Vector3 probe = GetPhysicsHitchWorldPosition();
            Vector3 from = player.PlayerCamera != null
                ? player.PlayerCamera.transform.position
                : player.transform.position;

            return Vector3.Distance(from, probe) <= InteractionReach
                   || Vector3.Distance(player.transform.position, transform.position) <= InteractionReach + 1.5f;
        }

        private void EnsureFlexibleHitch()
        {
            if (hitchHierarchyReady && HitchPivot != null && HitchPoint != null && HitchPhysicsAnchor != null)
            {
                return;
            }

            Transform pole = transform.Find("Hitch_Pole");
            Transform tip = transform.Find("Hitch_Pole_Tip");
            if (tip == null)
            {
                tip = transform.Find("Hitch_Pivot/Hitch_Pole_Tip");
            }

            if (tip == null)
            {
                HitchPoint = transform;
                hitchHierarchyReady = true;
                return;
            }

            HitchPoint = tip;

            // Fixed physics mount at the tip's rest pose (does not rotate with the visual pole).
            if (HitchPhysicsAnchor == null)
            {
                Transform existingPhysics = transform.Find("Hitch_Physics_Anchor");
                if (existingPhysics != null)
                {
                    HitchPhysicsAnchor = existingPhysics;
                }
                else
                {
                    GameObject physicsObject = new GameObject("Hitch_Physics_Anchor");
                    HitchPhysicsAnchor = physicsObject.transform;
                    HitchPhysicsAnchor.SetParent(transform, false);
                    HitchPhysicsAnchor.position = tip.position;
                    HitchPhysicsAnchor.rotation = tip.rotation;
                }
            }

            if (HitchPivot == null)
            {
                Transform existingPivot = transform.Find("Hitch_Pivot");
                if (existingPivot != null)
                {
                    HitchPivot = existingPivot;
                }
                else
                {
                    GameObject pivotObject = new GameObject("Hitch_Pivot");
                    HitchPivot = pivotObject.transform;
                    HitchPivot.SetParent(transform, false);

                    if (pole != null)
                    {
                        float halfLength = Mathf.Abs(pole.localScale.z) * 0.5f;
                        Vector3 poleForward = pole.rotation * Vector3.forward;
                        Vector3 baseWorld = pole.position - poleForward * halfLength;
                        HitchPivot.position = baseWorld;
                        HitchPivot.rotation = Quaternion.LookRotation(
                            (tip.position - baseWorld).normalized,
                            transform.up);
                    }
                    else
                    {
                        HitchPivot.localPosition = new Vector3(0f, 0.33f, 2.7f);
                        HitchPivot.localRotation = Quaternion.Euler(HitchPoleRestPitch, 0f, 0f);
                    }

                    if (pole != null)
                    {
                        float length = Mathf.Abs(pole.localScale.z);
                        pole.SetParent(HitchPivot, true);
                        pole.localPosition = new Vector3(0f, 0f, length * 0.5f);
                        pole.localRotation = Quaternion.identity;
                        pole.localScale = new Vector3(
                            Mathf.Abs(pole.localScale.x) < 0.001f ? 0.1f : Mathf.Abs(pole.localScale.x),
                            Mathf.Abs(pole.localScale.y) < 0.001f ? 0.1f : Mathf.Abs(pole.localScale.y),
                            length);
                    }

                    tip.SetParent(HitchPivot, true);
                    float tipDistance = Vector3.Distance(HitchPivot.position, tip.position);
                    if (tipDistance < 0.05f && pole != null)
                    {
                        tipDistance = Mathf.Abs(pole.localScale.z);
                    }

                    tip.localPosition = new Vector3(0f, 0f, tipDistance);
                    tip.localRotation = Quaternion.identity;
                }
            }

            if (HitchPoint.parent != HitchPivot && HitchPivot != null)
            {
                HitchPoint.SetParent(HitchPivot, true);
            }

            hitchPivotRestLocalRotation = Quaternion.Euler(HitchPoleRestPitch, 0f, 0f);
            if (!IsHitched && HitchPivot != null)
            {
                HitchPivot.localRotation = hitchPivotRestLocalRotation;
            }

            hitchHierarchyReady = true;
        }

        private void ConfigureFreeBody()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            body.mass = Mass;
            body.linearDamping = LinearDamping;
            body.angularDamping = AngularDamping;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.isKinematic = false;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.None;
            body.centerOfMass = new Vector3(0f, 0.35f, 0f);
            body.maxAngularVelocity = 8f;
        }

        private void ConfigureHitchedBody()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            body.mass = HitchedMass;
            body.linearDamping = HitchedLinearDamping;
            body.angularDamping = HitchedAngularDamping;
            body.centerOfMass = new Vector3(0f, 0.35f, -0.2f);
            body.maxAngularVelocity = 6f;
            if (hitchedVehicleBody != null)
            {
                Vector3 v = hitchedVehicleBody.linearVelocity;
                body.linearVelocity = new Vector3(v.x, body.linearVelocity.y, v.z);
            }

            body.angularVelocity = Vector3.zero;
        }

        private static bool IsPhysicsAuthority()
        {
            if (Unity.Netcode.NetworkManager.Singleton == null ||
                !Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                return true;
            }

            return Unity.Netcode.NetworkManager.Singleton.IsServer;
        }

        private void OnDestroy()
        {
            Unhitch();
        }
    }
}
