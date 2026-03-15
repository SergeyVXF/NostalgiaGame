using UnityEngine;

public class PlayAnimOnKey : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            Debug.LogError("Animator не найден на объекте!");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            animator.SetTrigger("PlayAnim");
            Debug.Log("SetTrigger PlayAnim");
        }
    }
} 