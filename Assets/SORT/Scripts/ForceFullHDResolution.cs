using UnityEngine;

/// <summary>
/// Принудительно устанавливает разрешение Full HD (1920x1080) при запуске игры
/// </summary>
public class ForceFullHDResolution : MonoBehaviour
{
    [Header("Настройки разрешения")]
    [Tooltip("Принудительно установить Full HD при запуске")]
    public bool forceFullHDOnStart = true;
    
    [Tooltip("Разрешить изменение разрешения пользователем")]
    public bool allowResolutionChange = false;
    
    [Header("Параметры Full HD")]
    [Tooltip("Ширина экрана")]
    public int targetWidth = 1920;
    
    [Tooltip("Высота экрана")]
    public int targetHeight = 1080;
    
    [Tooltip("Полноэкранный режим")]
    public FullScreenMode fullScreenMode = FullScreenMode.ExclusiveFullScreen;
    
    [Header("Отладка")]
    [Tooltip("Показывать информацию о разрешении в консоли")]
    public bool debugInfo = true;

    void Awake()
    {
        // Применяем настройки при запуске игры
        if (forceFullHDOnStart)
        {
            SetFullHDResolution();
        }
        
        // Если не разрешаем изменение разрешения - делаем объект постоянным
        if (!allowResolutionChange)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    void Start()
    {
        if (debugInfo)
        {
            LogCurrentResolution();
        }
    }

    /// <summary>
    /// Устанавливает Full HD разрешение
    /// </summary>
    [ContextMenu("Установить Full HD")]
    public void SetFullHDResolution()
    {
        if (debugInfo)
        {
            Debug.Log($"[ForceFullHDResolution] 🎮 Устанавливаю разрешение {targetWidth}x{targetHeight}");
            Debug.Log($"[ForceFullHDResolution] 📺 Текущее разрешение: {Screen.width}x{Screen.height}");
        }
        
        // Устанавливаем разрешение
        Screen.SetResolution(targetWidth, targetHeight, fullScreenMode);
        
        if (debugInfo)
        {
            Debug.Log($"[ForceFullHDResolution] ✅ Разрешение установлено!");
        }
    }
    
    /// <summary>
    /// Выводит информацию о текущем разрешении
    /// </summary>
    [ContextMenu("Показать текущее разрешение")]
    public void LogCurrentResolution()
    {
        Debug.Log($"[ForceFullHDResolution] 📊 ИНФОРМАЦИЯ О РАЗРЕШЕНИИ:");
        Debug.Log($"[ForceFullHDResolution] 📺 Текущее разрешение: {Screen.width}x{Screen.height}");
        Debug.Log($"[ForceFullHDResolution] 🖥️ Полноэкранный режим: {Screen.fullScreenMode}");
        Debug.Log($"[ForceFullHDResolution] 🎯 Целевое разрешение: {targetWidth}x{targetHeight}");
        
        // Показываем все доступные разрешения
        Resolution[] resolutions = Screen.resolutions;
        Debug.Log($"[ForceFullHDResolution] 📋 Доступные разрешения ({resolutions.Length}):");
        
        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution res = resolutions[i];
            string marker = (res.width == Screen.width && res.height == Screen.height) ? " ← ТЕКУЩЕЕ" : "";
            Debug.Log($"  {i}: {res.width}x{res.height} @ {res.refreshRate}Hz{marker}");
        }
    }
    
    void Update()
    {
        // Если не разрешаем изменение разрешения - следим за ним
        if (!allowResolutionChange)
        {
            // Проверяем, не изменилось ли разрешение
            if (Screen.width != targetWidth || Screen.height != targetHeight)
            {
                if (debugInfo)
                {
                    Debug.LogWarning($"[ForceFullHDResolution] ⚠️ Обнаружено изменение разрешения: {Screen.width}x{Screen.height}");
                    Debug.Log($"[ForceFullHDResolution] 🔄 Возвращаю к {targetWidth}x{targetHeight}");
                }
                
                SetFullHDResolution();
            }
        }
    }
    
    /// <summary>
    /// Проверяет, поддерживается ли Full HD на этом мониторе
    /// </summary>
    [ContextMenu("Проверить поддержку Full HD")]
    public void CheckFullHDSupport()
    {
        Resolution[] resolutions = Screen.resolutions;
        bool fullHDSupported = false;
        
        foreach (Resolution res in resolutions)
        {
            if (res.width == 1920 && res.height == 1080)
            {
                fullHDSupported = true;
                Debug.Log($"[ForceFullHDResolution] ✅ Full HD поддерживается @ {res.refreshRate}Hz");
                break;
            }
        }
        
        if (!fullHDSupported)
        {
            Debug.LogWarning("[ForceFullHDResolution] ❌ Full HD НЕ поддерживается на этом мониторе!");
            Debug.Log("[ForceFullHDResolution] 💡 Максимальное разрешение:");
            
            Resolution maxRes = resolutions[resolutions.Length - 1];
            Debug.Log($"  {maxRes.width}x{maxRes.height} @ {maxRes.refreshRate}Hz");
        }
    }
}
