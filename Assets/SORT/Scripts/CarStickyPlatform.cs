using UnityEngine;
using Invector.vCharacterController;

public class CarStickyPlatform : MonoBehaviour
{
    private void OnCollisionStay(Collision collision)
    {
        var player = collision.gameObject.GetComponent<vThirdPersonController>();
        if (player != null)
        {
            Rigidbody carRb = GetComponent<Rigidbody>();
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (carRb != null && playerRb != null)
            {
                Vector3 carVelocity = carRb.linearVelocity;
                carVelocity.y = 0;
                playerRb.AddForce(carVelocity * 0.05f, ForceMode.VelocityChange);
            }
        }
    }
} 