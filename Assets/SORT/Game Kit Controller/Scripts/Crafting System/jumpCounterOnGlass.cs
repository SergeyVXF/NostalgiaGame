using UnityEngine;
using System.Collections.Generic;

public class jumpCounterOnGlass : MonoBehaviour
{
    [Header("Task Counter Settings")]
    [Space]
    public taskCounterSystem taskCounter;
    
    [Header("Jump Detection Settings")]
    [Space]
    [Tooltip("Минимальная скорость вниз для определения приземления после прыжка")]
    public float minDownwardVelocity = 3f;
    
    [Tooltip("Минимальная скорость вверх для определения прыжка (чтобы отличить от переката)")]
    public float minUpwardVelocityForJump = 2f;
    
    [Tooltip("Время в секундах между прыжками для их подсчета (чтобы избежать множественных срабатываний)")]
    public float jumpDetectionTimeWindow = 0.5f;
    
    [Tooltip("Время в секундах для отслеживания скорости вверх перед приземлением")]
    public float upwardVelocityCheckTime = 2f;
    
    [Header("Debug Settings")]
    [Space]
    [Tooltip("Показывать отладочные сообщения в консоли")]
    public bool showDebugPrint = false;
    
    private float lastJumpTime = 0f;
    private Dictionary<GameObject, float> playerLastUpwardVelocity = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, float> playerLastUpwardVelocityTime = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, bool> playerWasInAir = new Dictionary<GameObject, bool>();
    private Dictionary<GameObject, bool> playerIsInTrigger = new Dictionary<GameObject, bool>();
    
    void OnTriggerEnter(Collider col)
    {
        GameObject character = applyDamage.getCharacter(col.gameObject);
        
        if (character != null)
        {
            playerController playerControllerScript = character.GetComponent<playerController>();
            
            if (playerControllerScript != null)
            {
                // Запоминаем начальное состояние игрока (в воздухе или на земле)
                bool wasInAir = !playerControllerScript.isPlayerOnGround();
                playerWasInAir[character] = wasInAir;
                playerIsInTrigger[character] = true;
                
                if (showDebugPrint)
                {
                    Debug.Log($"[JumpCounterOnGlass] Игрок вошел в триггер. В воздухе: {wasInAir}");
                }
            }
        }
    }
    
