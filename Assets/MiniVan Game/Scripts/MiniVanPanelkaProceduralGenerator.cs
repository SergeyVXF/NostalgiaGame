using UnityEngine;
using UnityEngine.ProBuilder;

namespace MiniVanGame
{
    [ExecuteAlways]
    public sealed class MiniVanPanelkaProceduralGenerator : MonoBehaviour
    {
        public enum ApartmentType { TwoRoom, ThreeRoom, FourRoom }

        [Min(1)] public int Floors = 10;
        [Min(2.8f)] public float FloorHeight = 3.1f;
        [Min(1.2f)] public float StairWidth = 1.45f;
        [Min(0.9f)] public float DoorWidth = 1.1f;
        [Min(2f)] public float DoorHeight = 2.3f;
        public bool GenerateOnStart;

        private const string GeneratedRootName = "Generated_Panelka_Core";
        private Material exterior;
        private Material interior;
        private Material floor;
        private Material door;
        private Material window;
        private Material trim;

        private void Start()
        {
            if (Application.isPlaying && GenerateOnStart && transform.Find(GeneratedRootName) == null)
            {
                Rebuild();
            }
        }

        [ContextMenu("Rebuild Procedural Core")]
        public void Rebuild()
        {
            ClearGenerated();
            CreateMaterials();

            Transform root = new GameObject(GeneratedRootName).transform;
            root.SetParent(transform, false);

            BuildStairCore(root);
            for (int floorIndex = 0; floorIndex < Floors; floorIndex++)
            {
                BuildApartment(root, floorIndex, false, (ApartmentType)(floorIndex % 3));
                BuildApartment(root, floorIndex, true, (ApartmentType)((floorIndex + 1) % 3));
            }
            BuildRoof(root);
        }

        [ContextMenu("Clear Generated Core")]
        public void ClearGenerated()
        {
            Transform old = transform.Find(GeneratedRootName);
            if (old == null) return;
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        private void CreateMaterials()
        {
            exterior = MaterialOf("Panelka Exterior", new Color(0.48f, 0.53f, 0.58f));
            interior = MaterialOf("Panelka Interior", new Color(0.72f, 0.70f, 0.64f));
            floor = MaterialOf("Panelka Floor", new Color(0.25f, 0.18f, 0.12f));
            door = MaterialOf("Panelka Door", new Color(0.26f, 0.11f, 0.04f));
            window = MaterialOf("Panelka Window", new Color(0.20f, 0.50f, 0.70f));
            trim = MaterialOf("Panelka Trim", new Color(0.14f, 0.12f, 0.10f));
        }

        private static Material MaterialOf(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.02f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            return material;
        }

        private GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collision = true)
        {
            ProBuilderMesh mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            GameObject obj = mesh.gameObject;
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localRotation = Quaternion.identity;
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;

            if (collision)
            {
                MeshCollider collider = obj.GetComponent<MeshCollider>();
                if (collider == null) collider = obj.AddComponent<MeshCollider>();
                MeshFilter filter = obj.GetComponent<MeshFilter>();
                collider.sharedMesh = filter != null ? filter.sharedMesh : null;
            }
            return obj;
        }

