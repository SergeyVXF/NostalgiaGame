using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// Base vanity: E opens mirror-cam face paint UI (brush / eraser / color / size).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanFacePaintStation : MonoBehaviour, IMiniVanGameModeInteractable
    {
        public float InteractionReach = 3.4f;
        public Transform StandPoint;
        public Transform MirrorCameraPivot;
        public Camera MirrorCamera;
        public float CameraBlendSeconds = 0.35f;
        public float RotateSpeedDegrees = 95f;
        [Tooltip("Editable Face Paint HUD prefab (Resources/FacePaintUI/FacePaintHUD).")]
        public MiniVanFacePaintUi UiPrefab;

        public bool IsSessionActive => activePlayer != null;

        private MiniVanPlayer activePlayer;
        private MiniVanFacePaintUi ui;
        private readonly List<RaycastResult> uiRaycastHits = new List<RaycastResult>(8);
        private float rotateHoldDir;

        private enum Tool
        {
            Brush,
            Eraser,
            Fill
        }

        private Tool tool = Tool.Brush;
        private Color paintColor = new Color(0.85f, 0.12f, 0.12f, 1f);
        private float brushSize = 0.22f;
        private bool painting;

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || activePlayer != null || !IsPlayerNear(player))
            {
                return string.Empty;
            }

            return "E - paint face at mirror";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || !player.IsOwner || activePlayer != null || !IsPlayerNear(player))
            {
                return;
            }

            BeginSession(player);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        private void Awake()
        {
            BuildStationVisuals();
        }

        /// <summary>Builds vanity mesh, camera, collider. Safe to call from editor.</summary>
        public void BuildStationVisuals()
        {
            EnsureVisual();
            RefreshTableMaterials();
            EnsureCamera();
            EnsureWorldMirror();
            if (GetComponent<Collider>() == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.7f, 0.15f);
                box.size = new Vector3(1.5f, 1.45f, 0.85f);
            }
        }

        private void Start()
        {
            EnsureUi();
        }

        private void Update()
        {
            if (activePlayer == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EndSession(confirm: false);
                return;
            }

            UpdatePlayerRotation();
            UpdateBrushCursor();
            HandlePaintInput();
        }

        private void BeginSession(MiniVanPlayer player)
        {
            activePlayer = player;
            EnsureCamera();
            EnsureUi();

            if (StandPoint != null)
            {
                CharacterController cc = player.CharacterController;
                if (cc != null)
                {
                    cc.enabled = false;
                }

                // CharacterController bottom must sit on StandPoint ground (prefab center is ~-0.09).
                Vector3 standPos = StandPoint.position;
                if (cc != null)
                {
                    float bottomOffset = cc.center.y - cc.height * 0.5f;
                    standPos.y -= bottomOffset;
                }

                player.transform.SetPositionAndRotation(standPos, StandPoint.rotation);
                if (cc != null)
                {
                    cc.enabled = true;
                }
            }

            player.BeginFacePaintSession(this);
            if (MirrorCamera != null)
            {
                MirrorCamera.gameObject.SetActive(true);
                MirrorCamera.enabled = true;
                AudioListener mirrorListener = MirrorCamera.GetComponent<AudioListener>();
                if (mirrorListener != null)
                {
                    mirrorListener.enabled = true;
                }
            }

            if (ui != null)
            {
                ui.SetVisible(true);
            }

            RefreshToolVisuals();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void EndSession(bool confirm)
        {
            if (activePlayer == null)
            {
                return;
            }

            MiniVanPlayer player = activePlayer;
            activePlayer = null;
            painting = false;
            rotateHoldDir = 0f;

            if (ui != null)
            {
                ui.SetVisible(false);
            }

            if (MirrorCamera != null)
            {
                AudioListener mirrorListener = MirrorCamera.GetComponent<AudioListener>();
                if (mirrorListener != null)
                {
                    mirrorListener.enabled = false;
                }

                MirrorCamera.enabled = false;
                MirrorCamera.gameObject.SetActive(false);
            }

            player.EndFacePaintSession(confirm);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void HandlePaintInput()
        {
            if (activePlayer == null || MirrorCamera == null)
            {
                return;
            }

            bool overUi = IsPointerOverToolPanel();

            if (tool == Tool.Fill)
            {
                if (Input.GetMouseButtonDown(0) && !overUi)
                {
                    Ray fillRay = MirrorCamera.ScreenPointToRay(Input.mousePosition);
                    activePlayer.TryFillFaceRay(fillRay, paintColor);
                }

                return;
            }

            if (Input.GetMouseButtonDown(0) && !overUi)
            {
                painting = true;
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (painting && activePlayer != null)
                {
                    activePlayer.EndFacePaintStroke();
                }

                painting = false;
            }

            if (!painting || !Input.GetMouseButton(0) || overUi)
            {
                return;
            }

            Ray ray = MirrorCamera.ScreenPointToRay(Input.mousePosition);
            activePlayer.TryPaintFaceRay(ray, tool == Tool.Eraser, paintColor, brushSize);
        }

        private void UpdatePlayerRotation()
        {
            if (activePlayer == null)
            {
                return;
            }

            float dir = rotateHoldDir;
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                dir -= 1f;
            }

            if (Input.GetKey(KeyCode.RightArrow))
            {
                dir += 1f;
            }

            dir = Mathf.Clamp(dir, -1f, 1f);
            if (Mathf.Abs(dir) < 0.01f)
            {
                return;
            }

            activePlayer.transform.Rotate(0f, dir * RotateSpeedDegrees * Time.unscaledDeltaTime, 0f, Space.World);
        }

        private bool IsPointerOverToolPanel()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            PointerEventData pointer = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };
            uiRaycastHits.Clear();
            eventSystem.RaycastAll(pointer, uiRaycastHits);
            return uiRaycastHits.Count > 0;
        }

        private void UpdateBrushCursor()
        {
            if (ui == null || ui.BrushCursor == null)
            {
                return;
            }

            ui.BrushCursor.position = Input.mousePosition;
            float px = tool == Tool.Fill ? 18f : Mathf.Lerp(6f, 48f, brushSize);
            ui.BrushCursor.sizeDelta = new Vector2(px, px);
        }

        private bool IsPlayerNear(MiniVanPlayer player)
        {
            Vector3 from = player.PlayerCamera != null
                ? player.PlayerCamera.transform.position
                : player.transform.position;
            return Vector3.Distance(from, transform.position) <= InteractionReach;
        }

        // ---------------------------------------------------------------- UI (prefab)

        private void EnsureUi()
        {
            if (ui != null)
            {
                return;
            }

            MiniVanFacePaintUi prefab = UiPrefab;
            if (prefab == null)
            {
                prefab = Resources.Load<MiniVanFacePaintUi>("FacePaintUI/FacePaintHUD");
            }

            if (prefab == null)
            {
                Debug.LogWarning("[FacePaint] Missing HUD prefab. Run menu MiniVan/Face Paint/Rebuild HUD Prefab.");
                return;
            }

            ui = Instantiate(prefab, transform);
            ui.name = "FacePaintHUD";
            ui.Bind(this);
            EnsureEventSystem();

            if (ui.ColorSwatches != null && ui.ColorSwatches.Length > 2 && ui.ColorSwatches[2] != null)
            {
                paintColor = ui.ColorSwatches[2].color;
            }

            if (ui.SizeSlider != null)
            {
                brushSize = ui.SizeSlider.value;
            }

            ui.SetVisible(false);
            SelectBrush();
            RefreshToolVisuals();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("FacePaint_EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        public void UiSelectBrush() => SelectBrush();
        public void UiSelectEraser() => SelectEraser();
        public void UiSelectFill() => SelectFill();

        public void UiSelectColor(Color color)
        {
            paintColor = color;
            if (tool == Tool.Eraser)
            {
                tool = Tool.Brush;
            }

            RefreshToolVisuals();
        }

        public void UiSetBrushSize(float value) => brushSize = value;
        public void UiUndo() => activePlayer?.UndoFacePaint();
        public void UiRedo() => activePlayer?.RedoFacePaint();
        public void UiClose() => EndSession(false);
        public void UiConfirm() => EndSession(true);
        public void UiSetRotateHold(float direction) => rotateHoldDir = direction;

        public void UiClearRotateHold(float direction)
        {
            if (Mathf.Approximately(rotateHoldDir, direction))
            {
                rotateHoldDir = 0f;
            }
        }

        private void SelectBrush()
        {
            tool = Tool.Brush;
            RefreshToolVisuals();
        }

        private void SelectEraser()
        {
            tool = Tool.Eraser;
            RefreshToolVisuals();
        }

        private void SelectFill()
        {
            tool = Tool.Fill;
            RefreshToolVisuals();
        }

        private void RefreshToolVisuals()
        {
            if (ui == null)
            {
                return;
            }

            ui.RefreshToolHighlights(
                tool == Tool.Brush,
                tool == Tool.Eraser,
                tool == Tool.Fill,
                paintColor,
                tool == Tool.Eraser);
        }

        // ---------------------------------------------------------------- visuals / camera

        private void EnsureCamera()
        {
            Transform pivot = transform.Find("MirrorCameraPivot");
            if (pivot == null)
            {
                GameObject pivotGo = new GameObject("MirrorCameraPivot");
                pivot = pivotGo.transform;
                pivot.SetParent(transform, false);
            }

            MirrorCameraPivot = pivot;
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);

            if (MirrorCamera == null)
            {
                Transform existingCam = pivot.Find("MirrorCamera");
                if (existingCam != null)
                {
                    MirrorCamera = existingCam.GetComponent<Camera>();
                }
            }

            if (MirrorCamera == null)
            {
                GameObject camGo = new GameObject("MirrorCamera");
                camGo.transform.SetParent(pivot, false);
                MirrorCamera = camGo.AddComponent<Camera>();
                if (camGo.GetComponent<AudioListener>() == null)
                {
                    camGo.AddComponent<AudioListener>();
                }

                camGo.SetActive(false);
            }

            // Framed in-editor: face height, not glued to the capsule.
            MirrorCamera.transform.localPosition = new Vector3(0f, 1.312f, -0.343f);
            MirrorCamera.transform.localRotation = Quaternion.Euler(0f, -0.393f, 0f);
            MirrorCamera.fieldOfView = 35f;
            MirrorCamera.nearClipPlane = 0.01f;
            MirrorCamera.farClipPlane = 25f;
            MirrorCamera.enabled = false;

            if (StandPoint == null)
            {
                Transform existingStand = transform.Find("StandPoint");
                if (existingStand != null)
                {
                    StandPoint = existingStand;
                }
                else
                {
                    GameObject stand = new GameObject("StandPoint");
                    StandPoint = stand.transform;
                    StandPoint.SetParent(transform, false);
                }
            }

            // Ground marker — player root Y is adjusted from CharacterController in BeginSession.
            // Far enough from camera (~1.2m) so the face isn't clipped.
            StandPoint.localPosition = new Vector3(0f, 0f, -1.55f);
            StandPoint.localRotation = Quaternion.identity;
        }

        private void EnsureWorldMirror()
        {
            Transform glass = transform.Find("TableVisual/MirrorGlass");
            if (glass == null)
            {
                return;
            }

            if (glass.GetComponent<MiniVanWorldMirror>() == null)
            {
                glass.gameObject.AddComponent<MiniVanWorldMirror>();
            }
        }

        private void EnsureVisual()
        {
            if (transform.Find("TableVisual") != null)
            {
                return;
            }

            Material wood = LoadFacePaintMat("FacePaint_Wood", new Color(0.22f, 0.14f, 0.08f));
            Material metal = LoadFacePaintMat("FacePaint_Metal", new Color(0.08f, 0.08f, 0.09f));
            Material glass = LoadFacePaintMat("FacePaint_MirrorBack", new Color(0.12f, 0.12f, 0.14f));

            GameObject root = new GameObject("TableVisual");
            root.transform.SetParent(transform, false);

            CreateBox(root.transform, "Top", new Vector3(0f, 0.92f, 0f), new Vector3(1.45f, 0.08f, 0.7f), wood);
            CreateBox(root.transform, "LegFL", new Vector3(-0.62f, 0.45f, -0.28f), new Vector3(0.07f, 0.9f, 0.07f), metal);
            CreateBox(root.transform, "LegFR", new Vector3(0.62f, 0.45f, -0.28f), new Vector3(0.07f, 0.9f, 0.07f), metal);
            CreateBox(root.transform, "LegBL", new Vector3(-0.62f, 0.45f, 0.28f), new Vector3(0.07f, 0.9f, 0.07f), metal);
            CreateBox(root.transform, "LegBR", new Vector3(0.62f, 0.45f, 0.28f), new Vector3(0.07f, 0.9f, 0.07f), metal);
            CreateBox(root.transform, "LowerShelf", new Vector3(0f, 0.28f, 0f), new Vector3(1.2f, 0.05f, 0.5f), wood);

            CreateBox(root.transform, "MirrorFrame", new Vector3(0f, 1.7f, 0.34f), new Vector3(0.95f, 1.15f, 0.06f), wood);
            CreateBox(root.transform, "MirrorGlass", new Vector3(0f, 1.7f, 0.3f), new Vector3(0.82f, 1.0f, 0.02f), glass);
            CreateBox(root.transform, "RodL", new Vector3(-0.28f, 1.35f, 0.38f), new Vector3(0.04f, 0.7f, 0.04f), metal);
            CreateBox(root.transform, "RodR", new Vector3(0.28f, 1.35f, 0.38f), new Vector3(0.04f, 0.7f, 0.04f), metal);

            string[] potMats =
            {
                "FacePaint_Pot_White", "FacePaint_Pot_Yellow", "FacePaint_Pot_Red",
                "FacePaint_Pot_Blue", "FacePaint_Pot_Green", "FacePaint_Pot_Purple"
            };
            Color[] potFallback =
            {
                Color.white, new Color(0.95f, 0.82f, 0.12f), new Color(0.85f, 0.12f, 0.12f),
                new Color(0.15f, 0.35f, 0.95f), new Color(0.18f, 0.72f, 0.22f), new Color(0.55f, 0.18f, 0.78f)
            };
            for (int i = 0; i < potMats.Length; i++)
            {
                float x = -0.45f + i * 0.18f;
                CreateCylinder(root.transform, "Pot" + i, new Vector3(x, 0.4f, 0.05f), new Vector3(0.09f, 0.1f, 0.09f),
                    LoadFacePaintMat(potMats[i], potFallback[i]));
            }

            CreateBox(root.transform, "Palette", new Vector3(0f, 0.98f, -0.05f), new Vector3(0.42f, 0.03f, 0.22f), wood);
            CreateBox(root.transform, "BrushA", new Vector3(0.35f, 0.99f, -0.12f), new Vector3(0.18f, 0.03f, 0.04f), metal);
            CreateBox(root.transform, "BrushB", new Vector3(0.38f, 0.99f, -0.18f), new Vector3(0.22f, 0.02f, 0.025f), metal);
            CreateCylinder(root.transform, "WaterCup", new Vector3(-0.45f, 1.02f, -0.12f), new Vector3(0.1f, 0.08f, 0.1f), metal);
            CreateBox(root.transform, "Rag", new Vector3(0.5f, 0.97f, 0.05f), new Vector3(0.18f, 0.02f, 0.14f),
                LoadFacePaintMat("FacePaint_Rag", new Color(0.45f, 0.4f, 0.32f)));

            CreateCylinder(root.transform, "LampArm", new Vector3(0.48f, 1.85f, 0.34f), new Vector3(0.03f, 0.25f, 0.03f), metal);
            CreateCylinder(root.transform, "LampShade", new Vector3(0.48f, 1.68f, 0.22f), new Vector3(0.12f, 0.08f, 0.12f), metal);
            Light light = new GameObject("LampLight").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(0.48f, 1.62f, 0.15f);
            light.type = LightType.Point;
            light.range = 3.5f;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.85f, 0.65f);
        }

        /// <summary>Reassigns project materials so prefab instances aren't stuck on pink runtime mats.</summary>
        private void RefreshTableMaterials()
        {
            Transform root = transform.Find("TableVisual");
            if (root == null)
            {
                return;
            }

            Material wood = LoadFacePaintMat("FacePaint_Wood", new Color(0.22f, 0.14f, 0.08f));
            Material metal = LoadFacePaintMat("FacePaint_Metal", new Color(0.08f, 0.08f, 0.09f));
            Material rag = LoadFacePaintMat("FacePaint_Rag", new Color(0.45f, 0.4f, 0.32f));
            Material mirrorBack = LoadFacePaintMat("FacePaint_MirrorBack", new Color(0.12f, 0.12f, 0.14f));
            string[] potMats =
            {
                "FacePaint_Pot_White", "FacePaint_Pot_Yellow", "FacePaint_Pot_Red",
                "FacePaint_Pot_Blue", "FacePaint_Pot_Green", "FacePaint_Pot_Purple"
            };
            Color[] potFallback =
            {
                Color.white, new Color(0.95f, 0.82f, 0.12f), new Color(0.85f, 0.12f, 0.12f),
                new Color(0.15f, 0.35f, 0.95f), new Color(0.18f, 0.72f, 0.22f), new Color(0.55f, 0.18f, 0.78f)
            };

            SetRendererMat(root, "Top", wood);
            SetRendererMat(root, "LowerShelf", wood);
            SetRendererMat(root, "MirrorFrame", wood);
            SetRendererMat(root, "Palette", wood);
            SetRendererMat(root, "LegFL", metal);
            SetRendererMat(root, "LegFR", metal);
            SetRendererMat(root, "LegBL", metal);
            SetRendererMat(root, "LegBR", metal);
            SetRendererMat(root, "RodL", metal);
            SetRendererMat(root, "RodR", metal);
            SetRendererMat(root, "BrushA", metal);
            SetRendererMat(root, "BrushB", metal);
            SetRendererMat(root, "WaterCup", metal);
            SetRendererMat(root, "LampArm", metal);
            SetRendererMat(root, "LampShade", metal);
            SetRendererMat(root, "Rag", rag);
            // MirrorGlass material is owned by MiniVanWorldMirror at runtime.
            if (root.Find("MirrorGlass") != null && root.Find("MirrorGlass").GetComponent<MiniVanWorldMirror>() == null)
            {
                SetRendererMat(root, "MirrorGlass", mirrorBack);
            }

            for (int i = 0; i < potMats.Length; i++)
            {
                SetRendererMat(root, "Pot" + i, LoadFacePaintMat(potMats[i], potFallback[i]));
            }
        }

        private static void SetRendererMat(Transform root, string childName, Material mat)
        {
            Transform child = root.Find(childName);
            if (child == null || mat == null)
            {
                return;
            }

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }
        }

        private static void CreateBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            RemovePrimitiveCollider(go);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void CreateCylinder(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            RemovePrimitiveCollider(go);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void RemovePrimitiveCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(col);
            }
            else
            {
                Object.DestroyImmediate(col);
            }
        }

        private static Material LoadFacePaintMat(string assetName, Color fallbackColor)
        {
#if UNITY_EDITOR
            Material asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/MiniVan Game/Materials/FacePaint/" + assetName + ".mat");
            if (asset != null)
            {
                return asset;
            }
#endif
            return MakeMat(fallbackColor);
        }

        private static Material MakeMat(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (color.a < 0.99f && material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            return material;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.7f, 0.85f, 1f, 0.5f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1.05f, new Vector3(1.6f, 2.1f, 1.1f));
            if (StandPoint != null)
            {
                Gizmos.DrawWireSphere(StandPoint.position + Vector3.up, 0.25f);
            }
        }
#endif
    }
}
