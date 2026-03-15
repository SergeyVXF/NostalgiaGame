using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
#if TMP_PRESENT
using TMPro;
#endif

namespace AG.Collectibles
{
    /// <summary>
    /// Утилита для создания тестовых UI-префабов прямо из редактора Unity.
    /// Используйте контекстное меню (правый клик на компоненте).
    /// </summary>
    public class TestContentCreator : MonoBehaviour
    {
        [Header("Настройки создания")]
        [SerializeField] private bool includeTextArea = true;
        
        [ContextMenu("Создать тестовый контент (Image + Text)")]
        public void CreateTestContentPrefab()
        {
            // Создаем корневой объект
            GameObject root = new GameObject("TestContentPrefab");
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(400, 300);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            
            // Создаем фоновое изображение
            GameObject imageObj = new GameObject("Background");
            imageObj.transform.SetParent(root.transform, false);
            
            Image image = imageObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            
            RectTransform imageRect = imageObj.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            
            // Добавляем текст
            AddTextArea(root);
            
            Debug.Log("[TestContentCreator] Тестовый контент создан! Сохраните его как префаб.");
        }
        
        [ContextMenu("Создать простой Image префаб")]
        public void CreateSimpleImagePrefab()
        {
            // Создаем корневой объект
            GameObject root = new GameObject("SimpleImagePrefab");
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(400, 300);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            
            // Создаем изображение
            GameObject imageObj = new GameObject("Image");
            imageObj.transform.SetParent(root.transform, false);
            
            Image image = imageObj.AddComponent<Image>();
            image.color = Color.white;
            
            RectTransform imageRect = imageObj.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            
            // Добавляем текст если включено
            if (includeTextArea)
            {
                AddTextArea(root);
            }
            
            Debug.Log("[TestContentCreator] Простой Image префаб создан! Сохраните его как префаб.");
        }
        
        #if TMP_PRESENT
        [ContextMenu("Создать TextMeshPro префаб")]
        public void CreateTextMeshProPrefab()
        {
            // Создаем корневой объект
            GameObject root = new GameObject("TextMeshProPrefab");
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(400, 100);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            
            // Создаем TextMeshPro
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(root.transform, false);
            
            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = "Текст TextMeshPro";
            tmpText.fontSize = 24;
            tmpText.color = Color.white;
            tmpText.alignment = TextAlignmentOptions.Center;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            Debug.Log("[TestContentCreator] TextMeshPro префаб создан! Сохраните его как префаб.");
        }
        #endif
        
        [ContextMenu("Создать Video префаб")]
        public void CreateVideoPrefab()
        {
            // Создаем корневой объект для видео
            GameObject videoRoot = new GameObject("VideoPrefab");
            
            // Добавляем RectTransform
            RectTransform rootRect = videoRoot.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(640, 360); // 16:9 соотношение
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            
            // Создаем RawImage для отображения видео
            GameObject rawImageObj = new GameObject("VideoDisplay");
            rawImageObj.transform.SetParent(videoRoot.transform, false);
            
            RawImage rawImage = rawImageObj.AddComponent<RawImage>();
            rawImage.color = Color.white;
            
            RectTransform imageRect = rawImageObj.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            
            // Добавляем VideoPlayer
            VideoPlayer videoPlayer = videoRoot.AddComponent<VideoPlayer>();
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = new RenderTexture(640, 360, 0);
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.waitForFirstFrame = true;
            
            // Привязываем RenderTexture к RawImage
            rawImage.texture = videoPlayer.targetTexture;
            
            Debug.Log("[TestContentCreator] Video префаб создан! Сохраните его как префаб.");
            Debug.Log("[TestContentCreator] Не забудьте назначить видеофайл в VideoPlayer компоненте.");
        }
        
        private void AddTextArea(GameObject parent)
        {
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(parent.transform, false);
            
            Text text = textObj.AddComponent<Text>();
            text.text = "Тестовый текст\nВторая строка";
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.1f, 0.1f);
            textRect.anchorMax = new Vector2(0.9f, 0.9f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }
    }
} 