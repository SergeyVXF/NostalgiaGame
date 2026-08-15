using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    public static class MiniVanPanelkaLandingFurnishing
    {
        /// <summary>
        /// Places apartment numbers on every Apartment_Entrance_Door DoorNumber plate
        /// under <paramref name="root"/> (e.g. Generated_Manual_Panelka).
        /// </summary>
        public static int ApplyDoorNumbersUnder(Transform root)
        {
            if (root == null)
                return 0;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            Material plaqueMaterial = FindExistingPlaqueMaterial(all);
            int applied = 0;
            for (int i = 0; i < all.Length; i++)
            {
                Transform entrance = all[i];
                if (entrance == null || entrance.name != "Apartment_Entrance_Door")
                    continue;

                Renderer panelRenderer = FindEntranceDoorPanel(entrance);
                Transform panel = panelRenderer != null ? panelRenderer.transform : entrance;
                int apartmentNumber = GetApartmentNumber(entrance, applied + 1);
                ApplyDoorNumberPlate(entrance, panel, apartmentNumber, plaqueMaterial);
                applied++;
            }

            RemoveLegacyFloatingApartmentNumbers(root);
            return applied;
        }

        public static void Build(
            Transform structure,
            int floorIndex,
            float yBase,
            float playerRadius,
            Material wood,
            Material metal,
            Material paper,
            Material darkPlastic,
            bool decorateLanding,
            bool furnishNotices = true,
            bool furnishLamps = true)
        {
            if (structure == null)
                return;

            _ = playerRadius;
            Transform root = Group(
                "Landing_Furnishing_Floor" + (floorIndex + 1).ToString("00"),
                structure);
            List<Transform> entrances = FindEntrances(structure);
            Color textColor = darkPlastic != null
                ? darkPlastic.color
                : new Color(0.08f, 0.06f, 0.05f, 1f);
            for (int i = 0; i < entrances.Count; i++)
            {
                int apartmentNumber = GetApartmentNumber(
                    entrances[i],
                    floorIndex * 4 + i + 1);
                BuildEntranceLabel(
                    root, structure, entrances[i], apartmentNumber,
                    yBase, metal, darkPlastic, textColor);
            }

            RemoveLegacyFloatingApartmentNumbers(root);

            if (decorateLanding)
            {
                if (furnishNotices)
                {
                    BuildNoticeBoard(root, new Vector3(-3.91f, yBase + 1.55f,
                        FindSafeWallZ(structure, entrances, -1f, 0.15f, 0.78f)),
                        wood, paper, textColor);
                    BuildWallText(
                        root, "Lift_Out_Of_Order_Sign", "LIFT\nOUT OF ORDER",
                        new Vector3(1.75f, yBase + 1.48f, -3.31f),
                        Quaternion.LookRotation(Vector3.forward, Vector3.up), paper, textColor);
                    BuildWallText(
                        root, "Landing_Wall_Text_NoSmoking", "NO SMOKING",
                        new Vector3(3.91f, yBase + 1.32f,
                            FindSafeWallZ(structure, entrances, 1f, -1.35f, 0.65f)),
                        Quaternion.LookRotation(Vector3.left, Vector3.up), null, textColor);
                    BuildWallText(
                        root, "Landing_Graffiti", "FLOOR " + (floorIndex + 1),
                        new Vector3(3.91f, yBase + 1.20f,
                            FindSafeWallZ(structure, entrances, 1f, 1.65f, 0.65f)),
                        Quaternion.LookRotation(Vector3.left, Vector3.up), null,
                        wood != null ? wood.color : textColor);
                    BuildWallDetails(root, floorIndex, yBase, metal, paper, darkPlastic);
                }

                if (furnishLamps)
                {
                    float lampY = yBase + (floorIndex == 8 ? 3.15f : 2.95f);
                    BuildLamp(root, new Vector3(-3.10f, lampY, -2.65f), metal, paper);
                    BuildLamp(root, new Vector3(3.10f, lampY, 6.55f), metal, paper);
                }
            }

            // Prefab-instantiated notice papers may skip AddText; keep all landing
            // TextMesh on the URP depth-tested material so numbers don't x-ray walls.
            EnsureLandingTextDepth(root);
        }

        private static void EnsureLandingTextDepth(Transform root)
        {
            if (root == null)
                return;

            MiniVanPanelkaWorldTextDepth depth =
                root.GetComponent<MiniVanPanelkaWorldTextDepth>();
            if (depth == null)
                depth = root.gameObject.AddComponent<MiniVanPanelkaWorldTextDepth>();
            depth.ApplyNow();
        }

        private static List<Transform> FindEntrances(Transform structure)
        {
            List<Transform> entrances = new List<Transform>();
            Transform[] all = structure.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == "Apartment_Entrance_Door")
                    entrances.Add(all[i]);

            entrances.Sort((a, b) =>
            {
                int zCompare = b.position.z.CompareTo(a.position.z);
                return zCompare != 0 ? zCompare : a.position.x.CompareTo(b.position.x);
            });
            return entrances;
        }

        private static int GetApartmentNumber(Transform entrance, int fallback)
        {
            Transform cursor = entrance;
            while (cursor != null)
            {
                MiniVanPanelkaApartmentRouteMarker marker =
                    cursor.GetComponent<MiniVanPanelkaApartmentRouteMarker>();
                if (marker != null && marker.ApartmentNumber > 0)
                    return marker.ApartmentNumber;
                cursor = cursor.parent;
            }

            return fallback;
        }

        private static void BuildEntranceLabel(
            Transform root,
            Transform structure,
            Transform entrance,
            int apartmentNumber,
            float yBase,
            Material metal,
            Material darkPlastic,
            Color textColor)
        {
            Vector3 doorLocal = structure.InverseTransformPoint(entrance.position);
            Vector3 facing = doorLocal.x < 0f ? Vector3.right : Vector3.left;
            Vector3 alongWall = Vector3.Cross(Vector3.up, -facing).normalized;
            Renderer panelRenderer = FindEntranceDoorPanel(entrance);
            Transform panel = panelRenderer != null ? panelRenderer.transform : entrance;

            ApplyDoorNumberPlate(
                entrance,
                panel,
                apartmentNumber,
                darkPlastic != null ? darkPlastic : metal);

            Vector3 panelLocal = structure.InverseTransformPoint(panel.position);
            Transform bell = MiniVanPanelkaPrefabLibrary.InstantiateOrBuild(
                "HouseProps", "Apartment_Doorbell_V2", root,
                model =>
                {
                    Box("Housing", model, Vector3.zero,
                        new Vector3(0.12f, 0.18f, 0.05f), darkPlastic);
                    Box("Button", model, new Vector3(0f, 0f, 0.035f),
                        new Vector3(0.045f, 0.045f, 0.018f), metal);
                });
            bell.name = "Doorbell_" + apartmentNumber;
            bell.localPosition = new Vector3(panelLocal.x, yBase + 1.72f, panelLocal.z) +
                                 alongWall * 0.72f + facing * 0.06f;
            bell.localRotation = Quaternion.LookRotation(facing, Vector3.up);
        }

        private static void RemoveLegacyFloatingApartmentNumbers(Transform root)
        {
            RemoveOrphanDoorNumbers(root);
        }

        /// <summary>
        /// Deletes number plates and labels that are not attached to a door: the pre-plate
        /// landing labels and plates that lost their leaf and now hang in mid-air.
        /// </summary>
        public static int RemoveOrphanDoorNumbers(Transform root)
        {
            if (root == null)
                return 0;

            int removed = 0;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = all.Length - 1; i >= 0; i--)
            {
                Transform candidate = all[i];
                if (candidate == null)
                    continue;

                // Old landing labels lived under Landing_Furnishing_* as Apartment_Number_N,
                // not on the door DoorNumber plate.
                bool legacyRoot = candidate.name.StartsWith(
                    "Apartment_Number_", System.StringComparison.Ordinal);
                bool orphanLabel = candidate.name == "ApartmentNumber" &&
                                   FindDoorNumberAncestor(candidate) == null;
                bool orphanPlate = candidate.name == "DoorNumber" &&
                                   FindEntranceAncestor(candidate) == null;
                if (!legacyRoot && !orphanLabel && !orphanPlate)
                    continue;
                if (!orphanPlate && FindDoorNumberAncestor(candidate) != null)
                    continue;

                RetireObject(candidate.gameObject);
                removed++;
            }

            return removed;
        }

        private static Transform FindEntranceAncestor(Transform transform)
        {
            Transform cursor = transform;
            while (cursor != null)
            {
                if (cursor.name == "Apartment_Entrance_Door")
                    return cursor;
                cursor = cursor.parent;
            }

            return null;
        }

        private static Transform FindDoorNumberAncestor(Transform transform)
        {
            Transform cursor = transform;
            while (cursor != null)
            {
                if (cursor.name == "DoorNumber")
                    return cursor;
                cursor = cursor.parent;
            }

            return null;
        }

        private static void ApplyDoorNumberPlate(
            Transform entrance,
            Transform panel,
            int apartmentNumber,
            Material plaqueMaterial)
        {
            if (panel == null)
                return;

            Transform doorNumber = ResolveDoorNumberPlate(entrance, panel, plaqueMaterial);
            if (doorNumber == null)
                return;

            RetireExtraDoorNumbers(entrance, doorNumber);

            // Door_Panel is a unit cube. Put the plate in the middle of the top panel
            // (upper third), on whichever face looks into the stairwell.
            bool faceNegativeX = IsHallwayOnNegativeX(entrance, panel);
            doorNumber.localPosition =
                new Vector3(faceNegativeX ? -0.51f : 0.51f, 0.294f, 0f);
            doorNumber.localRotation = Quaternion.identity;
            doorNumber.localScale = new Vector3(0.08f, 0.07f, 0.22f);

            Collider plaqueCollider = doorNumber.GetComponent<Collider>();
            if (plaqueCollider != null)
                Object.DestroyImmediate(plaqueCollider);

            // White digits on the dark plaque (matches stairwell door reference).
            ConfigureDoorNumberText(
                doorNumber,
                apartmentNumber.ToString(),
                new Color(0.95f, 0.95f, 0.95f, 1f),
                faceNegativeX);
        }

        /// <summary>
        /// A door may carry plates from older builds on its frame or runtime pivot. Only the
        /// one on the leaf may stay; the rest float next to the door.
        /// </summary>
        private static void RetireExtraDoorNumbers(Transform entrance, Transform keep)
        {
            if (entrance == null || keep == null)
                return;

            Transform[] all = entrance.GetComponentsInChildren<Transform>(true);
            for (int i = all.Length - 1; i >= 0; i--)
            {
                Transform candidate = all[i];
                if (candidate == null || candidate == keep || candidate.name != "DoorNumber")
                    continue;
                if (keep.IsChildOf(candidate))
                    continue;

                RetireObject(candidate.gameObject);
            }
        }

        private static void RetireObject(GameObject target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            // Prefab instance parts cannot be destroyed, but hiding them is enough.
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(target))
            {
                target.SetActive(false);
                return;
            }
