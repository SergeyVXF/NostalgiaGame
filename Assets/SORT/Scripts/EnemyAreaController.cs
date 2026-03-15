using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnemyAreaController : MonoBehaviour
{
    public List<EnemySpawner> spawners;
    public Slider progressBar;
    public Text progressText;
    public GameObject[] milestoneMessages; // 0:25%, 1:50%, 2:75%, 3:100%

    [Header("Настройка текста прогресса")]
    [Tooltip("Шаблон текста. Используйте {0} для процентов. Пример: 'Район очищен на {0}%' или 'Прогресс: {0}%' ")]
    public string progressTextTemplate = "Район отчищен на {0}%";

    [Header("Градиент цвета текста прогресса")]
    [Tooltip("Цвета для градиента. Первый — для 0%, последний — для 100%. Можно добавить промежуточные.")]
    public Color[] progressColors = new Color[] { Color.red, Color.yellow, Color.green };

    private int clearedSpawners = 0;
    private int lastMilestone = 0;

    void Start()
    {
        foreach (var spawner in spawners)
        {
            spawner.OnCleared += OnSpawnerCleared;
            spawner.SpawnEnemies();
        }
        UpdateUI();
    }

    void OnSpawnerCleared(EnemySpawner spawner)
    {
        clearedSpawners++;
        UpdateUI();
        CheckMilestones();
    }

    void UpdateUI()
    {
        float percent = (float)clearedSpawners / spawners.Count;
        progressBar.value = percent;
        int percentInt = Mathf.CeilToInt(percent * 100);
        string template = string.IsNullOrEmpty(progressTextTemplate) ? "Район отчищен на {0}%" : progressTextTemplate;
        progressText.text = string.Format(template, percentInt);
        // Пользовательский градиент
        if (progressColors != null && progressColors.Length > 1)
        {
            float scaled = percent * (progressColors.Length - 1);
            int idx = Mathf.FloorToInt(scaled);
            int nextIdx = Mathf.Clamp(idx + 1, 0, progressColors.Length - 1);
            float t = scaled - idx;
            progressText.color = Color.Lerp(progressColors[idx], progressColors[nextIdx], t);
        }
        else if (progressColors != null && progressColors.Length == 1)
        {
            progressText.color = progressColors[0];
        }
        else
        {
            progressText.color = Color.white;
        }
    }

    void CheckMilestones()
    {
        float percent = (float)clearedSpawners / spawners.Count;
        int milestone = Mathf.FloorToInt(percent * 4); // 0,1,2,3,4
        
        Debug.Log($"[EnemyAreaController] 🔍 Проверка milestone:");
        Debug.Log($"  📊 Очищено спавнеров: {clearedSpawners}/{spawners.Count}");
        Debug.Log($"  📈 Процент: {percent:F2} ({percent * 100:F0}%)");
        Debug.Log($"  🎯 Текущий milestone: {milestone}");
        Debug.Log($"  📝 Последний milestone: {lastMilestone}");
        
        if (milestone > lastMilestone)
        {
            lastMilestone = milestone;
            Debug.Log($"[EnemyAreaController] ✅ Новый milestone достигнут: {milestone}");
            
            if (milestone > 0 && milestone <= milestoneMessages.Length)
            {
                Debug.Log($"[EnemyAreaController] 🎬 Показываю milestone message [{milestone - 1}]: {milestoneMessages[milestone - 1].name}");
                
                var msg = milestoneMessages[milestone - 1].GetComponent<MilestoneMessageUI>();
                if (msg != null)
                {
                    Debug.Log($"[EnemyAreaController] ✅ MilestoneMessageUI найден, вызываю ShowMessage()");
                    msg.ShowMessage();
                }
                else
                {
                    Debug.Log($"[EnemyAreaController] ⚠️ MilestoneMessageUI не найден, активирую GameObject");
                    milestoneMessages[milestone - 1].SetActive(true); // fallback
                }
                // TODO: Выдать бонус игроку (заглушка)
                // GiveBonus(milestone);
            }
            else
            {
                Debug.LogWarning($"[EnemyAreaController] ⚠️ Milestone {milestone} вне диапазона массива (1-{milestoneMessages.Length})");
            }
        }
        else
        {
            Debug.Log($"[EnemyAreaController] ⏭️ Milestone {milestone} не больше последнего {lastMilestone}");
        }
    }
} 