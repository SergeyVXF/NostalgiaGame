using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TriggerBackPrefabCreator : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("GameObject/3D Object/TriggerBack")]
    private static void CreateTriggerBack()
    {
        // Создаем новый объект
        GameObject triggerObj = new GameObject("TriggerBack");
        
        // Добавляем BoxCollider как триггер
        BoxCollider boxCollider = triggerObj.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = new Vector3(5f, 3f, 1f); // Размер по умолчанию
        
        // Добавляем компонент TriggerBack
        triggerObj.AddComponent<TriggerBack>();
        
        // Создаем дочерний объект с Mesh для визуализации
        GameObject visualObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualObj.name = "VisualBox";
        visualObj.transform.SetParent(triggerObj.transform);
        visualObj.transform.localPosition = Vector3.zero;
        visualObj.transform.localScale = boxCollider.size;
        
        // Настраиваем материал для визуализации
        Renderer renderer = visualObj.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Transparent/Diffuse"));
        mat.color = new Color(1f, 0.3f, 0.3f, 0.5f); // Полупрозрачный красный
        renderer.material = mat;
        
        // Удаляем коллайдер с визуального объекта, так как коллайдер уже есть на родительском
        DestroyImmediate(visualObj.GetComponent<Collider>());
        
        // Создаем префаб
        string prefabPath = "Assets/Prefabs/TriggerBack.prefab";
        
        // Проверяем, существует ли уже префаб с таким именем
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            // Если существует, заменяем его
            PrefabUtility.SaveAsPrefabAsset(triggerObj, prefabPath);
            Debug.Log("Префаб TriggerBack обновлен: " + prefabPath);
        }
        else
        {
            // Если нет, создаем новый
            PrefabUtility.SaveAsPrefabAsset(triggerObj, prefabPath);
            Debug.Log("Создан новый префаб TriggerBack: " + prefabPath);
        }
        
        // Выбираем созданный объект
        Selection.activeGameObject = triggerObj;
        
        // Устанавливаем его позицию в центр текущего вида
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            triggerObj.transform.position = sceneView.camera.transform.position + sceneView.camera.transform.forward * 5f;
        }
    }
#endif
} 