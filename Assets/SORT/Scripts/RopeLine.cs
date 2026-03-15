using UnityEngine;
using Invector.vCharacterController;
using System.Collections.Generic;

public class RopeLine : MonoBehaviour
{
    [Header("Rope Settings")]
    [Tooltip("Список точек веревки (минимум 2)")]
    public List<Transform> ropePoints = new List<Transform>();
    
    [Tooltip("Скорость движения по веревке")]
    public float moveSpeed = 5f;
    
    [Tooltip("Материал веревки")]
    public Material ropeMaterial;
    
    [Header("Curve Settings")]
    [Tooltip("Сила изгиба веревки вниз (0 = прямая линия, 1 = максимальный изгиб)")]
    [Range(0f, 1f)]
    public float curveStrength = 0.3f;
    
    [Tooltip("Количество сегментов для изогнутой линии (больше = плавнее)")]
    [Range(10, 100)]
    public int curveSegments = 30;
    
    [Header("Interaction")]
    [Tooltip("Клавиша взаимодействия")]
    public KeyCode interactKey = KeyCode.E;
    
    [Tooltip("Размер бокса взаимодействия")]
    public Vector3 interactionBoxSize = new Vector3(2f, 2f, 2f);
    
    [Header("Jump Settings")]
    [Tooltip("Сила прыжка при откреплении")]
    public float jumpDismountForce = 7f;
    
    [Tooltip("Вертикальная сила прыжка")]
    public float jumpDismountUpForce = 5f;
    
    [Header("Particle System")]
    [Tooltip("Particle System для игрока во время движения по веревке")]
    public ParticleSystem ropeSlideParticles;
    
    [Tooltip("Смещение Particle System относительно игрока")]
    public Vector3 particleOffset = new Vector3(0f, -1f, 0f);
    
    [Header("RopeLine: Кастомный угол поворота по Y (опционально)")]
    [Tooltip("Использовать кастомный угол поворота")]
    public bool useCustomYRotation = false;
    
    [Tooltip("Кастомный угол поворота по Y")]
    public float customYRotation = 0f;
    
    [Header("Direction Settings")]
    [Tooltip("Определять направление движения на основе поворота игрока")]
    public bool usePlayerRotationForDirection = true;
    
    [Tooltip("Угол поворота для определения направления (в градусах)")]
    [Range(0f, 180f)]
    public float directionThresholdAngle = 90f;
    
    [Header("Input Settings")]
    [Tooltip("Клавиша для движения вперед по веревке")]
    public KeyCode forwardKey = KeyCode.W;
    
    [Tooltip("Клавиша для движения назад по веревке")]
    public KeyCode backwardKey = KeyCode.S;

    private bool isPlayerAttached = false;
    private Transform attachedPlayer;
    private Vector3 lastPosition;
    private float currentDistance = 0f;
    private bool movingForward = true;
    private int currentRopePointIndex = 0;
    private vThirdPersonController playerController;
    private Animator playerAnimator;
    private bool isJumping = false;
    private List<BoxCollider> interactionBoxes = new List<BoxCollider>();
    private Vector3 ropeMoveVelocity = Vector3.zero;
    private Rigidbody playerRigidbody;
    private CharacterController playerCharController;
    
    // Кэшированные точки изогнутой линии
    private List<Vector3> curvedLinePoints = new List<Vector3>();
    private float totalCurvedDistance = 0f;
    
    // Particle System
    private ParticleSystem playerParticleSystem;
    private bool particlesActive = false;

    private void Start()
    {
        if (ropePoints.Count < 2)
        {
            Debug.LogError("RopeLine: Нужно минимум 2 точки для веревки!");
            return;
        }

        SetupInteractionBoxes();
        GenerateCurvedLine();
        DrawRopeLine();
    }

    private void SetupInteractionBoxes()
    {
        // Очищаем старые боксы
        foreach (var box in interactionBoxes)
        {
            if (box != null)
                DestroyImmediate(box.gameObject);
        }
        interactionBoxes.Clear();

        // Создаем боксы для каждой точки
        for (int i = 0; i < ropePoints.Count; i++)
        {
            if (ropePoints[i] == null) continue;

            GameObject interactionBox = new GameObject($"InteractionBox_{i}");
            interactionBox.transform.SetParent(ropePoints[i]);
            interactionBox.transform.localPosition = Vector3.zero;
            
            BoxCollider boxCollider = interactionBox.AddComponent<BoxCollider>();
            boxCollider.size = interactionBoxSize;
            boxCollider.isTrigger = true;
            
            interactionBoxes.Add(boxCollider);
        }
    }