#endif

            Object.DestroyImmediate(target);
        }

        /// <summary>
        /// The plate has to hang on the door leaf so the number swings with the door.
        /// </summary>
        private static Transform ResolveDoorNumberPlate(
            Transform entrance,
            Transform panel,
            Material plaqueMaterial)
        {
            Transform onLeaf = panel.Find("DoorNumber");
            if (onLeaf != null)
                return onLeaf;

            Transform stray = FindDoorNumber(entrance);
            if (stray != null)
            {
                if (TrySetParent(stray, panel))
                    return stray;

                // Prefab instance parts cannot be reparented, so clone the plaque onto
                // the leaf and retire the original.
                GameObject clone = Object.Instantiate(stray.gameObject, panel);
                clone.name = "DoorNumber";
                clone.SetActive(true);
                stray.gameObject.SetActive(false);
                return clone.transform;
            }

            GameObject created = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.name = "DoorNumber";
            Object.DestroyImmediate(created.GetComponent<Collider>());
            if (plaqueMaterial != null)
            {
                Renderer plaque = created.GetComponent<Renderer>();
                if (plaque != null)
                    plaque.sharedMaterial = plaqueMaterial;
            }

            if (!TrySetParent(created.transform, panel))
            {
                Object.DestroyImmediate(created);
                return null;
            }

            return created.transform;
        }

        private static bool TrySetParent(Transform child, Transform parent)
        {
            if (child == null || parent == null)
                return false;
            if (child.parent == parent)
                return true;

#if UNITY_EDITOR
            // Unity refuses to move an object that already belongs to a prefab instance,
            // but adding a brand new child under one is allowed.
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(child))
                return false;
