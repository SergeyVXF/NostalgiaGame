using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Invector.vCharacterController.vActions;

public class SceneTransitionFixer : MonoBehaviour
{
    [Tooltip("Тег игрока")]
    public string playerTag = "Player";
    
    [Tooltip("Время задержки после загрузки сцены перед починкой ссылок")]
    public float fixDelay = 0.5f;
    
    [Tooltip("Включить отладочные сообщения")]
    public bool enableDebug = true;

    [Header("Настройки HandTarget")]
    [Tooltip("Путь к HandTarget относительно игрока (если пусто, будет выполнен автопоиск)")]
    public string handTargetPath = "";
    
    [Tooltip("Позиция HandTarget относительно игрока")]
    public Vector3 handTargetPosition = new Vector3(0, 1.5f, 0.2f);

    private void Awake()
    {
        // Убедимся, что объект не уничтожится при переходе между сценами
        DontDestroyOnLoad(gameObject);
        
        // Подписываемся на событие загрузки сцены
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Запускаем корутину для починки ссылок после загрузки сцены
        StartCoroutine(FixReferencesAfterSceneLoad());
    }
    
    private IEnumerator FixReferencesAfterSceneLoad()
    {
        // Ждем некоторое время, чтобы сцена полностью загрузилась
        yield return new WaitForSeconds(fixDelay);
        
        // Ищем игрока по тегу
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        
        if (player == null)
        {
            if (enableDebug)
                Debug.LogWarning($"[SceneTransitionFixer] Не найден игрок с тегом {playerTag}");
            yield break;
        }
        
        // Ищем компонент vFreeClimb
        vFreeClimb freeClimb = player.GetComponentInChildren<vFreeClimb>();
        
        if (freeClimb == null)
        {
            if (enableDebug)
                Debug.LogWarning("[SceneTransitionFixer] Не найден компонент vFreeClimb на игроке");
            yield break;
        }

        // Всегда создаем новый handTarget для нового игрока
        Transform handTarget = CreateNewHandTarget(player.transform);
        if (handTarget != null)
        {
            freeClimb.handTarget = handTarget;
            if (enableDebug)
                Debug.Log($"[SceneTransitionFixer] Создан и назначен новый handTarget для игрока");
        }
        else
        {
            if (enableDebug)
                Debug.LogError("[SceneTransitionFixer] Не удалось создать handTarget!");
        }
    }
    
    private Transform CreateNewHandTarget(Transform parent)
    {
        // Сначала пробуем найти по указанному пути
        if (!string.IsNullOrEmpty(handTargetPath))
        {
            Transform existingTarget = parent.Find(handTargetPath);
            if (existingTarget != null)
            {
                if (enableDebug)
                    Debug.Log($"[SceneTransitionFixer] Найден существующий handTarget по пути: {handTargetPath}");
                return existingTarget;
            }
        }

        // Проверяем, существует ли уже handTarget с нужным именем
        Transform existing = parent.Find("vFreeClimbHandTarget");
        if (existing != null)
        {
            if (enableDebug)
                Debug.Log("[SceneTransitionFixer] Найден существующий vFreeClimbHandTarget");
            
            // Обновляем позицию существующего объекта
            existing.localPosition = handTargetPosition;
            existing.localRotation = Quaternion.identity;
            return existing;
        }
        
        // Создаем новый handTarget
        GameObject handTargetObj = new GameObject("vFreeClimbHandTarget");
        handTargetObj.transform.SetParent(parent);
        handTargetObj.transform.localPosition = handTargetPosition;
        handTargetObj.transform.localRotation = Quaternion.identity;
        
        if (enableDebug)
            Debug.Log("[SceneTransitionFixer] Создан новый vFreeClimbHandTarget");
        
        return handTargetObj.transform;
    }
    
    // Дополнительно вызовем FixReferencesAfterSceneLoad при старте
    private IEnumerator Start()
    {
        // Ждем один кадр, чтобы SceneManager успел зарегистрировать обработчики
        yield return null;
        
        // Запускаем проверку ссылок сразу после старта
        StartCoroutine(FixReferencesAfterSceneLoad());
    }
} 