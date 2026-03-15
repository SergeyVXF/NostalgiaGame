using UnityEngine;
using UnityEngine.Playables;

public class CutsceneEndTrigger : MonoBehaviour
{
    [Tooltip("Если true, катсцена автоматически завершится, когда закончится таймлайн")]
    [SerializeField] private bool autoEndOnComplete = true;
    
    [Tooltip("PlayableDirector компонент, содержащий таймлайн")]
    [SerializeField] private PlayableDirector director;
    
    [Tooltip("Время до конца таймлайна, когда начнется затемнение")]
    [SerializeField] private float fadeStartTime = 1f;
    
    private CutsceneFadeManager fadeManager;
    private bool isFading = false;
    
    private void Awake()
    {
        // Если не указан PlayableDirector, пытаемся найти его на этом объекте
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
        
        if (director == null)
        {
            Debug.LogError("PlayableDirector не найден! Отключаю скрипт.", this);
            enabled = false;
            return;
        }
        
        // Находим менеджер затемнения
        fadeManager = FindObjectOfType<CutsceneFadeManager>();
        if (fadeManager == null)
        {
            Debug.LogError("CutsceneFadeManager не найден в сцене!");
            enabled = false;
            return;
        }
        
        if (autoEndOnComplete)
        {
            // Подписываемся на событие завершения таймлайна
            director.stopped += OnPlayableDirectorStopped;
        }
    }
    
    private void Update()
    {
        if (!autoEndOnComplete || isFading) return;
        
        // Проверяем, близко ли таймлайн к концу
        if (director.time >= director.duration - fadeStartTime)
        {
            isFading = true;
            // Завершаем катсцену с затемнением
            EndCutscene();
        }
    }
    
    private void OnPlayableDirectorStopped(PlayableDirector _)
    {
        // Этот метод больше не нужен, так как мы завершаем катсцену в Update
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события при уничтожении объекта
        if (director != null && autoEndOnComplete)
        {
            director.stopped -= OnPlayableDirectorStopped;
        }
    }
    
    // Публичный метод для вызова из сигналов таймлайна или других скриптов
    public void EndCutscene()
    {
        fadeManager.EndCutsceneWithFade();
    }
} 