using UnityEngine;
using UnityEditor;
using Invector.vCharacterController.AI;

public class AutoCrouchDisabler : MonoBehaviour
{
    [MenuItem("Tools/AI/Disable Auto Crouch on All AI")]
    public static void DisableAutoCrouchOnAllAI()
    {
        // Находим все AI моторы в сцене
        v_AIMotor[] aiMotors = FindObjectsOfType<v_AIMotor>();
        
        if (aiMotors.Length == 0)
        {
            EditorUtility.DisplayDialog("Auto Crouch Disabler", "AI моторы не найдены в сцене!", "OK");
            return;
        }
        
        int disabledCount = 0;
        
        foreach (v_AIMotor ai in aiMotors)
        {
            if (ai.aiUseAutoCrouch)
            {
                ai.aiUseAutoCrouch = false;
                EditorUtility.SetDirty(ai);
                disabledCount++;
            }
        }
        
        EditorUtility.DisplayDialog("Auto Crouch Disabler", 
            $"Auto Crouch отключен на {disabledCount} из {aiMotors.Length} AI моторов!", "OK");
    }
    
    [MenuItem("Tools/AI/Set Auto Crouch Delay to 2 seconds")]
    public static void SetAutoCrouchDelayTo2Seconds()
    {
        // Находим все AI моторы в сцене
        v_AIMotor[] aiMotors = FindObjectsOfType<v_AIMotor>();
        
        if (aiMotors.Length == 0)
        {
            EditorUtility.DisplayDialog("Auto Crouch Disabler", "AI моторы не найдены в сцене!", "OK");
            return;
        }
        
        int updatedCount = 0;
        
        foreach (v_AIMotor ai in aiMotors)
        {
            if (ai.aiUseAutoCrouch)
            {
                ai.aiAutoCrouchDelay = 2f;
                EditorUtility.SetDirty(ai);
                updatedCount++;
            }
        }
        
        EditorUtility.DisplayDialog("Auto Crouch Disabler", 
            $"Задержка Auto Crouch установлена на 2 секунды для {updatedCount} из {aiMotors.Length} AI моторов!", "OK");
    }
    
    [MenuItem("Tools/AI/Set Auto Crouch Delay to 0.5 seconds")]
    public static void SetAutoCrouchDelayToHalfSecond()
    {
        // Находим все AI моторы в сцене
        v_AIMotor[] aiMotors = FindObjectsOfType<v_AIMotor>();
        
        if (aiMotors.Length == 0)
        {
            EditorUtility.DisplayDialog("Auto Crouch Disabler", "AI моторы не найдены в сцене!", "OK");
            return;
        }
        
        int updatedCount = 0;
        
        foreach (v_AIMotor ai in aiMotors)
        {
            if (ai.aiUseAutoCrouch)
            {
                ai.aiAutoCrouchDelay = 0.5f;
                EditorUtility.SetDirty(ai);
                updatedCount++;
            }
        }
        
        EditorUtility.DisplayDialog("Auto Crouch Disabler", 
            $"Задержка Auto Crouch установлена на 0.5 секунды для {updatedCount} из {aiMotors.Length} AI моторов!", "OK");
    }
    
    [MenuItem("Tools/AI/Enable Auto Crouch on All AI")]
    public static void EnableAutoCrouchOnAllAI()
    {
        // Находим все AI моторы в сцене
        v_AIMotor[] aiMotors = FindObjectsOfType<v_AIMotor>();
        
        if (aiMotors.Length == 0)
        {
            EditorUtility.DisplayDialog("Auto Crouch Disabler", "AI моторы не найдены в сцене!", "OK");
            return;
        }
        
        int enabledCount = 0;
        
        foreach (v_AIMotor ai in aiMotors)
        {
            if (!ai.aiUseAutoCrouch)
            {
                ai.aiUseAutoCrouch = true;
                EditorUtility.SetDirty(ai);
                enabledCount++;
            }
        }
        
        EditorUtility.DisplayDialog("Auto Crouch Disabler", 
            $"Auto Crouch включен на {enabledCount} из {aiMotors.Length} AI моторов!", "OK");
    }
    
    [MenuItem("Tools/AI/Show Auto Crouch Status")]
    public static void ShowAutoCrouchStatus()
    {
        // Находим все AI моторы в сцене
        v_AIMotor[] aiMotors = FindObjectsOfType<v_AIMotor>();
        
        if (aiMotors.Length == 0)
        {
            EditorUtility.DisplayDialog("Auto Crouch Status", "AI моторы не найдены в сцене!", "OK");
            return;
        }
        
        int enabledCount = 0;
        int disabledCount = 0;
        
        foreach (v_AIMotor ai in aiMotors)
        {
            if (ai.aiUseAutoCrouch)
                enabledCount++;
            else
                disabledCount++;
        }
        
        EditorUtility.DisplayDialog("Auto Crouch Status", 
            $"Всего AI моторов: {aiMotors.Length}\n" +
            $"Auto Crouch включен: {enabledCount}\n" +
            $"Auto Crouch отключен: {disabledCount}", "OK");
    }
} 