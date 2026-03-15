using UnityEngine;
using UnityEditor;

public class SpeedBoostZoneCreator
{
    [MenuItem("GameObject/3D Object/Speed Boost Zone")]
    static void CreateSpeedBoostZone()
    {
        // Создаем новый GameObject
        GameObject boostZone = new GameObject("SpeedBoostZone");
        
        // Добавляем компоненты
        BoxCollider col = boostZone.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(4f, 2f, 8f); // Прямоугольная зона
        
        MeshRenderer renderer = boostZone.AddComponent<MeshRenderer>();
        MeshFilter filter = boostZone.AddComponent<MeshFilter>();
        
        // Создаем простой куб как меш
        filter.mesh = CreatePrimitiveMesh(PrimitiveType.Cube);
        
        // Создаем материал для зоны
        Material boostMaterial = new Material(Shader.Find("Standard"));
        boostMaterial.color = new Color(0f, 1f, 1f, 0.3f); // Прозрачный голубой
        boostMaterial.SetFloat("_Mode", 3); // Transparent mode
        boostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        boostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        boostMaterial.SetInt("_ZWrite", 0);
        boostMaterial.DisableKeyword("_ALPHATEST_ON");
        boostMaterial.EnableKeyword("_ALPHABLEND_ON");
        boostMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        boostMaterial.renderQueue = 3000;
        
        renderer.material = boostMaterial;
        
        // Добавляем скрипт зоны
        SpeedBoostZone boostScript = boostZone.AddComponent<SpeedBoostZone>();
        boostScript.cooldownTime = 3f;
        
        // Позиционируем относительно сцены
        if (SceneView.lastActiveSceneView != null)
        {
            boostZone.transform.position = SceneView.lastActiveSceneView.pivot;
        }
        
        // Выделяем созданный объект
        Selection.activeGameObject = boostZone;
        
        Debug.Log("[SpeedBoostZoneCreator] Зона ускорения создана! Настройте параметры в инспекторе.");
    }
    
    static Mesh CreatePrimitiveMesh(PrimitiveType type)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        Mesh mesh = primitive.GetComponent<MeshFilter>().mesh;
        Object.DestroyImmediate(primitive);
        return mesh;
    }
} 