        private Transform Group(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        // Builds one physical wall on an X plane with a door aperture. The frame is not an overlapping wall.
        private void WallXWithDoor(string name, Transform parent, float x, float zCenter, float zSpan, float yBase, float openingZ, float openingWidth, Material material)
        {
            Transform wall = Group(name, parent);
            float half = zSpan * 0.5f;
            float doorMin = openingZ - openingWidth * 0.5f;
            float doorMax = openingZ + openingWidth * 0.5f;
            float zMin = zCenter - half;
            float zMax = zCenter + half;

            if (doorMin > zMin) Box("Left", wall, new Vector3(x, yBase + FloorHeight * 0.5f, (zMin + doorMin) * 0.5f), new Vector3(0.18f, FloorHeight, doorMin - zMin), material);
            if (doorMax < zMax) Box("Right", wall, new Vector3(x, yBase + FloorHeight * 0.5f, (doorMax + zMax) * 0.5f), new Vector3(0.18f, FloorHeight, zMax - doorMax), material);
            Box("Lintel", wall, new Vector3(x, yBase + DoorHeight + (FloorHeight - DoorHeight) * 0.5f, openingZ), new Vector3(0.18f, FloorHeight - DoorHeight, openingWidth), material);
        }

        // Builds the outside facade from proper window openings, sill and lintel. No invisible holes remain.
        private void WindowFacade(string name, Transform parent, float x, float yBase, float zCenter, float zSpan, int windowCount, float side)
        {
            Transform facade = Group(name, parent);
            float zMin = zCenter - zSpan * 0.5f;
            float zMax = zCenter + zSpan * 0.5f;
            float windowWidth = 1.25f;
            float windowBottom = 0.95f;
            float windowTop = 2.50f;
            float cursor = zMin;

            for (int index = 0; index < windowCount; index++)
            {
                float ratio = (index + 1f) / (windowCount + 1f);
                float center = Mathf.Lerp(zMin + 0.9f, zMax - 0.9f, ratio);
                float left = center - windowWidth * 0.5f;
                float right = center + windowWidth * 0.5f;

                if (left > cursor)
                {
                    Box("Solid_" + index, facade, new Vector3(x, yBase + FloorHeight * 0.5f, (cursor + left) * 0.5f), new Vector3(0.18f, FloorHeight, left - cursor), exterior);
                }

                Box("Sill_" + index, facade, new Vector3(x, yBase + windowBottom * 0.5f, center), new Vector3(0.18f, windowBottom, windowWidth), exterior);
                Box("Lintel_" + index, facade, new Vector3(x, yBase + windowTop + (FloorHeight - windowTop) * 0.5f, center), new Vector3(0.18f, FloorHeight - windowTop, windowWidth), exterior);
                Box("Glass_" + index, facade, new Vector3(x - side * 0.03f, yBase + (windowBottom + windowTop) * 0.5f, center), new Vector3(0.06f, windowTop - windowBottom, windowWidth), window);
                cursor = right;
            }

            if (cursor < zMax)
            {
                Box("Solid_End", facade, new Vector3(x, yBase + FloorHeight * 0.5f, (cursor + zMax) * 0.5f), new Vector3(0.18f, FloorHeight, zMax - cursor), exterior);
            }
        }

        private void PlaceDoor(string name, Transform parent, Vector3 hingePosition, bool planeX, float openAngle, float direction)
        {
            Transform hinge = Group(name, parent);
            hinge.localPosition = hingePosition;

            MiniVanPanelkaRoomDoor interactable = hinge.gameObject.AddComponent<MiniVanPanelkaRoomDoor>();
            interactable.Type = MiniVanPanelkaInteractableType.Door;
            interactable.OpenEuler = new Vector3(0f, openAngle, 0f);
            interactable.Message = "Door";

            Vector3 panelSize = planeX ? new Vector3(0.09f, DoorHeight, DoorWidth) : new Vector3(DoorWidth, DoorHeight, 0.09f);
            Vector3 panelOffset = planeX ? new Vector3(direction * 0.06f, DoorHeight * 0.5f, 0.55f) : new Vector3(0.55f, DoorHeight * 0.5f, direction * 0.06f);
            GameObject panel = Box("Panel", hinge, panelOffset, panelSize, door);
            panel.AddComponent<BoxCollider>();
        }

        private void BuildStairCore(Transform root)
        {
            Transform core = Group("StairCore", root);
            float coreWidth = 4.8f;
            float coreDepth = 8.6f;
            float totalHeight = Floors * FloorHeight;

            // North and south walls stay solid except the ground-floor exterior entrance.
            Box("Core_NorthWall", core, new Vector3(0f, totalHeight * 0.5f, coreDepth * 0.5f), new Vector3(coreWidth, totalHeight, 0.18f), exterior);
            Box("Core_SouthWall_Upper", core, new Vector3(0f, totalHeight * 0.5f + FloorHeight * 0.5f, -coreDepth * 0.5f), new Vector3(coreWidth, totalHeight - FloorHeight, 0.18f), exterior);
            BuildSouthEntrance(core);

            for (int floorIndex = 0; floorIndex < Floors; floorIndex++)
            {
                float y = floorIndex * FloorHeight;
                WallXWithDoor("Core_WestWall_" + floorIndex, core, -coreWidth * 0.5f, 0f, coreDepth, y, 3.55f, DoorWidth, exterior);
                WallXWithDoor("Core_EastWall_" + floorIndex, core, coreWidth * 0.5f, 0f, coreDepth, y, 3.55f, DoorWidth, exterior);
                Box("Landing_" + floorIndex, core, new Vector3(0f, y + 0.1f, 3.55f), new Vector3(4.4f, 0.2f, 1.5f), floor);
                PlaceDoor("ApartmentDoor_L_" + floorIndex, core, new Vector3(-2.35f, y, 3.0f), true, -90f, -1f);
                PlaceDoor("ApartmentDoor_R_" + floorIndex, core, new Vector3(2.35f, y, 3.0f), true, 90f, 1f);
            }

            BuildStairs(core);
        }

        private void BuildSouthEntrance(Transform core)
        {
            float z = -4.3f;
            float y = 0f;
            float openingHalf = DoorWidth * 0.5f;
            float sideWidth = (4.8f - DoorWidth) * 0.5f;

            Box("EntrySouth_Left", core, new Vector3(-openingHalf - sideWidth * 0.5f, FloorHeight * 0.5f, z), new Vector3(sideWidth, FloorHeight, 0.18f), exterior);
            Box("EntrySouth_Right", core, new Vector3(openingHalf + sideWidth * 0.5f, FloorHeight * 0.5f, z), new Vector3(sideWidth, FloorHeight, 0.18f), exterior);
            Box("EntrySouth_Lintel", core, new Vector3(0f, DoorHeight + (FloorHeight - DoorHeight) * 0.5f, z), new Vector3(DoorWidth, FloorHeight - DoorHeight, 0.18f), exterior);
            Box("EntryJamb_Left", core, new Vector3(-openingHalf, DoorHeight * 0.5f, z), new Vector3(0.12f, DoorHeight, 0.12f), trim);
            Box("EntryJamb_Right", core, new Vector3(openingHalf, DoorHeight * 0.5f, z), new Vector3(0.12f, DoorHeight, 0.12f), trim);
            PlaceDoor("ExteriorEntranceDoor", core, new Vector3(-openingHalf, y, z + 0.05f), false, 90f, 1f);
        }

        private void BuildStairs(Transform core)
        {
            const int stepsPerFlight = 10;
            float rise = FloorHeight * 0.5f / stepsPerFlight;
            float run = 0.28f;

            for (int level = 0; level < Floors; level++)
            {
                float baseY = level * FloorHeight;
                for (int step = 0; step < stepsPerFlight; step++)
                {
                    float hA = (step + 1) * rise;
                    Box("StairA_" + level + "_" + step, core, new Vector3(-0.78f, baseY + hA * 0.5f, 2.10f - step * run), new Vector3(StairWidth, hA, run), floor);

                    float hB = FloorHeight * 0.5f + (step + 1) * rise;
                    Box("StairB_" + level + "_" + step, core, new Vector3(0.78f, baseY + hB * 0.5f, -0.98f + step * run), new Vector3(StairWidth, hB, run), floor);
                }
                Box("MidLanding_" + level, core, new Vector3(0f, baseY + FloorHeight * 0.5f, -1.12f), new Vector3(4.35f, 0.2f, 1.45f), floor);
                // The final step meets this connector, which meets the next floor landing: no air gap.
                Box("TopConnector_" + level, core, new Vector3(0f, baseY + FloorHeight - 0.1f, 2.16f), new Vector3(4.35f, 0.2f, 1.42f), floor);
            }
            Box("RoofLanding", core, new Vector3(0f, Floors * FloorHeight - 0.1f, 3.55f), new Vector3(4.35f, 0.2f, 1.5f), floor);
        }

        private void BuildApartment(Transform root, int floorIndex, bool rightSide, ApartmentType type)
        {
            Transform apartment = Group("Apartment_" + floorIndex + "_" + (rightSide ? "R_" : "L_") + type, root);
            float side = rightSide ? 1f : -1f;
            float y = floorIndex * FloorHeight;
            float innerX = side * 2.4f;
            float hallX = side * 3.75f;
            float partitionX = side * 4.95f;
            float outerX = side * 11.9f;
            float centerX = side * 7.15f;
            float depth = 8.4f;
            int roomCount = type == ApartmentType.TwoRoom ? 2 : type == ApartmentType.ThreeRoom ? 3 : 4;

            Box("Floor", apartment, new Vector3(centerX, y - 0.1f, 0f), new Vector3(9.5f, 0.2f, depth), floor);
            Box("Ceiling", apartment, new Vector3(centerX, y + FloorHeight - 0.1f, 0f), new Vector3(9.5f, 0.2f, depth), interior);
            WindowFacade("Facade", apartment, outerX, y, 0f, depth, roomCount, side);
            Box("NorthWall", apartment, new Vector3(centerX, y + FloorHeight * 0.5f, depth * 0.5f), new Vector3(9.5f, FloorHeight, 0.18f), exterior);
            Box("SouthWall", apartment, new Vector3(centerX, y + FloorHeight * 0.5f, -depth * 0.5f), new Vector3(9.5f, FloorHeight, 0.18f), exterior);

                        // Hall is a continuous strip from the core door. Each room gets one doorway from it.
            Box("HallFloor", apartment, new Vector3(hallX, y + 0.02f, 0f), new Vector3(2.5f, 0.05f, depth - 0.3f), trim, false);
            float roomSpan = depth / roomCount;
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                float zCenter = -depth * 0.5f + (roomIndex + 0.5f) * roomSpan;

                // Adjacent sections exactly cover the partition: no gaps between rooms and hall.
                WallXWithDoor("HallRoomWall_" + roomIndex, apartment, partitionX, zCenter, roomSpan, y, zCenter, DoorWidth, interior);
                PlaceDoor("RoomDoor_" + roomIndex, apartment, new Vector3(partitionX - side * 0.05f, y, zCenter - 0.55f), true, rightSide ? 90f : -90f, side);

                if (roomIndex > 0)
                {
                    float separatorZ = -depth * 0.5f + roomIndex * roomSpan;
                    Box("RoomSeparator_" + roomIndex, apartment, new Vector3((partitionX + outerX) * 0.5f, y + FloorHeight * 0.5f, separatorZ), new Vector3(Mathf.Abs(outerX - partitionX), FloorHeight, 0.18f), interior);
                }
            }

            CreateFurniture(apartment, y, side, centerX, type);
        }

