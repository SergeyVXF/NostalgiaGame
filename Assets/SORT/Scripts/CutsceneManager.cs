using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Реализуем интерфейс ICutsceneEvents
public class CutsceneManager : MonoBehaviour, ICutsceneEvents
{
    // Делегат Singleton для того чтобы можно обращаться к CutsceneManager через CutsceneManager.Instance.метод()
    public static CutsceneManager Instance;

    // События для оповещения других скриптов о запуске и завершении катсцены
    // Используем тип делегата из интерфейса
    public static event ICutsceneEvents.CutsceneEvent OnCutsceneStarted;
    public static event ICutsceneEvents.CutsceneEvent OnCutsceneEnded;

    // Этот лист содержит элементы, в которых есть Key и Value которые в дальнейшем будут перенесены в Dictionary "cutsceneDataBase"
    // Именно из инс нужно для того чтобы было визуально в инспекторе
    [SerializeField] private List<CutsceneStruct> cutscenes = new List<CutsceneStruct>();

    // Этот словарь необходим для быстрого доступа к кадой катсцене, к в дльнейшем будет перенесен весь контент из списка
    // Так как для Dictionary невозможно в инспекторе назначить а нам до нужно именно так - CutsceneManager.cutsceneDataBase["имя Вашей катсцены"]
    public static Dictionary<string, GameObject> cutsceneDataBase = new Dictionary<string, GameObject>();

    // Ссылка в этой переменной хранит активированную в данный момент, если не одной не активировано - тут будет null
    public static GameObject activeCutscene;

    private void Awake()
    {
        // Паттер Singleton
        Instance = this;

        // Вызываем метод инициализации базы данных в Dictionary
        InitializeCutsceneDataBase();

        // Перебираем все катсцены и выключаем их (чтобы они случйно все не включались при старте)
        foreach (var cutscene in cutsceneDataBase)
        {
            cutscene.Value.SetActive(false);
        }
    }

    // Метод в котором мы заполняем Dictionary cutsceneDataBase
    private void InitializeCutsceneDataBase()
    {
        // Чтобы избавиться от ошибок всегда надо писе чистку
        cutsceneDataBase.Clear();

        // Заполняем cutsceneDataBase ключами и значениями которые мы указали в листе cutscenes
        for (int i = 0; i < cutscenes.Count; i++)
        {           
            cutsceneDataBase.Add(cutscenes[i].cutsceneKey, cutscenes[i].cutsceneObject);
        }
    }

    // Метод для активации катсцены по ключу
    public void StartCutscene(string cutsceneKey)
    {
        Debug.Log($"[CutsceneManager] Попытка запустить катсцену {cutsceneKey}");

        // Если cutsceneDataBase не содержит элемента с cutsceneKey то выбрасыаем об этом в консоль и не запускаем этот дальнейший метод
        if (!cutsceneDataBase.ContainsKey(cutsceneKey)) 
        {
            Debug.LogError($"Катсцены c ключом \"{cutsceneKey}\" нету в cutsceneDataBase");
            return;
        } 

        // Для катсцены CutScene_02 проверяем наличие предмета Tempo
        if (cutsceneKey == "CutScene_02")
        {
            Debug.Log($"[CutsceneManager] Запрошена катсцена {cutsceneKey}, требуется проверка предмета Tempo");
            DEDQuest dedQuest = FindObjectOfType<DEDQuest>();
            if (dedQuest != null)
            {
                bool hasItem = dedQuest.HasRequiredItem();
                Debug.Log($"[CutsceneManager] DEDQuest найден, hasRequiredItem = {hasItem}");
                
                if (!hasItem)
                {
                    Debug.LogWarning($"[CutsceneManager] Попытка запустить катсцену {cutsceneKey}, но у игрока нет предмета Tempo!");
                    return; // Не запускаем катсцену, если нет предмета
                }
                else
                {
                    Debug.Log($"[CutsceneManager] Запуск катсцены {cutsceneKey} - предмет Tempo найден!");
                }
            }
            else
            {
                Debug.LogError("[CutsceneManager] DEDQuest не найден в сцене! Не могу проверить наличие предмета");
            }
        }

        // Если сейчас проигрывается катсцена и ты пытаешся включить с тем самым ключом её же то не нужно запускать повторного старта
        if (activeCutscene != null)
        {
            if (activeCutscene == cutsceneDataBase[cutsceneKey])
            {
                return;
            }
        }

        // Запоминаем активную катсцену
        activeCutscene = cutsceneDataBase[cutsceneKey];

        // Выключаем все катсцены
        foreach (var cutscene in cutsceneDataBase)
        {
            cutscene.Value.SetActive(false);
        }

        // Включаем ту катсцены которая нужна именно
        cutsceneDataBase[cutsceneKey].SetActive(true);
        
        // Вызываем событие начала катсцены
        OnCutsceneStarted?.Invoke(activeCutscene);
    }

    // Метод который выключает текущую катсцену
    public void EndCutscene()
    {
        if (activeCutscene != null)
        {
            GameObject lastActiveCutscene = activeCutscene;
            activeCutscene.SetActive(false);
            activeCutscene = null;
            
            // Проверка именно для CutScene_02 и вывод дополнительной отладочной информации
            if (lastActiveCutscene.name.Contains("CutScene_02"))
            {
                Debug.Log("[CutsceneManager] Завершена CutScene_02, проверка состояния игрока...");
                
                // Находим контроллер игрока
                var playerController = GameObject.FindObjectOfType<Invector.vCharacterController.vThirdPersonController>();
                if (playerController != null)
                {
                    Debug.Log($"[CutsceneManager] Состояние контроллера игрока: enabled={playerController.enabled}, isKinematic={playerController.GetComponent<Rigidbody>().isKinematic}");
                }
                
                // Находим компонент FreeClimb
                var freeClimb = GameObject.FindObjectOfType<Invector.vCharacterController.vActions.vFreeClimb>();
                if (freeClimb != null)
                {
                    Debug.Log($"[CutsceneManager] Компонент FreeClimb найден. Отправляем событие завершения катсцены...");
                }
            }
            
            // Вызываем событие окончания катсцены
            OnCutsceneEnded?.Invoke(lastActiveCutscene);
        }
    }
    
    // Явная реализация событий из интерфейса (теперь типы совместимы)
    event ICutsceneEvents.CutsceneEvent ICutsceneEvents.OnCutsceneStarted
    {
        add { OnCutsceneStarted += value; }
        remove { OnCutsceneStarted -= value; }
    }
    
    event ICutsceneEvents.CutsceneEvent ICutsceneEvents.OnCutsceneEnded
    {
        add { OnCutsceneEnded += value; }
        remove { OnCutsceneEnded -= value; }
    }
}

// Структура которая для листа, чтобы можно было визуально при создании и Key и Value в Dictionary cutsceneDataBase
[System.Serializable]
public struct CutsceneStruct
{
    public string cutsceneKey;
    public GameObject cutsceneObject;
}