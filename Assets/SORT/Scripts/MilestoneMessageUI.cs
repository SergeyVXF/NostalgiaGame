using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class MilestoneMessageUI : MonoBehaviour
{
    public float showTime = 3f;
    public float fadeTime = 1f;

    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void ShowMessage()
    {
        Debug.Log($"[MilestoneMessageUI] 🎬 ShowMessage() вызван для: {gameObject.name}");
        
        if (fadeRoutine != null)
        {
            Debug.Log($"[MilestoneMessageUI] ⏹️ Останавливаю предыдущую корутину");
            StopCoroutine(fadeRoutine);
        }
        
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        Debug.Log($"[MilestoneMessageUI] ✅ Сообщение активировано: {gameObject.name}");
        
        fadeRoutine = StartCoroutine(FadeOutRoutine());
        Debug.Log($"[MilestoneMessageUI] 🕐 Запущена корутина FadeOutRoutine");
    }

    private IEnumerator FadeOutRoutine()
    {
        yield return new WaitForSeconds(showTime);

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
} 