using UnityEngine;

namespace AG.Collectibles
{
    /// <summary>
    /// Анимация для коробки-коллекционера: вращение и движение вверх-вниз
    /// </summary>
    public class BoxAnimation : MonoBehaviour
    {
        [Header("Вращение")]
        [SerializeField] private bool enableRotation = true;
        [SerializeField] private Vector3 rotationSpeed = new Vector3(0, 50, 0); // градусов в секунду
        
        [Header("Движение вверх-вниз")]
        [SerializeField] private bool enableBobbing = true;
        [SerializeField] private float bobbingHeight = 0.25f; // амплитуда движения (уменьшена в 2 раза)
        [SerializeField] private float bobbingSpeed = 2f; // частота движения
        
        private Vector3 _startPosition;
        private float _bobbingTime;
        
        private void Start()
        {
            _startPosition = transform.position;
            _bobbingTime = 0f;
        }
        
        private void Update()
        {
            // Вращение
            if (enableRotation)
            {
                transform.Rotate(rotationSpeed * Time.deltaTime);
            }
            
            // Движение вверх-вниз
            if (enableBobbing)
            {
                _bobbingTime += Time.deltaTime * bobbingSpeed;
                float yOffset = Mathf.Sin(_bobbingTime) * bobbingHeight;
                
                Vector3 newPosition = _startPosition;
                newPosition.y += yOffset;
                transform.position = newPosition;
            }
        }
    }
}

