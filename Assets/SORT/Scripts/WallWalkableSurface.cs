using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WallWalkableSurface : MonoBehaviour
{
    [Tooltip("Включить или отключить возможность хождения по этой поверхности")]
    public bool isWalkable = true;

    [Tooltip("Материал для отображения в режиме отладки")]
    public Material debugMaterial;

    [Tooltip("Цвет контура в режиме отладки")]
    public Color debugColor = new Color(0, 1, 0, 0.3f);

    private Renderer rend;
    private Material originalMaterial;
    private bool isShowingDebug = false;

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("На объекте с WallWalkableSurface должен быть Collider!");
            return;
        }

        // Проверяем, что слой объекта настроен правильно
        if (gameObject.layer != LayerMask.NameToLayer("WalkableWall") && isWalkable)
        {
            Debug.LogWarning("Объект с WallWalkableSurface должен быть на слое 'WalkableWall', " +
                "иначе WallWalker не сможет его обнаружить. Текущий слой: " + 
                LayerMask.LayerToName(gameObject.layer));
        }

        // Сохраняем ссылку на рендерер для отладки
        rend = GetComponent<Renderer>();
        if (rend != null && Application.isEditor)
        {
            originalMaterial = rend.material;
        }
    }

    // Метод для включения отладочного отображения
    public void ShowDebugVisuals(bool show)
    {
        if (rend == null || debugMaterial == null || !Application.isEditor)
            return;

        isShowingDebug = show;
        
        if (show)
        {
            // Создаем временный материал для отладки
            Material tempMat = new Material(debugMaterial);
            tempMat.color = debugColor;
            rend.material = tempMat;
        }
        else
        {
            // Возвращаем оригинальный материал
            rend.material = originalMaterial;
        }
    }

    private void OnDestroy()
    {
        // Возвращаем оригинальный материал, если он был изменен
        if (isShowingDebug && rend != null)
        {
            rend.material = originalMaterial;
        }
    }

    // Отображение границ в редакторе Unity
    private void OnDrawGizmos()
    {
        if (!isWalkable)
            return;

        Gizmos.color = new Color(debugColor.r, debugColor.g, debugColor.b, 0.2f);
        
        // Получаем все компоненты коллайдера
        Collider[] colliders = GetComponents<Collider>();
        
        foreach (Collider collider in colliders)
        {
            if (collider is BoxCollider)
            {
                BoxCollider boxCollider = collider as BoxCollider;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            }
            else if (collider is MeshCollider)
            {
                // Для меш-коллайдера просто рисуем каркас объекта
                Gizmos.DrawWireMesh(GetComponent<MeshFilter>().sharedMesh, 0, transform.position, transform.rotation, transform.lossyScale);
            }
        }
    }
} 