        private void CreateFurniture(Transform apartment, float y, float side, float centerX, ApartmentType type)
        {
            Box("KitchenCounter", apartment, new Vector3(centerX + side * 2.3f, y + 0.5f, -3.2f), new Vector3(2.8f, 0.9f, 0.65f), trim, false);
            Box("Sofa", apartment, new Vector3(centerX - side * 1.5f, y + 0.45f, 2.7f), new Vector3(2.1f, 0.55f, 0.8f), door, false);
            Box("Table", apartment, new Vector3(centerX + side * 1.1f, y + 0.72f, 1.1f), new Vector3(1.2f, 0.12f, 0.85f), trim, false);
            Box("Wardrobe", apartment, new Vector3(centerX - side * 2.8f, y + 1.2f, 3.3f), new Vector3(0.65f, 2.4f, 1.1f), door, false);
            Box("Bathroom", apartment, new Vector3(centerX - side * 2.6f, y + 0.45f, -2.0f), new Vector3(1.2f, 0.8f, 1.1f), interior, false);

            if (type != ApartmentType.TwoRoom)
            {
                Box("Bed", apartment, new Vector3(centerX + side * 1.2f, y + 0.35f, -0.3f), new Vector3(1.8f, 0.45f, 0.9f), door, false);
            }
            if (type == ApartmentType.FourRoom)
            {
                Box("Desk", apartment, new Vector3(centerX - side * 1.1f, y + 0.55f, -1.5f), new Vector3(1.1f, 0.75f, 0.55f), trim, false);
            }
        }

