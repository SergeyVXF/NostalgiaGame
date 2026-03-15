using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JumpBoostZone))]
public class JumpBoostZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        JumpBoostZone jumpZone = (JumpBoostZone)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Быстрые настройки", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Слабое подбрасывание"))
        {
            jumpZone.jumpForce = 1000f;
            jumpZone.cooldownTime = 2f;
            EditorUtility.SetDirty(jumpZone);
        }
        
        if (GUILayout.Button("Среднее подбрасывание"))
        {
            jumpZone.jumpForce = 2000f;
            jumpZone.cooldownTime = 3f;
            EditorUtility.SetDirty(jumpZone);
        }
        
        if (GUILayout.Button("Сильное подбрасывание"))
        {
            jumpZone.jumpForce = 3500f;
            jumpZone.cooldownTime = 5f;
            EditorUtility.SetDirty(jumpZone);
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Вертикальное"))
        {
            jumpZone.jumpDirection = Vector3.zero;
            jumpZone.horizontalBoost = 0f;
            EditorUtility.SetDirty(jumpZone);
        }
        
        if (GUILayout.Button("С наклоном вперед"))
        {
            jumpZone.jumpDirection = new Vector3(0f, 0.7f, 0.7f).normalized;
            jumpZone.horizontalBoost = 500f;
            EditorUtility.SetDirty(jumpZone);
        }
        
        if (GUILayout.Button("С наклоном назад"))
        {
            jumpZone.jumpDirection = new Vector3(0f, 0.7f, -0.7f).normalized;
            jumpZone.horizontalBoost = -500f;
            EditorUtility.SetDirty(jumpZone);
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Утилиты", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Принудительная перезарядка"))
        {
            jumpZone.ForceRecharge();
        }
        
        if (GUILayout.Button("Создать материалы"))
        {
            CreateMaterials();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "💡 Советы:\n" +
            "• Сила подбрасывания: 1000-1500 (слабое), 2000-2500 (среднее), 3000+ (сильное)\n" +
            "• Для платформерного эффекта используйте вертикальное подбрасывание\n" +
            "• Добавьте горизонтальную составляющую для трамплинов\n" +
            "• Настройте минимальную скорость для активации только при быстром движении",
            MessageType.Info
        );
    }
    
    private void CreateMaterials()
    {
        // Создаем активный материал (зеленый)
        Material activeMat = new Material(Shader.Find("Standard"));
        activeMat.color = new Color(0f, 1f, 0f, 0.3f); // Зеленый полупрозрачный
        activeMat.SetFloat("_Mode", 3); // Transparent mode
        activeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        activeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        activeMat.SetInt("_ZWrite", 0);
        activeMat.DisableKeyword("_ALPHATEST_ON");
        activeMat.EnableKeyword("_ALPHABLEND_ON");
        activeMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        activeMat.renderQueue = 3000;
        
        // Создаем материал перезарядки (красный)
        Material cooldownMat = new Material(Shader.Find("Standard"));
        cooldownMat.color = new Color(1f, 0f, 0f, 0.3f); // Красный полупрозрачный
        cooldownMat.SetFloat("_Mode", 3); // Transparent mode
        cooldownMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        cooldownMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        cooldownMat.SetInt("_ZWrite", 0);
        cooldownMat.DisableKeyword("_ALPHATEST_ON");
        cooldownMat.EnableKeyword("_ALPHABLEND_ON");
        cooldownMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        cooldownMat.renderQueue = 3000;
        
        // Сохраняем материалы
        AssetDatabase.CreateAsset(activeMat, "Assets/Materials/JumpBoostZone_Active.mat");
        AssetDatabase.CreateAsset(cooldownMat, "Assets/Materials/JumpBoostZone_Cooldown.mat");
        AssetDatabase.SaveAssets();
        
        // Применяем к зоне
        JumpBoostZone jumpZone = (JumpBoostZone)target;
        jumpZone.activeMaterial = activeMat;
        jumpZone.cooldownMaterial = cooldownMat;
        EditorUtility.SetDirty(jumpZone);
        
        Debug.Log("✅ Материалы для зоны подбрасывания созданы!");
    }
} 