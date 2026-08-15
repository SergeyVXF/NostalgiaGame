using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MiniVanPanelkaApartmentPrefabRepair
{
    private const string TriggerPath =
        "Library/CodexTools/RepairApartmentPrefabRenderers.flag";
    private const string ResultPath =
        "Library/CodexTools/RepairApartmentPrefabRenderers.result";
    private const string Folder =
        "Assets/MiniVan Game/Prefabs/Panelka/Interiors/ApartmentTemplates";

    static MiniVanPanelkaApartmentPrefabRepair()
    {
        EditorApplication.update += RunIfTriggered;
    }

    [MenuItem("MiniVan Game/Panelka/Repair Full Apartment Prefab Renderers")]
    public static void Repair()
    {
        int repairedRenderers = 0;
        int repairedPrefabs = 0;
        try
        {
            for (int index = 1; index <= 5; index++)
            {
                string[] matches = AssetDatabase.FindAssets(
                    "ApartmentTemplate_" + index.ToString("00") + "_ t:Prefab",
                    new[] { Folder });
                if (matches.Length != 1)
                    throw new InvalidOperationException(
                        "Expected one apartment prefab for index " + index +
                        ", found " + matches.Length + ".");

                string path = AssetDatabase.GUIDToAssetPath(matches[0]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                    int changedInPrefab = 0;
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Renderer renderer = renderers[rendererIndex];
                        if (renderer.enabled || ShouldRemainHidden(renderer.transform, root.transform))
                            continue;
                        renderer.enabled = true;
                        EditorUtility.SetDirty(renderer);
                        changedInPrefab++;
                    }

                    if (changedInPrefab > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        repairedRenderers += changedInPrefab;
                        repairedPrefabs++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            File.WriteAllText(
                ResultPath,
                "PASS: prefabs=" + repairedPrefabs +
                ", renderers=" + repairedRenderers + Environment.NewLine);
            Debug.Log("[Apartment Prefab Repair] Enabled " + repairedRenderers +
                      " visual renderers in " + repairedPrefabs + " prefabs.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, "FAIL: " + exception + Environment.NewLine);
            Debug.LogException(exception);
        }
    }

    private static void RunIfTriggered()
    {
        if (!File.Exists(TriggerPath))
            return;
        File.Delete(TriggerPath);
        Repair();
    }

    private static bool ShouldRemainHidden(Transform item, Transform root)
    {
        Transform current = item;
        while (current != null && current != root.parent)
        {
            string name = current.name;
            if (Contains(name, "GUIDE") ||
                Contains(name, "SOCKET") ||
                Contains(name, "MARKER") ||
                Contains(name, "ANCHOR") ||
                Contains(name, "NO_FURNITURE") ||
                Contains(name, "BACK_WALL_ID") ||
                Contains(name, "FRONT_ROOM_ID") ||
                Contains(name, "COLLIDER") ||
                Contains(name, "TRIGGER") ||
                Contains(name, "HIGHLIGHT") ||
                Contains(name, "OUTLINE") ||
                Contains(name, "DEBUG") ||
                Contains(name, "GIZMO"))
                return true;
            current = current.parent;
        }
        return false;
    }

    private static bool Contains(string value, string token)
    {
        return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
