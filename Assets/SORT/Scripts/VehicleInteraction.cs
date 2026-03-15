using UnityEngine;
using Invector.vCharacterController;

public class VehicleInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    public KeyCode actionKey = KeyCode.E;
    public LayerMask playerLayer = 1;
    
    [Header("UI")]
    public GameObject interactionUI;
    
    private VehicleController vehicleController;
    private bool playerInRange = false;
    private vThirdPersonController nearbyPlayer;
    
    void Start()
    {
        // Ищем VehicleController на родительском объекте
        vehicleController = GetComponentInParent<VehicleController>();
        if (vehicleController == null)
        {
            Debug.LogError("[VehicleInteraction] VehicleController не найден на родительском объекте!");
        }
        else
        {
            Debug.Log("[VehicleInteraction] VehicleController найден: " + vehicleController.gameObject.name);
        }
        
        Debug.Log("[VehicleInteraction] Инициализирован на " + gameObject.name);
        
        // Скрываем UI при старте
        if (interactionUI != null)
            interactionUI.SetActive(false);
    }
    
    void Update()
    {
        if (playerInRange && Input.GetKeyDown(actionKey))
        {
            Debug.Log("[VehicleInteraction] Нажата клавиша E, игрок в зоне");
            if (vehicleController != null && nearbyPlayer != null)
            {
                Debug.Log("[VehicleInteraction] Вызываю EnterVehicle");
                var playerInput = nearbyPlayer.GetComponent<vThirdPersonInput>();
                var playerAnimator = nearbyPlayer.GetComponent<Animator>();
                
                vehicleController.EnterVehicle(nearbyPlayer, playerInput, playerAnimator);
            }
            else
            {
                Debug.LogError("[VehicleInteraction] vehicleController или nearbyPlayer == null!");
                if (vehicleController == null)
                    Debug.LogError("[VehicleInteraction] vehicleController == null");
                if (nearbyPlayer == null)
                    Debug.LogError("[VehicleInteraction] nearbyPlayer == null");
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("[VehicleInteraction] Триггер сработал с объектом: " + other.gameObject.name);
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            var player = other.GetComponent<vThirdPersonController>();
            if (player != null)
            {
                playerInRange = true;
                nearbyPlayer = player;
                Debug.Log("[VehicleInteraction] Игрок вошёл в зону взаимодействия");
                
                // Показываем UI
                if (interactionUI != null)
                    interactionUI.SetActive(true);
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerInRange = false;
            nearbyPlayer = null;
            Debug.Log("[VehicleInteraction] Игрок вышел из зоны взаимодействия");
            
            // Скрываем UI
            if (interactionUI != null)
                interactionUI.SetActive(false);
        }
    }
} 