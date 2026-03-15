using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Video;

namespace AG.Collectibles
{
    /// <summary>
    /// Показывает любой UI-префаб как полноэкранное модальное окно.
    /// Поддерживает затемнение фона, автозакрытие и опциональную музыку.
    /// Контентом может быть любой префаб (изображение, анимированный UI, VideoPlayer и т.п.).
    /// </summary>
    public class ContentModalManager : MonoBehaviour
    {
        private static ContentModalManager _instance;
        public static ContentModalManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("ContentModalManager");
                    _instance = go.AddComponent<ContentModalManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Визуальные настройки")]
        [SerializeField] private Color overlayColor = new Color(0, 0, 0, 0.6f);
        [SerializeField] private bool fadeOverlay = true;
        [SerializeField] private float fadeDuration = 0.15f;
        
        [Header("Анимация контента")]
        [SerializeField] private bool scaleInContent = true;
        [SerializeField] private float scaleInDuration = 0.3f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Управление закрытием")]
        [SerializeField] private bool closeOnEscape = true;
        [SerializeField] private bool closeOnBackgroundClick = true;

        private Canvas _canvas;
        private CanvasGroup _overlayCanvasGroup;
        private Image _overlayImage;
        private RectTransform _contentRoot;
        private GameObject _spawnedContent;
        private AudioSource _musicSource;
        private bool _isOpen;
        private float _autoCloseAfterSeconds;
        private float _autoCloseTimer;
        private bool _pauseGame;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureEventSystem();
            BuildCanvasIfNeeded();
        }