    private void GenerateCurvedLine()
    {
        curvedLinePoints.Clear();
        totalCurvedDistance = 0f;

        if (ropePoints.Count < 2) return;

        // Создаем изогнутую линию между всеми точками
        for (int i = 0; i < ropePoints.Count - 1; i++)
        {
            Vector3 startPoint = ropePoints[i].position;
            Vector3 endPoint = ropePoints[i + 1].position;
            
            // Добавляем сегменты между текущей и следующей точкой
            for (int j = 0; j <= curveSegments; j++)
            {
                float t = (float)j / curveSegments;
                Vector3 point = Vector3.Lerp(startPoint, endPoint, t);
                
                // Применяем изгиб вниз
                float curveOffset = Mathf.Sin(t * Mathf.PI) * curveStrength * Vector3.Distance(startPoint, endPoint) * 0.3f;
                point.y -= curveOffset;
                
                curvedLinePoints.Add(point);
                
                // Вычисляем общую длину
                if (j > 0)
                {
                    totalCurvedDistance += Vector3.Distance(curvedLinePoints[curvedLinePoints.Count - 2], point);
                }
            }
        }
    }

    private void Update()
    {
        if (!isPlayerAttached)
        {
            CheckForPlayerInteraction();
        }
        else
        {
            HandleRopeMovement();
        }
    }

    private void CheckForPlayerInteraction()
    {
        // Проверяем все точки взаимодействия
        for (int i = 0; i < ropePoints.Count; i++)
        {
            if (ropePoints[i] == null) continue;

            Collider[] colliders = Physics.OverlapBox(ropePoints[i].position, interactionBoxSize / 2f);
            foreach (Collider col in colliders)
            {
                if (col.CompareTag("Player") && Input.GetKeyDown(interactKey))
                {
                    Debug.Log($"Player detected near point {i} and E pressed!");
                    AttachPlayer(col.transform, i);
                    break;
                }
            }
        }
    }

    private void HandleRopeMovement()
    {
        if (isJumping || curvedLinePoints.Count == 0) return;

        // Проверяем нажатие Space для спрыгивания с веревки
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Space pressed - jumping off rope!");
            JumpOffRope();
            return;
        }

        // Проверяем изменение направления на основе поворота игрока
        if (usePlayerRotationForDirection && attachedPlayer != null)
        {
            CheckDirectionChange();
        }

        // Обновляем текущее расстояние в зависимости от направления
        if (movingForward)
        {
            currentDistance += moveSpeed * Time.deltaTime;
            if (currentDistance >= totalCurvedDistance)
            {
                DetachPlayer();
                return;
            }
        }
        else
        {
            currentDistance -= moveSpeed * Time.deltaTime;
            if (currentDistance <= 0)
            {
                DetachPlayer();
                return;
            }
        }

        // Находим позицию на изогнутой линии
        Vector3 newPosition = GetPositionOnCurvedLine(currentDistance);
        
