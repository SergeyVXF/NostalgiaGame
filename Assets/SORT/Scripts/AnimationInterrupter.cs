using UnityEngine;
using Invector.vCharacterController;

public class AnimationInterrupter : MonoBehaviour
{
    [Header("Animation Interruption Settings")]
    [Tooltip("Имя параметра аниматора для ropeslide")]
    public string ropeSlideParameterName = "RopeSlide";
    
    [Tooltip("Имя параметра аниматора для слайдинга")]
    public string slideParameterName = "IsSliding";
    
    [Tooltip("Список анимаций, которые нужно прерывать")]
    public string[] animationsToInterrupt = { "Falling", "Fall", "Jump", "Air", "Land" };
    
    [Tooltip("Приоритет ropeslide над другими анимациями")]
    public bool ropeSlideHasPriority = true;

    private Animator animator;
    private SlideAnimationController slideController;
    private RopeSlideParticleSpawner particleSpawner;
    private bool wasRopeSliding = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        slideController = GetComponent<SlideAnimationController>();
        particleSpawner = GetComponent<RopeSlideParticleSpawner>();
        
        if (animator == null)
        {
            Debug.LogError("[AnimationInterrupter] Animator не найден!");
        }
    }

    void Update()
    {
        if (animator == null) return;

        // Проверяем, нужно ли активировать ropeslide
        bool shouldRopeSlide = ShouldActivateRopeSlide();
        
        // Если нужно активировать ropeslide и он не был активен
        if (shouldRopeSlide && !wasRopeSliding)
        {
            ForceRopeSlide();
        }
        // Если ropeslide был активен, но больше не нужен
        else if (!shouldRopeSlide && wasRopeSliding)
        {
            DeactivateRopeSlide();
        }
        
        wasRopeSliding = shouldRopeSlide;
    }

    bool ShouldActivateRopeSlide()
    {
        // Проверяем параметр ropeslide в аниматоре
        bool animatorRopeSlide = animator.GetBool(ropeSlideParameterName);
        
        // Проверяем активность RopeSlideParticleSpawner
        bool particleSpawnerActive = false;
        if (particleSpawner != null)
        {
            particleSpawnerActive = particleSpawner.IsRopeSliding();
        }
        
        // Активируем если любой из компонентов активен
        return animatorRopeSlide || particleSpawnerActive;
    }

    void ForceRopeSlide()
    {
        if (animator == null) return;

        // Получаем текущее состояние аниматора
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        
        // Проверяем, играет ли мешающая анимация
        bool isPlayingInterruptingAnimation = false;
        foreach (string animName in animationsToInterrupt)
        {
            if (currentState.IsName(animName))
            {
                isPlayingInterruptingAnimation = true;
                Debug.Log($"[AnimationInterrupter] Обнаружена мешающая анимация: {animName}");
                break;
            }
        }

        // ПРИНУДИТЕЛЬНО прерываем анимации даже если ropeslide уже активен
        if (isPlayingInterruptingAnimation || ShouldActivateRopeSlide())
        {
            // Принудительно прерываем все мешающие анимации
            InterruptAllAnimations();
            
            // Принудительно устанавливаем ropeslide
            animator.SetBool(ropeSlideParameterName, true);
            animator.SetBool(slideParameterName, true);
            
            // Принудительно обновляем аниматор
            animator.Update(0f);
            
            Debug.Log("[AnimationInterrupter] Принудительно прервал анимацию и активировал RopeSlide!");
        }
    }

    void DeactivateRopeSlide()
    {
        if (animator == null) return;

        // Отключаем ropeslide
        animator.SetBool(ropeSlideParameterName, false);
        
        Debug.Log("[AnimationInterrupter] Деактивировал RopeSlide");
    }

    void InterruptAllAnimations()
    {
        if (animator == null) return;

        // Сбрасываем все bool параметры, которые могут мешать
        animator.SetBool("IsFalling", false);
        animator.SetBool("IsJumping", false);
        animator.SetBool("IsGrounded", true);
        animator.SetBool("IsLanding", false);
        animator.SetBool("IsAirborne", false);
        animator.SetBool("IsInAir", false);
        
        // Сбрасываем все триггеры
        animator.ResetTrigger("Jump");
        animator.ResetTrigger("Fall");
        animator.ResetTrigger("Land");
        animator.ResetTrigger("Air");
        animator.ResetTrigger("Falling");
        animator.ResetTrigger("ResetState");
        
        // Сбрасываем все float параметры, связанные с прыжком
        animator.SetFloat("VerticalVelocity", 0f);
        animator.SetFloat("InputMagnitude", 0f);
        animator.SetFloat("Vertical", 0f);
        animator.SetFloat("Horizontal", 0f);
        
        // ПРИНУДИТЕЛЬНО устанавливаем ropeslide
        animator.SetBool(ropeSlideParameterName, true);
        animator.SetBool(slideParameterName, true);
        
        // Принудительно обновляем аниматор
        animator.Update(0f);
        
        Debug.Log("[AnimationInterrupter] Сбросил все мешающие параметры аниматора и принудительно активировал RopeSlide!");
    }

    // Публичный метод для принудительного прерывания
    public void ForceInterrupt()
    {
        InterruptAllAnimations();
        ForceRopeSlide();
    }
    
    // Публичный метод для принудительного прерывания Falling
    public void ForceInterruptFalling()
    {
        if (animator == null) return;
        
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        
        // Проверяем, играет ли Falling анимация
        if (currentState.IsName("Falling") || currentState.IsName("Fall") || currentState.IsName("Air"))
        {
            Debug.Log("[AnimationInterrupter] Принудительно прерываю Falling анимацию!");
            InterruptAllAnimations();
            ForceRopeSlide();
        }
    }

    // Публичный метод для проверки текущего состояния
    public string GetCurrentAnimationState()
    {
        if (animator == null) return "No Animator";
        
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        return currentState.IsName("") ? "Unknown" : currentState.fullPathHash.ToString();
    }

    // Публичный метод для отладки
    public void DebugAnimationState()
    {
        if (animator == null) return;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        bool isRopeSliding = animator.GetBool(ropeSlideParameterName);
        bool isSliding = animator.GetBool(slideParameterName);
        
        Debug.Log($"[AnimationInterrupter] Текущее состояние: {currentState.fullPathHash}, " +
                  $"RopeSlide: {isRopeSliding}, Slide: {isSliding}");
    }
} 