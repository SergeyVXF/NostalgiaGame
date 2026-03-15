using UnityEngine;
using Invector.vCharacterController;

public class PlayerCutsceneController : MonoBehaviour
{
    private vThirdPersonController characterController;
    private static ICutsceneEvents.CutsceneEvent onCutsceneStartedHandler;
    private static ICutsceneEvents.CutsceneEvent onCutsceneEndedHandler;

    private void Awake()
    {
        characterController = GetComponent<vThirdPersonController>();
        
        // Создаем обработчики событий только один раз
        if (onCutsceneStartedHandler == null)
        {
            onCutsceneStartedHandler = new ICutsceneEvents.CutsceneEvent(DisablePlayerControl);
        }
        
        if (onCutsceneEndedHandler == null)
        {
            onCutsceneEndedHandler = new ICutsceneEvents.CutsceneEvent(EnablePlayerControl);
        }
        
        // Находим менеджер катсцен и подписываемся на события
        CutsceneManager manager = FindObjectOfType<CutsceneManager>();
        if (manager != null)
        {
            ICutsceneEvents events = manager as ICutsceneEvents;
            events.OnCutsceneStarted += onCutsceneStartedHandler;
            events.OnCutsceneEnded += onCutsceneEndedHandler;
        }
        else
        {
            Debug.LogWarning("CutsceneManager не найден в сцене! PlayerCutsceneController не будет работать.");
        }
    }
    
    private void OnDestroy()
    {
        // Отписываемся от событий при уничтожении объекта
        CutsceneManager manager = FindObjectOfType<CutsceneManager>();
        if (manager != null)
        {
            ICutsceneEvents events = manager as ICutsceneEvents;
            events.OnCutsceneStarted -= onCutsceneStartedHandler;
            events.OnCutsceneEnded -= onCutsceneEndedHandler;
        }
    }
    
    private void DisablePlayerControl(GameObject _)
    {
        if (characterController != null)
        {
            characterController.enabled = false;
            
            // Отключаем и компонент ввода, если он есть
            var input = GetComponent<vThirdPersonInput>();
            if (input != null)
            {
                input.enabled = false;
            }
        }
    }
    
    private void EnablePlayerControl(GameObject _)
    {
        if (characterController != null)
        {
            characterController.enabled = true;
            
            // Включаем и компонент ввода, если он есть
            var input = GetComponent<vThirdPersonInput>();
            if (input != null)
            {
                input.enabled = true;
            }
        }
    }
} 