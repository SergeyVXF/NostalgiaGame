using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.Editor
{
    public static class MiniVanPanelkaPrefabFirstMigration
    {
        private const string TemplateFolder =
            "Assets/MiniVan Game/Prefabs/Panelka/Interiors/ApartmentTemplates";
        private const string ExteriorPrefabPath =
            "Assets/MiniVan Game/Prefabs/Panelka/Interiors/ApartmentExteriorOnly.prefab";
        private const string CatalogPath =
            "Assets/MiniVan Game/Resources/Panelka/ApartmentTemplateCatalog.asset";
        private const string CeilingMaterialPath =
            "Assets/MiniVan Game/Materials/Panelka/Stage1/PanelkaStage1_Ceiling.mat";

        [MenuItem("MiniVan Game/Panelka/Migrate To Prefab-First Apartments")]
        public static void Migrate()
        {
            Material ceilingMaterial = GetOrCreateCeilingMaterial();
            string[] templatePaths = AssetDatabase
                .FindAssets("t:Prefab", new[] { TemplateFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            if (templatePaths.Length != 5)
            {
                throw new InvalidOperationException(
                    "Expected exactly five apartment templates, found " +
                    templatePaths.Length + ".");
            }

            for (int i = 0; i < templatePaths.Length; i++)
            {
                MigrateTemplate(templatePaths[i], ceilingMaterial);
            }

            GameObject exteriorPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ExteriorPrefabPath);
            if (exteriorPrefab == null)
            {
                exteriorPrefab = CreateExteriorOnlyPrefab(templatePaths[0]);
            }

            MiniVanPanelkaApartmentTemplateCatalog catalog =
                AssetDatabase.LoadAssetAtPath<MiniVanPanelkaApartmentTemplateCatalog>(
                    CatalogPath);
            if (catalog == null)
                throw new InvalidOperationException("Apartment template catalog is missing.");

            catalog.SetExteriorOnlyPrefab(exteriorPrefab);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[Panelka Prefab First] Migrated five editable apartment prefabs and " +
                "registered ApartmentExteriorOnly.");
        }

        private static void MigrateTemplate(string path, Material ceilingMaterial)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MiniVanPanelkaApartmentTemplate metadata =
                    root.GetComponent<MiniVanPanelkaApartmentTemplate>();
                if (metadata == null)
                    throw new InvalidOperationException(path + " has no template metadata.");

                RemoveDoorCollisionProxies(root);
                MigrateDoors(root);
                ClearMovingDoorStaticFlags(root);
                EnsureExplicitCeilings(root, ceilingMaterial);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void MigrateDoors(GameObject root)
        {
            MiniVanPanelkaRoomDoor[] oldDoors =
                root.GetComponentsInChildren<MiniVanPanelkaRoomDoor>(true);
            for (int i = 0; i < oldDoors.Length; i++)
            {
                MiniVanPanelkaRoomDoor oldDoor = oldDoors[i];
                Transform pivot = oldDoor.Pivot != null
                    ? oldDoor.Pivot
                    : oldDoor.transform;
                EnsureSingleDoorPanel(pivot);

                MiniVanApartmentDoor door =
                    oldDoor.GetComponent<MiniVanApartmentDoor>();
                if (door == null)
                    door = oldDoor.gameObject.AddComponent<MiniVanApartmentDoor>();
                door.Configure(
                    pivot,
                    oldDoor.ClosedEuler,
                    oldDoor.OpenEuler,
                    oldDoor.DoorAnimationSpeed);

                if (oldDoor.name == "Apartment_Entrance_Door")
                {
                    MiniVanApartmentDoorLock doorLock =
                        oldDoor.GetComponent<MiniVanApartmentDoorLock>();
                    if (doorLock == null)
                    {
                        doorLock =
                            oldDoor.gameObject.AddComponent<MiniVanApartmentDoorLock>();
                    }
                    doorLock.Configure(string.Empty, false);
                }

                UnityEngine.Object.DestroyImmediate(oldDoor, true);
            }
        }

        private static void EnsureSingleDoorPanel(Transform pivot)
        {
            Renderer[] panels = pivot
                .GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && renderer.name == "Door_Panel")
                .ToArray();
            if (panels.Length == 0)
            {
                throw new InvalidOperationException(
                    "Door " + pivot.parent.name + " has no Door_Panel.");
            }

            for (int i = 1; i < panels.Length; i++)
                UnityEngine.Object.DestroyImmediate(panels[i].gameObject);

            Collider collider = panels[0].GetComponent<Collider>();
            if (collider == null)
                collider = panels[0].gameObject.AddComponent<BoxCollider>();
            collider.enabled = true;
            collider.isTrigger = false;
        }

        private static void RemoveDoorCollisionProxies(GameObject root)
        {
            MiniVanPanelkaDoorCollisionProxy[] proxies =
                root.GetComponentsInChildren<MiniVanPanelkaDoorCollisionProxy>(true);
            for (int i = 0; i < proxies.Length; i++)
                UnityEngine.Object.DestroyImmediate(proxies[i], true);
        }

        private static void ClearMovingDoorStaticFlags(GameObject root)
        {
            MiniVanApartmentDoor[] doors =
                root.GetComponentsInChildren<MiniVanApartmentDoor>(true);
            for (int i = 0; i < doors.Length; i++)
            {
                Transform[] movingParts =
                    doors[i].GetComponentsInChildren<Transform>(true);
                for (int partIndex = 0; partIndex < movingParts.Length; partIndex++)
                {
                    GameObjectUtility.SetStaticEditorFlags(
                        movingParts[partIndex].gameObject,
                        0);
                }
            }
        }

        private static void EnsureExplicitCeilings(
            GameObject root,
            Material material)
        {
            MiniVanPanelkaRoomIdentity[] rooms =
                root.GetComponentsInChildren<MiniVanPanelkaRoomIdentity>(true);
            for (int i = 0; i < rooms.Length; i++)
            {
                MiniVanPanelkaRoomIdentity room = rooms[i];
                Transform existing = room.transform.Find("EXPLICIT_CEILING");
                if (existing != null)
                {
                    existing.gameObject.SetActive(true);
                    continue;
                }

                GameObject ceiling =
                    GameObject.CreatePrimitive(PrimitiveType.Cube);
                ceiling.name = "EXPLICIT_CEILING";
                ceiling.transform.SetParent(room.transform, false);

                Transform coordinateParent = room.transform.parent;
                Vector3 centerInParent = new Vector3(
                    room.RoomCenterLocal.x,
                    2.96f,
                    room.RoomCenterLocal.z);
                ceiling.transform.position = coordinateParent.TransformPoint(centerInParent);
                ceiling.transform.rotation = coordinateParent.rotation;
                ceiling.transform.localScale = new Vector3(
                    Mathf.Max(0.1f, room.RoomSizeLocal.x - 0.04f),
                    0.08f,
                    Mathf.Max(0.1f, room.RoomSizeLocal.z - 0.04f));

                Renderer renderer = ceiling.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = material;
            }
        }

        private static GameObject CreateExteriorOnlyPrefab(string sourcePath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                root.name = "ApartmentExteriorOnly";
                MiniVanPanelkaApartmentTemplate metadata =
                    root.GetComponent<MiniVanPanelkaApartmentTemplate>();
                Transform content = metadata != null ? metadata.ContentRoot : null;
                Transform layout =
                    content != null && content.childCount > 0 ? content.GetChild(0) : null;
                if (layout == null)
                    throw new InvalidOperationException("Template has no apartment layout.");

                for (int i = layout.childCount - 1; i >= 0; i--)
                {
                    Transform child = layout.GetChild(i);
                    bool keep =
                        child.name == "APARTMENT_LAYOUT_SHELL" ||
                        child.name == "Apartment_Entrance_Door" ||
                        child.name.StartsWith(
                            "Door_Frame_Entrance",
                            StringComparison.Ordinal);
                    if (!keep)
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                Transform shell = layout.Find("APARTMENT_LAYOUT_SHELL");
                if (shell != null)
                {
                    for (int i = shell.childCount - 1; i >= 0; i--)
                    {
                        Transform child = shell.GetChild(i);
                        bool keep =
                            child.name.StartsWith(
                                "FacadeWall_",
                                StringComparison.Ordinal) ||
                            child.GetComponentInChildren<
                                MiniVanPanelkaApartmentFacadeMarker>(true) != null;
                        if (!keep)
                            UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                }

                MiniVanApartmentDoor[] doors =
                    root.GetComponentsInChildren<MiniVanApartmentDoor>(true);
                for (int i = 0; i < doors.Length; i++)
                    UnityEngine.Object.DestroyImmediate(doors[i], true);
                MiniVanApartmentDoorLock[] locks =
                    root.GetComponentsInChildren<MiniVanApartmentDoorLock>(true);
                for (int i = 0; i < locks.Length; i++)
                    UnityEngine.Object.DestroyImmediate(locks[i], true);
                RemoveDoorCollisionProxies(root);

                return PrefabUtility.SaveAsPrefabAsset(root, ExteriorPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Material GetOrCreateCeilingMaterial()
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(CeilingMaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            material = new Material(shader)
            {
                name = "PanelkaStage1_Ceiling",
                color = new Color(0.76f, 0.77f, 0.75f, 1f)
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", material.color);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, CeilingMaterialPath);
            return material;
        }
    }
}
