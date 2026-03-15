using UnityEngine;
using Invector.vCharacterController;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 100f;
    
    private bool canMove = true;
    private vThirdPersonController thirdPersonController;
    private Animator animator;
    
    private void Awake()
    {
        // Получаем компоненты
        thirdPersonController = GetComponent<vThirdPersonController>();
        animator = GetComponent<Animator>();
        
        if (thirdPersonController == null)
        {
            Debug.LogError("vThirdPersonController не найден на объекте " + gameObject.name);
        }
        
        if (animator == null)
        {
            Debug.LogError("Animator не найден на объекте " + gameObject.name);
        }
    }
    
    private void Update()
    {
        if (!canMove) return;
        
        // Получаем ввод от игрока
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        
        // Движение вперед/назад
        Vector3 movement = transform.forward * verticalInput * moveSpeed * Time.deltaTime;
        transform.position += movement;
        
        // Вращение влево/вправо
        float rotation = horizontalInput * rotationSpeed * Time.deltaTime;
        transform.Rotate(Vector3.up, rotation);
    }
    
    public void SetCanMove(bool value)
    {
        canMove = value;
        Debug.Log($"PlayerController: Управление игроком {(value ? "включено" : "выключено")}");
    }

    // Метод для вызова из анимационного события
    public void OnGreatSwordCastingComplete()
    {
        Debug.Log("Анимация GreatSwordCasting завершена");
        FinalQuestDeathHandler.OnGreatSwordCastingComplete();
    }
} 