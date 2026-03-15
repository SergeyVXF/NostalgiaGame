using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class WaypointInfo
{
    public Transform point;
    [Tooltip("Длительность остановки на точке (сек), 0 = не останавливаться")] public float stopDuration = 0f;
}

public class CarAIController : MonoBehaviour
{
    [Header("Маршрут (точки движения)")]
    private List<WaypointInfo> waypoints; // Список точек маршрута с паузами (только через CarSpawner)
    public float speed = 5f; // Скорость движения
    public float rotationSpeed = 5f; // Скорость поворота корпуса
    public float stopDistance = 1f; // Расстояние, при котором считается, что машина доехала до точки

    [Header("Детектор препятствий перед машиной")]
    public BoxCollider detectionZone; // Коллайдер-триггер перед машиной
    public List<string> stopTags; // Теги объектов, из-за которых машина должна останавливаться

    [Header("Телепортация")]
    [Tooltip("Индекс точки, при достижении которой машина телепортируется к waypoint 1 (-1 = не использовать)")]
    public int teleportAtWaypointIndex = -1;

    [Header("Музыка из авто")]
    public List<AudioClip> musicTracks;
    public AudioSource audioSource;

    private int currentWaypoint = 0;
    private bool isStopped = false;
    private List<Collider> objectsInZone = new List<Collider>();
    private Rigidbody rb;
    public bool reachedLastWaypoint = false;
    private float stopTimer = 0f;
    private bool waitingAtWaypoint = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;
        rb.mass = 1000f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        // Воспроизводим случайный трек
        if (audioSource != null && musicTracks != null && musicTracks.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, musicTracks.Count);
            audioSource.clip = musicTracks[randomIndex];
            audioSource.Play();
        }
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Count == 0 || isStopped) return;

        if (waypoints == null || waypoints.Count == 0) return;
        WaypointInfo wp = waypoints[currentWaypoint];
        Transform target = wp.point;
        Vector3 direction = (target.position - transform.position);
        direction.y = 0;

        if (waitingAtWaypoint)
        {
            stopTimer -= Time.fixedDeltaTime;
            if (stopTimer <= 0f)
            {
                waitingAtWaypoint = false;
                if (currentWaypoint < waypoints.Count - 1)
                {
                    currentWaypoint++;
                }
                else
                {
                    reachedLastWaypoint = true;
                }
            }
            return;
        }

        if (direction.magnitude > 0.01f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, lookRotation, rotationSpeed * Time.fixedDeltaTime));
        }

        Vector3 move = direction.normalized * speed * Time.fixedDeltaTime;
        move.y = 0;
        rb.MovePosition(rb.position + move);

        if (direction.magnitude < stopDistance)
        {
            if (wp.stopDuration > 0f)
            {
                waitingAtWaypoint = true;
                stopTimer = wp.stopDuration;
            }
            else
            {
                if (currentWaypoint < waypoints.Count - 1)
                {
                    currentWaypoint++;
                }
                else
                {
                    reachedLastWaypoint = true;
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (detectionZone != null && other.transform != transform)
        {
            bool isStopTag = stopTags.Contains(other.tag);
            bool isOtherCar = other.GetComponent<CarAIController>() != null && other.gameObject != this.gameObject;
            if (isStopTag || isOtherCar)
            {
                if (!objectsInZone.Contains(other))
                    objectsInZone.Add(other);
                isStopped = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (detectionZone != null && other.transform != transform)
        {
            bool isStopTag = stopTags.Contains(other.tag);
            bool isOtherCar = other.GetComponent<CarAIController>() != null && other.gameObject != this.gameObject;
            if (isStopTag || isOtherCar)
            {
                if (objectsInZone.Contains(other))
                    objectsInZone.Remove(other);
                if (objectsInZone.Count == 0)
                {
                    isStopped = false;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (detectionZone != null)
        {
            Gizmos.color = Color.red;
            Gizmos.matrix = detectionZone.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(detectionZone.center, detectionZone.size);
        }
    }

    public bool IsAtLastWaypoint()
    {
        if (waypoints == null || waypoints.Count == 0) return false;
        return currentWaypoint == waypoints.Count - 1;
    }

    public bool IsAtLastWaypointReached()
    {
        return reachedLastWaypoint;
    }

    public void SetWaypoints(List<WaypointInfo> newWaypoints)
    {
        waypoints = newWaypoints;
        currentWaypoint = 0;
        reachedLastWaypoint = false;
        waitingAtWaypoint = false;
        stopTimer = 0f;
    }
} 