    void OnTriggerStay(Collider col)
    {
        GameObject character = applyDamage.getCharacter(col.gameObject);
        
        if (character != null)
        {
            Rigidbody playerRigidbody = character.GetComponent<Rigidbody>();
            playerController playerControllerScript = character.GetComponent<playerController>();
            
            if (playerRigidbody != null && playerControllerScript != null)
            {
                // Получаем текущее состояние игрока
                bool isOnGround = playerControllerScript.isPlayerOnGround();
                // Используем мировую скорость по Y, а не локальную (чтобы избежать проблем с поворотом объекта)
                Vector3 worldVelocity = playerRigidbody.linearVelocity;
                float upwardVelocity = worldVelocity.y;
                
                // Инициализируем состояние, если игрок еще не отслеживается
                if (!playerWasInAir.ContainsKey(character))
                {
                    playerWasInAir[character] = !isOnGround;
                }
                
                bool previousWasInAir = playerWasInAir[character];
                
                // Отслеживаем скорость вверх для определения прыжка
                if (upwardVelocity > minUpwardVelocityForJump)
                {
                    playerLastUpwardVelocity[character] = upwardVelocity;
                    playerLastUpwardVelocityTime[character] = Time.time;
                    
                    if (showDebugPrint)
                    {
                        Debug.Log($"[JumpCounterOnGlass] Отслежена скорость вверх: {upwardVelocity:F2} (требуется: {minUpwardVelocityForJump})");
                    }
                }
                
                // Проверяем переход из "на земле" в "в воздухе" (начало прыжка)
                if (!previousWasInAir && !isOnGround)
                {
                    if (showDebugPrint)
                    {
                        Debug.Log($"[JumpCounterOnGlass] Прыжок начался! Игрок перешел с земли в воздух. Скорость вверх: {upwardVelocity:F2}");
                    }
                }
                
                // Проверяем переход из "в воздухе" в "на земле" (приземление)
                if (previousWasInAir && isOnGround)
                {
                    if (showDebugPrint)
                    {
                        Debug.Log($"[JumpCounterOnGlass] Приземление обнаружено!");
                    }
                    
                    // Проверяем, что прошло достаточно времени с последнего прыжка
                    float timeSinceLastJump = Time.time - lastJumpTime;
                    if (timeSinceLastJump > jumpDetectionTimeWindow)
                    {
                        // Проверяем, что скорость вверх была достаточно большой перед приземлением
                        bool hadUpwardVelocity = false;
                        if (playerLastUpwardVelocity.ContainsKey(character))
                        {
                            float timeSinceUpwardVelocity = Time.time - playerLastUpwardVelocityTime[character];
                            float upwardVel = playerLastUpwardVelocity[character];
                            
                            if (showDebugPrint)
                            {
                                Debug.Log($"[JumpCounterOnGlass] Проверка скорости вверх. Время с последней скорости вверх: {timeSinceUpwardVelocity:F2}s, Скорость: {upwardVel:F2}");
                            }
                            
                            if (timeSinceUpwardVelocity < upwardVelocityCheckTime)
                            {
                                if (upwardVel > minUpwardVelocityForJump)
                                {
                                    hadUpwardVelocity = true;
                                }
                            }
                        }
                        else
                        {
                            if (showDebugPrint)
                            {
                                Debug.Log($"[JumpCounterOnGlass] ✗ Нет данных о скорости вверх. Возможно, это перекат.");
                            }
                        }
                        
                        // Если была достаточная скорость вверх, это прыжок (не перекат)
                        if (hadUpwardVelocity)
                        {
                            lastJumpTime = Time.time;
                            
                            if (showDebugPrint)
                            {
                                float upwardVel = playerLastUpwardVelocity.ContainsKey(character) ? playerLastUpwardVelocity[character] : 0f;
                                Debug.Log($"[JumpCounterOnGlass] ✓ ПРЫЖОК ЗАСЧИТАН! Скорость вверх: {upwardVel:F2}. Счетчик: {(taskCounter != null ? taskCounter.currentTaskCounter + 1 : 0)}");
                            }
                            
                            if (taskCounter != null)
                            {
                                int counterBefore = taskCounter.currentTaskCounter;
                                taskCounter.increaseTaskCounter();
                                int counterAfter = taskCounter.currentTaskCounter;
                                
                                if (showDebugPrint)
                                {
                                    Debug.Log($"[JumpCounterOnGlass] Счетчик изменен: {counterBefore} -> {counterAfter} (требуется: {taskCounter.numberOfTasks})");
                                    
                                    if (counterAfter >= taskCounter.numberOfTasks)
                                    {
                                        Debug.Log($"[JumpCounterOnGlass] ⚠ Счетчик достиг цели! Событие должно быть вызвано. Проверьте настройку eventOnTaskCounterComplete в taskCounterSystem!");
                                    }
                                }
                            }
                            else
                            {
                                if (showDebugPrint)
                                {
                                    Debug.LogError($"[JumpCounterOnGlass] ✗ taskCounter не назначен!");
                                }
                            }
                            
                            // Очищаем данные о скорости вверх после засчитывания прыжка
                            if (playerLastUpwardVelocity.ContainsKey(character))
                            {
                                playerLastUpwardVelocity.Remove(character);
                            }
                            if (playerLastUpwardVelocityTime.ContainsKey(character))
                            {
                                playerLastUpwardVelocityTime.Remove(character);
                            }
                        }
                        else
                        {
                            if (showDebugPrint)
                            {
                                Debug.Log($"[JumpCounterOnGlass] ✗ Перекат игнорирован. Скорость вверх недостаточна или время истекло.");
                            }
                        }
                    }
                    else
                    {
                        if (showDebugPrint)
                        {
                            Debug.Log($"[JumpCounterOnGlass] Слишком рано после последнего прыжка. Время: {timeSinceLastJump:F2}s (требуется: {jumpDetectionTimeWindow}s)");
                        }
                    }
                }
                
                // Обновляем состояние игрока для следующего кадра
                playerWasInAir[character] = !isOnGround;
            }
        }
    }
    