#endif

            child.SetParent(parent, false);
            return child.parent == parent;
        }

        private static Vector3 AbsVec(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static bool IsHallwayOnNegativeX(Transform entrance, Transform panel)
        {
            Transform structure = entrance != null ? entrance.parent : null;
            while (structure != null && structure.parent != null &&
                   !structure.name.StartsWith("Floor_", System.StringComparison.Ordinal) &&
                   !structure.name.StartsWith("Structure", System.StringComparison.Ordinal))
            {
                structure = structure.parent;
            }

            if (structure == null)
                structure = entrance != null ? entrance.root : panel;

            Vector3 doorLocal = structure.InverseTransformPoint(
                entrance != null ? entrance.position : panel.position);
            Vector3 hallwayDir = structure.TransformDirection(
                doorLocal.x < 0f ? Vector3.right : Vector3.left).normalized;
            return Vector3.Dot(-panel.right, hallwayDir) >= Vector3.Dot(panel.right, hallwayDir);
        }

        private static Material FindExistingPlaqueMaterial(Transform[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == null || candidates[i].name != "DoorNumber")
                    continue;

                Renderer renderer = candidates[i].GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                    return renderer.sharedMaterial;
            }

            return null;
        }

        private static Transform FindDoorNumber(Transform entrance)
        {
            if (entrance == null)
                return null;

            Transform[] children = entrance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == "DoorNumber")
                    return children[i];
            }

            return null;
        }

        private static void ConfigureDoorNumberText(
            Transform doorNumber,
            string apartmentNumber,
            Color textColor,
            bool faceNegativeX)
        {
            const int FontSize = 20;

            Transform label = doorNumber.Find("ApartmentNumber");
            TextMesh mesh;
            if (label == null)
            {
                mesh = AddText(
                    doorNumber,
                    "ApartmentNumber",
                    apartmentNumber,
                    Vector3.zero,
                    0.05f,
                    textColor);
                label = mesh.transform;
            }
            else
            {
                mesh = label.GetComponent<TextMesh>();
                if (mesh == null)
                    mesh = label.gameObject.AddComponent<TextMesh>();
                mesh.text = apartmentNumber;
                mesh.color = textColor;
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null)
                    mesh.font = font;
            }

            Vector3 plaqueSize = AbsVec(doorNumber.lossyScale);
            int digits = Mathf.Max(1, apartmentNumber.Length);
            float characterSize = Mathf.Min(
                plaqueSize.y * 0.52f,
                plaqueSize.z * 0.78f / digits);
            characterSize = Mathf.Clamp(characterSize, 0.02f, 0.09f);

            mesh.fontSize = FontSize;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.richText = false;
            mesh.color = textColor;

            // Mirrored apartment leaves give Door_Panel a negative scale, which would
            // reflect the glyphs. Drive the label from the world basis it has to end up
            // with instead of trying to guess the sign of every local axis.
            Vector3 outwardLocal = faceNegativeX ? Vector3.left : Vector3.right;
            Matrix4x4 plaqueToWorld = doorNumber.localToWorldMatrix;
            Vector3 outward = plaqueToWorld.MultiplyVector(outwardLocal);
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.000001f)
                return;

            outward.Normalize();
            // A TextMesh reads correctly from the side its forward points away from, so the
            // glyphs look back into the leaf while sitting on the stairwell face (unit cube ±0.5).
            Vector3 forward = -outward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 position = plaqueToWorld.MultiplyPoint3x4(outwardLocal * 0.52f);
            SetWorldBasis(label, position, right, Vector3.up, forward);

            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            Material depthMaterial = Resources.Load<Material>("Panelka_WorldTextDepth");
            if (renderer != null && depthMaterial != null)
                renderer.sharedMaterial = depthMaterial;
            if (label.GetComponent<MiniVanPanelkaWorldTextDepth>() == null)
                label.gameObject.AddComponent<MiniVanPanelkaWorldTextDepth>();
        }

        /// <summary>
        /// Forces the world orientation and unit world scale of <paramref name="target"/>,
        /// cancelling any mirroring the parent chain applies.
        /// </summary>
        private static void SetWorldBasis(
            Transform target,
            Vector3 worldPosition,
            Vector3 right,
            Vector3 up,
            Vector3 forward)
        {
            Matrix4x4 parentToWorld = target.parent != null
                ? target.parent.localToWorldMatrix
                : Matrix4x4.identity;
            Matrix4x4 desired = Matrix4x4.identity;
            desired.SetColumn(0, right);
            desired.SetColumn(1, up);
            desired.SetColumn(2, forward);
            desired.SetColumn(3, new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, 1f));

            Matrix4x4 local = parentToWorld.inverse * desired;
            Vector3 columnX = local.GetColumn(0);
            Vector3 columnY = local.GetColumn(1);
            Vector3 columnZ = local.GetColumn(2);
            float scaleX = columnX.magnitude;
            float scaleY = columnY.magnitude;
            float scaleZ = columnZ.magnitude;
            if (scaleX < 0.0001f || scaleY < 0.0001f || scaleZ < 0.0001f)
                return;

            columnX /= scaleX;
            columnY /= scaleY;
            columnZ /= scaleZ;
            // A mirroring parent leaves a left-handed basis, which no rotation can express.
            // Flip the width so the remaining part is a proper rotation.
            if (Vector3.Dot(Vector3.Cross(columnY, columnZ), columnX) < 0f)
                scaleX = -scaleX;

            target.localPosition = local.GetColumn(3);
            target.localRotation = Quaternion.LookRotation(columnZ, columnY);
            target.localScale = new Vector3(scaleX, scaleY, scaleZ);
        }

        private static Renderer FindEntranceDoorPanel(Transform entrance)
        {
            if (entrance == null)
                return null;

            MiniVanApartmentDoor door = entrance.GetComponent<MiniVanApartmentDoor>();
            if (door != null && door.Pivot != null)
            {
                Renderer[] runtimePanels =
                    door.Pivot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < runtimePanels.Length; i++)
                {
                    Renderer renderer = runtimePanels[i];
                    if (renderer != null && renderer.name == "Door_Panel")
                        return renderer;
                }
            }

            Renderer[] panels = entrance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < panels.Length; i++)
            {
                Renderer renderer = panels[i];
                if (renderer != null && renderer.name == "Door_Panel")
                    return renderer;
            }

            return null;
        }

        private static void BuildNoticeBoard(
            Transform parent, Vector3 position, Material wood, Material paper, Color textColor)
        {
            Transform notice = MiniVanPanelkaPrefabLibrary.InstantiateOrBuild(
                "HouseProps", "Landing_Notice_Board_Text_V2", parent,
                model =>
                {
                    Box("Wood_Frame", model, Vector3.zero,
                        new Vector3(1.56f, 1.16f, 0.06f), wood);
                    Box("Paper", model, new Vector3(0f, 0f, 0.042f),
                        new Vector3(1.28f, 0.92f, 0.018f), paper);
                    AddText(model, "Text", "NOTICE\nLIFT OUT OF ORDER\nMEETING 19:00",
                        new Vector3(0f, 0f, 0.055f), 0.018f, textColor);
                });
            notice.name = "Landing_NoticeBoard";
            notice.localPosition = position;
            notice.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        }

        private static void BuildWallText(
            Transform parent,
            string name,
            string text,
            Vector3 position,
            Quaternion rotation,
            Material backing,
            Color color)
        {
            Transform item = Group(name, parent);
            if (backing != null)
                Box("Backing", item, Vector3.zero, new Vector3(1.18f, 0.48f, 0.025f), backing);
            AddText(item, "Text", text, new Vector3(0f, 0f, 0.019f), 0.022f, color);
            item.localPosition = position;
            item.localRotation = rotation;
        }

        private static void BuildLamp(
            Transform parent, Vector3 position, Material metal, Material paper)
        {
            Transform lamp = MiniVanPanelkaPrefabLibrary.InstantiateOrBuild(
                "HouseProps", "Landing_Lamp", parent,
                model =>
                {
                    Box("Metal_Base", model, Vector3.zero,
                        new Vector3(0.34f, 0.10f, 0.34f), metal);
                    Box("Glass_Cover", model, new Vector3(0f, -0.08f, 0f),
                        new Vector3(0.50f, 0.12f, 0.50f), paper);
                });
            lamp.name = "Landing_Lamp";
            lamp.localPosition = position;
        }

        private static void BuildWallDetails(
            Transform parent, int floorIndex, float yBase,
            Material metal, Material paper, Material darkPlastic)
        {
            Color textColor = darkPlastic != null
                ? darkPlastic.color
                : new Color(0.08f, 0.06f, 0.05f, 1f);
            Transform floorPlate = Group("Floor_Number_Plate", parent);
            Box("Plate", floorPlate, Vector3.zero,
                new Vector3(0.52f, 0.42f, 0.025f), paper);
            AddText(floorPlate, "Text", (floorIndex + 1).ToString(),
                new Vector3(0f, 0f, 0.019f), 0.055f, textColor);
            floorPlate.localPosition = new Vector3(-2.85f, yBase + 1.72f, 8.88f);
            floorPlate.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

            Transform electrical = Group("Electrical_Panel", parent);
            Box("Cabinet", electrical, Vector3.zero,
                new Vector3(0.68f, 0.86f, 0.12f), metal);
            Box("Handle", electrical, new Vector3(0.24f, 0f, 0.075f),
                new Vector3(0.05f, 0.18f, 0.025f), darkPlastic);
            electrical.localPosition = new Vector3(2.85f, yBase + 1.55f, 8.82f);
            electrical.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

            Transform pipes = Group("Utility_Pipes", parent);
            Box("Pipe_A", pipes, new Vector3(-0.08f, 0f, 0f),
                new Vector3(0.06f, 2.55f, 0.06f), metal);
            Box("Pipe_B", pipes, new Vector3(0.08f, 0f, 0f),
                new Vector3(0.06f, 2.55f, 0.06f), metal);
            pipes.localPosition = new Vector3(3.82f, yBase + 1.38f, 7.80f);
        }

        private static float FindSafeWallZ(
            Transform structure,
            IList<Transform> entrances,
            float wallXSign,
            float preferredZ,
            float halfWidth)
        {
            const float clearance = 0.18f;
            const float minZ = -7.5f;
            const float maxZ = 7.5f;
            float minimum = minZ + halfWidth + clearance;
            float maximum = maxZ - halfWidth - clearance;
            float start = Mathf.Clamp(preferredZ, minimum, maximum);
            for (int step = 0; step <= 300; step++)
            {
                float offset = step * 0.05f;
                float positive = Mathf.Clamp(start + offset, minimum, maximum);
                if (IsWallSegmentFree(
                    structure, entrances, wallXSign, positive, halfWidth, clearance))
                    return positive;
                if (step == 0)
                    continue;
                float negative = Mathf.Clamp(start - offset, minimum, maximum);
                if (IsWallSegmentFree(
                    structure, entrances, wallXSign, negative, halfWidth, clearance))
                    return negative;
            }
            return start;
        }

        private static bool IsWallSegmentFree(
            Transform structure,
            IList<Transform> entrances,
            float wallXSign,
            float candidateZ,
            float halfWidth,
            float clearance)
        {
            float candidateMin = candidateZ - halfWidth - clearance;
            float candidateMax = candidateZ + halfWidth + clearance;
            for (int i = 0; i < entrances.Count; i++)
            {
                Transform panel = entrances[i].Find("Door_Panel");
                if (panel == null)
                    panel = entrances[i];
                Vector3 local = structure.InverseTransformPoint(panel.position);
                if (Mathf.Sign(local.x) != Mathf.Sign(wallXSign))
                    continue;
                Renderer renderer = panel.GetComponent<Renderer>();
                float halfDoor = renderer != null
                    ? Mathf.Max(0.6f, renderer.bounds.extents.z)
                    : 0.6f;
                if (candidateMax > local.z - halfDoor && candidateMin < local.z + halfDoor)
                    return false;
            }
            return true;
        }

        private static TextMesh AddText(
            Transform parent,
            string objectName,
            string text,
            Vector3 localPosition,
            float characterSize,
            Color color)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localScale = new Vector3(-1f, 1f, 1f);
            TextMesh mesh = textObject.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.richText = false;
            mesh.color = color;
            MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
            if (renderer != null && font != null)
            {
                Material depthMaterial = Resources.Load<Material>("Panelka_WorldTextDepth");
                renderer.sharedMaterial = depthMaterial != null ? depthMaterial : font.material;
            }
            if (textObject.GetComponent<MiniVanPanelkaWorldTextDepth>() == null)
                textObject.AddComponent<MiniVanPanelkaWorldTextDepth>();
            return mesh;
        }

        private static GameObject Box(
            string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            Renderer renderer = box.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            Collider collider = box.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            return box;
        }

        private static Transform Group(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }
    }
}
