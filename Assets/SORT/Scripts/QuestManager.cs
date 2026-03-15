using UnityEngine;
using System.Collections;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }
    
    [SerializeField] private Transform playerStartPosition;
    [SerializeField] private Transform raceStartPosition;
    [SerializeField] private Transform finishPosition;
    [SerializeField] private PlayerRaceController playerController;
    [SerializeField] private AIOpponent aiOpponent;
    
    private bool isQuestActive = false;
    private bool isRaceStarted = false;

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

    public void StartQuest()
    {
        if (!isQuestActive)
        {
            isQuestActive = true;
            StartCoroutine(TeleportPlayerToRaceStart());
        }
    }

    private IEnumerator TeleportPlayerToRaceStart()
    {
        // Телепортируем игрока на стартовую позицию
        if (playerController != null)
        {
            playerController.transform.position = raceStartPosition.position;
            playerController.transform.rotation = raceStartPosition.rotation;
        }
        
        yield return new WaitForSeconds(1f);
        StartRace();
    }

    public void StartRace()
    {
        if (!isRaceStarted)
        {
            isRaceStarted = true;
            RaceManager.Instance.StartRace();
            
            // Запускаем гонку для игрока и ИИ
            if (playerController != null)
                playerController.StartRacing();
                
            if (aiOpponent != null)
                aiOpponent.StartRacing();
        }
    }
} 