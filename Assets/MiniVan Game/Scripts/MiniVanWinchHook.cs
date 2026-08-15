using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiniVanGame
{
    /// <summary>
    /// Portable screw-eye winch hook. Hand-carry only (no inventory slot):
    /// E pick up, Q drop, hold E 1.5s to screw into any static surface along its normal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanWinchHook : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float PickupReach = 2.35f;
        private const float PlaceRayDistance = 4.5f;
        private const float PlaceHoldSeconds = 1.5f;
        private const float TipEmbed = 0.025f;

        // Mesh tip is near local origin, screw axis = local +Z (eye outward).
        private static readonly Vector3 CarryLocalPosition = new Vector3(0.55f, -0.35f, 0.55f);
        private static readonly Vector3 CarryLocalEuler = new Vector3(15f, -25f, 70f);
        private static readonly Vector3 RidingCarryLocalPosition = new Vector3(0.55f, 1.05f, 0.1f);
        private static readonly Vector3 RidingCarryLocalEuler = new Vector3(0f, 0f, 80f);

        private static readonly Dictionary<MiniVanPlayer, MiniVanWinchHook> carriedByPlayer =
            new Dictionary<MiniVanPlayer, MiniVanWinchHook>();

        private static Material ghostGreenMaterial;
        private static Material ghostRedMaterial;
        private static Texture2D holdRingTexture;
        private static int holdRingBucket = -1;

        private Rigidbody body;
        private Collider[] colliders;
        private MiniVanPlayer carrier;
        private bool isInstalled;
        private Transform installAnchor;

        private GameObject placementGhost;
        private Renderer[] ghostRenderers;
        private bool placementHasHit;
        private bool placementValid;
        private Vector3 placementPosition;
        private Quaternion placementRotation = Quaternion.identity;
        private Transform placementParent;
        private float placeHoldProgress;
        private bool lastGhostValid = true;

        private Renderer[] highlightRenderers;
        private Material[][] originalMaterials;
        private Material outlineMaterial;
        private bool isHighlighted;

        private Vector3 freeLocalScale = Vector3.one;
        private bool hasFreeLocalScale;

        public bool IsCarried => carrier != null;
        public bool IsInstalled => isInstalled;

        /// <summary>World point on the eye/ring for winch cable attachment (local +Z).</summary>
        public Vector3 CableAnchorWorld => transform.TransformPoint(new Vector3(0f, 0f, 0.30f));

        public static MiniVanWinchHook GetCarriedBy(MiniVanPlayer player)
        {
            return player != null && carriedByPlayer.TryGetValue(player, out MiniVanWinchHook hook)
                ? hook
                : null;
        }

        private void Awake()
        {
            RememberFreeScale();
            EnsurePhysics();
            if (!isInstalled)
            {
                ConfigureFreeBody();
            }

            highlightRenderers = GetComponentsInChildren<Renderer>(true);
            CacheHighlightMaterials();
        }

        private void OnValidate()
        {
            // Keep designer Transform scale as the carried/installed size.
            if (!isInstalled && carrier == null)
            {
                RememberFreeScale();
            }
        }

        private void OnDisable()
        {
            SetHighlighted(false);
            HidePlacementGhost();
            placeHoldProgress = 0f;
        }

        private void OnDestroy()
        {
            SetHighlighted(false);
            DestroyPlacementGhost();
            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
                outlineMaterial = null;
            }

            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }

            DetachFromInstallParent();
        }

        private void LateUpdate()
        {
            if (carrier == null)
            {
                // While free (unparented), accept Transform scale edits from Inspector.
                if (!isInstalled && transform.parent == null)
                {
                    RememberFreeScale();
                }

                HidePlacementGhost();
                placeHoldProgress = 0f;
                return;
            }

            if (carrier == MiniVanPlayer.LocalPlayer &&
                !carrier.IsRidingBoard &&
                MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop))
            {
                DropNear(carrier);
                return;
            }

            ApplyCarryPose(carrier);
            if (carrier != MiniVanPlayer.LocalPlayer)
            {
                HidePlacementGhost();
                placeHoldProgress = 0f;
                return;
            }

            UpdatePlacementPreview(carrier);
            UpdatePlaceHold(carrier);
        }

        private void OnGUI()
        {
            MiniVanPlayer local = MiniVanPlayer.LocalPlayer;
            if (local == null || carrier != local)
            {
                return;
            }

            string placePrompt;
            if (!placementHasHit)
            {
                placePrompt = "Look at a static surface";
            }
            else if (!placementValid)
            {
                placePrompt = "Cannot install here";
            }
            else
            {
                placePrompt = "Hold E - install hook  |  Q - drop";
            }

            GUI.Box(new Rect(Screen.width * 0.5f - 170f, Screen.height - 118f, 340f, 34f), placePrompt);

            if (placeHoldProgress > 0.01f)
            {
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + 72f);
                DrawHoldRing(center, 78f, placeHoldProgress);
            }
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            // Installed hooks are permanent — no pickup prompt / outline.
            if (player == null || isInstalled || IsCarried || GetCarriedBy(player) != null)
            {
                return string.Empty;
            }

            if (Vector3.Distance(player.transform.position, transform.position) > PickupReach)
            {
                return string.Empty;
            }

            if (!CanPlayerPickUp(player))
            {
                return string.Empty;
            }

            return "E - take winch hook";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1) || IsCarried || isInstalled)
            {
                return;
            }

            TryPickup(player);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool TryPickup(MiniVanPlayer player)
        {
            if (player == null || isInstalled || IsCarried || !CanPlayerPickUp(player))
            {
                return false;
            }

            if (Vector3.Distance(player.transform.position, transform.position) > PickupReach)
            {
                return false;
            }

            SetHighlighted(false);
            isInstalled = false;
            carriedByPlayer[player] = this;
            carrier = player;
            DetachFromInstallParent();
            ConfigureCarriedBody();
            placeHoldProgress = 0f;
            return true;
        }

        public void DropNear(MiniVanPlayer player)
        {
            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }

            SetHighlighted(false);
            HidePlacementGhost();
            placeHoldProgress = 0f;
            isInstalled = false;
            DetachFromInstallParent();

            if (player != null)
            {
                transform.position = player.transform.position + player.transform.forward * 0.9f + Vector3.up * 0.35f;
                transform.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
            }

            ConfigureFreeBody();
        }

        public void SetHighlighted(bool highlighted)
        {
            if (isHighlighted == highlighted)
            {
                return;
            }

            if (highlightRenderers == null || highlightRenderers.Length == 0)
            {
                highlightRenderers = GetComponentsInChildren<Renderer>(true);
                CacheHighlightMaterials();
            }

            isHighlighted = highlighted;
            if (highlighted)
            {
                Material outline = GetOutlineMaterial();
                for (int i = 0; i < highlightRenderers.Length; i++)
                {
                    Renderer renderer = highlightRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    Material[] source = renderer.sharedMaterials;
                    Material[] materials = new Material[source.Length + 1];
                    for (int j = 0; j < source.Length; j++)
                    {
                        materials[j] = source[j];
                    }

                    materials[materials.Length - 1] = outline;
                    renderer.sharedMaterials = materials;
                }
            }
            else if (originalMaterials != null)
            {
                for (int i = 0; i < highlightRenderers.Length; i++)
                {
                    if (highlightRenderers[i] != null && i < originalMaterials.Length)
                    {
                        highlightRenderers[i].sharedMaterials = originalMaterials[i];
                    }
                }
            }
        }

        private void UpdatePlaceHold(MiniVanPlayer player)
        {
            bool holding = MiniVanKeyBindings.GetKey(MiniVanKeyAction.Interact);
            if (!holding || !placementHasHit || !placementValid)
            {
                placeHoldProgress = 0f;
                return;
            }

            placeHoldProgress += Time.deltaTime / PlaceHoldSeconds;
            if (placeHoldProgress < 1f)
            {
                return;
            }

            placeHoldProgress = 0f;
            InstallAtPreview(player);
        }

        private void InstallAtPreview(MiniVanPlayer player)
        {
            if (carrier != player || !placementHasHit || !placementValid)
            {
                return;
            }

            carriedByPlayer.Remove(carrier);
            carrier = null;
            HidePlacementGhost();
            placeHoldProgress = 0f;

            // Keep world-space pose/scale. Parenting under scaled statics (or their
            // ancestors) multiplies lossy scale and blows up / shears the mesh.
            // Hook is permanent once installed, so following the surface isn't needed.
            Vector3 desiredScale = hasFreeLocalScale ? freeLocalScale : Vector3.one;
            if (desiredScale.sqrMagnitude < 1e-8f)
            {
                desiredScale = Vector3.one;
            }

            DetachFromInstallParent();
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(placementPosition, placementRotation);
            transform.localScale = desiredScale;
            freeLocalScale = desiredScale;
            hasFreeLocalScale = true;

            isInstalled = true;
            ConfigureInstalledBody();
        }

        private void DetachFromInstallParent()
        {
            Transform previousParent = transform.parent;
            transform.SetParent(null, true);
            if (hasFreeLocalScale)
            {
                transform.localScale = freeLocalScale;
            }

            Transform anchorToDestroy = installAnchor;
            installAnchor = null;
            if (anchorToDestroy == null &&
                previousParent != null &&
                previousParent.name == "WinchHook_InstallAnchor")
            {
                anchorToDestroy = previousParent;
            }

            if (anchorToDestroy != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(anchorToDestroy.gameObject);
                }
                else
                {
                    DestroyImmediate(anchorToDestroy.gameObject);
                }
            }
        }

        private void UpdatePlacementPreview(MiniVanPlayer player)
        {
            placementHasHit = false;
            placementValid = false;
            placementParent = null;

            if (player == null || player.PlayerCamera == null)
            {
                HidePlacementGhost();
                return;
            }

            Ray ray = new Ray(player.PlayerCamera.transform.position, player.PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, PlaceRayDistance, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                HidePlacementGhost();
                return;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(transform) ||
                    (placementGhost != null && hit.collider.transform.IsChildOf(placementGhost.transform)) ||
                    player.ShouldIgnoreAimCollider(hit.collider))
                {
                    continue;
                }

                if (!IsStaticPlaceable(hit, player))
                {
                    // Show red ghost on first solid hit that isn't placeable? Skip to keep ghost only for candidates.
                    continue;
                }

                placementHasHit = true;
                placementValid = true;
                placementParent = hit.collider.transform;
                placementRotation = BuildScrewRotation(hit.normal, player.transform.forward);
                // Tip at origin → sit tip slightly into the surface along -normal.
                placementPosition = hit.point - hit.normal.normalized * TipEmbed;
                break;
            }

            if (!placementHasHit)
            {
                HidePlacementGhost();
                return;
            }

            EnsurePlacementGhost();
            if (placementGhost == null)
            {
                return;
            }

            placementGhost.SetActive(true);
            placementGhost.transform.SetPositionAndRotation(placementPosition, placementRotation);
            placementGhost.transform.localScale = hasFreeLocalScale ? freeLocalScale : transform.lossyScale;
            if (lastGhostValid != placementValid)
            {
                lastGhostValid = placementValid;
                ApplyGhostColor(placementValid);
            }
        }

        private static bool IsStaticPlaceable(RaycastHit hit, MiniVanPlayer player)
        {
            if (hit.collider == null || hit.collider.isTrigger)
            {
                return false;
            }

            if (hit.collider.GetComponentInParent<MiniVanPlayer>() != null ||
                hit.collider.GetComponentInParent<MiniVanWinchHook>() != null ||
                hit.collider.GetComponentInParent<MiniVanDetachedWheel>() != null ||
                hit.collider.GetComponentInParent<MiniVanWoodenBoard>() != null ||
                hit.collider.GetComponentInParent<MiniVanBatteryCharger>() != null)
            {
                return false;
            }

            Rigidbody rb = hit.rigidbody != null ? hit.rigidbody : hit.collider.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Align local +Z (tip→eye) with the surface normal so the screw sticks straight out.
        /// </summary>
        private static Quaternion BuildScrewRotation(Vector3 surfaceNormal, Vector3 preferredTwist)
        {
            Vector3 outward = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
            Vector3 twist = Vector3.ProjectOnPlane(preferredTwist, outward);
            if (twist.sqrMagnitude < 0.001f)
            {
                twist = Vector3.ProjectOnPlane(Vector3.up, outward);
            }

            if (twist.sqrMagnitude < 0.001f)
            {
                twist = Vector3.ProjectOnPlane(Vector3.right, outward);
            }

            return Quaternion.LookRotation(outward, twist.normalized);
        }

        private void ApplyCarryPose(MiniVanPlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (hasFreeLocalScale)
            {
                transform.localScale = freeLocalScale;
            }

            if (player.IsRidingBoard)
            {
                Vector3 sidePosition = player.transform.TransformPoint(RidingCarryLocalPosition);
                Quaternion sideRotation = player.transform.rotation * Quaternion.Euler(RidingCarryLocalEuler);
                transform.SetPositionAndRotation(sidePosition, sideRotation);
                return;
            }

            Transform cam = player.PlayerCamera != null
                ? player.PlayerCamera.transform
                : (player.CameraRoot != null ? player.CameraRoot : player.transform);
            Vector3 position = cam.TransformPoint(CarryLocalPosition);
            Quaternion rotation = cam.rotation * Quaternion.Euler(CarryLocalEuler);
            transform.SetPositionAndRotation(position, rotation);
        }

        private bool CanPlayerPickUp(MiniVanPlayer player)
        {
            if (player == null || player.IsDowned)
            {
                return false;
            }

            return MiniVanCarBattery.GetCarriedBy(player) == null &&
                   MiniVanBridgeBattery.GetCarriedBy(player) == null &&
                   MiniVanBridgePowerCable.HasCarriedEnd(player) == false &&
                   MiniVanBatteryCharger.GetCarriedBy(player) == null &&
                   MiniVanWoodenBoard.GetCarriedBy(player) == null &&
                   MiniVanDetachedWheel.GetCarriedBy(player) == null &&
                   GetCarriedBy(player) == null;
        }

        private void EnsurePhysics()
        {
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            colliders = GetComponentsInChildren<Collider>(true);
            bool hasCollider = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    hasCollider = true;
                    break;
                }
            }

            if (!hasCollider)
            {
                MeshCollider meshCol = gameObject.AddComponent<MeshCollider>();
                MeshFilter filter = GetComponent<MeshFilter>();
                if (filter != null)
                {
                    meshCol.sharedMesh = filter.sharedMesh;
                }

                meshCol.convex = true;
                colliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void ConfigureFreeBody()
        {
            if (body == null)
            {
                EnsurePhysics();
            }

            if (body != null)
            {
                body.mass = 4.5f;
                body.linearDamping = 0.35f;
                body.angularDamping = 0.65f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.isKinematic = false;
                body.useGravity = true;
                body.detectCollisions = true;
            }

            SetCollidersEnabled(true);
            SetCollidersTrigger(false);
        }

        private void ConfigureCarriedBody()
        {
            if (body == null)
            {
                EnsurePhysics();
            }

            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
            }

            SetCollidersEnabled(false);
        }

        private void ConfigureInstalledBody()
        {
            if (body == null)
            {
                EnsurePhysics();
            }

            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.None;
                body.position = transform.position;
                body.rotation = transform.rotation;
            }

            SetCollidersEnabled(true);
            SetCollidersTrigger(false);
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = enabled;
                }
            }
        }

        private void SetCollidersTrigger(bool isTrigger)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].isTrigger = isTrigger;
                }
            }
        }

        private void RememberFreeScale()
        {
            freeLocalScale = transform.localScale;
            if (freeLocalScale.sqrMagnitude < 1e-8f)
            {
                freeLocalScale = Vector3.one;
            }

            hasFreeLocalScale = true;
        }

        private void EnsurePlacementGhost()
        {
            if (placementGhost != null)
            {
                return;
            }

            placementGhost = new GameObject("WinchHook_PlacementGhost");
            placementGhost.hideFlags = HideFlags.HideAndDontSave;

            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
            Material ghostMat = GetGhostMaterial(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                GameObject part = new GameObject(filter.gameObject.name + "_Ghost");
                part.transform.SetParent(placementGhost.transform, false);

                Matrix4x4 relative = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                part.transform.localPosition = relative.GetColumn(3);
                part.transform.localRotation = relative.rotation;
                Vector3 lossy = filter.transform.lossyScale;
                Vector3 rootLossy = transform.lossyScale;
                part.transform.localScale = new Vector3(
                    SafeDiv(lossy.x, rootLossy.x),
                    SafeDiv(lossy.y, rootLossy.y),
                    SafeDiv(lossy.z, rootLossy.z));

                MeshFilter ghostFilter = part.AddComponent<MeshFilter>();
                ghostFilter.sharedMesh = filter.sharedMesh;
                MeshRenderer ghostRenderer = part.AddComponent<MeshRenderer>();
                ghostRenderer.sharedMaterial = ghostMat;
                ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
                ghostRenderer.receiveShadows = false;
            }

            if (placementGhost.transform.childCount == 0)
            {
                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.name = "WinchHookGhostFallback";
                cyl.transform.SetParent(placementGhost.transform, false);
                cyl.transform.localScale = new Vector3(0.08f, 0.18f, 0.08f);
                Object.Destroy(cyl.GetComponent<Collider>());
                MeshRenderer renderer = cyl.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = ghostMat;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            ghostRenderers = placementGhost.GetComponentsInChildren<Renderer>(true);
            placementGhost.SetActive(false);
        }

        private void ApplyGhostColor(bool valid)
        {
            Material material = GetGhostMaterial(valid);
            if (ghostRenderers == null)
            {
                return;
            }

            for (int i = 0; i < ghostRenderers.Length; i++)
            {
                if (ghostRenderers[i] != null)
                {
                    ghostRenderers[i].sharedMaterial = material;
                }
            }
        }

        private void HidePlacementGhost()
        {
            if (placementGhost != null)
            {
                placementGhost.SetActive(false);
            }

            placementHasHit = false;
            placementValid = false;
        }

        private void DestroyPlacementGhost()
        {
            if (placementGhost != null)
            {
                Destroy(placementGhost);
                placementGhost = null;
                ghostRenderers = null;
            }
        }

        private static Material GetGhostMaterial(bool valid)
        {
            if (valid)
            {
                if (ghostGreenMaterial == null)
                {
                    ghostGreenMaterial = CreateGhostMaterial(new Color(0.15f, 1f, 0.3f, 0.42f));
                }

                return ghostGreenMaterial;
            }

            if (ghostRedMaterial == null)
            {
                ghostRedMaterial = CreateGhostMaterial(new Color(1f, 0.2f, 0.15f, 0.42f));
            }

            return ghostRedMaterial;
        }

        private static Material CreateGhostMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color,
                renderQueue = 3000
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            return material;
        }

        private void CacheHighlightMaterials()
        {
            if (highlightRenderers == null)
            {
                return;
            }

            originalMaterials = new Material[highlightRenderers.Length][];
            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                originalMaterials[i] = highlightRenderers[i] != null
                    ? highlightRenderers[i].sharedMaterials
                    : System.Array.Empty<Material>();
            }
        }

        private Material GetOutlineMaterial()
        {
            if (outlineMaterial != null)
            {
                return outlineMaterial;
            }

            Material shared = Resources.Load<Material>("Panelka/ThinWhiteOutline");
            Shader shader = Shader.Find("MiniVanGame/ThinWhiteOutline");
            if (shared != null)
            {
                outlineMaterial = new Material(shared) { name = "WinchHook Outline" };
            }
            else
            {
                outlineMaterial = new Material(shader != null ? shader : Shader.Find("Standard"))
                {
                    name = "WinchHook Outline"
                };
            }

            MiniVanSnesOutline.ApplyOutlineSettings(outlineMaterial, Color.white);
            return outlineMaterial;
        }

        private static void DrawHoldRing(Vector2 center, float diameter, float progress)
        {
            Texture2D texture = GetHoldRingTexture(progress);
            if (texture == null)
            {
                return;
            }

            Color old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), texture);
            GUI.color = old;
        }

        private static Texture2D GetHoldRingTexture(float progress)
        {
            const int size = 96;
            int bucket = Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);
            if (holdRingTexture != null && holdRingBucket == bucket)
            {
                return holdRingTexture;
            }

            if (holdRingTexture == null)
            {
                holdRingTexture = new Texture2D(size, size, TextureFormat.ARGB32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            holdRingBucket = bucket;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color track = new Color(0f, 0f, 0f, 0.45f);
            Color fill = new Color(0.25f, 0.85f, 0.45f, 0.95f);
            float radius = size * 0.5f - 2f;
            float inner = radius - 10f;
            float angleMax = progress * Mathf.PI * 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < inner || dist > radius)
                    {
                        holdRingTexture.SetPixel(x, y, clear);
                        continue;
                    }

                    float angle = Mathf.Atan2(dx, -dy);
                    if (angle < 0f)
                    {
                        angle += Mathf.PI * 2f;
                    }

                    holdRingTexture.SetPixel(x, y, angle <= angleMax ? fill : track);
                }
            }

            holdRingTexture.Apply(false);
            return holdRingTexture;
        }

        private static float SafeDiv(float value, float divisor)
        {
            float abs = Mathf.Abs(divisor);
            return abs < 1e-4f ? value : value / divisor;
        }
    }
}