    void OnTriggerExit(Collider col)
    {
        GameObject character = applyDamage.getCharacter(col.gameObject);
        
        if (character != null)
        {
            playerController playerControllerScript = character.GetComponent<playerController>();
            
            // Не очищаем данные, если игрок еще в воздухе - продолжаем отслеживать приземление
            if (playerControllerScript != null)
            {
                bool isInAir = !playerControllerScript.isPlayerOnGround();
                
                if (isInAir)
                {
                    // Игрок в воздухе - продолжаем отслеживать в Update
                    playerIsInTrigger[character] = false;
                    
                    if (showDebugPrint)
                    {
                        Debug.Log($"[JumpCounterOnGlass] Игрок вышел из триггера, но еще в воздухе. Продолжаем отслеживание приземления.");
                    }
                }
                else
                {
                    // Игрок на земле - можно очистить данные
                    if (playerLastUpwardVelocity.ContainsKey(character))
                    {
                        playerLastUpwardVelocity.Remove(character);
                    }
                    if (playerLastUpwardVelocityTime.ContainsKey(character))
                    {
                        playerLastUpwardVelocityTime.Remove(character);
                    }
                    if (playerWasInAir.ContainsKey(character))
                    {
                        playerWasInAir.Remove(character);
                    }
                    if (playerIsInTrigger.ContainsKey(character))
                    {
                        playerIsInTrigger.Remove(character);
                    }
                    
                    if (showDebugPrint)
                    {
                        Debug.Log($"[JumpCounterOnGlass] Игрок вышел из триггера на земле. Данные очищены.");
                    }
                }
            }
        }
    }
    
