using System.IO;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Netcode marks Unity.Netcode.Runtime as autoReferenced, so ILPP rewrites every
    /// assembly including the huge Assembly-CSharp (SORT + leftovers) and OOMs.
    /// Force autoReferenced=false; MiniVanGame.asmdef references Netcode explicitly.
    /// </summary>
    [InitializeOnLoad]
    internal static class MiniVanNetcodeAutoReferencePatch
    {
        private const string MarkerKey = "MiniVanGame.NetcodeAutoRefPatched";

        static MiniVanNetcodeAutoReferencePatch()
        {
            EditorApplication.delayCall += Apply;
        }

        [MenuItem("MiniVan/Fix/Patch Netcode Auto-Reference (ILPP OOM)")]
        private static void ApplyFromMenu()
        {
            SessionState.EraseBool(MarkerKey);
            Apply();
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            string packageCache = Path.Combine(Directory.GetCurrentDirectory(), "Library", "PackageCache");
            if (!Directory.Exists(packageCache))
            {
                return;
            }

            bool changed = false;
            string[] roots = Directory.GetDirectories(packageCache, "com.unity.netcode.gameobjects@*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < roots.Length; i++)
            {
                string runtimeAsmdef = Path.Combine(roots[i], "Runtime", "Unity.Netcode.Runtime.asmdef");
                changed |= PatchAutoReferenced(runtimeAsmdef, false);
            }

            if (!changed)
            {
                SessionState.SetBool(MarkerKey, true);
                return;
            }

            SessionState.SetBool(MarkerKey, true);
            Debug.Log("[MiniVan] Patched Unity.Netcode.Runtime autoReferenced=false to avoid ILPP OOM on Assembly-CSharp. Reimporting...");
            AssetDatabase.Refresh();
        }

        private static bool PatchAutoReferenced(string asmdefPath, bool autoReferenced)
        {
            if (!File.Exists(asmdefPath))
            {
                return false;
            }

            string text = File.ReadAllText(asmdefPath);
            string target = "\"autoReferenced\": " + (autoReferenced ? "true" : "false");
            string opposite = "\"autoReferenced\": " + (autoReferenced ? "false" : "true");
            if (text.Contains(target))
            {
                return false;
            }

            if (!text.Contains(opposite))
            {
                return false;
            }

            File.WriteAllText(asmdefPath, text.Replace(opposite, target));
            return true;
        }
    }
}
