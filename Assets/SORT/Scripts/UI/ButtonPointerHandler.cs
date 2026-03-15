using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonPointerHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Image buttonImage;
    private string buttonText;

    public void Initialize(Image image, string text)
    {
        buttonImage = image;
        buttonText = text;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"Наведение на кнопку: {buttonText}");
        if (buttonImage != null)
        {
            buttonImage.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Светлее
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"Уход с кнопки: {buttonText}");
        if (buttonImage != null)
        {
            buttonImage.color = new Color(0.1f, 0.1f, 0.1f, 1f); // Обычный цвет
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"Нажатие на кнопку: {buttonText}");
        if (buttonImage != null)
        {
            buttonImage.color = new Color(0.05f, 0.05f, 0.05f, 1f); // Темнее при нажатии
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log($"Отпускание кнопки: {buttonText}");
        if (buttonImage != null)
        {
            buttonImage.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Возвращаем к подсветке
        }
    }
}