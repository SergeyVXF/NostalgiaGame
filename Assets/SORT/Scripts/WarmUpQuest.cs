using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using Invector.vCamera;
using UnityEngine.UI;
using Invector.vCharacterController;

public class WarmUpQuest : MonoBehaviour
{
    [SerializeField] private GameObject spherePrefab;
    [SerializeField] private TextMeshProUGUI questText;
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private Transform npcSpawnPoint;
    [SerializeField] private Transform[] sphereSpawnPoints;
    [SerializeField] private Camera[] cinematicCameras; // Массив камер для катсцены
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float cinematicDuration = 15f; // Общая длительность катсцены
    [SerializeField] private vThirdPersonController playerController; // Добавляем поле для ручного указания контроллера
    [SerializeField] private GameObject questZoneVisual; // Добавляем ссылку на визуальную зону квеста
    
    private List<GameObject> spheres = new List<GameObject>();
    private int collectedSpheres = 0;
    private bool questStarted = false;
    private bool questCompleted = false;
    private Coroutine messageCoroutine;
    private vThirdPersonCamera mainCamera;
    private Coroutine cameraCoroutine;
    private Image fadeImage;

    private void Awake()
    {
        ValidateComponents();
        questText.text = "";
        mainCamera = FindObjectOfType<vThirdPersonCamera>();
        
        // Если контроллер не указан вручную, пытаемся найти его
        if (playerController == null)
        {
            playerController = FindObjectOfType<vThirdPersonController>();
            if (playerController != null)
            {
                Debug.Log("Контроллер игрока найден автоматически");
            }
        }
        
        CreateFadeUI();
    }

    private void CreateFadeUI()
    {
        GameObject canvasObj = new GameObject("FadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        
        RectTransform rectTransform = fadeImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
    }

    private void ValidateComponents()
    {
        if (spherePrefab == null)
        {
            Debug.LogError("Не задан префаб сферы!");
            enabled = false;
            return;
        }

        var collectibleSphere = spherePrefab.GetComponent<CollectibleSphere>();
        if (collectibleSphere == null)
        {
            Debug.LogError("На префабе сферы отсутствует компонент CollectibleSphere!");
            enabled = false;
            return;
        }

        var sphereCollider = spherePrefab.GetComponent<Collider>();
        if (sphereCollider == null)
        {
            Debug.LogError("На префабе сферы отсутствует компонент Collider!");
            enabled = false;
            return;
        }

        if (questText == null)
        {
            Debug.LogError("Не задан UI текст для квеста!");
            enabled = false;
            return;
        }

        if (sphereSpawnPoints == null || sphereSpawnPoints.Length == 0)
        {
            Debug.LogError("Не заданы точки появления сфер!");
            enabled = false;
            return;
        }

        if (cinematicCameras == null || cinematicCameras.Length == 0)
        {
            Debug.LogError("Не заданы камеры для катсцены!");
            enabled = false;
            return;
        }

        // Проверяем каждую камеру в массиве
        for (int i = 0; i < cinematicCameras.Length; i++)
        {
            if (cinematicCameras[i] == null)
            {
                Debug.LogError($"Камера {i} в массиве не задана!");
                enabled = false;
                return;
            }
        }

        if (playerController == null)
        {
            Debug.LogError("Не найден контроллер игрока! Укажите его вручную в инспекторе или убедитесь, что он есть в сцене.");
            enabled = false;
            return;
        }

        // Отключаем все камеры катсцены при старте
        foreach (var camera in cinematicCameras)
        {
            if (camera != null)
            {
                camera.gameObject.SetActive(false); // Полностью деактивируем объекты камер
            }
        }

        for (int i = 0; i < sphereSpawnPoints.Length; i++)
        {
            if (sphereSpawnPoints[i] == null)
            {
                Debug.LogError($"Точка появления сферы {i} не задана!");
                enabled = false;
                return;
            }
        }

        if (questZoneVisual == null)
        {
            Debug.LogError("Не задана визуальная зона квеста!");
            enabled = false;
            return;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!questStarted && other.CompareTag("Player"))
        {
            StartQuest();
        }
    }

    private void StartQuest()
    {
        questStarted = true;
        StartCinematic();
        SpawnSpheres();
        StartCoroutine(ShowStartMessages());
    }

    private IEnumerator ShowStartMessages()
    {
        yield return new WaitForSeconds(4f);
        ShowMessage("Это может пригодиться", 4f);
        yield return new WaitForSeconds(4f);
        ShowMessage("Надо собрать все", 4f);
        yield return new WaitForSeconds(4f);
        ShowMessage("");
    }

    private void StartCinematic()
    {
        if (cameraCoroutine != null)
        {
            StopCoroutine(cameraCoroutine);
        }
        cameraCoroutine = StartCoroutine(CinematicSequence());
    }

    private IEnumerator CinematicSequence()
    {
        if (mainCamera != null && cinematicCameras.Length > 0)
        {
            // Активируем все камеры катсцены
            foreach (var camera in cinematicCameras)
            {
                if (camera != null)
                {
                    camera.gameObject.SetActive(true);
                    camera.enabled = false;
                }
            }

            // Отключаем управление игроком
            playerController.enabled = false;

            // Затемняем экран
            yield return StartCoroutine(FadeScreen(0, 1));

            // Переключаем на первую камеру
            mainCamera.enabled = false;
            cinematicCameras[0].enabled = true;

            // Показываем экран
            yield return StartCoroutine(FadeScreen(1, 0));

            // Вычисляем время для каждой камеры
            float timePerCamera = cinematicDuration / cinematicCameras.Length;

            // Показываем каждую камеру по очереди
            for (int i = 0; i < cinematicCameras.Length; i++)
            {
                if (i > 0)
                {
                    // Переключаем на следующую камеру
                    cinematicCameras[i - 1].enabled = false;
                    cinematicCameras[i].enabled = true;
                }

                // Ждем время для текущей камеры
                yield return new WaitForSeconds(timePerCamera);
            }

            // Затемняем экран
            yield return StartCoroutine(FadeScreen(0, 1));

            // Возвращаемся к основной камере
            cinematicCameras[cinematicCameras.Length - 1].enabled = false;
            mainCamera.enabled = true;

            // Показываем экран
            yield return StartCoroutine(FadeScreen(1, 0));

            // Включаем управление игроком
            playerController.enabled = true;

            // Деактивируем все камеры катсцены
            foreach (var camera in cinematicCameras)
            {
                if (camera != null)
                {
                    camera.gameObject.SetActive(false);
                }
            }

            // Делаем Sphere (1) неактивным
            GameObject sphere1 = GameObject.Find("Sphere (1)");
            if (sphere1 != null)
            {
                sphere1.SetActive(false);
            }
        }
    }

    private IEnumerator FadeScreen(float startAlpha, float targetAlpha)
    {
        float elapsedTime = 0;
        Color color = fadeImage.color;
        color.a = startAlpha;
        fadeImage.color = color;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / fadeDuration);
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        fadeImage.color = color;
    }

