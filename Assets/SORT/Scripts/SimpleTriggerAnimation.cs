using UnityEngine;

public class SimpleTriggerAnimation : MonoBehaviour
{
    public string requiredTag = "Player";
    public string animationName = "PullUps"; // Имя параметра Trigger или Bool в Animator
    public Animator targetAnimator;
    public KeyCode actionKey = KeyCode.E;
    public bool useBool = false; // Если true — переключает Bool, если false — Trigger

    private bool playerInZone = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(requiredTag))
        {
            playerInZone = true;
            if (targetAnimator == null)
                targetAnimator = other.GetComponent<Animator>();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(requiredTag))
        {
            playerInZone = false;
        }
    }

    void Update()
    {
        if (playerInZone && targetAnimator != null && Input.GetKeyDown(actionKey))
        {
            if (useBool)
            {
                bool current = targetAnimator.GetBool(animationName);
                targetAnimator.SetBool(animationName, !current);
                Debug.Log($"[SimpleTriggerAnimation] SetBool {animationName} = {!current} на {targetAnimator.gameObject.name}");
            }
            else
            {
                targetAnimator.SetTrigger(animationName);
                Debug.Log($"[SimpleTriggerAnimation] SetTrigger {animationName} на {targetAnimator.gameObject.name}");
            }
        }
    }
} 