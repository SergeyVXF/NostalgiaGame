using UnityEngine;

public class DogZoneTrigger : MonoBehaviour
{
    public DogPatrol dogPatrol;
    public string playerTag = "Player";
    public string vehicleTag = "Vehicle"; // Тег для машин
    public string boneTag = "Bone"; // Тег для косточек

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[DogZoneTrigger] OnTriggerEnter: {other.name}, tag: '{other.tag}', ожидаемый тег косточки: '{boneTag}'");
        Debug.Log($"[DogZoneTrigger] Позиция объекта: {other.transform.position}");
        Debug.Log($"[DogZoneTrigger] Позиция триггера: {transform.position}");
        
        // ПРИОРИТЕТ 1: Проверяем косточку (ВЫСШИЙ ПРИОРИТЕТ!)
        if (other.CompareTag(boneTag))
        {
            Debug.Log($"[DogZoneTrigger] 🦴 КОСТОЧКА ОБНАРУЖЕНА! {other.name} - бегу к ней!");
            // Просто преследуем косточку как игрока, но с высшим приоритетом
            dogPatrol.SetChasing(true, other.transform);
            return;
        }
        
        // ПРИОРИТЕТ 2: Проверяем игрока (только если нет косточки)
        if (other.CompareTag(playerTag))
        {
            Debug.Log($"[DogZoneTrigger] ✅ Обнаружен игрок: {other.name}");
            dogPatrol.SetChasing(true, other.transform);
            return;
        }
        
        // Проверяем, является ли объект машиной (по тегу)
        if (other.CompareTag(vehicleTag))
        {
            VehicleController vehicle = other.GetComponent<VehicleController>();
            if (vehicle != null && vehicle.isPlayerInVehicle)
            {
                Debug.Log($"[DogZoneTrigger] ✅ Обнаружена машина с игроком: {vehicle.name}");
                // Ищем игрока в машине
                Transform playerInVehicle = FindPlayerInVehicle(vehicle);
                if (playerInVehicle != null)
                {
                    dogPatrol.SetChasing(true, playerInVehicle);
                    return;
                }
            }
            else
            {
                Debug.Log($"[DogZoneTrigger] Машина без игрока: {other.name}");
            }
        }
        
        // Проверяем, является ли объект машиной с игроком (старый способ)
        VehicleController vehicleOld = other.GetComponent<VehicleController>();
        if (vehicleOld != null && vehicleOld.isPlayerInVehicle)
        {
            Debug.Log($"[DogZoneTrigger] ✅ Обнаружена машина с игроком (старый способ): {vehicleOld.name}");
            Transform playerInVehicle = FindPlayerInVehicle(vehicleOld);
            if (playerInVehicle != null)
            {
                dogPatrol.SetChasing(true, playerInVehicle);
                return;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log($"[DogZoneTrigger] OnTriggerExit: {other.name}, tag: {other.tag}");
        
        // Проверяем, является ли объект игроком
        if (other.CompareTag(playerTag))
        {
            Debug.Log($"[DogZoneTrigger] Игрок покинул зону: {other.name}");
            dogPatrol.SetChasing(false, null);
            return;
        }
        
        // Проверяем, является ли объект машиной (по тегу)
        if (other.CompareTag(vehicleTag))
        {
            VehicleController vehicle = other.GetComponent<VehicleController>();
            if (vehicle != null && vehicle.isPlayerInVehicle)
            {
                Debug.Log($"[DogZoneTrigger] Машина с игроком покинула зону: {vehicle.name}");
                dogPatrol.SetChasing(false, null);
            }
        }
        
        // Проверяем, является ли объект машиной с игроком (старый способ)
        VehicleController vehicleOld = other.GetComponent<VehicleController>();
        if (vehicleOld != null && vehicleOld.isPlayerInVehicle)
        {
            Debug.Log($"[DogZoneTrigger] Машина с игроком покинула зону (старый способ): {vehicleOld.name}");
            dogPatrol.SetChasing(false, null);
        }
    }
    
    private Transform FindPlayerInVehicle(VehicleController vehicle)
    {
        // Ищем игрока среди дочерних объектов машины
        Transform[] allChildren = vehicle.GetComponentsInChildren<Transform>();
        
        foreach (Transform child in allChildren)
        {
            if (child.CompareTag(playerTag))
            {
                return child;
            }
        }
        
        // Если не нашли среди дочерних, проверяем родителя машины
        if (vehicle.transform.parent != null && vehicle.transform.parent.CompareTag(playerTag))
        {
            return vehicle.transform.parent;
        }
        
        return null;
    }
} 