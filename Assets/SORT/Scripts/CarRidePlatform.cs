using UnityEngine;
using Invector.vCharacterController;

public class CarRidePlatform : MonoBehaviour
{
    private Vector3 lastCarPosition;
    private Quaternion lastCarRotation;

    void Start()
    {
        lastCarPosition = transform.position;
        lastCarRotation = transform.rotation;
    }

    void FixedUpdate()
    {
        lastCarPosition = transform.position;
        lastCarRotation = transform.rotation;
    }

    void OnCollisionStay(Collision collision)
    {
        var player = collision.gameObject.GetComponent<vThirdPersonController>();
        if (player != null && player.isGrounded)
        {
            // Смещение позиции
            Vector3 carDelta = transform.position - lastCarPosition;
            player.transform.position += carDelta;

            // Смещение поворота
            Quaternion carRotDelta = transform.rotation * Quaternion.Inverse(lastCarRotation);
            player.transform.rotation = carRotDelta * player.transform.rotation;
        }
    }
} 