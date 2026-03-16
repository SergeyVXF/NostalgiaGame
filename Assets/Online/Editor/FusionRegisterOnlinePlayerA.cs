#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Принудительно добавляет префаб OnlinePlayer_A в таблицу Fusion (метка FusionPrefab + переимпорт конфига).
/// Вызывать после Rebuild Prefab Table, если у OnlinePlayer_A NetworkObject на дочернем объекте и префаб не попадает в таблицу.
/// </summary>
public static class FusionRegisterOnlinePlayerA
{
    public const string OnlinePlayerAPrefabGuid = "ddf69063629389849aa7f3e696366fe5";
    public const string FusionPrefabLabel = "FusionPrefab";
    public const string FusionConfigPath = "Assets/SORT/Photon/Fusion/Resources/NetworkProjectConfig.fusion";

    [MenuItem("Tools/Fusion/Register OnlinePlayer_A in Prefab Table", priority = 101)]
    public static void RegisterOnlinePlayerA()
    {
        string prefabPath = AssetDatabase.GUIDToAssetPath(OnlinePlayerAPrefabGuid);
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError("[FusionRegisterOnlinePlayerA] Prefab with GUID " + OnlinePlayerAPrefabGuid + " not found. Is OnlinePlayer_A in the project?");
            return;
        }

        Object asset = AssetDatabase.LoadAssetAtPath<Object>(prefabPath);
        if (asset == null)
        {
            Debug.LogError("[FusionRegisterOnlinePlayerA] Could not load asset at " + prefabPath);
            return;
        }

        string[] labels = AssetDatabase.GetLabels(asset);
        bool hasLabel = false;
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == FusionPrefabLabel)
            {
                hasLabel = true;
                break;
            }
        }

        if (!hasLabel)
        {
            string[] newLabels = new string[labels.Length + 1];
            labels.CopyTo(newLabels, 0);
            newLabels[labels.Length] = FusionPrefabLabel;
            AssetDatabase.SetLabels(asset, newLabels);
            AssetDatabase.SaveAssets();
            Debug.Log("[FusionRegisterOnlinePlayerA] Added label '" + FusionPrefabLabel + "' to " + prefabPath);
        }
        else
        {
            Debug.Log("[FusionRegisterOnlinePlayerA] Prefab already has label '" + FusionPrefabLabel + "'.");
        }

        string configPath = FusionConfigPath;
        if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(configPath)))
        {
            string[] guids = AssetDatabase.FindAssets("NetworkProjectConfig t:DefaultAsset");
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p != null && p.EndsWith("NetworkProjectConfig.fusion"))
                {
                    configPath = p;
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(configPath)))
        {
            AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[FusionRegisterOnlinePlayerA] Reimported " + configPath + ". Restart Play if the game is running.");
        }
        else
        {
            Debug.LogWarning("[FusionRegisterOnlinePlayerA] NetworkProjectConfig.fusion not found. Add label FusionPrefab to OnlinePlayer_A manually and reimport the config.");
        }
    }
}
#endif