        private void Update()
        {
            if (!_isOpen) return;

            if (closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (_autoCloseAfterSeconds > 0f)
            {
                _autoCloseTimer += Time.unscaledDeltaTime;
                if (_autoCloseTimer >= _autoCloseAfterSeconds)
                {
                    Close();
                }
            }
        }

        /// <summary>
        /// Показать модальное окно с контентом указанного префаба.
        /// </summary>
        /// <param name="contentPrefab">Префаб с UI/видео/анимацией. Будет инстанцирован в центр экрана.</param>
        /// <param name="optionalMusic">Опциональный трек, проигрываемый на время показа.</param>
        /// <param name="pauseGame">Приостановить игру (Time.timeScale = 0) во время показа.</param>
        /// <param name="autoCloseAfterSeconds">Автоматически закрыть через указанное время (0 — не закрывать автоматически).</param>
        /// <param name="overrideCloseOnEscape">Переопределить настройку закрытия на Escape только для этого показа.</param>
        /// <param name="textPrefab">Опциональный TextMeshPro префаб для отображения вместе с контентом.</param>
        public void ShowContentPrefab(GameObject contentPrefab,
                                      AudioClip optionalMusic = null,
                                      bool pauseGame = false,
                                      float autoCloseAfterSeconds = 0f,
                                      bool? overrideCloseOnEscape = null,
                                      GameObject textPrefab = null)
        {
            if (contentPrefab == null)
            {
                Debug.LogError("[ContentModalManager] contentPrefab == null");
                return;
            }

            BuildCanvasIfNeeded();

            // Очистка на случай повторного вызова
            InternalDestroySpawnedContent();

            _pauseGame = pauseGame;
            if (_pauseGame)
            {
                Time.timeScale = 0f; // Полная пауза игры
                Debug.Log("[ContentModalManager] Игра на паузе");
            }

            // Показ оверлея
            _overlayImage.color = overlayColor;
            _overlayCanvasGroup.alpha = 0f;
            _overlayCanvasGroup.interactable = true;
            _overlayCanvasGroup.blocksRaycasts = true;
            _canvas.gameObject.SetActive(true);

            // Инстанс контента
            _spawnedContent = Instantiate(contentPrefab, _contentRoot);
            var rect = _spawnedContent.GetComponent<RectTransform>();
            
            if (rect != null)
            {
                // UI объект - настраиваем как обычно
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                Debug.Log($"[ContentModalManager] UI контент размещен: {contentPrefab.name}");
            }
            else
            {
                // 3D объект - размещаем в центре экрана БЕЗ Canvas
                _spawnedContent.transform.SetParent(null); // Убираем из Canvas
                _spawnedContent.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 2f;
                _spawnedContent.transform.LookAt(Camera.main.transform);
                Debug.Log($"[ContentModalManager] 3D контент размещен: {contentPrefab.name}");
            }
            
            // Добавляем текст, если указан
            if (textPrefab != null)
            {
                AddTextPrefabToContent(_spawnedContent, textPrefab);
            }
            
            // Запускаем видео, если есть VideoPlayer
            StartVideoIfPresent(_spawnedContent);
            
            // Запускаем видео, если есть VideoPlayer
            var videoPlayers = _spawnedContent.GetComponentsInChildren<VideoPlayer>();
            foreach (var vp in videoPlayers)
            {
                if (vp.clip != null)
                {
                    // Просто запускаем видео
                    vp.Play();
                    Debug.Log($"[ContentModalManager] Видео запущено: {vp.clip.name}");
                }
            }

            // Музыка
            if (optionalMusic != null)
            {
                if (_musicSource == null)
                {
                    _musicSource = _canvas.gameObject.AddComponent<AudioSource>();
                    _musicSource.playOnAwake = false;
                    _musicSource.loop = false;
                    _musicSource.spatialBlend = 0f;
                    _musicSource.ignoreListenerPause = true; // чтобы играть при паузе игры
                }
                _musicSource.clip = optionalMusic;
                _musicSource.Play();
            }

            _autoCloseAfterSeconds = Mathf.Max(0f, autoCloseAfterSeconds);
            _autoCloseTimer = 0f;
            bool originalCloseOnEscape = closeOnEscape;
            if (overrideCloseOnEscape.HasValue)
            {
                closeOnEscape = overrideCloseOnEscape.Value;
            }

            _isOpen = true;
            if (fadeOverlay)
            {
                StopAllCoroutines();
                StartCoroutine(FadeCanvasGroup(_overlayCanvasGroup, 0f, 1f, fadeDuration));
            }
            else
            {
                _overlayCanvasGroup.alpha = 1f;
            }
            
            // Запускаем анимацию контента
            if (scaleInContent)
            {
                Debug.Log($"[ContentModalManager] Запускаем анимацию масштабирования. ScaleInContent={scaleInContent}, Duration={scaleInDuration}");
                StartCoroutine(ScaleInContent());
            }
            else
            {
                Debug.Log("[ContentModalManager] Анимация масштабирования отключена");
            }
        }

        /// <summary>
        /// Закрыть модальное окно и очистить ресурсы.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            StopAllCoroutines();
            if (fadeOverlay)
            {
                StartCoroutine(FadeAndCleanup());
            }
            else
            {
                InternalCleanupImmediately();
            }
        }

        private System.Collections.IEnumerator FadeAndCleanup()
        {
            yield return FadeCanvasGroup(_overlayCanvasGroup, _overlayCanvasGroup.alpha, 0f, fadeDuration);
            InternalCleanupImmediately();
        }

        private void InternalCleanupImmediately()
        {
            InternalDestroySpawnedContent();

            if (_musicSource != null && _musicSource.isPlaying)
            {
                _musicSource.Stop();
            }

            _overlayCanvasGroup.alpha = 0f;
            _overlayCanvasGroup.interactable = false;
            _overlayCanvasGroup.blocksRaycasts = false;
            _canvas.gameObject.SetActive(false);

            if (_pauseGame)
            {
                Time.timeScale = 1f;
                _pauseGame = false;
            }
        }

        private void InternalDestroySpawnedContent()
        {
            if (_spawnedContent != null)
            {
                Destroy(_spawnedContent);
                _spawnedContent = null;
            }
        }

