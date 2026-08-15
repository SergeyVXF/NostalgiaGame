using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanPizzaStationKind
    {
        DoughArea,
        CuttingBoard,
        Grater,
        Assembly
    }

    public class MiniVanPizzaStation : MonoBehaviour
    {
        public MiniVanPizzaStationKind Kind = MiniVanPizzaStationKind.DoughArea;
        public float InteractRadius = 2.1f;
        public float WorkSeconds = 2f;
        public Vector3 VisualLocalOffset = new Vector3(0f, 0.22f, 0f);

        public MiniVanInventoryItem ActiveItem = MiniVanInventoryItem.None;
        public bool HasFlour;
        public bool HasWater;
        public bool HasDough;
        public bool HasRoundDough;
        public bool HasTomatoPaste;
        public bool HasGratedCheese;
        public bool HasSlicedSausage;
        public bool HasSausage;
        public bool HasCheese;
        public bool HasRawPizza;

        private Transform visualRoot;

        private void Awake()
        {
            EnsureCollider();
            RefreshVisual();
        }

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= InteractRadius;
        }

        public bool TryPlaceItem(MiniVanInventoryItem item, out bool consumed, out string status)
        {
            consumed = false;
            status = null;

            if (ActiveItem != MiniVanInventoryItem.None)
            {
                status = "Use LMB to add item";
                return false;
            }

            if (!CanStartWith(item))
            {
                status = "Wrong item";
                return false;
            }

            SetActiveItem(item);
            consumed = true;
            status = GetInventoryLabel(item) + " placed";
            return true;
        }

        public bool TryUseItem(MiniVanInventoryItem item, out bool consumed, out string status)
        {
            consumed = false;
            status = null;

            if (ActiveItem == MiniVanInventoryItem.None)
            {
                return TryPlaceItem(item, out consumed, out status);
            }

            switch (Kind)
            {
                case MiniVanPizzaStationKind.DoughArea:
                case MiniVanPizzaStationKind.Assembly:
                    return TryUseDoughArea(item, out consumed, out status);
                case MiniVanPizzaStationKind.CuttingBoard:
                    return TryUseCuttingBoard(item, out consumed, out status);
                case MiniVanPizzaStationKind.Grater:
                    return TryUseGrater(item, out consumed, out status);
                default:
                    return false;
            }
        }

        public bool TryTakeItem(out MiniVanInventoryItem item, out string status)
        {
            item = ActiveItem;
            status = null;
            if (item == MiniVanInventoryItem.None)
            {
                status = "Table is empty";
                return false;
            }

            ClearTable();
            status = GetInventoryLabel(item) + " taken";
            return true;
        }

        private bool TryUseDoughArea(MiniVanInventoryItem item, out bool consumed, out string status)
        {
            consumed = false;
            status = null;

            if (ActiveItem == MiniVanInventoryItem.Flour && item == MiniVanInventoryItem.Water)
            {
                consumed = true;
                HasWater = true;
                SetActiveItem(MiniVanInventoryItem.Dough);
                status = "Dough ready";
                return true;
            }

            if (ActiveItem == MiniVanInventoryItem.Dough && item == MiniVanInventoryItem.RollingPin)
            {
                SetActiveItem(MiniVanInventoryItem.RoundDough);
                status = "Dough rolled";
                return true;
            }

            if (ActiveItem == MiniVanInventoryItem.RoundDough)
            {
                if (item == MiniVanInventoryItem.TomatoPaste && !HasTomatoPaste)
                {
                    consumed = true;
                    HasTomatoPaste = true;
                    status = "Tomato added";
                    FinishRawPizzaIfReady();
                    RefreshVisual();
                    return true;
                }

                if (item == MiniVanInventoryItem.SlicedSausage && !HasSlicedSausage)
                {
                    consumed = true;
                    HasSlicedSausage = true;
                    status = "Sausage added";
                    FinishRawPizzaIfReady();
                    RefreshVisual();
                    return true;
                }

                if (item == MiniVanInventoryItem.GratedCheese && !HasGratedCheese)
                {
                    consumed = true;
                    HasGratedCheese = true;
                    status = "Cheese added";
                    FinishRawPizzaIfReady();
                    RefreshVisual();
                    return true;
                }
            }

            status = "Wrong item";
            return false;
        }

        private bool TryUseCuttingBoard(MiniVanInventoryItem item, out bool consumed, out string status)
        {
            consumed = false;
            status = null;
            if (ActiveItem == MiniVanInventoryItem.Sausage && item == MiniVanInventoryItem.None)
            {
                SetActiveItem(MiniVanInventoryItem.SlicedSausage);
                status = "Sausage sliced";
                return true;
            }

            status = ActiveItem == MiniVanInventoryItem.Sausage ? "Use empty hand" : "Wrong item";
            return false;
        }

        private bool TryUseGrater(MiniVanInventoryItem item, out bool consumed, out string status)
        {
            consumed = false;
            status = null;
            if (ActiveItem == MiniVanInventoryItem.Cheese && item == MiniVanInventoryItem.None)
            {
                SetActiveItem(MiniVanInventoryItem.GratedCheese);
                status = "Cheese grated";
                return true;
            }

            status = ActiveItem == MiniVanInventoryItem.Cheese ? "Use empty hand" : "Wrong item";
            return false;
        }

        private bool CanStartWith(MiniVanInventoryItem item)
        {
            switch (Kind)
            {
                case MiniVanPizzaStationKind.CuttingBoard:
                    return item == MiniVanInventoryItem.Sausage;
                case MiniVanPizzaStationKind.Grater:
                    return item == MiniVanInventoryItem.Cheese;
                case MiniVanPizzaStationKind.DoughArea:
                case MiniVanPizzaStationKind.Assembly:
                    return item == MiniVanInventoryItem.Flour || item == MiniVanInventoryItem.Dough || item == MiniVanInventoryItem.RoundDough || item == MiniVanInventoryItem.RawPizza;
                default:
                    return false;
            }
        }

        private void FinishRawPizzaIfReady()
        {
            if (HasTomatoPaste && HasSlicedSausage && HasGratedCheese)
            {
                SetActiveItem(MiniVanInventoryItem.RawPizza, true);
            }
        }

        private void SetActiveItem(MiniVanInventoryItem item, bool keepToppings = false)
        {
            ActiveItem = item;
            HasFlour = item == MiniVanInventoryItem.Flour;
            HasDough = item == MiniVanInventoryItem.Dough;
            HasRoundDough = item == MiniVanInventoryItem.RoundDough;
            HasSausage = item == MiniVanInventoryItem.Sausage;
            HasCheese = item == MiniVanInventoryItem.Cheese;
            HasRawPizza = item == MiniVanInventoryItem.RawPizza;

            if (!keepToppings && item != MiniVanInventoryItem.RoundDough)
            {
                HasTomatoPaste = false;
                HasSlicedSausage = false;
                HasGratedCheese = false;
            }

            RefreshVisual();
        }

        private void ClearTable()
        {
            ActiveItem = MiniVanInventoryItem.None;
            HasFlour = false;
            HasWater = false;
            HasDough = false;
            HasRoundDough = false;
            HasTomatoPaste = false;
            HasSlicedSausage = false;
            HasGratedCheese = false;
            HasSausage = false;
            HasCheese = false;
            HasRawPizza = false;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            EnsureVisualRoot();
            for (int i = visualRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(visualRoot.GetChild(i).gameObject);
            }

            if (ActiveItem == MiniVanInventoryItem.None)
            {
                return;
            }

            if (ActiveItem == MiniVanInventoryItem.RoundDough && (HasTomatoPaste || HasSlicedSausage || HasGratedCheese))
            {
                BuildPizzaInProgressVisual();
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(GetPizzaItemResourcePath(ActiveItem));
            GameObject visual = prefab != null ? Instantiate(prefab, visualRoot, false) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Station Visual " + ActiveItem;
            if (visual.transform.parent == null)
            {
                visual.transform.SetParent(visualRoot, false);
            }

            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            DisableInteractionComponents(visual);
        }

        private void BuildPizzaInProgressVisual()
        {
            if (HasTomatoPaste && HasSlicedSausage && HasGratedCheese)
            {
                GameObject rawPizzaPrefab = Resources.Load<GameObject>(GetPizzaItemResourcePath(MiniVanInventoryItem.RawPizza));
                if (rawPizzaPrefab != null)
                {
                    GameObject rawPizza = Instantiate(rawPizzaPrefab, visualRoot, false);
                    rawPizza.name = "Station Visual Raw Pizza";
                    DisableInteractionComponents(rawPizza);
                    return;
                }
            }

            GameObject doughPrefab = Resources.Load<GameObject>(GetPizzaItemResourcePath(MiniVanInventoryItem.RoundDough));
            GameObject dough = doughPrefab != null ? Instantiate(doughPrefab, visualRoot, false) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dough.name = "Station Visual Pizza In Progress";
            DisableInteractionComponents(dough);

            if (HasTomatoPaste)
            {
                AddCylinder("Tomato Layer", new Vector3(0f, 0.075f, 0f), new Vector3(0.25f, 0.006f, 0.25f), new Color(0.82f, 0.03f, 0.02f, 1f));
            }

            if (HasSlicedSausage)
            {
                AddPrefabOverlay(MiniVanInventoryItem.SlicedSausage, "Sliced Sausage Layer", new Vector3(0f, 0.09f, 0f), Vector3.one * 0.82f);
            }

            if (HasGratedCheese)
            {
                AddPrefabOverlay(MiniVanInventoryItem.GratedCheese, "Grated Cheese Layer", new Vector3(0f, 0.115f, 0f), Vector3.one * 0.9f);
            }
        }

        private void AddPrefabOverlay(MiniVanInventoryItem item, string name, Vector3 localPosition, Vector3 localScale)
        {
            GameObject prefab = Resources.Load<GameObject>(GetPizzaItemResourcePath(item));
            if (prefab == null)
            {
                return;
            }

            GameObject overlay = Instantiate(prefab, visualRoot, false);
            overlay.name = name;
            overlay.transform.localPosition = localPosition;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = localScale;
            DisableInteractionComponents(overlay);
        }

        private void EnsureVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            Transform existing = transform.Find("Pizza Station Visual Root");
            if (existing != null)
            {
                visualRoot = existing;
            }
            else
            {
                GameObject root = new GameObject("Pizza Station Visual Root");
                visualRoot = root.transform;
                visualRoot.SetParent(transform, false);
            }

            visualRoot.localPosition = VisualLocalOffset;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = SafeInverseScale(transform.lossyScale);
        }

        private static Vector3 SafeInverseScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Abs(scale.x) > 0.0001f ? 1f / scale.x : 1f,
                Mathf.Abs(scale.y) > 0.0001f ? 1f / scale.y : 1f,
                Mathf.Abs(scale.z) > 0.0001f ? 1f / scale.z : 1f);
        }

        private static void DisableInteractionComponents(GameObject visual)
        {
            foreach (MiniVanPizzaItem item in visual.GetComponentsInChildren<MiniVanPizzaItem>(true))
            {
                item.enabled = false;
            }

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        private void AddCylinder(string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            SetupOverlay(go, name, localPosition, localScale, color);
        }

        private void AddCube(string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SetupOverlay(go, name, localPosition, localScale, color);
        }

        private void SetupOverlay(GameObject go, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            go.name = name;
            go.transform.SetParent(visualRoot, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                renderer.material = new Material(shader);
                renderer.material.color = color;
                if (renderer.material.HasProperty("_BaseColor"))
                {
                    renderer.material.SetColor("_BaseColor", color);
                }
            }
        }

        private void EnsureCollider()
        {
            Collider existing = GetComponent<Collider>();
            if (existing != null)
            {
                return;
            }

            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.1f, 0.5f, 1.1f);
        }

        private static string GetPizzaItemResourcePath(MiniVanInventoryItem item)
        {
            return "PizzaLoop/PizzaItem_" + item;
        }

        private static string GetInventoryLabel(MiniVanInventoryItem item)
        {
            switch (item)
            {
                case MiniVanInventoryItem.Flour: return "Flour";
                case MiniVanInventoryItem.Water: return "Water";
                case MiniVanInventoryItem.TomatoPaste: return "Tomato";
                case MiniVanInventoryItem.Cheese: return "Cheese";
                case MiniVanInventoryItem.Sausage: return "Sausage";
                case MiniVanInventoryItem.RollingPin: return "Rolling pin";
                case MiniVanInventoryItem.Dough: return "Dough";
                case MiniVanInventoryItem.RoundDough: return "Round dough";
                case MiniVanInventoryItem.GratedCheese: return "Grated cheese";
                case MiniVanInventoryItem.SlicedSausage: return "Sliced sausage";
                case MiniVanInventoryItem.RawPizza: return "Raw pizza";
                default: return item.ToString();
            }
        }
    }
}
