using UnityEngine;

public class FreeLookCamera : MonoBehaviour
{
    public Transform target; // Цель для слежения (машина или игрок)
    public Vector3 offset = new Vector3(0, 2, -6); // Смещение камеры
    public float mouseSensitivity = 2f;
    public float minY = -30f;
    public float maxY = 60f;
    public float followSpeed = 10f;

    private float currentYaw = 0f;
    private float currentPitch = 10f;

    void LateUpdate()
    {
        if (target == null) return;

        // Управление мышью
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        currentYaw += mouseX;
        currentPitch -= mouseY;
        currentPitch = Mathf.Clamp(currentPitch, minY, maxY);

        // Позиция камеры
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 desiredPosition = target.position + rotation * offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1.2f);
    }

    // Позволяет динамически менять цель
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        // Сброс углов для плавного перехода
        currentYaw = target.eulerAngles.y;
        currentPitch = 10f;
    }
} 