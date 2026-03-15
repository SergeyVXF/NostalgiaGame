using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private string groundTag = "Ground";
    
    private Rigidbody rb;
    private bool isGrounded;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("PlayerMovement: Rigidbody не найден!");
            return;
        }
    }
    
    private void Update()
    {
        // Получаем ввод от игрока
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        
        // Движение
        Vector3 movement = transform.forward * verticalInput * moveSpeed;
        rb.linearVelocity = new Vector3(movement.x, rb.linearVelocity.y, movement.z);
        
        // Вращение
        transform.Rotate(Vector3.up * horizontalInput * rotationSpeed * Time.deltaTime);
        
        // Прыжок
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Проверяем, находится ли игрок на земле
        if (collision.gameObject.CompareTag(groundTag))
        {
            isGrounded = true;
        }
    }
    
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag(groundTag))
        {
            isGrounded = false;
        }
    }
} 