                private void BuildRoof(Transform root)
        {
            float top = Floors * FloorHeight;
            float hatchMinX = -0.85f;
            float hatchMaxX = 0.85f;
            float hatchMinZ = 2.65f;
            float hatchMaxZ = 4.05f;

            // Four roof slabs leave exactly one 1.7 x 1.4 m hatch above the final landing.
            Box("Roof_West", root, new Vector3((-12f + hatchMinX) * 0.5f, top + 0.1f, 0f), new Vector3(hatchMinX + 12f, 0.2f, 8.8f), exterior);
            Box("Roof_East", root, new Vector3((hatchMaxX + 12f) * 0.5f, top + 0.1f, 0f), new Vector3(12f - hatchMaxX, 0.2f, 8.8f), exterior);
            Box("Roof_SouthOfHatch", root, new Vector3(0f, top + 0.1f, (-4.4f + hatchMinZ) * 0.5f), new Vector3(hatchMaxX - hatchMinX, 0.2f, hatchMinZ + 4.4f), exterior);
            Box("Roof_NorthOfHatch", root, new Vector3(0f, top + 0.1f, (hatchMaxZ + 4.4f) * 0.5f), new Vector3(hatchMaxX - hatchMinX, 0.2f, 4.4f - hatchMaxZ), exterior);

            Transform hatch = Group("RoofHatch", root);
            hatch.localPosition = new Vector3(hatchMinX, top + 0.2f, hatchMinZ);
            MiniVanPanelkaRoomDoor hatchDoor = hatch.gameObject.AddComponent<MiniVanPanelkaRoomDoor>();
            hatchDoor.Type = MiniVanPanelkaInteractableType.RoofHatch;
            hatchDoor.OpenEuler = new Vector3(-88f, 0f, 0f);
            hatchDoor.Message = "Roof hatch";
            GameObject panel = Box("HatchPanel", hatch, new Vector3((hatchMaxX - hatchMinX) * 0.5f, 0.07f, (hatchMaxZ - hatchMinZ) * 0.5f), new Vector3(hatchMaxX - hatchMinX, 0.12f, hatchMaxZ - hatchMinZ), door);
            panel.AddComponent<BoxCollider>();
        }
    }
}