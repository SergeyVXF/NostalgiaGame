using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CutsceneFadeManager : MonoBehaviour
{
    [Header("Настройки затемнения")]
    [Tooltip("Изображение для затемнения экрана")]
    [SerializeField] private Image fadeImage;
    
    [Tooltip("Цвет затемнения")]
    [SerializeField] private Color fadeColor = Color.black;
    
    [Tooltip("Длительность затемнения в секундах")]
    [SerializeField] private float fadeDuration = 1f;
    
    [Tooltip("Кривая анимации затемнения")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private CutsceneManager cutsceneManager;
    private bool isFading = false;
    
    private void Awake()
    {
        InitializeComponents();
    }
    
    private void InitializeComponents()
    {
        // Поиск CutsceneManager
        cutsceneManager = FindObjectOfType<CutsceneManager>();
        if (cutsceneManager == null)
        {
            Debug.LogError($"[CutsceneFadeManager] CutsceneManager не найден в сцене! Отключаю скрипт на объекте {gameObject.name}");
            enabled = false;
            return;
        }
        
        // Проверка наличия Image компонента
        if (fadeImage == null)
        {
            Debug.LogError($"[CutsceneFadeManager] Image компонент не назначен! Отключаю скрипт на объекте {gameObject.name}");
            enabled = false;
            return;
        }
        
        // Настройка начального состояния
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
    }
    
    /// <summary>
    /// Запускает катсцену с эффектом затемнения
    /// </summary>
    public void StartCutsceneWithFade(string cutsceneKey)
    {
        if (isFading)
        {
            Debug.LogWarning("[CutsceneFadeManager] Уже выполняется затемнение!");
            return;
        }
        
        StartCoroutine(FadeInAndStartCutscene(cutsceneKey));
    }
    
    /// <summary>
    /// Завершает катсцену с эффектом осветления
    /// </summary>
    public void EndCutsceneWithFade()
    {
        if (isFading)
        {
            Debug.LogWarning("[CutsceneFadeManager] Уже выполняется затемнение!");
            return;
        }
        
        StartCoroutine(FadeOutAndEndCutscene());
    }
    
    /// <summary>
    /// Выполняет затемнение экрана
    /// </summary>
    public void FadeIn()
    {
        if (isFading)
        {
            Debug.LogWarning("[CutsceneFadeManager] Уже выполняется затемнение!");
            return;
        }
        
        StartCoroutine(FadeCoroutine(0f, 1f));
    }
    
    /// <summary>
    /// Выполняет осветление экрана
    /// </summary>
    public void FadeOut()
    {
        if (isFading)
        {
            Debug.LogWarning("[CutsceneFadeManager] Уже выполняется затемнение!");
            return;
        }
        
        StartCoroutine(FadeCoroutine(1f, 0f));
    }
    
    private IEnumerator FadeInAndStartCutscene(string cutsceneKey)
    {
        // Затемняем экран
        yield return StartCoroutine(FadeCoroutine(0f, 1f));
        
        // Запускаем катсцену
        cutsceneManager.StartCutscene(cutsceneKey);
        
        // Осветляем экран
        yield return StartCoroutine(FadeCoroutine(1f, 0f));
    }
    
    private IEnumerator FadeOutAndEndCutscene()
    {
        // Если экран уже затемнен, просто завершаем катсцену
        if (fadeImage.color.a >= 0.99f)
        {
            // Завершаем катсцену
            cutsceneManager.EndCutscene();
            
            // Осветляем экран
            yield return StartCoroutine(FadeCoroutine(1f, 0f));
            yield break;
        }
        
        // Если экран не затемнен, делаем полный цикл
        // Затемняем экран
        yield return StartCoroutine(FadeCoroutine(0f, 1f));
        
        // Ждем небольшой момент, чтобы затемнение было видно
        yield return new WaitForSeconds(0.1f);
        
        // Завершаем катсцену
        cutsceneManager.EndCutscene();
        
        // Осветляем экран
        yield return StartCoroutine(FadeCoroutine(1f, 0f));
    }
    
    private IEnumerator FadeCoroutine(float startAlpha, float targetAlpha)
    {
        isFading = true;
        float elapsedTime = 0f;
        
        Color startColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, startAlpha);
        Color targetColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, targetAlpha);
        
        fadeImage.color = startColor;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / fadeDuration;
            float curveValue = fadeCurve.Evaluate(normalizedTime);
            
            fadeImage.color = Color.Lerp(startColor, targetColor, curveValue);
            yield return null;
        }
        
        fadeImage.color = targetColor;
        isFading = false;
    }
    
    /// <summary>
    /// Устанавливает цвет затемнения
    /// </summary>
    public void SetFadeColor(Color color)
    {
        fadeColor = color;
        if (fadeImage != null)
        {
            fadeImage.color = new Color(color.r, color.g, color.b, fadeImage.color.a);
        }
    }
    
    /// <summary>
    /// Устанавливает длительность затемнения
    /// </summary>
    public void SetFadeDuration(float duration)
    {
        fadeDuration = Mathf.Max(0.1f, duration);
    }
} 