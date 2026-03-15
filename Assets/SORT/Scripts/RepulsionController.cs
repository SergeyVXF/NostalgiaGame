using UnityEngine;
using Invector.vCharacterController;
using UnityEngine.Playables;

public class RepulsionController : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Время зажатия ПКМ для активации (секунды)")]
    public float holdTime = 0.5f;
    
    [Tooltip("Префаб отталкивающей сферы")]
    public GameObject repulsiveSpherePrefab;
    
    [Tooltip("Высота спавна сферы относительно игрока")]
    public float spawnHeight = 1.5f;
    
    [Tooltip("Расстояние спавна сферы от игрока")]
    public float spawnDistance = 1f;
    
    [Header("Эффекты у игрока")]
    [Tooltip("Система частиц для эффекта у игрока при запуске")]
    public ParticleSystem playerEffectPrefab;
    
    [Tooltip("Позиция эффекта относительно игрока")]
    public Vector3 effectPosition = Vector3.zero;
    
    [Tooltip("Поворот эффекта (Euler углы)")]
    public Vector3 effectRotation = Vector3.zero;
    
    [Tooltip("Время жизни эффекта у игрока (секунды)")]
    public float effectDuration = 1f;
    
    [Header("Звуковые эффекты")]
    [Tooltip("Звуковой файл для воспроизведения при запуске сферы")]
    public AudioClip launchSound;
    
    [Tooltip("Громкость первого звука (0-1)")]
    [Range(0f, 1f)]
    public float soundVolume = 0.8f;
    
    [Tooltip("Питч первого звука (0.1-3)")]
    [Range(0.1f, 3f)]
    public float soundPitch = 1f;
    
    [Tooltip("Второй звуковой файл для воспроизведения при запуске сферы")]
    public AudioClip launchSound2;
    
    [Tooltip("Громкость второго звука (0-1)")]
    [Range(0f, 1f)]
    public float soundVolume2 = 0.6f;
    
    [Tooltip("Питч второго звука (0.1-3)")]
    [Range(0.1f, 3f)]
    public float soundPitch2 = 1.2f;
    
    [Tooltip("Задержка воспроизведения второго звука (секунды)")]
    public float secondSoundDelay = 0.1f;
    
    [Header("Кулдаун")]
    [Tooltip("Время между запусками сфер (секунды)")]
    public float cooldownTime = 2f;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    private float rightClickStartTime = 0f;
    private bool isHoldingRightClick = false;
    private bool hasActivated = false; // Предотвращает повторную активацию
    private float lastActivationTime = 0f; // Время последней активации
    
    private void Update()
    {
        HandleRightClickInput();
    }
    
    /// <summary>
    /// Обрабатывает ввод ПКМ
    /// </summary>
    private void HandleRightClickInput()
    {
        // Начало зажатия ПКМ
        if (Input.GetMouseButtonDown(1))
        {
            rightClickStartTime = Time.time;
            isHoldingRightClick = true;
            hasActivated = false;
            
            if (showDebugLog)
                Debug.Log("[RepulsionController] 🖱️ ПКМ зажата - начинаю отсчет");
        }
        
        // Во время зажатия ПКМ
        if (isHoldingRightClick && Input.GetMouseButton(1))
        {
            float holdDuration = Time.time - rightClickStartTime;
            
            // Проверяем, прошло ли достаточно времени
            if (holdDuration >= holdTime && !hasActivated)
            {
                // Проверяем кулдаун
                float timeSinceLastActivation = Time.time - lastActivationTime;
                if (timeSinceLastActivation >= cooldownTime)
                {
                    if (showDebugLog)
                        Debug.Log($"[RepulsionController] ⏰ ПКМ зажата {holdDuration:F2}с - ГОТОВ К АКТИВАЦИИ!");
                    hasActivated = true;
                }
                else
                {
                    float remainingCooldown = cooldownTime - timeSinceLastActivation;
                    if (showDebugLog)
                        Debug.Log($"[RepulsionController] ⏰ ПКМ зажата {holdDuration:F2}с - КУЛДАУН АКТИВЕН! Осталось {remainingCooldown:F1}с");
                    hasActivated = true; // Предотвращаем повторные сообщения
                }
            }
        }
        
        // Отпускание ПКМ
        if (Input.GetMouseButtonUp(1))
        {
            float holdDuration = Time.time - rightClickStartTime;
            
            if (showDebugLog)
                Debug.Log($"[RepulsionController] 🖱️ ПКМ отпущена после {holdDuration:F2}с зажатия");
            
            // Если зажимали достаточно долго - активируем отталкивание
            if (holdDuration >= holdTime)
            {
                // Проверяем кулдаун
                float timeSinceLastActivation = Time.time - lastActivationTime;
                if (timeSinceLastActivation >= cooldownTime)
                {
                    ActivateRepulsion();
                    lastActivationTime = Time.time; // Обновляем время последней активации
                }
                else
                {
                    float remainingCooldown = cooldownTime - timeSinceLastActivation;
                    if (showDebugLog)
                        Debug.Log($"[RepulsionController] ⏰ Кулдаун активен! Осталось {remainingCooldown:F1}с");
                }
            }
            else
            {
                if (showDebugLog)
                    Debug.Log($"[RepulsionController] ❌ Недостаточно времени зажатия ({holdDuration:F2}с < {holdTime}с)");
            }
            
            // Сбрасываем состояние
            isHoldingRightClick = false;
            hasActivated = false;
        }
    }
    
    /// <summary>
    /// Активирует отталкивающую сферу
    /// </summary>
    private void ActivateRepulsion()
    {
        if (showDebugLog)
            Debug.Log($"[RepulsionController] 💥💥💥 АКТИВАЦИЯ ОТТАЛКИВАНИЯ! (Кулдаун: {cooldownTime}с)");
        
        // Проверяем, не идет ли катсцена
        if (IsCutscenePlaying())
        {
            if (showDebugLog)
                Debug.LogWarning("[RepulsionController] ⚠️ Катсцена активна, отталкивание отменено!");
            return;
        }
        
        // Находим игрока с улучшенной логикой поиска
        GameObject player = FindPlayer();
        if (player == null)
        {
            Debug.LogError("[RepulsionController] ❌ Игрок не найден!");
            return;
        }
        
        // Проверяем, что игрок активен
        if (!player.activeInHierarchy)
        {
            Debug.LogWarning("[RepulsionController] ⚠️ Игрок найден, но неактивен! Возможно, катсцена еще не закончилась.");
            return;
        }
        
        // Воспроизводим звуки запуска
        PlayLaunchSound(player.transform);
        
        // Создаем эффект у игрока
        CreatePlayerEffect(player.transform);
        
        if (repulsiveSpherePrefab == null)
        {
            // Создаем временную сферу если префаб не назначен
            CreateTemporarySphere(player.transform);
        }
        else
        {
            // Используем назначенный префаб
            CreateRepulsiveSphere(player.transform);
        }
    }
    
    /// <summary>
    /// Проверяет, идет ли катсцена
    /// </summary>
    private bool IsCutscenePlaying()
    {
        // Ищем CutsceneManager и проверяем его состояние
        var cutsceneManager = FindObjectOfType<CutsceneManager>();
        if (cutsceneManager != null)
        {
            // Если есть PlayableDirector, проверяем его состояние
            var playableDirector = cutsceneManager.GetComponent<PlayableDirector>();
            if (playableDirector != null && playableDirector.state == PlayState.Playing)
            {
                return true;
            }
        }
        
        // Альтернативная проверка: ищем неактивного игрока
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && !player.activeInHierarchy)
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Создает отталкивающую сферу от игрока
    /// </summary>
    private void CreateRepulsiveSphere(Transform player)
    {
        // Позиция спавна: перед игроком на уровне груди
        Vector3 spawnPosition = player.position + player.forward * spawnDistance + Vector3.up * spawnHeight;
        
        GameObject sphere = Instantiate(repulsiveSpherePrefab, spawnPosition, Quaternion.identity);
        
        // Устанавливаем направление движения сферы
        RepulsiveSphere repulsiveScript = sphere.GetComponent<RepulsiveSphere>();
        if (repulsiveScript != null)
        {
            repulsiveScript.SetMoveDirection(player.forward);
        }
        
        if (showDebugLog)
            Debug.Log($"[RepulsionController] ✅ Отталкивающая сфера создана на позиции {spawnPosition}, направление: {player.forward}");
    }
    
    /// <summary>
    /// Создает временную сферу для тестирования
    /// </summary>
    private void CreateTemporarySphere(Transform player)
    {
        if (showDebugLog)
            Debug.Log("[RepulsionController] 🔧 Создаю временную отталкивающую сферу");
        
        // Позиция спавна
        Vector3 spawnPosition = player.position + player.forward * spawnDistance + Vector3.up * spawnHeight;
        
        // Создаем сферу
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "RepulsiveSphere_Temp";
        sphere.transform.position = spawnPosition;
        sphere.transform.localScale = Vector3.one * 3f; // Увеличиваем размер для лучшей видимости
        
        // Добавляем скрипт отталкивания
        RepulsiveSphere repulsiveScript = sphere.AddComponent<RepulsiveSphere>();
        
        // Используем значения из префаба (если они установлены)
        // Если значения не установлены, используем разумные по умолчанию
        if (repulsiveScript.repulsionRadius <= 0f)
            repulsiveScript.repulsionRadius = 10f;
        if (repulsiveScript.repulsionForce <= 0f)
            repulsiveScript.repulsionForce = 100f;
        if (repulsiveScript.lifetime <= 0f)
            repulsiveScript.lifetime = 2.0f;
        
        // Устанавливаем направление движения
        repulsiveScript.SetMoveDirection(player.forward);
        
        // Настраиваем материал для видимости
        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Создаем временный материал
            Material tempMaterial = new Material(Shader.Find("Standard"));
            tempMaterial.color = Color.red;
            tempMaterial.SetFloat("_Metallic", 0.5f);
            tempMaterial.SetFloat("_Smoothness", 0.8f);
            renderer.material = tempMaterial;
        }
        
        if (showDebugLog)
            Debug.Log($"[RepulsionController] ✅ Временная сфера создана на позиции {spawnPosition}");
    }
    
    /// <summary>
    /// Создает эффект частиц у игрока при активации
    /// </summary>
    private void CreatePlayerEffect(Transform player)
    {
        if (playerEffectPrefab == null)
        {
            if (showDebugLog)
                Debug.Log("[RepulsionController] ⚠️ Префаб эффекта игрока не назначен - пропускаю создание эффекта");
            return;
        }
        
        // Вычисляем позицию эффекта
        Vector3 effectWorldPosition = player.position + player.TransformDirection(effectPosition);
        
        // Вычисляем поворот эффекта
        Quaternion effectWorldRotation = player.rotation * Quaternion.Euler(effectRotation);
        
        // Создаем эффект
        ParticleSystem effect = Instantiate(playerEffectPrefab, effectWorldPosition, effectWorldRotation);
        
        if (showDebugLog)
        {
            Debug.Log($"[RepulsionController] ✨ Эффект создан у игрока:");
            Debug.Log($"  📍 Позиция: {effectWorldPosition} (игрок: {player.position} + смещение: {effectPosition})");
            Debug.Log($"  🔄 Поворот: {effectWorldRotation.eulerAngles} (игрок: {player.rotation.eulerAngles} + смещение: {effectRotation})");
            Debug.Log($"  ⏰ Время жизни: {effectDuration}с");
        }
        
        // Запускаем эффект
        if (effect != null)
        {
            effect.Play();
            
            // Уничтожаем эффект через заданное время
            Destroy(effect.gameObject, effectDuration);
        }
    }
    
    /// <summary>
    /// Воспроизводит звуки запуска сферы
    /// </summary>
    private void PlayLaunchSound(Transform player)
    {
        // Воспроизводим первый звук
        if (launchSound != null)
        {
            PlaySingleSound(player, launchSound, soundVolume, soundPitch, "Первый звук");
        }
        else
        {
            if (showDebugLog)
                Debug.Log("[RepulsionController] ⚠️ Первый звуковой файл не назначен");
        }
        
        // Воспроизводим второй звук с задержкой
        if (launchSound2 != null)
        {
            StartCoroutine(PlayDelayedSound(player, launchSound2, soundVolume2, soundPitch2, secondSoundDelay, "Второй звук"));
        }
        else
        {
            if (showDebugLog)
                Debug.Log("[RepulsionController] ⚠️ Второй звуковой файл не назначен");
        }
    }
    
    /// <summary>
    /// Воспроизводит один звук
    /// </summary>
    private void PlaySingleSound(Transform player, AudioClip clip, float volume, float pitch, string soundName)
    {
        // Создаем временный AudioSource для воспроизведения звука
        GameObject audioObject = new GameObject($"LaunchSound_{soundName}_Temp");
        audioObject.transform.position = player.position;
        
        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.pitch = pitch;
        audioSource.spatialBlend = 1f; // 3D звук
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 50f;
        
        // Воспроизводим звук
        audioSource.Play();
        
        if (showDebugLog)
        {
            Debug.Log($"[RepulsionController] 🔊 {soundName} воспроизведен:");
            Debug.Log($"  🎵 Файл: {clip.name}");
            Debug.Log($"  🔊 Громкость: {volume}");
            Debug.Log($"  🎼 Питч: {pitch}");
            Debug.Log($"  📍 Позиция: {player.position}");
        }
        
        // Уничтожаем объект после воспроизведения
        Destroy(audioObject, clip.length + 0.1f);
    }
    
    /// <summary>
    /// Воспроизводит звук с задержкой
    /// </summary>
    private System.Collections.IEnumerator PlayDelayedSound(Transform player, AudioClip clip, float volume, float pitch, float delay, string soundName)
    {
        yield return new WaitForSeconds(delay);
        PlaySingleSound(player, clip, volume, pitch, soundName);
    }

    /// <summary>
    /// Находит игрока с улучшенной логикой поиска
    /// </summary>
    private GameObject FindPlayer()
    {
        // Сначала ищем по тегу
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        // Если не найден по тегу, ищем по имени
        if (player == null)
        {
            if (showDebugLog)
                Debug.LogWarning("[RepulsionController] ⚠️ Игрок не найден по тегу 'Player', ищу по имени...");
            
            // Ищем объекты с именами, которые могут быть игроком
            string[] possiblePlayerNames = { "Player", "PlayerController", "ThirdPersonController", "PlayerCharacter" };
            foreach (string name in possiblePlayerNames)
            {
                player = GameObject.Find(name);
                if (player != null)
                {
                    if (showDebugLog)
                        Debug.Log($"[RepulsionController] ✅ Игрок найден по имени: {name}");
                    break;
                }
            }
        }
        
        // Если все еще не найден, ищем по компонентам
        if (player == null)
        {
            if (showDebugLog)
                Debug.LogWarning("[RepulsionController] ⚠️ Игрок не найден по имени, ищу по компонентам...");
            
            // Ищем объекты с компонентами игрока
            var playerControllers = FindObjectsOfType<Invector.vCharacterController.vThirdPersonController>();
            if (playerControllers.Length > 0)
            {
                player = playerControllers[0].gameObject;
                if (showDebugLog)
                    Debug.Log($"[RepulsionController] ✅ Игрок найден по компоненту vThirdPersonController");
            }
        }
        
        if (player == null)
        {
            Debug.LogError("[RepulsionController] ❌ Игрок не найден никакими методами!");
            return null;
        }
        
        if (showDebugLog)
        {
            Debug.Log($"[RepulsionController] ✅ Игрок найден: {player.name}");
            Debug.Log($"[RepulsionController] 📍 Позиция игрока: {player.transform.position}");
            Debug.Log($"[RepulsionController] 🎯 Направление игрока: {player.transform.forward}");
            Debug.Log($"[RepulsionController] 🔄 Активен: {player.activeInHierarchy}");
        }
        
        return player;
    }
}
