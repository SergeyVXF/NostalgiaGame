using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Turns off the out-of-process asset import workers for this project.
    ///
    /// The workers open the same Library files as the main editor and keep
    /// memory-mapped views of them. When the editor then rewrites one of those
    /// files Windows refuses with "Win32 IO returned 1224" (ERROR_USER_MAPPED_FILE),
    /// which is what has been spamming the console from Bee, ILPP and StateCache.
    /// With zero workers the import runs in-process and nothing else holds the
    /// mappings.
    ///
    /// Cost: asset imports stop being parallel, so a big reimport is slower.
    /// Undo it in Project Settings > Editor > Asset Pipeline.
    /// </summary>
    public static class MiniVanImportWorkerFix
    {
        [MenuItem("Tools/AutoService/Disable Import Workers")]
        public static void DisableWorkers()
        {
            var singleton = Unsupported.GetSerializedAssetInterfaceSingleton("EditorSettings");
            if (singleton == null)
            {
                Debug.LogError("[ImportWorkerFix] EditorSettings singleton not available");
                return;
            }

            var so = new SerializedObject(singleton);
            string report = "[ImportWorkerFix]";
            report += Apply(so, "m_DesiredImportWorkerCount", 0);
            report += Apply(so, "m_StandbyImportWorkerCount", 0);
            report += Apply(so, "m_IdleImportWorkerShutdownDelay", 1000);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log(report);
        }

        static string Apply(SerializedObject so, string path, int value)
        {
            var prop = so.FindProperty(path);
            if (prop == null)
                return "  " + path + "=<missing>";
            int before = prop.intValue;
            prop.intValue = value;
            return "  " + path + ": " + before + " -> " + value;
        }
    }
}
