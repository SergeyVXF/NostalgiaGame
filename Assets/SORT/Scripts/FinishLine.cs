using UnityEngine;
using TMPro;
using System.Collections;

public class FinishLine : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI victoryText;
    [SerializeField] private float displayTime = 5f;
    [SerializeField] private GameObject player;
    [SerializeField] private Vector3 returnPosition; // Позиция для возврата игрока
    
    private bool hasFinished = false;
    private DialogueSystem dialogueSystem;
    private RaceTimer raceTimer;
    
    private void Awake()
    {
        // Скрываем текст при инициализации
        if (victoryText != null)
        {
            victoryText.gameObject.SetActive(false);
        }
        
        // Находим необходимые компоненты
        dialogueSystem = FindObjectOfType<DialogueSystem>();
        if (dialogueSystem == null)
        {
            Debug.LogError("FinishLine: DialogueSystem не найден!");
        }
        
        raceTimer = FindObjectOfType<RaceTimer>();
        if (raceTimer == null)
        {
            Debug.LogError("FinishLine: RaceTimer не найден!");
        }
        else
        {
            // Подписываемся на события
            raceTimer.OnCountdownStarted += OnCountdownStart;
            raceTimer.OnRaceStarted += OnRaceStart;
        }
        
        // Отключаем весь объект финишной линии при старте
        gameObject.SetActive(false);
    }
    
    private void OnDestroy()
    {
        // Отписываемся от событий при уничтожении объекта
        if (raceTimer != null)
        {
            raceTimer.OnCountdownStarted -= OnCountdownStart;
            raceTimer.OnRaceStarted -= OnRaceStart;
        }
    }
    
    private void OnCountdownStart()
    {
        // Включаем финишную линию при начале обратного отсчета
        gameObject.SetActive(true);
        Debug.Log("FinishLine: Финишная линия активирована (начало отсчета)");
    }
    
    private void OnRaceStart()
    {
        Debug.Log("FinishLine: Гонка началась");
        // Сбрасываем флаг завершения при начале новой гонки
        hasFinished = false;
    }
    
    private void Start()
    {
        if (victoryText == null)
        {
            Debug.LogError("FinishLine: Текст победы не назначен!");
            return;
        }
        
        // Убеждаемся, что текст скрыт
        victoryText.gameObject.SetActive(false);
        
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("FinishLine: Игрок не найден!");
                return;
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!hasFinished && other.CompareTag("Player"))
        {
            Debug.Log("FinishLine: Игрок пересек финишную линию");
            hasFinished = true;
            ShowVictory();
        }
    }
    
    private void ShowVictory()
    {
        if (victoryText != null)
        {
            victoryText.text = "ПОБЕДА!";
            victoryText.gameObject.SetActive(true);
            Debug.Log("FinishLine: Победа!");
            StartCoroutine(HideVictoryAndTeleport());
        }
    }
    
    private IEnumerator HideVictoryAndTeleport()
    {
        // Ждем displayTime секунд
        yield return new WaitForSeconds(displayTime);
        
        // Скрываем текст победы
        if (victoryText != null)
        {
            victoryText.gameObject.SetActive(false);
        }
        
        // Если есть DialogueSystem, используем его для обработки победы игрока
        if (dialogueSystem != null)
        {
            // Вызываем метод победы, который телепортирует игрока и деактивирует AI и финишную линию
            dialogueSystem.OnPlayerVictory();
            Debug.Log("FinishLine: Вызван метод OnPlayerVictory для телепортации игрока к NPC");
        }
        else
        {
            Debug.LogError("FinishLine: DialogueSystem не найден для телепортации!");
        }
    }
} 