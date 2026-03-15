using UnityEngine;

public class TriggerCustomAnimZone : MonoBehaviour
{
    public Animator animator;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            bool current = animator.GetBool("TestAnim");
            animator.SetBool("TestAnim", !current);
            Debug.Log("TestAnim set to: " + !current);
        }
    }
} 