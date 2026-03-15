using UnityEngine;
using Invector.vCharacterController;

public class RopeSlideTester : MonoBehaviour
{
    [Header("Test Controls")]
    [Tooltip("Клавиша для принудительной активации RopeSlide")]
    public KeyCode forceRopeSlideKey = KeyCode.R;
    
    [Tooltip("Клавиша для сброса состояния")]
    public KeyCode resetStateKey = KeyCode.T;
    
    [Tooltip("Клавиша для отладки состояния анимации")]
    public KeyCode debugStateKey = KeyCode.Y;

    private SlideAnimationController slideController;
    private AnimationInterrupter animationInterrupter;
    private Animator animator;

    void Start()
    {
        slideController = GetComponent<SlideAnimationController>();
        animationInterrupter = GetComponent<AnimationInterrupter>();
        animator = GetComponent<Animator>();
        
        if (slideController == null)
        {
            Debug.LogWarning("[RopeSlideTester] SlideAnimationController не найден!");
        }
        
        if (animationInterrupter == null)
        {
            Debug.LogWarning("[RopeSlideTester] AnimationInterrupter не найден!");
        }
    }

    void Update()
    {
        // Принудительная активация RopeSlide
        if (Input.GetKeyDown(forceRopeSlideKey))
        {
            ForceRopeSlide();
        }
        
        // Сброс состояния
        if (Input.GetKeyDown(resetStateKey))
        {
            ResetState();
        }
        
        // Отладка состояния
        if (Input.GetKeyDown(debugStateKey))
        {
            DebugState();
        }
    }

    void ForceRopeSlide()
    {
        Debug.Log("[RopeSlideTester] Принудительная активация RopeSlide!");
        
        if (slideController != null)
        {
            slideController.ForceRopeSlide(true);
        }
        
        if (animationInterrupter != null)
        {
            animationInterrupter.ForceInterrupt();
        }
        
        if (animator != null)
        {
            // Принудительно устанавливаем параметры
            animator.SetBool("RopeSlide", true);
            animator.SetBool("IsSliding", true);
            
            // Сбрасываем мешающие параметры
            animator.SetBool("IsFalling", false);
            animator.SetBool("IsJumping", false);
            animator.SetBool("IsGrounded", true);
            
            // Принудительно обновляем аниматор
            animator.Update(0f);
        }
    }

    void ResetState()
    {
        Debug.Log("[RopeSlideTester] Сброс состояния!");
        
        if (slideController != null)
        {
            slideController.ResetSlideState();
        }
        
        if (animator != null)
        {
            animator.SetBool("RopeSlide", false);
            animator.SetBool("IsSliding", false);
        }
    }

    void DebugState()
    {
        Debug.Log("[RopeSlideTester] === ОТЛАДКА СОСТОЯНИЯ ===");
        
        if (animator != null)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            bool isRopeSliding = animator.GetBool("RopeSlide");
            bool isSliding = animator.GetBool("IsSliding");
            bool isFalling = animator.GetBool("IsFalling");
            bool isJumping = animator.GetBool("IsJumping");
            bool isGrounded = animator.GetBool("IsGrounded");
            
            Debug.Log($"Текущее состояние: {currentState.fullPathHash}");
            Debug.Log($"RopeSlide: {isRopeSliding}");
            Debug.Log($"IsSliding: {isSliding}");
            Debug.Log($"IsFalling: {isFalling}");
            Debug.Log($"IsJumping: {isJumping}");
            Debug.Log($"IsGrounded: {isGrounded}");
        }
        
        if (animationInterrupter != null)
        {
            animationInterrupter.DebugAnimationState();
        }
        
        Debug.Log("[RopeSlideTester] === КОНЕЦ ОТЛАДКИ ===");
    }

    void OnGUI()
    {
        // Отображаем подсказки на экране
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.Label("=== ROPE SLIDE TESTER ===");
        GUILayout.Label($"R: Принудительная активация RopeSlide");
        GUILayout.Label($"T: Сброс состояния");
        GUILayout.Label($"Y: Отладка состояния");
        GUILayout.EndArea();
    }
} 