using UnityEngine;
using System.Collections.Generic;

public class RopePointCreator : MonoBehaviour
{
    [Header("Rope Point Creation")]
    [Tooltip("Количество точек для создания")]
    public int numberOfPoints = 3;
    
    [Tooltip("Расстояние между точками")]
    public float distanceBetweenPoints = 5f;
    
    [Tooltip("Высота точек (Y координата)")]
    public float pointHeight = 10f;
    
    [Tooltip("Создать точки в виде дуги")]
    public bool createArc = false;
    
    [Tooltip("Радиус дуги (если createArc = true)")]
    public float arcRadius = 10f;
    
    [Tooltip("Угол дуги в градусах")]
    [Range(0f, 180f)]
    public float arcAngle = 90f;

    [Header("Rope Line Reference")]
    [Tooltip("Ссылка на RopeLine компонент")]
    public RopeLine ropeLine;

    [ContextMenu("Create Rope Points")]
    public void CreateRopePoints()
    {
        if (numberOfPoints < 2)
        {
            Debug.LogError("Нужно минимум 2 точки!");
            return;
        }

        // Удаляем старые точки
        ClearRopePoints();

        // Создаем новые точки
        List<Transform> newPoints = new List<Transform>();

        for (int i = 0; i < numberOfPoints; i++)
        {
            GameObject point = new GameObject($"RopePoint_{i}");
            point.transform.SetParent(transform);

            Vector3 position;
            if (createArc)
            {
                // Создаем точки в виде дуги
                float angle = (arcAngle / (numberOfPoints - 1)) * i * Mathf.Deg2Rad;
                position = new Vector3(
                    Mathf.Cos(angle) * arcRadius,
                    pointHeight,
                    Mathf.Sin(angle) * arcRadius
                );
            }
            else
            {
                // Создаем точки по прямой линии
                position = new Vector3(
                    i * distanceBetweenPoints,
                    pointHeight,
                    0f
                );
            }

            point.transform.position = position;
            newPoints.Add(point.transform);
        }

        // Обновляем RopeLine
        if (ropeLine != null)
        {
            ropeLine.ropePoints = newPoints;
            ropeLine.UpdateCurvedLine();
        }

        Debug.Log($"Создано {numberOfPoints} точек веревки!");
    }

    [ContextMenu("Clear Rope Points")]
    public void ClearRopePoints()
    {
        // Удаляем все дочерние объекты
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(transform.GetChild(i).gameObject);
            else
                DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // Очищаем список точек в RopeLine
        if (ropeLine != null)
        {
            ropeLine.ropePoints.Clear();
            ropeLine.UpdateCurvedLine();
        }

        Debug.Log("Все точки веревки удалены!");
    }

    [ContextMenu("Update Rope Line")]
    public void UpdateRopeLine()
    {
        if (ropeLine != null)
        {
            // Собираем все дочерние точки
            List<Transform> points = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                points.Add(transform.GetChild(i));
            }

            ropeLine.ropePoints = points;
            ropeLine.UpdateCurvedLine();
            Debug.Log($"Обновлена веревка с {points.Count} точками!");
        }
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            // Показываем предварительный вид точек
            Gizmos.color = Color.cyan;
            for (int i = 0; i < numberOfPoints; i++)
            {
                Vector3 position;
                if (createArc)
                {
                    float angle = (arcAngle / (numberOfPoints - 1)) * i * Mathf.Deg2Rad;
                    position = new Vector3(
                        Mathf.Cos(angle) * arcRadius,
                        pointHeight,
                        Mathf.Sin(angle) * arcRadius
                    );
                }
                else
                {
                    position = new Vector3(
                        i * distanceBetweenPoints,
                        pointHeight,
                        0f
                    );
                }

                Gizmos.DrawWireSphere(position, 0.5f);
                
                // Рисуем линии между точками
                if (i > 0)
                {
                    Vector3 prevPosition;
                    if (createArc)
                    {
                        float prevAngle = (arcAngle / (numberOfPoints - 1)) * (i - 1) * Mathf.Deg2Rad;
                        prevPosition = new Vector3(
                            Mathf.Cos(prevAngle) * arcRadius,
                            pointHeight,
                            Mathf.Sin(prevAngle) * arcRadius
                        );
                    }
                    else
                    {
                        prevPosition = new Vector3(
                            (i - 1) * distanceBetweenPoints,
                            pointHeight,
                            0f
                        );
                    }
                    
                    Gizmos.DrawLine(prevPosition, position);
                }
            }
        }
    }
} 