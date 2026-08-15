// Drop-anywhere static ladder.
//
// Place the root at the FOOT of the ladder; it builds upward along local +Y.
// Local +Z is the climbing side (open space), local -Z is the wall / the landing
// the climber steps onto at the top. Set Height, rotate so +Z faces away from the
// wall, done — every collider, the climb volume and the MiniVanLadder tuning are
// derived from that.
using UnityEngine;

namespace MiniVanGame
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MiniVanLadder))]
    public class MiniVanUniversalLadder : MonoBehaviour
    {
        private const string GeometryName = "Ladder_Geometry";
        private const string ClimbTriggerName = "ClimbTrigger";

        [Header("Shape")]
        [Tooltip("Distance from the foot of the ladder to the landing it serves. " +
                 "Mount it so the top rung is level with that landing.")]
        [Min(0.6f)] public float Height = 3f;

        [Tooltip("Outer rail-to-rail width.")]
        [Min(0.3f)] public float Width = 0.56f;

        [Min(0.15f)] public float RungSpacing = 0.3f;
        [Min(0.02f)] public float RailThickness = 0.07f;
        [Min(0.02f)] public float RungThickness = 0.05f;

        [Header("Look")]
        [Tooltip("Leave empty to keep whatever material the rebuilt boxes get by default.")]
        public Material RailMaterial;

        [Tooltip("Turn off for an invisible climb volume over ladder art you modelled yourself.")]
        public bool BuildVisual = true;

        [Header("Feel")]
        [Min(0.5f)] public float ClimbSpeed = 3.2f;

        [Tooltip("Gap the climber holds in front of the rungs. Roughly capsule radius + rung " +
                 "half-depth, so the body never intersects the ladder.")]
        [Min(0.15f)] public float StandOffDistance = 0.5f;

        [Tooltip("Free planar wobble before the soft stick pulls back. Larger = looser, " +
                 "smaller = snappier. Zero would feel like a magnet, so keep it generous.")]
        [Min(0f)] public float LateralDeadzone = 0.3f;

        [Tooltip("How far past the top rung the climber is eased onto the landing.")]
        [Min(0.2f)] public float TopExitForward = 0.95f;

        [Tooltip("How far below the landing the top exit begins. The climber pops over the " +
                 "edge from here instead of riding the rails into the sky.")]
        [Min(0.05f)] public float TopExitStartDrop = 0.4f;

        // Guards against rebuilding (and dirtying the scene) on every domain reload.
        [SerializeField, HideInInspector] private string builtSignature;

        /// <summary>Rebuilds colliders, climb volume and (optionally) the rail/rung boxes.</summary>
        public void Rebuild()
        {
            MiniVanLadder ladder = GetComponent<MiniVanLadder>();
            if (ladder == null)
            {
                return;
            }

            float height = Mathf.Max(0.6f, Height);
            float width = Mathf.Max(0.3f, Width);

            BuildBlocker(height, width);
            BoxCollider climbTrigger = BuildClimbTrigger(height, width);
            ConfigureLadder(ladder, climbTrigger, height, width);
            BuildGeometry(height, width);
            builtSignature = CurrentSignature();
        }

        private string CurrentSignature()
        {
            return string.Join("|",
                Height.ToString("F3"),
                Width.ToString("F3"),
                RungSpacing.ToString("F3"),
                RailThickness.ToString("F3"),
                RungThickness.ToString("F3"),
                BuildVisual ? "1" : "0",
                RailMaterial != null ? RailMaterial.name : "-",
                transform.Find(GeometryName) != null ? "g" : "-");
        }

        private void Reset()
        {
            Rebuild();
        }

        private void OnEnable()
        {
            // Runtime start: colliders and tuning are serialized, but a ladder dropped in by
            // script still needs one pass. Geometry is skipped when it already exists.
            if (Application.isPlaying)
            {
                ConfigureLadderOnly();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // OnValidate cannot create/destroy objects; defer one editor tick.
            UnityEditor.EditorApplication.delayCall += DeferredRebuild;
        }

        private void DeferredRebuild()
        {
            UnityEditor.EditorApplication.delayCall -= DeferredRebuild;
            if (this == null || Application.isPlaying)
            {
                return;
            }

            if (builtSignature == CurrentSignature())
            {
                // Domain reload / unrelated inspector edit: nothing structural to redo.
                return;
            }

            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(gameObject) &&
                !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                // Adding / removing children of a prefab instance is not allowed. Tune the
                // shape in Prefab Mode, or unpack the instance first.
                ConfigureLadderOnly();
                return;
            }

            Rebuild();
        }
#endif

        /// <summary>Refresh the tuning without touching the hierarchy.</summary>
        private void ConfigureLadderOnly()
        {
            MiniVanLadder ladder = GetComponent<MiniVanLadder>();
            if (ladder == null)
            {
                return;
            }

            Transform existing = transform.Find(ClimbTriggerName);
            BoxCollider climbTrigger = existing != null ? existing.GetComponent<BoxCollider>() : null;
            ConfigureLadder(ladder, climbTrigger, Mathf.Max(0.6f, Height), Mathf.Max(0.3f, Width));
        }

        /// <summary>
        /// The root box is the solid rung plane: thin enough to leave the climb column free,
        /// wide enough that the capsule cannot squeeze between the rails.
        /// </summary>
        private void BuildBlocker(float height, float width)
        {
            BoxCollider blocker = GetComponent<BoxCollider>();
            if (blocker == null)
            {
                blocker = gameObject.AddComponent<BoxCollider>();
            }

            // Stop just under the landing so the top face never fights the deck floor.
            float blockerHeight = Mathf.Max(0.3f, height - 0.05f);
            blocker.center = new Vector3(0f, blockerHeight * 0.5f, 0f);
            blocker.size = new Vector3(width + 0.04f, blockerHeight, 0.16f);
            blocker.isTrigger = false;
            blocker.enabled = true;
        }

        /// <summary>
        /// Latch volume. Reaches above the landing so the player can re-grab from the deck to
        /// climb back down, and below the foot so the first rung grabs from the ground.
        /// </summary>
        private BoxCollider BuildClimbTrigger(float height, float width)
        {
            Transform triggerTransform = transform.Find(ClimbTriggerName);
            if (triggerTransform == null)
            {
                GameObject holder = new GameObject(ClimbTriggerName);
                holder.transform.SetParent(transform, false);
                triggerTransform = holder.transform;
            }

            triggerTransform.localPosition = Vector3.zero;
            triggerTransform.localRotation = Quaternion.identity;
            triggerTransform.localScale = Vector3.one;

            BoxCollider trigger = triggerTransform.GetComponent<BoxCollider>();
            if (trigger == null)
            {
                trigger = triggerTransform.gameObject.AddComponent<BoxCollider>();
            }

            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, height * 0.5f + 0.55f, 0.2f);
            trigger.size = new Vector3(width + 1f, height + 1.5f, 1.4f);
            return trigger;
        }

        private void ConfigureLadder(MiniVanLadder ladder, BoxCollider climbTrigger, float height, float width)
        {
            ladder.ClimbSpeed = ClimbSpeed;
            ladder.ClimbStandOffLocalDirection = Vector3.forward;
            ladder.ClimbStandOffDistance = StandOffDistance;
            ladder.ClimbLateralDeadzone = LateralDeadzone;
            ladder.ClimbSoftStickStrength = 4.5f;
            ladder.ClimbMaxLateralDrift = Mathf.Max(LateralDeadzone + 0.2f, 0.6f);

            // Step onto the landing behind the rails, not off the front of the ladder.
            ladder.RoofEntryLocalDirection = Vector3.back;
            ladder.RoofEntryHeight = Mathf.Max(0.3f, height - TopExitStartDrop);
            ladder.AutoTopExit = true;
            ladder.TopExitForward = TopExitForward;
            ladder.RoofEntryPushSpeed = 0f;

            ladder.EngageHalfWidth = Mathf.Max(1.05f, width * 0.5f + 0.55f);
            ladder.EngageDepth = 1.05f;

            if (climbTrigger != null)
            {
                ladder.SetAuthoredClimbTrigger(climbTrigger);
            }
            else
            {
                ladder.SyncClimbVolume();
            }
        }

        private void BuildGeometry(float height, float width)
        {
            Transform existing = transform.Find(GeometryName);
            if (existing != null)
            {
                DestroyObject(existing.gameObject);
            }

            if (!BuildVisual)
            {
                return;
            }

            GameObject geometry = new GameObject(GeometryName);
            geometry.transform.SetParent(transform, false);

            float rail = Mathf.Max(0.02f, RailThickness);
            float rung = Mathf.Max(0.02f, RungThickness);
            float halfWidth = width * 0.5f - rail * 0.5f;

            AddBox(geometry.transform, "Rail_Left",
                new Vector3(-halfWidth, height * 0.5f, 0f),
                new Vector3(rail, height, rail));
            AddBox(geometry.transform, "Rail_Right",
                new Vector3(halfWidth, height * 0.5f, 0f),
                new Vector3(rail, height, rail));

            float spacing = Mathf.Max(0.15f, RungSpacing);
            int rungCount = Mathf.Max(1, Mathf.FloorToInt((height - 0.1f) / spacing));
            for (int i = 0; i < rungCount; i++)
            {
                float y = Mathf.Min(height - 0.05f, spacing * 0.5f + i * spacing);
                AddBox(geometry.transform, "Rung_" + i,
                    new Vector3(0f, y, 0f),
                    new Vector3(width - rail, rung, rung));
            }
        }

        /// <summary>
        /// Rails and rungs are decoration only — the single thin root box does all the
        /// blocking, so the capsule can never catch on an individual rung.
        /// </summary>
        private void AddBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = localScale;

            Collider collider = box.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyObject(collider);
            }

            if (RailMaterial != null)
            {
                MeshRenderer renderer = box.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = RailMaterial;
                }
            }
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDrawGizmosSelected()
        {
            float height = Mathf.Max(0.6f, Height);
            Gizmos.matrix = transform.localToWorldMatrix;

            // Climb column the player is eased toward.
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            Gizmos.DrawLine(
                new Vector3(0f, 0f, StandOffDistance),
                new Vector3(0f, height, StandOffDistance));

            // Where the top exit drops the player.
            Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.9f);
            Vector3 exitStart = new Vector3(0f, height - TopExitStartDrop, StandOffDistance);
            Vector3 exitEnd = new Vector3(0f, height, -TopExitForward);
            Gizmos.DrawLine(exitStart, exitEnd);
            Gizmos.DrawWireSphere(exitEnd, 0.12f);
        }
    }
}
