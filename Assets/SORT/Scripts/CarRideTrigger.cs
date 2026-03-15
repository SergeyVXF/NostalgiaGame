using UnityEngine;
using Invector.vCharacterController;

public class CarRideTrigger : MonoBehaviour
{
    private Vector3 lastCarPosition;
    private bool playerOnCar = false;
    private Transform playerTransform;

    private void Start()
    {
        lastCarPosition = transform.position;
    }

    private void FixedUpdate()
    {
        if (playerOnCar && playerTransform != null)
        {
            Vector3 delta = transform.position - lastCarPosition;
            // Только горизонтальное смещение (XZ), чтобы не мешать прыжкам
            delta.y = 0;
            playerTransform.position += delta;
        }
        lastCarPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<vThirdPersonController>();
        if (player != null && !playerOnCar)
        {
            playerTransform = other.transform;
            playerOnCar = true;
            // Управление НЕ блокируем!
            // Rigidbody НЕ делаем isKinematic!
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<vThirdPersonController>();
        if (player != null && playerOnCar)
        {
            playerOnCar = false;
            playerTransform = null;
        }
    }
} 