        private System.Collections.IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                group.alpha = to;
                yield break;
            }
            float t = 0f;
            group.alpha = from;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }

        private System.Collections.IEnumerator ScaleInContent()
        {
            Debug.Log("[ContentModalManager] ScaleInContent запущена");
            
            if (_spawnedContent == null)
            {
                Debug.LogError("[ContentModalManager] _spawnedContent == null");
                yield break;
            }
            
            var rect = _spawnedContent.GetComponent<RectTransform>();
            if (rect == null)
            {
                Debug.LogError("[ContentModalManager] RectTransform не найден на _spawnedContent");
                yield break;
            }
            
            Debug.Log($"[ContentModalManager] Начинаем анимацию масштабирования. Длительность: {scaleInDuration}");
            
            // Начинаем с маленького размера
            rect.localScale = Vector3.zero;
            Debug.Log("[ContentModalManager] Установлен начальный scale = 0");
            
            float t = 0f;
            
            while (t < scaleInDuration)
            {
                t += Time.unscaledDeltaTime;
                float progress = t / scaleInDuration;
                
                // Используем кривую анимации
                float scale = scaleCurve.Evaluate(progress);
                rect.localScale = Vector3.one * scale;
                
                Debug.Log($"[ContentModalManager] Анимация: t={t:F2}, progress={progress:F2}, scale={scale:F2}");
                
                yield return null;
            }
            
            rect.localScale = Vector3.one;
            Debug.Log("[ContentModalManager] Анимация масштабирования завершена");
        }

        private void BuildCanvasIfNeeded()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("ContentModalCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            // Настраиваем CanvasScaler для лучшей поддержки TextMeshPro
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasGo.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGo);

            var overlayGo = new GameObject("Overlay");
            overlayGo.transform.SetParent(canvasGo.transform, false);
            var overlayRect = overlayGo.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            _overlayImage = overlayGo.AddComponent<Image>();
            _overlayImage.color = overlayColor;
            _overlayImage.raycastTarget = true;

            _overlayCanvasGroup = overlayGo.AddComponent<CanvasGroup>();
            _overlayCanvasGroup.alpha = 0f;
            _overlayCanvasGroup.interactable = false;
            _overlayCanvasGroup.blocksRaycasts = false;

            if (closeOnBackgroundClick)
            {
                var btn = overlayGo.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => { if (_isOpen) Close(); });
            }

            var contentRootGo = new GameObject("ContentRoot");
            contentRootGo.transform.SetParent(overlayGo.transform, false);
            _contentRoot = contentRootGo.AddComponent<RectTransform>();
            _contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRoot.pivot = new Vector2(0.5f, 0.5f);
            _contentRoot.anchoredPosition = Vector2.zero;

            _canvas.gameObject.SetActive(false);
        }

        private void AddTextPrefabToContent(GameObject contentRoot, GameObject textPrefab)
        {
            if (textPrefab == null)
            {
                Debug.LogWarning("[ContentModalManager] textPrefab == null");
                return;
            }
            
            // Инстанцируем TextMeshPro префаб
            GameObject textInstance = Instantiate(textPrefab, contentRoot.transform);
            
            // Проверяем и настраиваем TextMeshPro компонент
            #if TMP_PRESENT
            var tmpText = textInstance.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmpText != null)
            {
                // Принудительно обновляем TextMeshPro
                tmpText.ForceMeshUpdate();
                tmpText.UpdateGeometry();
                
                Debug.Log($"[ContentModalManager] TextMeshPro настроен: {tmpText.text}");
            }
            #endif
            
            Debug.Log($"[ContentModalManager] Добавлен TextMeshPro префаб: {textPrefab.name}");
            
            // Дополнительная проверка через кадр для TextMeshPro
            StartCoroutine(ValidateTextMeshProNextFrame(textInstance));
        }
        
        private System.Collections.IEnumerator ValidateTextMeshProNextFrame(GameObject textInstance)
        {
            yield return null; // Ждем один кадр
            
            #if TMP_PRESENT
            var tmpText = textInstance.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmpText != null)
            {
                // Принудительно обновляем TextMeshPro еще раз
                tmpText.ForceMeshUpdate(true, true);
                tmpText.UpdateGeometry();
                
                Debug.Log($"[ContentModalManager] TextMeshPro валидирован: {tmpText.text}");
            }
            #endif
        }
        
        private void StartVideoIfPresent(GameObject contentRoot)
        {
            var videoPlayer = contentRoot.GetComponent<VideoPlayer>();
            if (videoPlayer != null && videoPlayer.clip != null)
            {
                videoPlayer.Play();
                Debug.Log($"[ContentModalManager] Видео запущено: {videoPlayer.clip.name}");
            }
        }
        

        

        
        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }
        }
    }
}
