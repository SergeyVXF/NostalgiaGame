using UnityEngine;
using TMPro;

public class NPCDialogueTrigger : MonoBehaviour
{
    [SerializeField] private DialogueSystem dialogueSystem;
    [SerializeField] private string[] dialogueLines;
    [SerializeField] private GameObject interactionIndicator;
    [SerializeField] private TextMeshProUGUI interactionText;
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private float maxDialogueDistance = 10f; // Максимальное расстояние для продолжения диалога
    [SerializeField] private bool canRepeatDialogue = true;
    [SerializeField] private float indicatorPulseSpeed = 2f;
    [SerializeField] private float indicatorPulseScale = 0.2f;
    
    private bool isInRange = false;
    private bool hasStartedDialogue = false;
    private bool isDialogueActive = false;
    private Transform playerTransform;
    private Vector3 originalScale;
    
    private void Start()
    {
        Debug.Log($"NPCDialogueTrigger: Start на объекте {gameObject.name}");
        
        // Автоматически находим DialogueSystem если не назначен
        if (dialogueSystem == null)
        {
            dialogueSystem = FindObjectOfType<DialogueSystem>();
            if (dialogueSystem == null)
            {
                Debug.LogError("DialogueSystem не найден в сцене! Убедитесь, что он существует.");
                return;
            }
            Debug.Log("DialogueSystem найден автоматически");
        }
        else
        {
            Debug.Log("DialogueSystem назначен через инспектор");
        }
        
        // Проверяем наличие коллайдера
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            Debug.LogError($"На объекте {gameObject.name} отсутствует компонент Collider!");
            return;
        }
        Debug.Log($"На объекте {gameObject.name} найден коллайдер типа {collider.GetType().Name}");
        
        if (interactionIndicator != null)
        {
            interactionIndicator.SetActive(false);
            originalScale = interactionIndicator.transform.localScale;
            Debug.Log("Индикатор взаимодействия настроен");
        }
        else
        {
            Debug.LogWarning("Индикатор взаимодействия не назначен");
        }
            
        if (interactionText != null)
        {
            interactionText.text = "Нажмите E для разговора";
            Debug.Log("Текст взаимодействия настроен");
        }
        else
        {
            Debug.LogWarning("Текст взаимодействия не назначен");
        }
    }
    
    private void Update()
    {
        if (isDialogueActive && playerTransform != null)
        {
            // Проверяем расстояние до игрока
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            if (distance > maxDialogueDistance)
            {
                Debug.Log($"Игрок слишком далеко (расстояние: {distance:F1}), закрываем диалог");
                EndDialogue();
                return;
            }
        }

        // Проверяем нажатие клавиши E независимо от зоны
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("Клавиша E нажата");
            if (isDialogueActive)
            {
                // Если диалог активен, продолжаем его
                if (dialogueSystem != null)
                {
                    dialogueSystem.ShowNextLine();
                }
                else
                {
                    Debug.LogError("DialogueSystem не найден!");
                    EndDialogue();
                }
            }
            else if (isInRange && (!hasStartedDialogue || canRepeatDialogue))
            {
                // Если диалог не активен и мы в зоне, начинаем новый диалог
                if (dialogueLines != null && dialogueLines.Length > 0)
                {
                    Debug.Log($"Запуск диалога. Количество строк: {dialogueLines.Length}");
                    dialogueSystem.StartDialogue(dialogueLines);
                    hasStartedDialogue = true;
                    isDialogueActive = true;
                }
                else
                {
                    Debug.LogWarning("Нет строк диалога для показа!");
                }
            }
        }

        // Анимация индикатора
        if ((isInRange || isDialogueActive) && interactionIndicator != null)
        {
            interactionIndicator.SetActive(true);
            float pulse = Mathf.Sin(Time.time * indicatorPulseSpeed) * indicatorPulseScale;
            interactionIndicator.transform.localScale = originalScale * (1f + pulse);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"OnTriggerEnter: {other.gameObject.name} с тегом {other.tag}");
        if (other.CompareTag("Player"))
        {
            Debug.Log("Игрок вошел в зону взаимодействия");
            isInRange = true;
            playerTransform = other.transform;
            
            if (interactionIndicator != null)
            {
                interactionIndicator.SetActive(true);
                interactionIndicator.transform.localScale = originalScale;
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        Debug.Log($"OnTriggerExit: {other.gameObject.name} с тегом {other.tag}");
        if (other.CompareTag("Player"))
        {
            Debug.Log("Игрок вышел из зоны взаимодействия");
            isInRange = false;
            
            // Не скрываем индикатор, если диалог активен
            if (!isDialogueActive && interactionIndicator != null)
                interactionIndicator.SetActive(false);
        }
    }
    
    private void EndDialogue()
    {
        isDialogueActive = false;
        dialogueSystem.EndDialogue();
        
        if (interactionIndicator != null)
            interactionIndicator.SetActive(false);
    }
    
    private void OnDrawGizmosSelected()
    {
        // Визуализация зоны взаимодействия в редакторе
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
        
        // Визуализация максимального расстояния для диалога
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxDialogueDistance);
    }
} 