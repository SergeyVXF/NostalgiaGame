using UnityEngine;
using Invector.vCharacterController;

public class SlideAnimationController : MonoBehaviour
{
    [Header("Slide Animation Settings")]
    [Tooltip("Имя параметра аниматора для слайдинга")]
    public string slideParameterName = "IsSliding";
    
    [Tooltip("Имя параметра аниматора для ropeslide")]
    public string ropeSlideParameterName = "RopeSlide";
    
    [Tooltip("Минимальная скорость для активации слайдинга с прыжка")]
    public float minSlideSpeed = 3f;
    
    [Tooltip("Минимальная высота прыжка для активации слайдинга")]
    public float minJumpHeight = 1f;
    
    [Tooltip("Время задержки после прыжка для активации слайдинга")]
    public float slideDelayAfterJump = 0.5f;

    private Animator animator;
    private vThirdPersonMotor motor;
    private vThirdPersonController controller;
    private AnimationInterrupter animationInterrupter;
    private bool wasGrounded = true;
    private bool wasJumping = false;
    private float jumpStartTime = 0f;
    private float jumpStartHeight = 0f;
    private bool canSlideFromJump = false;
    private bool isSlidingFromJump = false;
    private bool forceRopeSlide = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        motor = GetComponent<vThirdPersonMotor>();
        controller = GetComponent<vThirdPersonController>();
        animationInterrupter = GetComponent<AnimationInterrupter>();
        
        if (animator == null)
        {
            Debug.LogError("[SlideAnimationController] Animator не найден!");
        }
        
        if (motor == null)
        {
            Debug.LogError("[SlideAnimationController] vThirdPersonMotor не найден!");
        }
    }

    void Update()
    {
        if (animator == null || motor == null) return;

        // Отслеживаем прыжок
        TrackJump();
        
        // Проверяем слайдинг с прыжка
        CheckSlideFromJump();
        
        // Устанавливаем параметры аниматора
        UpdateAnimatorParameters();
        
        // Принудительно прерываем falling анимацию
        ForceInterruptFalling();
    }

    void TrackJump()
    {
        bool isGrounded = motor.isGrounded;
        bool isJumping = motor.isJumping;
        
        // Определяем начало прыжка
        if (!wasGrounded && isGrounded)
        {
            // Игрок приземлился
            wasJumping = false;
            canSlideFromJump = false;
            isSlidingFromJump = false;
            forceRopeSlide = false;
        }
        else if (wasGrounded && !isGrounded && !wasJumping)
        {
            // Игрок начал прыжок
            wasJumping = true;
            jumpStartTime = Time.time;
            jumpStartHeight = transform.position.y;
        }
        
        wasGrounded = isGrounded;
    }

    void CheckSlideFromJump()
    {
        if (!wasJumping) return;

        // Проверяем, прошло ли достаточно времени после прыжка
        if (Time.time - jumpStartTime < slideDelayAfterJump) return;

        // Проверяем, была ли достаточная высота прыжка
        float jumpHeight = jumpStartHeight - transform.position.y;
        if (jumpHeight < minJumpHeight) return;

        // Проверяем скорость движения
        float currentSpeed = motor.velocity;
        if (currentSpeed < minSlideSpeed) return;

        // Проверяем, находится ли игрок на склоне
        if (motor.isSliding)
        {
            canSlideFromJump = true;
            isSlidingFromJump = true;
            forceRopeSlide = true; // Принудительно активируем ropeslide
        }
    }

    void UpdateAnimatorParameters()
    {
        if (animator == null) return;

        // Устанавливаем параметр слайдинга
        bool isSliding = motor.isSliding || isSlidingFromJump;
        animator.SetBool(slideParameterName, isSliding);
        
        // Устанавливаем параметр ropeslide (если игрок слайдит с прыжка)
        bool isRopeSliding = isSlidingFromJump && canSlideFromJump;
        animator.SetBool(ropeSlideParameterName, isRopeSliding);
        
        // Отладочная информация
        if (isSlidingFromJump)
        {
            Debug.Log($"[SlideAnimationController] Слайдинг с прыжка: {isRopeSliding}, Скорость: {motor.velocity:F2}");
        }
    }

    void ForceInterruptFalling()
    {
        if (animator == null) return;

        // Проверяем, играет ли анимация falling
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        
        // Если играет falling анимация и нужно активировать ropeslide
        if (currentState.IsName("Falling") || currentState.IsName("Fall") || 
            currentState.IsName("Jump") || currentState.IsName("Air"))
        {
            if (forceRopeSlide || (isSlidingFromJump && canSlideFromJump))
            {
                // Используем AnimationInterrupter если он есть
                if (animationInterrupter != null)
                {
                    animationInterrupter.ForceInterruptFalling();
                }
                else
                {
                    // Принудительно прерываем falling и переключаемся на ropeslide
                    animator.SetBool("IsFalling", false);
                    animator.SetBool("IsJumping", false);
                    animator.SetBool("IsGrounded", true);
                    
                    // Принудительно устанавливаем ropeslide
                    animator.SetBool(ropeSlideParameterName, true);
                    
                    // Сбрасываем все триггеры, которые могут мешать
                    animator.ResetTrigger("Jump");
                    animator.ResetTrigger("Fall");
                    
                    // Принудительно обновляем аниматор
                    animator.Update(0f);
                    
                    Debug.Log("[SlideAnimationController] Принудительно прервал falling и переключился на RopeSlide!");
                }
            }
        }
    }

    // Публичный метод для принудительной активации ropeslide
    public void ForceRopeSlide(bool active)
    {
        if (animator != null)
        {
            animator.SetBool(ropeSlideParameterName, active);
            forceRopeSlide = active;
            
            // Если активируем ropeslide, принудительно прерываем все мешающие анимации
            if (active)
            {
                animator.SetBool("IsFalling", false);
                animator.SetBool("IsJumping", false);
                animator.SetBool("IsGrounded", true);
                animator.ResetTrigger("Jump");
                animator.ResetTrigger("Fall");
                animator.Update(0f);
            }
            
            Debug.Log($"[SlideAnimationController] Принудительная активация RopeSlide: {active}");
        }
    }

    // Публичный метод для сброса состояния слайдинга
    public void ResetSlideState()
    {
        canSlideFromJump = false;
        isSlidingFromJump = false;
        wasJumping = false;
        forceRopeSlide = false;
        
        if (animator != null)
        {
            animator.SetBool(slideParameterName, false);
            animator.SetBool(ropeSlideParameterName, false);
        }
    }
} 