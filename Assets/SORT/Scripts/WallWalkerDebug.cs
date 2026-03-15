using UnityEngine;
using UnityEditor;

public class WallWalkerDebug : MonoBehaviour
{
    [Header("Debug Settings")]
    [Tooltip("Включить режим отладки")]
    public bool enableDebug = false;
    
    [Tooltip("Показывать ли Gizmos для стен, по которым можно ходить")]
    public bool showWalkableWalls = true;
    
    [Tooltip("Цвет для отображения Gizmos")]
    public Color debugColor = new Color(0, 1, 0, 0.5f);
    
    private WallWalker wallWalker;
    private GameObject[] walkableWalls;
    
    private void Start()
    {
        wallWalker = GetComponent<WallWalker>();
        if (wallWalker == null)
        {
            Debug.LogError("На объекте с WallWalkerDebug должен быть компонент WallWalker!");
            enabled = false;
            return;
        }
        
        // Находим все объекты с компонентом WallWalkableSurface
        RefreshWalkableWalls();
    }
    
    private void Update()
    {
        if (!enableDebug) return;
        
        // Выводим текущий статус WallWalker
        if (wallWalker.IsWallWalking())
        {
            Debug.Log("Игрок ходит по стене");
        }
        
        // Если включено отображение стен, по которым можно ходить
        if (showWalkableWalls)
        {
            // Находим все объекты с компонентом WallWalkableSurface
            RefreshWalkableWalls();
            
            // Устанавливаем режим отладки для каждой поверхности
            foreach (GameObject wall in walkableWalls)
            {
                WallWalkableSurface surface = wall.GetComponent<WallWalkableSurface>();
                if (surface != null)
                {
                    surface.ShowDebugVisuals(true);
                }
            }
        }
        else
        {
            // Отключаем режим отладки для каждой поверхности
            if (walkableWalls != null)
            {
                foreach (GameObject wall in walkableWalls)
                {
                    if (wall != null)
                    {
                        WallWalkableSurface surface = wall.GetComponent<WallWalkableSurface>();
                        if (surface != null)
                        {
                            surface.ShowDebugVisuals(false);
                        }
                    }
                }
            }
        }
    }
    
    private void RefreshWalkableWalls()
    {
        walkableWalls = GameObject.FindGameObjectsWithTag("WalkableWall");
        if (walkableWalls.Length == 0)
        {
            // Если нет объектов с тегом "WalkableWall", находим все объекты с компонентом WallWalkableSurface
            WallWalkableSurface[] surfaces = FindObjectsOfType<WallWalkableSurface>();
            walkableWalls = new GameObject[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                walkableWalls[i] = surfaces[i].gameObject;
            }
        }
        
        // Выводим количество найденных стен
        if (enableDebug)
        {
            Debug.Log("Найдено " + walkableWalls.Length + " стен для хождения");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!enableDebug || !showWalkableWalls) return;
        
        Gizmos.color = debugColor;
        
        // Отображаем границы области поиска WallWalker
        if (wallWalker != null)
        {
            Vector3 origin = transform.position + Vector3.up * 1.0f;
            Gizmos.DrawWireSphere(origin, wallWalker.wallCheckDistance);
            Gizmos.DrawRay(origin, transform.forward * wallWalker.wallCheckDistance);
        }
    }
    
    private void OnDestroy()
    {
        // Отключаем режим отладки для всех поверхностей при удалении компонента
        if (walkableWalls != null)
        {
            foreach (GameObject wall in walkableWalls)
            {
                if (wall != null)
                {
                    WallWalkableSurface surface = wall.GetComponent<WallWalkableSurface>();
                    if (surface != null)
                    {
                        surface.ShowDebugVisuals(false);
                    }
                }
            }
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(WallWalkerDebug))]
    public class WallWalkerDebugEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Отображаем стандартные элементы инспектора
            DrawDefaultInspector();
            
            WallWalkerDebug debugger = (WallWalkerDebug)target;
            
            // Добавляем кнопку для обновления списка стен
            if (GUILayout.Button("Обновить список стен"))
            {
                debugger.RefreshWalkableWalls();
            }
            
            // Добавляем кнопку для включения/выключения отладки
            if (GUILayout.Button(debugger.enableDebug ? "Выключить отладку" : "Включить отладку"))
            {
                debugger.enableDebug = !debugger.enableDebug;
            }
        }
    }
#endif
} 