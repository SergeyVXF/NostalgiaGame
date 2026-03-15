using UnityEngine;

// Интерфейс для определения событий катсцены
public interface ICutsceneEvents
{
    // Определяет тип делегата для события катсцены
    delegate void CutsceneEvent(GameObject cutsceneObject);
    
    // События для оповещения других скриптов
    event CutsceneEvent OnCutsceneStarted;
    event CutsceneEvent OnCutsceneEnded;
} 