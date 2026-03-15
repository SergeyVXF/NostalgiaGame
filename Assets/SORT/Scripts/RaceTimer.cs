using UnityEngine;
using TMPro;
using System.Collections;
using System;

public class RaceTimer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private float countdownTime = 5f;
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject timerVisualObject; // Объект, который будет отображаться во время таймера
    
    private bool isCountdownActive = false;
    private bool isRaceStarted = false;
    
    // События для уведомления
    public event Action OnCountdownStarted; // Новое событие для начала отсчета
    public event Action OnRaceStarted;
    
    public void SetTimerText(TextMeshProUGUI text)
    {
        timerText = text;
        Debug.Log("RaceTimer: Текст таймера установлен");
        // Скрываем текст при старте
        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }
    }
    
    private void Start()
    {
        if (timerText == null)
        {
            Debug.LogError("RaceTimer: Текст таймера не назначен!");
            return;
        }
        
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("RaceTimer: Игрок не найден!");
                return;
            }
        }
        
        // Отключаем управление игроком
        PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }
        
        // Скрываем текст при старте
        timerText.gameObject.SetActive(false);
    }
    
    public void StartCountdown()
    {
        if (!isCountdownActive)
        {
            // Показываем текст только когда начинается обратный отсчет
            if (timerText != null)
            {
                timerText.gameObject.SetActive(true);
            }
            
            // Уведомляем о начале обратного отсчета
            OnCountdownStarted?.Invoke();
            Debug.Log("RaceTimer: Начался обратный отсчет");
            
            StartCoroutine(Countdown());
        }
    }
    
    private IEnumerator Countdown()
    {
        isCountdownActive = true;
        float currentTime = countdownTime;
        
        // Показываем визуальный объект таймера, если он задан
        if (timerVisualObject != null)
        {
            timerVisualObject.SetActive(true);
            Debug.Log("RaceTimer: Визуальный объект таймера активирован");
        }
        
        while (currentTime > 0)
        {
            timerText.text = Mathf.Ceil(currentTime).ToString();
            currentTime -= Time.deltaTime;
            yield return null;
        }
        
        timerText.text = "GO!";
        yield return new WaitForSeconds(1f);
        
        // Включаем управление игроком
        PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }
        
        isRaceStarted = true;
        timerText.gameObject.SetActive(false);
        
        // Скрываем визуальный объект таймера
        if (timerVisualObject != null)
        {
            timerVisualObject.SetActive(false);
            Debug.Log("RaceTimer: Визуальный объект таймера деактивирован");
        }
        
        // Уведомляем о начале гонки
        OnRaceStarted?.Invoke();
        
        Debug.Log("RaceTimer: Гонка началась!");
    }
    
    public bool IsRaceStarted()
    {
        return isRaceStarted;
    }

    public void ResetTimer()
    {
        countdownTime = 3f;
        isRaceStarted = false;
        isCountdownActive = false;
        if (timerText != null)
        {
            timerText.text = "";
        }
        
        // Убедимся, что объект таймера скрыт при сбросе
        if (timerVisualObject != null)
        {
            timerVisualObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("FinishLine") && isRaceStarted)
        {
            isRaceStarted = false;
            isCountdownActive = false;
            Debug.Log("ИГРОК ПОБЕДИЛ");
            Debug.Log("RaceTimer: Игрок победил!");
            
            // Не вызываем OnPlayerVictory здесь, так как это делает FinishLine.cs
            // при обработке пересечения финишной линии игроком
        }
    }
} 