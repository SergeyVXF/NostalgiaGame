using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DogPatrolUI : MonoBehaviour
{
    public static DogPatrolUI Instance;
    public CanvasGroup blackScreen;
    public TextMeshProUGUI catchText;
    public float fadeDuration = 0.5f;
    public float showTextTime = 1.5f;

    void Awake()
    {
        Instance = this;
        blackScreen.alpha = 0f;
        blackScreen.gameObject.SetActive(false);
        catchText.gameObject.SetActive(false);
    }

    public void ShowCatchScreen(System.Action onComplete)
    {
        StartCoroutine(CatchRoutine(onComplete));
    }

    private IEnumerator CatchRoutine(System.Action onComplete)
    {
        blackScreen.gameObject.SetActive(true);
        catchText.gameObject.SetActive(true);
        catchText.text = "Пойман";
        // Fade in
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            blackScreen.alpha = Mathf.Lerp(0, 1, t / fadeDuration);
            yield return null;
        }
        blackScreen.alpha = 1f;
        // Перемещаем игрока сразу после fade-in
        onComplete?.Invoke();
        yield return new WaitForSeconds(showTextTime);
        // Fade out
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            blackScreen.alpha = Mathf.Lerp(1, 0, t / fadeDuration);
            yield return null;
        }
        blackScreen.alpha = 0f;
        blackScreen.gameObject.SetActive(false);
        catchText.gameObject.SetActive(false);
    }
} 