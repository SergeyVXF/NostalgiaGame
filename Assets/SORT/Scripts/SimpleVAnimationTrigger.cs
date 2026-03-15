using UnityEngine;
using Invector.vCharacterController;

public class SimpleVAnimationTrigger : MonoBehaviour
{
    public string requiredTag = "Player";
    public string animationStateName = "PullUps"; // Имя состояния в Animator с vAnimationTag
    public string idleStateName = "Idle"; // Имя состояния для возврата
    public Animator targetAnimator;
    public KeyCode actionKey = KeyCode.E;
    public Transform targetTransform;
    public float animationDuration = 2f;
    [Header("Точное позиционирование")]
    public bool useExactPositionAndRotation = true;
    [Header("Управление камерой")]
    public bool allowCameraControl = true;

    private bool playerInZone = false;
    private Transform playerTransform;
    private Collider playerCollider;
    private Rigidbody playerRb;
    private vThirdPersonController vController;
    private vThirdPersonInput vInput;
    private bool isLooping = false;
    private bool originalRotateToCameraFwdWhenMoving;
    private bool originalRotateToCameraFwdWhenStanding;
    private bool originalIgnoreCameraRotation;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(requiredTag))
        {
            playerInZone = true;
            if (targetAnimator == null)
                targetAnimator = other.GetComponent<Animator>();
            playerTransform = other.transform;
            playerCollider = other.GetComponent<Collider>();
            playerRb = other.GetComponent<Rigidbody>();
            vController = other.GetComponent<vThirdPersonController>();
            vInput = other.GetComponent<vThirdPersonInput>();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(requiredTag))
        {
            playerInZone = false;
            playerTransform = null;
            playerCollider = null;
            playerRb = null;
            vController = null;
            vInput = null;
            if (isLooping)
            {
                StopLoopAnim();
            }
        }
    }

    void Update()
    {
        if (playerInZone && targetAnimator != null && Input.GetKeyDown(actionKey))
        {
            if (!isLooping)
            {
                StartLoopAnim();
            }
            else
            {
                StopLoopAnim();
            }
        }
    }

    void StartLoopAnim()
    {
        if (targetTransform != null && playerTransform != null && useExactPositionAndRotation)
        {
            // Устанавливаем точную позицию и поворот из targetTransform
            playerTransform.position = targetTransform.position;
            playerTransform.rotation = targetTransform.rotation;
            
            Debug.Log($"[SimpleVAnimationTrigger] Установил точную позицию: {targetTransform.position} и поворот: {targetTransform.rotation.eulerAngles}");
        }
        
        // Отключаем компоненты управления персонажем
        if (vController != null)
        {
            if (allowCameraControl)
            {
                // Блокируем только поворот, но оставляем контроллер активным
                vController.lockRotation = true;
                Debug.Log("[SimpleVAnimationTrigger] Заблокировал поворот vThirdPersonController");
            }
            else
            {
                vController.enabled = false;
                Debug.Log("[SimpleVAnimationTrigger] Отключил vThirdPersonController");
            }
        }
        if (vInput != null)
        {
            if (allowCameraControl)
            {
                // Сохраняем оригинальные настройки поворота
                originalRotateToCameraFwdWhenMoving = vInput.rotateToCameraFwdWhenMoving;
                originalRotateToCameraFwdWhenStanding = vInput.rotateToCameraFwdWhenStanding;
                originalIgnoreCameraRotation = vInput.ignoreCameraRotation;
                
                // Блокируем только движение персонажа, но оставляем управление камерой
                vInput.lockInput = true;
                vInput.lockCameraInput = false;
                
                // Отключаем поворот персонажа за камерой
                vInput.rotateToCameraFwdWhenMoving = false;
                vInput.rotateToCameraFwdWhenStanding = false;
                vInput.ignoreCameraRotation = true;
                
                Debug.Log("[SimpleVAnimationTrigger] Заблокировал движение игрока, камера активна, поворот за камерой отключен");
            }
            else
            {
                // Отключаем полностью
                vInput.enabled = false;
                Debug.Log("[SimpleVAnimationTrigger] Отключил vThirdPersonInput полностью");
            }
        }
        if (playerCollider != null)
        {
            playerCollider.enabled = false;
            Debug.Log("[SimpleVAnimationTrigger] Отключил коллайдер игрока");
        }
        if (playerRb != null)
        {
            playerRb.isKinematic = true;
            Debug.Log("[SimpleVAnimationTrigger] Сделал Rigidbody isKinematic");
        }
        
        // Запускаем анимацию и блокируем другие
        if (targetAnimator != null)
        {
            targetAnimator.SetBool("InPullUps", true);
            targetAnimator.CrossFadeInFixedTime(animationStateName, 0.1f);
            Debug.Log($"[SimpleVAnimationTrigger] CrossFadeInFixedTime {animationStateName}, InPullUps=true");
        }
        isLooping = true;
    }

    void StopLoopAnim()
    {
        // Останавливаем анимацию и разблокируем другие
        if (targetAnimator != null)
        {
            targetAnimator.SetBool("InPullUps", false);
            if (!string.IsNullOrEmpty(idleStateName))
            {
                targetAnimator.CrossFadeInFixedTime(idleStateName, 0.1f);
                Debug.Log($"[SimpleVAnimationTrigger] CrossFadeInFixedTime {idleStateName}, InPullUps=false");
            }
        }
        
        // Включаем компоненты управления обратно
        if (vController != null)
        {
            if (allowCameraControl)
            {
                // Разблокируем поворот
                vController.lockRotation = false;
                Debug.Log("[SimpleVAnimationTrigger] Разблокировал поворот vThirdPersonController");
            }
            else
            {
                vController.enabled = true;
                Debug.Log("[SimpleVAnimationTrigger] Включил vThirdPersonController");
            }
        }
        if (vInput != null)
        {
            if (allowCameraControl)
            {
                // Разблокируем движение и поворот
                vInput.lockInput = false;
                vInput.lockCameraInput = false;
                
                // Восстанавливаем оригинальные настройки поворота
                vInput.rotateToCameraFwdWhenMoving = originalRotateToCameraFwdWhenMoving;
                vInput.rotateToCameraFwdWhenStanding = originalRotateToCameraFwdWhenStanding;
                vInput.ignoreCameraRotation = originalIgnoreCameraRotation;
                
                Debug.Log("[SimpleVAnimationTrigger] Разблокировал движение игрока, восстановил поворот за камерой");
            }
            else
            {
                // Включаем полностью
                vInput.enabled = true;
                Debug.Log("[SimpleVAnimationTrigger] Включил vThirdPersonInput");
            }
        }
        if (playerCollider != null)
            playerCollider.enabled = true;
        if (playerRb != null)
            playerRb.isKinematic = false;
        isLooping = false;
        Debug.Log("[SimpleVAnimationTrigger] Вернул управление игроку");
    }
}