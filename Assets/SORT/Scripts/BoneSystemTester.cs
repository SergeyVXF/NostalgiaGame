using UnityEngine;

public class BoneSystemTester : MonoBehaviour
{
    private void Start()
    {
        // Ждем 3 секунды после старта игры, затем тестируем систему косточки
        Invoke("TestBoneSystemAutomatically", 3f);
    }
    
    private void TestBoneSystemAutomatically()
    {
        Debug.Log("[BoneSystemTester] 🧪 Автоматическое тестирование системы косточки...");
        
        // Найдем косточку
        BoneBehavior bone = FindObjectOfType<BoneBehavior>();
        if (bone != null)
        {
            Debug.Log($"[BoneSystemTester] Найдена косточка: {bone.name}");
            bone.TestBoneSystem();
        }
        else
        {
            Debug.LogError("[BoneSystemTester] Косточка не найдена в сцене!");
        }
    }
}
