using UnityEngine;
using UnityEngine.Events;

namespace AG.Collectibles
{
    /// <summary>
    /// Коробка-коллекционер, которая показывает контент при касании игрока.
    /// Поддерживает изображения, видео, текст и музыку.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CollectibleBox : MonoBehaviour
    {
        [Header("Контент")]
        [SerializeField] private GameObject contentPrefab;
        [SerializeField] private GameObject textPrefab; // TextMeshPro префаб
        [SerializeField] private AudioClip optionalMusic;
        
        [Header("Звуки")]
        [SerializeField] private AudioClip openBoxSound; // Звук открывания коробки
        
        [Header("Настройки")]
        [SerializeField] private bool pauseGame = true;
        [SerializeField] private bool destroyAfterCollect = true;
        [SerializeField] private bool addBoxAnimation = true;
        
        [Header("События")]
        [SerializeField] private UnityEvent onCollect;
        
        private bool _collected = false;
        
        private void Start()
        {
            // Проверяем коллайдер и добавляем анимацию если нужно
            var collider = GetComponent<Collider>();
            if (collider != null && !collider.isTrigger)
            {
                collider.isTrigger = true;
                Debug.LogWarning($"[CollectibleBox] Коллайдер на {gameObject.name} автоматически настроен как триггер");
            }
            
            // Добавляем анимацию если включено
            if (addBoxAnimation && GetComponent<BoxAnimation>() == null)
            {
                gameObject.AddComponent<BoxAnimation>();
                Debug.Log($"[CollectibleBox] Добавлена анимация к {gameObject.name}");
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (_collected) return;
            
            // Проверяем, что это игрок
            if (other.CompareTag("Player") || other.CompareTag("MainCamera"))
            {
                Collect();
            }
        }
        
        private void Collect()
        {
            if (_collected) return;
            _collected = true;
            
            Debug.Log($"[CollectibleBox] Игрок собрал коробку: {gameObject.name}");
            
            // Проигрываем звук открывания коробки
            if (openBoxSound != null)
            {
                AudioSource.PlayClipAtPoint(openBoxSound, transform.position);
                Debug.Log($"[CollectibleBox] Проигран звук открывания: {openBoxSound.name}");
            }
            
            // Показываем контент
            if (contentPrefab != null)
            {
                ContentModalManager.Instance.ShowContentPrefab(
                    contentPrefab,
                    optionalMusic,
                    pauseGame,
                    0f, // не закрывать автоматически
                    null, // использовать настройки по умолчанию для Escape
                    textPrefab // передаем TextMeshPro префаб
                );
            }
            else
            {
                Debug.LogWarning($"[CollectibleBox] contentPrefab не назначен на {gameObject.name}");
            }
            
            // Вызываем событие
            onCollect?.Invoke();
            
            // Уничтожаем коробку если нужно
            if (destroyAfterCollect)
            {
                Destroy(gameObject);
            }
        }
        
        private void OnValidate()
        {
            // Автоматически настраиваем коллайдер как триггер
            var collider = GetComponent<Collider>();
            if (collider != null && !collider.isTrigger)
            {
                collider.isTrigger = true;
            }
        }
        
        private void Reset()
        {
            // При сбросе компонента автоматически настраиваем коллайдер
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }
    }
} 