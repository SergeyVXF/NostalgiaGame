using UnityEngine;

public class FinalQuestDistanceFailHandler : MonoBehaviour
{
    [Header("Ссылки на объекты")]
    public GameObject questPrefab; // Префаб квеста (всё, что нужно сбрасывать)
    public Transform questSpawnPoint; // Точка спавна квеста (можно оставить пустым, если спавнить в (0,0,0))
    public GameObject finalEnemyAI03; // Враг, за которым следим (будет обновляться при каждом запуске квеста)
    public GameObject finalQuestTrigger01; // Точка возврата
    public GameObject player; // Игрок

    [Header("Настройки")]
    public float maxDistance = 5f; // Максимальная допустимая дистанция
    public bool questActive = false; // Активен ли квест (можно включать/выключать)
    [Header("Дистанция возврата игрока от триггера (метры)")]
    public float returnDistance = 2f; // Дистанция возврата от триггера

    private GameObject questInstance; // Текущий экземпляр квеста

    void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");
        if (finalQuestTrigger01 == null)
            finalQuestTrigger01 = GameObject.Find("FinalQuest_Trigger_01");
        StartQuest();
    }

    void Update()
    {
        if (!questActive) return;
        if (finalEnemyAI03 == null || player == null || finalQuestTrigger01 == null) return;
        if (!finalEnemyAI03.activeInHierarchy) return;

        float distance = Vector3.Distance(player.transform.position, finalEnemyAI03.transform.position);
        if (distance > maxDistance)
        {
            Debug.Log($"FinalQuest: Игрок слишком далеко от Final_EnemyAI_03 ({distance:F2} м). Квест сброшен!");
            ResetQuestAndReturnPlayer();
        }
    }

    public void StartQuest()
    {
        // Удаляем старый экземпляр, если есть
        if (questInstance != null)
            Destroy(questInstance);
        // Создаём новый экземпляр квеста
        Vector3 spawnPos = questSpawnPoint != null ? questSpawnPoint.position : Vector3.zero;
        questInstance = Instantiate(questPrefab, spawnPos, Quaternion.identity);
        // Находим врага и триггер внутри нового квеста
        finalEnemyAI03 = GameObject.Find("Final_EnemyAI_03");
        finalQuestTrigger01 = GameObject.Find("FinalQuest_Trigger_01");
        questActive = true;
    }

    void ResetQuestAndReturnPlayer()
    {
        // Удаляем текущий квестовый экземпляр и создаём новый
        if (questInstance != null)
            Destroy(questInstance);
        Vector3 spawnPos = questSpawnPoint != null ? questSpawnPoint.position : Vector3.zero;
        questInstance = Instantiate(questPrefab, spawnPos, Quaternion.identity);
        // Обновляем ссылки на врага и триггер
        finalEnemyAI03 = GameObject.Find("Final_EnemyAI_03");
        finalQuestTrigger01 = GameObject.Find("FinalQuest_Trigger_01");

        // Телепортируем игрока на 2 метра от триггера по направлению от врага (если враг найден)
        Vector3 targetPos = finalQuestTrigger01.transform.position;
        if (finalEnemyAI03 != null)
        {
            Vector3 dir = (targetPos - finalEnemyAI03.transform.position).normalized;
            if (dir == Vector3.zero) dir = Vector3.right;
            targetPos += dir * returnDistance;
        }
        else
        {
            targetPos += Vector3.right * returnDistance;
        }
        player.transform.position = targetPos;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        // Автоматически запускаем квест заново
        StartQuest();
    }
} 