    private void ShowMessage(string message, float duration = 1f)
    {
        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
        }
        questText.text = message;
        messageCoroutine = StartCoroutine(HideMessageAfterDelay(duration));
    }

    private IEnumerator HideMessageAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        questText.text = "";
    }

    private void SpawnSpheres()
    {
        Debug.Log($"Создание {sphereSpawnPoints.Length} сфер");
        foreach (Transform spawnPoint in sphereSpawnPoints)
        {
            if (spawnPoint != null)
            {
                GameObject sphere = Instantiate(spherePrefab, spawnPoint.position, spawnPoint.rotation);
                var collectibleSphere = sphere.GetComponent<CollectibleSphere>();
                if (collectibleSphere != null)
                {
                    collectibleSphere.OnCollected += OnSphereCollected;
                    spheres.Add(sphere);
                    Debug.Log($"Сфера создана в позиции: {spawnPoint.position}");
                }
                else
                {
                    Debug.LogError($"На созданной сфере отсутствует компонент CollectibleSphere! Позиция: {spawnPoint.position}");
                    Destroy(sphere);
                }
            }
        }
    }

    private void OnSphereCollected(GameObject sphere)
    {
        if (sphere == null) return;
        
        Debug.Log($"Сфера собрана! Осталось: {sphereSpawnPoints.Length - collectedSpheres - 1}");
        collectedSpheres++;
        spheres.Remove(sphere);
        
        // Показываем счетчик собранных пакетиков
        ShowMessage($"{collectedSpheres} из {sphereSpawnPoints.Length} собраны", 2f);
        
        if (collectedSpheres >= sphereSpawnPoints.Length && !questCompleted)
        {
            CompleteQuest();
        }
    }

    private void CompleteQuest()
    {
        Debug.Log("Квест завершен!");
        questCompleted = true;
        ShowMessage("Все пакетики собраны", 2f);
        
        // Скрываем визуальную зону квеста
        questZoneVisual.SetActive(false);
        
        if (npcPrefab != null && npcSpawnPoint != null)
        {
            Instantiate(npcPrefab, npcSpawnPoint.position, npcSpawnPoint.rotation);
            Debug.Log("NPC создан");
        }
        else
        {
            Debug.LogWarning("Не задан префаб NPC или точка его появления!");
        }
    }

    private void OnDestroy()
    {
        if (fadeImage != null)
        {
            Destroy(fadeImage.transform.parent.gameObject);
        }
    }
} 