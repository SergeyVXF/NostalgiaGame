using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MiniVanPanelkaPlayModeSmokeRunner
{
    // Full rebuild and quick saved-scene checks intentionally use separate trigger files.
    private const string ScenePath = "Assets/MiniVan Game/Scenes/Game_v01.unity";
    private const string TriggerPath = "Library/CodexTools/RunPanelkaSmoke.flag";
    private const string QuickTriggerPath = "Library/CodexTools/RunPanelkaSavedSceneSmoke.flag";
    private const string EditorResultPath =
        "Library/CodexTools/PanelkaEditorSmoke.result";
    private const string RuntimeResultPath =
        "Library/CodexTools/PanelkaRuntimeSmoke.result";
    private const string SessionKey = "MiniVanGame.PanelkaSmokeRunning";
    private const string RerunKey = "MiniVanGame.PanelkaSmokeRerun";
    private const string QuickRerunKey = "MiniVanGame.PanelkaSavedSmokeRerun";
    private const string StartedAtKey = "MiniVanGame.PanelkaSmokeStartedAt";
    private static bool triggerHandled;
    private static bool quickTriggerHandled;

    static MiniVanPanelkaPlayModeSmokeRunner()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("MiniVan Game/Panelka/Run Game_v01 PlayMode Smoke Test")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        try
        {
            DeleteResult(EditorResultPath);
            DeleteResult(RuntimeResultPath);
            MiniVanPanelkaRouteAudit.RebuildAndAuditGameScene();
            File.WriteAllText(
                EditorResultPath,
                "AUDIT_PASS " + DateTime.Now.ToString("O") + Environment.NewLine);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(SessionKey, true);
            SessionState.SetString(StartedAtKey, "0");
            Debug.Log("[Panelka PlayMode Smoke] START: Game_v01 rebuilt and entering Play Mode.");
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            SessionState.SetBool(SessionKey, false);
            File.WriteAllText(
                EditorResultPath,
                "AUDIT_FAIL " + DateTime.Now.ToString("O") + Environment.NewLine +
                exception + Environment.NewLine);
            Debug.LogException(exception);
        }
    }

    [MenuItem("MiniVan Game/Panelka/Run Saved Game_v01 PlayMode Smoke Test")]
    public static void RunSavedScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        try
        {
            DeleteResult(EditorResultPath);
            DeleteResult(RuntimeResultPath);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MiniVanPanelkaRouteAudit.AuditSavedGameScene();
            File.WriteAllText(
                EditorResultPath,
                "SAVED_SCENE_AUDIT_PASS " + DateTime.Now.ToString("O") + Environment.NewLine);
            SessionState.SetBool(SessionKey, true);
            SessionState.SetString(StartedAtKey, "0");
            Debug.Log("[Panelka PlayMode Smoke] START: entering saved Game_v01 without rebuilding it.");
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            SessionState.SetBool(SessionKey, false);
            File.WriteAllText(
                EditorResultPath,
                "SAVED_SCENE_OPEN_FAIL " + DateTime.Now.ToString("O") + Environment.NewLine +
                exception + Environment.NewLine);
            Debug.LogException(exception);
        }
    }

    private static void Update()
    {
        if (!File.Exists(TriggerPath))
            triggerHandled = false;
        if (!File.Exists(QuickTriggerPath))
            quickTriggerHandled = false;

        if (!quickTriggerHandled && File.Exists(QuickTriggerPath))
        {
            quickTriggerHandled = true;
            File.Delete(QuickTriggerPath);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(QuickRerunKey, true);
                EditorApplication.isPlaying = false;
                return;
            }
            RunSavedScene();
            return;
        }

        if (!triggerHandled && File.Exists(TriggerPath))
        {
            triggerHandled = true;
            File.Delete(TriggerPath);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(RerunKey, true);
                EditorApplication.isPlaying = false;
                return;
            }
            Run();
        }

        if (!SessionState.GetBool(SessionKey, false) || !EditorApplication.isPlaying)
            return;

        double startedAt;
        if (!double.TryParse(
                SessionState.GetString(StartedAtKey, "0"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out startedAt))
            startedAt = 0.0;
        if (startedAt <= 0.0)
            return;
        if (EditorApplication.timeSinceStartup - startedAt >= 8.0)
            EditorApplication.isPlaying = false;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetBool(SessionKey, false))
        {
            SessionState.SetString(
                StartedAtKey,
                EditorApplication.timeSinceStartup.ToString(CultureInfo.InvariantCulture));
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        if (SessionState.GetBool(SessionKey, false))
        {
            SessionState.SetBool(SessionKey, false);
            File.AppendAllText(
                EditorResultPath,
                "PLAYMODE_COMPLETE " + DateTime.Now.ToString("O") + Environment.NewLine);
            Debug.Log("[Panelka PlayMode Smoke] COMPLETE: Play Mode stopped after runtime checks.");
        }

        if (SessionState.GetBool(RerunKey, false))
        {
            SessionState.SetBool(RerunKey, false);
            Run();
            return;
        }

        if (SessionState.GetBool(QuickRerunKey, false))
        {
            SessionState.SetBool(QuickRerunKey, false);
            RunSavedScene();
        }
    }

    private static void DeleteResult(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