        // Обновляем позицию игрока
        if (attachedPlayer != null)
        {
            Vector3 currentPos = attachedPlayer.position;
            Vector3 targetPos = newPosition;
            float lerpFactor = 0.8f;
            float y = targetPos.y;
            float x = Mathf.Lerp(currentPos.x, targetPos.x, lerpFactor);
            float z = Mathf.Lerp(currentPos.z, targetPos.z, lerpFactor);
            attachedPlayer.position = new Vector3(x, y, z);
            lastPosition = newPosition;

            // Обновляем позицию Particle System
            UpdateParticleSystemPosition();

            // Поворачиваем игрока в направлении движения
            if (useCustomYRotation)
            {
                Vector3 euler = attachedPlayer.eulerAngles;
                euler.y = customYRotation;
                attachedPlayer.eulerAngles = euler;
            }
            else
            {
                // Находим направление движения на изогнутой линии
                Vector3 direction = GetDirectionOnCurvedLine(currentDistance);
                direction.y = 0;
                if (direction.sqrMagnitude > 0.01f)
                    attachedPlayer.forward = direction.normalized;
            }
        }
    }

    private Vector3 GetPositionOnCurvedLine(float distance)
    {
        if (curvedLinePoints.Count == 0) return Vector3.zero;

        // Находим сегмент на изогнутой линии
        float accumulatedDistance = 0f;
        for (int i = 0; i < curvedLinePoints.Count - 1; i++)
        {
            float segmentLength = Vector3.Distance(curvedLinePoints[i], curvedLinePoints[i + 1]);
            if (accumulatedDistance + segmentLength >= distance)
            {
                // Интерполируем внутри сегмента
                float localDistance = distance - accumulatedDistance;
                float t = localDistance / segmentLength;
                return Vector3.Lerp(curvedLinePoints[i], curvedLinePoints[i + 1], t);
            }
            accumulatedDistance += segmentLength;
        }

        return curvedLinePoints[curvedLinePoints.Count - 1];
    }

    private Vector3 GetDirectionOnCurvedLine(float distance)
    {
        if (curvedLinePoints.Count < 2) return Vector3.forward;

        // Находим сегмент на изогнутой линии
        float accumulatedDistance = 0f;
        for (int i = 0; i < curvedLinePoints.Count - 1; i++)
        {
            float segmentLength = Vector3.Distance(curvedLinePoints[i], curvedLinePoints[i + 1]);
            if (accumulatedDistance + segmentLength >= distance)
            {
                // Возвращаем направление сегмента
                return (curvedLinePoints[i + 1] - curvedLinePoints[i]).normalized;
            }
            accumulatedDistance += segmentLength;
        }

        return (curvedLinePoints[curvedLinePoints.Count - 1] - curvedLinePoints[curvedLinePoints.Count - 2]).normalized;
    }

    private void AttachPlayer(Transform player, int startPointIndex)
    {
        attachedPlayer = player;
        isPlayerAttached = true;
        lastPosition = player.position;
        currentRopePointIndex = startPointIndex;
        
        // Отключаем ragdoll, если он есть
        var ragdoll = player.GetComponent<Invector.vCharacterController.vRagdoll>();
        if (ragdoll != null && ragdoll.isActive)
        {
            ragdoll.isActive = false;
            if (ragdoll.iChar != null)
                ragdoll.iChar.ResetRagdoll();
        }

        // Отключаем Rigidbody и CharacterController
        playerRigidbody = player.GetComponent<Rigidbody>();
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = true;
        }
        playerCharController = player.GetComponent<CharacterController>();
        if (playerCharController != null)
        {
            playerCharController.enabled = false;
        }
        
        // Устанавливаем начальную позицию и направление
        if (startPointIndex == 0)
        {
            currentDistance = 0f;
            movingForward = true;
            player.position = ropePoints[0].position;
        }
        else if (startPointIndex == ropePoints.Count - 1)
        {
            currentDistance = totalCurvedDistance;
            movingForward = false;
            player.position = ropePoints[ropePoints.Count - 1].position;
        }
        else
        {
            // Находим расстояние до этой точки на изогнутой линии
            currentDistance = GetDistanceToPoint(startPointIndex);
            
            // Определяем направление на основе поворота игрока
            if (usePlayerRotationForDirection)
            {
                DetermineDirectionFromPlayerRotation(startPointIndex);
            }
            else
            {
                movingForward = startPointIndex < ropePoints.Count / 2;
            }
            
            player.position = ropePoints[startPointIndex].position;
        }

        // Отключаем контроллер игрока
        playerController = player.GetComponent<vThirdPersonController>();
        if (playerController != null)
        {
            playerController.enabled = false;
            Debug.Log("Invector controller found and disabled");
        }

        // Получаем аниматор
        playerAnimator = player.GetComponent<Animator>();
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("RopeSlide", true);
        }
        
        // Получаем контроллер слайдинга и принудительно активируем ropeslide
        var slideController = player.GetComponent<SlideAnimationController>();
        if (slideController != null)
        {
            slideController.ForceRopeSlide(true);
        }

        // Настраиваем Particle System
        SetupParticleSystem(player);
    }
    
    private void DetermineDirectionFromPlayerRotation(int pointIndex)
    {
        if (attachedPlayer == null || pointIndex < 0 || pointIndex >= ropePoints.Count) return;
        
        // Получаем направление взгляда игрока
        Vector3 playerForward = attachedPlayer.forward;
        playerForward.y = 0f; // Игнорируем вертикальную составляющую
        playerForward.Normalize();
        
        // Получаем направления к соседним точкам
        Vector3 directionToNext = Vector3.zero;
        Vector3 directionToPrev = Vector3.zero;
        
        if (pointIndex < ropePoints.Count - 1)
        {
            directionToNext = (ropePoints[pointIndex + 1].position - ropePoints[pointIndex].position).normalized;
            directionToNext.y = 0f;
        }
        
        if (pointIndex > 0)
        {
            directionToPrev = (ropePoints[pointIndex - 1].position - ropePoints[pointIndex].position).normalized;
            directionToPrev.y = 0f;
        }
        
        // Вычисляем углы между направлением игрока и направлениями к точкам
        float angleToNext = 0f;
        float angleToPrev = 0f;
        
        if (directionToNext.sqrMagnitude > 0.01f)
        {
            angleToNext = Vector3.Angle(playerForward, directionToNext);
        }
        
        if (directionToPrev.sqrMagnitude > 0.01f)
        {
            angleToPrev = Vector3.Angle(playerForward, directionToPrev);
        }
        
        // Определяем направление движения
        if (angleToNext <= directionThresholdAngle && angleToNext < angleToPrev)
        {
            // Игрок смотрит в сторону следующей точки
            movingForward = true;
            Debug.Log($"[RopeLine] Игрок смотрит вперед (к точке {pointIndex + 1}), угол: {angleToNext:F1}°");
        }
        else if (angleToPrev <= directionThresholdAngle && angleToPrev < angleToNext)
        {
            // Игрок смотрит в сторону предыдущей точки
            movingForward = false;
            Debug.Log($"[RopeLine] Игрок смотрит назад (к точке {pointIndex - 1}), угол: {angleToPrev:F1}°");
        }
        else
        {
            // Если углы слишком большие, используем направление по умолчанию
            movingForward = pointIndex < ropePoints.Count / 2;
            Debug.Log($"[RopeLine] Используется направление по умолчанию: {(movingForward ? "вперед" : "назад")}");
        }
    }
    
    private void CheckDirectionChange()
    {
        if (attachedPlayer == null) return;
        
        // Получаем направление взгляда игрока
        Vector3 playerForward = attachedPlayer.forward;
        playerForward.y = 0f;
        playerForward.Normalize();
        
        // Получаем текущее направление движения
        Vector3 currentDirection = GetDirectionOnCurvedLine(currentDistance);
        currentDirection.y = 0f;
        currentDirection.Normalize();
        
        // Вычисляем угол между направлением игрока и направлением движения
        float angle = Vector3.Angle(playerForward, currentDirection);
        
        // Если игрок повернулся на большой угол, меняем направление
        if (angle > directionThresholdAngle)
        {
            // Определяем, в какую сторону повернулся игрок
            Vector3 cross = Vector3.Cross(currentDirection, playerForward);
            bool shouldGoForward = cross.y > 0;
            
            // Меняем направление только если это безопасно
            if (shouldGoForward && currentDistance < totalCurvedDistance - 1f)
            {
                movingForward = true;
                Debug.Log("[RopeLine] Изменено направление на ВПЕРЕД");
            }
            else if (!shouldGoForward && currentDistance > 1f)
            {
                movingForward = false;
                Debug.Log("[RopeLine] Изменено направление на НАЗАД");
            }
        }
    }
    
    private void SetupParticleSystem(Transform player)
    {
        // Ищем или создаем RopeSlideParticleSpawner на игроке
        var particleSpawner = player.GetComponent<RopeSlideParticleSpawner>();
        if (particleSpawner == null)
        {
            particleSpawner = player.gameObject.AddComponent<RopeSlideParticleSpawner>();
        }
        
        // Если указан Particle System в RopeLine - передаем его в spawner
        if (ropeSlideParticles != null)
        {
            particleSpawner.SetParticleSystem(ropeSlideParticles);
        }
        
        // Активируем Particle System
        particleSpawner.StartRopeSliding();
    }
    
    private void UpdateParticleSystemPosition()
    {
        // Позиция обновляется автоматически в RopeSlideParticleSpawner
    }
    
    private void StopParticleSystem()
    {
        if (attachedPlayer != null)
        {
            var particleSpawner = attachedPlayer.GetComponent<RopeSlideParticleSpawner>();
            if (particleSpawner != null)
            {
                particleSpawner.StopRopeSliding();
            }
        }
    }

    private float GetDistanceToPoint(int pointIndex)
    {
        if (pointIndex == 0) return 0f;
        if (pointIndex >= ropePoints.Count) return totalCurvedDistance;

        float distance = 0f;
        for (int i = 0; i < pointIndex; i++)
        {
            // Добавляем длину сегмента между точками
            Vector3 startPoint = ropePoints[i].position;
            Vector3 endPoint = ropePoints[i + 1].position;
            distance += Vector3.Distance(startPoint, endPoint);
        }
        return distance;
    }

    private void DetachPlayer()
    {
        if (attachedPlayer != null)
        {
            // Останавливаем Particle System
            StopParticleSystem();

            // Включаем Rigidbody и CharacterController обратно
            if (playerRigidbody != null)
            {
                playerRigidbody.isKinematic = false;
            }
            if (playerCharController != null)
            {
                playerCharController.enabled = true;
            }

            // Включаем контроллер игрока
            if (playerController != null)
            {
                playerController.enabled = true;
                Debug.Log("Re-enabling Invector controller");
            }

            // Останавливаем анимацию
            if (playerAnimator != null)
            {
                playerAnimator.SetBool("RopeSlide", false);
            }
            
            // Отключаем ropeslide в контроллере слайдинга
            var slideController = attachedPlayer.GetComponent<SlideAnimationController>();
            if (slideController != null)
            {
                slideController.ForceRopeSlide(false);
            }

            attachedPlayer = null;
            isPlayerAttached = false;
            currentDistance = 0f;
        }
    }

    private void JumpOffRope()
    {
        if (isJumping) return;
        isJumping = true;

        // Получаем направление камеры
        Vector3 cameraForward = Camera.main.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        // Применяем силу прыжка
        Rigidbody playerRigidbody = attachedPlayer.GetComponent<Rigidbody>();
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.AddForce(cameraForward * jumpDismountForce, ForceMode.Impulse);
            playerRigidbody.AddForce(Vector3.up * jumpDismountUpForce, ForceMode.Impulse);
        }

        DetachPlayer();
        isJumping = false;
    }

    private void DrawRopeLine()
    {
        LineRenderer lineRenderer = gameObject.GetComponent<LineRenderer>();
        if (!lineRenderer)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        
        // Устанавливаем материал
        if (ropeMaterial != null)
        {
            lineRenderer.material = ropeMaterial;
        }
        else
        {
            Debug.LogWarning("RopeLine: Material not set! Using default material.");
            ropeMaterial = new Material(Shader.Find("Sprites/Default"));
            ropeMaterial.color = Color.white;
            lineRenderer.material = ropeMaterial;
        }
        
        // Устанавливаем точки изогнутой линии
        lineRenderer.positionCount = curvedLinePoints.Count;
        lineRenderer.SetPositions(curvedLinePoints.ToArray());
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
    }

    private void OnDrawGizmos()
    {
        if (ropePoints.Count < 2) return;

        // Рисуем линии между точками
        Gizmos.color = Color.yellow;
        for (int i = 0; i < ropePoints.Count - 1; i++)
        {
            if (ropePoints[i] != null && ropePoints[i + 1] != null)
            {
                Gizmos.DrawLine(ropePoints[i].position, ropePoints[i + 1].position);
            }
        }
        
        // Отображаем зоны взаимодействия
        Gizmos.color = Color.green;
        for (int i = 0; i < ropePoints.Count; i++)
        {
            if (ropePoints[i] != null)
            {
                Gizmos.DrawWireCube(ropePoints[i].position, interactionBoxSize);
            }
        }
    }

    // Публичный метод для обновления изогнутой линии (вызывать при изменении точек)
    public void UpdateCurvedLine()
    {
        GenerateCurvedLine();
        DrawRopeLine();
    }

    // Публичный метод для принудительной активации/деактивации Particle System
    public void SetParticleSystemActive(bool active)
    {
        if (attachedPlayer != null)
        {
            var particleSpawner = attachedPlayer.GetComponent<RopeSlideParticleSpawner>();
            if (particleSpawner != null)
            {
                if (active)
                {
                    particleSpawner.StartRopeSliding();
                }
                else
                {
                    particleSpawner.StopRopeSliding();
                }
            }
        }
    }
    
    // Публичный метод для изменения направления движения
    public void ChangeDirection(bool goForward)
    {
        if (isPlayerAttached && !isJumping)
        {
            movingForward = goForward;
            Debug.Log($"[RopeLine] Направление изменено на: {(goForward ? "ВПЕРЕД" : "НАЗАД")}");
        }
    }
    
    // Публичный метод для получения текущего направления
    public bool IsMovingForward()
    {
        return movingForward;
    }
    
    // Публичный метод для получения текущей точки веревки
    public int GetCurrentRopePointIndex()
    {
        return currentRopePointIndex;
    }
    
    // Публичный метод для прикрепления игрока извне
    public void AttachPlayerExternal(Transform player, int startPointIndex)
    {
        AttachPlayer(player, startPointIndex);
    }
    
    // Публичный метод для открепления игрока извне
    public void DetachPlayerExternal()
    {
        DetachPlayer();
    }
} 