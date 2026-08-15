using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Dashboard winch button. Hold E while looking to reel in or out.
    /// Front/Back select which hook; In shortens, Out lengthens.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanWinchDashboardButton : MonoBehaviour, IMiniVanGameModeInteractable
    {
        public enum WinchButtonKind
        {
            InFront = 0,
            OutFront = 1,
            InBack = 2,
            OutBack = 3
        }

        public WinchButtonKind Kind = WinchButtonKind.InFront;
        public MiniVanVehicle Vehicle;
        public Transform Icon;
        public float PressDepth = 0.012f;
        public Vector3 PressLocalAxis = new Vector3(0f, -1f, 0f);
        public Color IconActiveColor = new Color(0.15f, 1f, 0.28f, 1f);
        public float InteractionReach = 3.6f;

        private Vector3 restLocalPosition;
        private Renderer iconRenderer;
        private Material iconMaterialInstance;
        private Color iconRestColor = Color.white;
        private Color iconRestEmission = Color.black;
        private bool hasEmission;
        private bool pressing;
        private bool holdArmed;
        private Renderer[] highlightRenderers;
        private Material[][] originalMaterials;
        private Material outlineMaterial;
        private bool isHighlighted;

        public bool IsFront => Kind == WinchButtonKind.InFront || Kind == WinchButtonKind.OutFront;
        public bool IsReelIn => Kind == WinchButtonKind.InFront || Kind == WinchButtonKind.InBack;

        private void Awake()
        {
            if (Vehicle == null)
            {
                Vehicle = GetComponentInParent<MiniVanVehicle>();
            }

            restLocalPosition = transform.localPosition;
            if (Icon == null)
            {
                Transform found = transform.Find("Icon");
                Icon = found != null ? found : null;
            }

            if (Icon != null)
            {
                iconRenderer = Icon.GetComponent<Renderer>();
                CacheIconMaterial();
            }

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false;
            }

            highlightRenderers = GetComponentsInChildren<Renderer>(true);
            CacheOriginalMaterials();
        }

        private void OnDisable()
        {
            SetHighlighted(false);
        }

        private void OnDestroy()
        {
            SetHighlighted(false);
            if (iconMaterialInstance != null)
            {
                Destroy(iconMaterialInstance);
                iconMaterialInstance = null;
            }

            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
                outlineMaterial = null;
            }
        }

        private void Update()
        {
            MiniVanPlayer player = MiniVanPlayer.LocalPlayer;
            bool holding =
                holdArmed &&
                player != null &&
                !player.IsDowned &&
                MiniVanKeyBindings.GetKey(MiniVanKeyAction.Interact) &&
                IsPlayerInReach(player) &&
                CanOperateNow();

            if (!holding)
            {
                holdArmed = false;
            }
            else
            {
                MiniVanWinchCable.TryHoldReel(Vehicle, IsFront, IsReelIn);
            }

            SetPressedVisual(holding);
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || player.IsDowned || !IsPlayerInReach(player))
            {
                return string.Empty;
            }

            return "\u200B";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || player.IsDowned || !IsPlayerInReach(player))
            {
                return;
            }

            if (!CanOperateNow())
            {
                return;
            }

            holdArmed = true;
            MiniVanWinchCable.TryHoldReel(Vehicle, IsFront, IsReelIn);
            SetPressedVisual(true);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
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
                CacheOriginalMaterials();
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

                if (pressing)
                {
                    ApplyIconColor(true);
                }
            }
        }

        private bool CanOperateNow()
        {
            return Vehicle != null && MiniVanWinchCable.CanReel(Vehicle, IsFront, IsReelIn);
        }

        private bool IsPlayerInReach(MiniVanPlayer player)
        {
            return player != null &&
                   Vector3.Distance(player.transform.position, transform.position) <= InteractionReach;
        }

        private void SetPressedVisual(bool pressed)
        {
            if (pressing == pressed)
            {
                return;
            }

            pressing = pressed;
            Vector3 axis = PressLocalAxis.sqrMagnitude > 0.0001f ? PressLocalAxis.normalized : Vector3.down;
            transform.localPosition = pressed
                ? restLocalPosition + axis * PressDepth
                : restLocalPosition;

            ApplyIconColor(pressed);
        }

        private void CacheIconMaterial()
        {
            if (iconRenderer == null)
            {
                return;
            }

            iconMaterialInstance = iconRenderer.material;
            if (iconMaterialInstance.HasProperty("_BaseColor"))
            {
                iconRestColor = iconMaterialInstance.GetColor("_BaseColor");
            }
            else if (iconMaterialInstance.HasProperty("_Color"))
            {
                iconRestColor = iconMaterialInstance.GetColor("_Color");
            }

            hasEmission = iconMaterialInstance.HasProperty("_EmissionColor");
            if (hasEmission)
            {
                iconRestEmission = iconMaterialInstance.GetColor("_EmissionColor");
            }
        }

        private void ApplyIconColor(bool active)
        {
            if (iconMaterialInstance == null)
            {
                return;
            }

            Color color = active ? IconActiveColor : iconRestColor;
            if (iconMaterialInstance.HasProperty("_BaseColor"))
            {
                iconMaterialInstance.SetColor("_BaseColor", color);
            }

            if (iconMaterialInstance.HasProperty("_Color"))
            {
                iconMaterialInstance.SetColor("_Color", color);
            }

            if (hasEmission)
            {
                if (active)
                {
                    iconMaterialInstance.EnableKeyword("_EMISSION");
                    iconMaterialInstance.SetColor("_EmissionColor", IconActiveColor * 1.4f);
                }
                else
                {
                    iconMaterialInstance.SetColor("_EmissionColor", iconRestEmission);
                }
            }
        }

        private void CacheOriginalMaterials()
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
            if (shared != null)
            {
                outlineMaterial = new Material(shared)
                {
                    name = "Winch Button Thin White Outline"
                };
                MiniVanSnesOutline.ApplyOutlineSettings(outlineMaterial, Color.white);
                return outlineMaterial;
            }

            Shader shader = Shader.Find("MiniVanGame/ThinWhiteOutline");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            outlineMaterial = new Material(shader != null ? shader : Shader.Find("Standard"))
            {
                name = "Winch Button Thin White Outline"
            };
            MiniVanSnesOutline.ApplyOutlineSettings(outlineMaterial, Color.white);
            return outlineMaterial;
        }
    }
}
