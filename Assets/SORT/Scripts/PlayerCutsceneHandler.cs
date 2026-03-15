using UnityEngine;
using Invector.vCharacterController;
using Invector.vCharacterController.vActions;
using System.Collections;

public class PlayerCutsceneHandler : MonoBehaviour
{
    [Header("Компоненты игрока")]
    [Tooltip("Если null, компоненты будут найдены автоматически")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private vThirdPersonController playerController;
    [SerializeField] private vThirdPersonInput playerInput;
    
    [Header("Настройки")]
    [Tooltip("Полностью скрывать игрока во время катсцены")]
    [SerializeField] private bool hidePlayerMesh = false;
    
    private Renderer[] playerRenderers;
    private Animator playerAnimator;
    private bool wasActive = true;
    private vFreeClimb freeClimbComponent;
    private bool wasInFreeClimb = false;
    private SkinnedMeshRenderer modelLOD3Renderer;

    private void Awake()
    {
        // Инициализация компонентов, если не были указаны
        if (playerObject == null)
        {
            playerObject = gameObject;
        }
        
        if (playerController == null)
        {
            playerController = GetComponentInChildren<vThirdPersonController>();
        }
        
        if (playerInput == null)
        {
            playerInput = GetComponentInChildren<vThirdPersonInput>();
        }
        
        // Получаем компонент FreeClimb
        freeClimbComponent = GetComponentInChildren<vFreeClimb>();
        
        // Получаем компоненты для визуализации игрока
        playerRenderers = playerObject.GetComponentsInChildren<Renderer>();
        playerAnimator = playerObject.GetComponentInChildren<Animator>();
        
        // Ищем SkinnedMeshRenderer на model_LOD3
        var vThirdPerson = GameObject.Find("vThirdPersonBasic");
        if (vThirdPerson != null)
        {
            var modelLOD3 = vThirdPerson.transform.Find("model_LOD3");
            if (modelLOD3 != null)
            {
                modelLOD3Renderer = modelLOD3.GetComponent<SkinnedMeshRenderer>();
            }
        }
        
        // Находим менеджер катсцен и подписываемся на события
        CutsceneManager manager = FindObjectOfType<CutsceneManager>();
        if (manager != null)
        {
            ICutsceneEvents events = manager as ICutsceneEvents;
            events.OnCutsceneStarted += OnCutsceneStarted;
            events.OnCutsceneEnded += OnCutsceneEnded;
            Debug.Log("PlayerCutsceneHandler: Подписка на события катсцен успешна");
        }
        else
        {
            Debug.LogWarning("CutsceneManager не найден в сцене!");
        }
    }
    
    private void OnDestroy()
    {
        // Отписываемся от событий при уничтожении объекта
        CutsceneManager manager = FindObjectOfType<CutsceneManager>();
        if (manager != null)
        {
            ICutsceneEvents events = manager as ICutsceneEvents;
            events.OnCutsceneStarted -= OnCutsceneStarted;
            events.OnCutsceneEnded -= OnCutsceneEnded;
        }
    }
    
    private void OnCutsceneStarted(GameObject cutsceneObject)
    {
        Debug.Log("Началась катсцена: " + cutsceneObject.name);
        
        // Проверяем, был ли игрок в режиме лазания перед катсценой
        if (freeClimbComponent != null)
        {
            // Используем публичный метод для проверки состояния
            wasInFreeClimb = freeClimbComponent.IsInClimbMode();
            Debug.Log($"Игрок был в режиме лазания перед катсценой: {wasInFreeClimb}");
        }
        
        // Отключаем SkinnedMeshRenderer на model_LOD3
        if (modelLOD3Renderer != null)
        {
            modelLOD3Renderer.enabled = false;
        }
        
        DisablePlayer();
    }
    
    private void OnCutsceneEnded(GameObject cutsceneObject)
    {
        Debug.Log("Завершилась катсцена: " + cutsceneObject.name);
        
        // Включаем SkinnedMeshRenderer на model_LOD3
        if (modelLOD3Renderer != null)
        {
            modelLOD3Renderer.enabled = true;
        }
        
        EnablePlayer();
        
        // Телепортация игрока после CutScene_02
        if (cutsceneObject.name.Contains("CutScene_02"))
        {
            // Телепортируем игрока на землю
            Vector3 teleportPosition = new Vector3(-92f, 0.4f, -179.5f);
            transform.position = teleportPosition;
            Debug.Log("Игрок телепортирован на позицию: " + teleportPosition);
        }
        
        // Принудительно выходим из режима лазания, если игрок был в нем
        if (wasInFreeClimb && freeClimbComponent != null)
        {
            // Используем публичный метод для выхода из режима лазания
            Debug.Log("Принудительный выход из режима лазания после катсцены");
            freeClimbComponent.ForceExitClimb();
            
            // Дополнительная страховка - сброс контроллера
            if (playerController != null)
            {
                // Убедимся, что Rigidbody не кинематический
                Rigidbody rb = playerController.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                }
            }
            
            // Сбрасываем флаг
            wasInFreeClimb = false;
        }
        
        // Добавляем вызов корутины для сброса контроллера
        StartCoroutine(ResetControllerNextFrame());
    }
    
    private System.Collections.IEnumerator ResetControllerNextFrame()
    {
        yield return null; // Ждем один кадр
        
        // Включаем компоненты снова
        if (playerController != null)
        {
            playerController.enabled = true;
            
            // Убедимся, что Rigidbody не кинематический
            Rigidbody rb = playerController.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
        }
        
        if (playerInput != null)
        {
            playerInput.enabled = true;
        }
        
        Debug.Log("Компоненты контроллера игрока сброшены");
    }
    
    public void DisablePlayer()
    {
        if (playerController != null)
        {
            wasActive = playerController.enabled;
            playerController.enabled = false;
        }
        
        if (playerInput != null)
        {
            playerInput.enabled = false;
        }
        
        // Если нужно скрыть игрока
        if (hidePlayerMesh)
        {
            foreach (var renderer in playerRenderers)
            {
                renderer.enabled = false;
            }
        }
        
        // Останавливаем анимацию
        if (playerAnimator != null)
        {
            playerAnimator.speed = 0;
        }
        
        Debug.Log("Игрок деактивирован для катсцены");
    }
    
    public void EnablePlayer()
    {
        if (playerController != null)
        {
            // Всегда включаем контроллер независимо от предыдущего состояния
            playerController.enabled = true;
            
            // Убеждаемся, что Rigidbody не кинематический
            Rigidbody rb = playerController.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
        }
        
        if (playerInput != null)
        {
            playerInput.enabled = true;
        }
        
        // Всегда показываем игрока независимо от настройки hidePlayerMesh
        foreach (var renderer in playerRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
        
        // Восстанавливаем анимацию
        if (playerAnimator != null)
        {
            playerAnimator.speed = 1;
        }
        
        Debug.Log("Игрок активирован после катсцены (принудительно включены все компоненты)");
    }

    public void OnGreatSwordCastingComplete()
    {
        Debug.Log("Анимация GreatSwordCasting завершена");
        FinalQuestDeathHandler.OnGreatSwordCastingComplete();
    }
} 