using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance { get; private set; }
    
    [SerializeField] private Transform finishLine;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI resultText;
    
    private float raceTimer = 0f;
    private bool isRacing = false;
    private bool raceFinished = false;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Update()
    {
        if (isRacing && !raceFinished)
        {
            raceTimer += Time.deltaTime;
            UpdateTimerDisplay();
        }
    }
    
    public void StartRace()
    {
        isRacing = true;
        raceFinished = false;
        raceTimer = 0f;
    }
    
    public void FinishRace(bool isPlayer)
    {
        if (!raceFinished)
        {
            raceFinished = true;
            isRacing = false;
            
            if (isPlayer)
            {
                resultText.text = "Вы победили!";
                resultText.color = Color.green;
            }
            else
            {
                resultText.text = "Вы проиграли!";
                resultText.color = Color.red;
            }
            
            resultText.gameObject.SetActive(true);
        }
    }
    
    private void UpdateTimerDisplay()
    {
        timerText.text = $"Время: {raceTimer:F2}";
    }
} 