    void Update()
    {
        // Отслеживаем приземление игроков, которые вышли из триггера, но еще в воздухе
        List<GameObject> charactersToRemove = new List<GameObject>();
        
        foreach (var kvp in playerIsInTrigger)
        {
            GameObject character = kvp.Key;
            
            if (character == null)
            {
                charactersToRemove.Add(character);
                continue;
            }
            
            // Если игрок не в триггере, но мы его отслеживаем
            if (!kvp.Value)
            {
                Rigidbody playerRigidbody = character.GetComponent<Rigidbody>();
                playerController playerControllerScript = character.GetComponent<playerController>();
                
                if (playerRigidbody != null && playerControllerScript != null)
                {
                    bool isOnGround = playerControllerScript.isPlayerOnGround();
                    float upwardVelocity = playerRigidbody.linearVelocity.y;
                    float downwardVelocity = -upwardVelocity;
                    
                    // Если игрок приземлился
                    if (playerWasInAir.ContainsKey(character) && playerWasInAir[character] && isOnGround)
                    {
                        if (showDebugPrint)
                        {
                            Debug.Log($"[JumpCounterOnGlass] Приземление обнаружено вне триггера.");
                        }
                        
                        float timeSinceLastJump = Time.time - lastJumpTime;
                        if (timeSinceLastJump > jumpDetectionTimeWindow)
                        {
                            bool hadUpwardVelocity = false;
                            if (playerLastUpwardVelocity.ContainsKey(character))
                            {
                                float timeSinceUpwardVelocity = Time.time - playerLastUpwardVelocityTime[character];
                                float upwardVel = playerLastUpwardVelocity[character];
                                
                                if (showDebugPrint)
                                {
                                    Debug.Log($"[JumpCounterOnGlass] Проверка скорости вверх (вне триггера). Время: {timeSinceUpwardVelocity:F2}s, Скорость: {upwardVel:F2}");
                                }
                                
                                if (showDebugPrint)
                                {
                                    Debug.Log($"[JumpCounterOnGlass] Условия: timeSinceUpwardVelocity ({timeSinceUpwardVelocity:F2}) < upwardVelocityCheckTime ({upwardVelocityCheckTime:F2}) = {timeSinceUpwardVelocity < upwardVelocityCheckTime}, upwardVel ({upwardVel:F2}) > minUpwardVelocityForJump ({minUpwardVelocityForJump:F2}) = {upwardVel > minUpwardVelocityForJump}");
                                }
                                
                                if (timeSinceUpwardVelocity < upwardVelocityCheckTime && upwardVel > minUpwardVelocityForJump)
                                {
                                    hadUpwardVelocity = true;
                                    
                                    if (showDebugPrint)
                                    {
                                        Debug.Log($"[JumpCounterOnGlass] ✓ Условие скорости вверх выполнено!");
                                    }
                                }
                                else
                                {
                                    if (showDebugPrint)
                                    {
                                        Debug.Log($"[JumpCounterOnGlass] ✗ Условие скорости вверх НЕ выполнено!");
                                    }
                                }
                            }
                            
                            if (hadUpwardVelocity)
                            {
                                lastJumpTime = Time.time;
                                
                                if (showDebugPrint)
                                {
                                    float upwardVel = playerLastUpwardVelocity.ContainsKey(character) ? playerLastUpwardVelocity[character] : 0f;
                                    Debug.Log($"[JumpCounterOnGlass] ✓ ПРЫЖОК ЗАСЧИТАН (вне триггера)! Скорость вверх: {upwardVel:F2}. Счетчик: {(taskCounter != null ? taskCounter.currentTaskCounter + 1 : 0)}");
                                }
                                
                                if (taskCounter != null)
                                {
                                    int counterBefore = taskCounter.currentTaskCounter;
                                    taskCounter.increaseTaskCounter();
                                    int counterAfter = taskCounter.currentTaskCounter;
                                    
                                    if (showDebugPrint)
                                    {
                                        Debug.Log($"[JumpCounterOnGlass] Счетчик изменен (вне триггера): {counterBefore} -> {counterAfter} (требуется: {taskCounter.numberOfTasks})");
                                        
                                        if (counterAfter >= taskCounter.numberOfTasks)
                                        {
                                            Debug.Log($"[JumpCounterOnGlass] ⚠ Счетчик достиг цели! Событие должно быть вызвано. Проверьте настройку eventOnTaskCounterComplete в taskCounterSystem!");
                                        }
                                    }
                                }
                                else
                                {
                                    if (showDebugPrint)
                                    {
                                        Debug.LogError($"[JumpCounterOnGlass] ✗ taskCounter не назначен!");
                                    }
                                }
                            }
                            else
                            {
                                if (showDebugPrint)
                                {
                                    Debug.Log($"[JumpCounterOnGlass] ✗ Перекат игнорирован (вне триггера).");
                                }
                            }
                        }
                        
                        // Очищаем данные после приземления
                        charactersToRemove.Add(character);
                    }
                    else
                    {
                        // Обновляем состояние
                        playerWasInAir[character] = !isOnGround;
                    }
                }
                else
                {
                    charactersToRemove.Add(character);
                }
            }
        }
        
        // Очищаем данные для удаленных игроков
        foreach (var character in charactersToRemove)
        {
            if (playerLastUpwardVelocity.ContainsKey(character))
            {
                playerLastUpwardVelocity.Remove(character);
            }
            if (playerLastUpwardVelocityTime.ContainsKey(character))
            {
                playerLastUpwardVelocityTime.Remove(character);
            }
            if (playerWasInAir.ContainsKey(character))
            {
                playerWasInAir.Remove(character);
            }
            if (playerIsInTrigger.ContainsKey(character))
            {
                playerIsInTrigger.Remove(character);
            }
        }
    }
}
