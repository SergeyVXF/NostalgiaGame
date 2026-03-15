using UnityEngine;
using System.Collections.Generic;

namespace Invector
{
    public class CarAI_V02 : vMonoBehaviour
    {
        #region Variables

        public List<vPlatformPoint> points = new List<vPlatformPoint>();
        [Tooltip("Movement speed between points")]
        public float defaultSpeed = 1f;
        [Tooltip("Time to stay in current point")]
        public float defaultStayTime = 2f;
        [Tooltip("Index to Starting point")]
        public int startIndex;

        [HideInInspector]
        public bool canMove;

        Vector3 oldEuler;
        int index = 0;
        bool invert;
        float currentTime;
        float currentSpeed;
        float dist, currentDist;
        Transform targetTransform;

        [Tooltip("Скорость поворота (градусов в секунду)")]
        public float rotationSpeed = 180f;

        #endregion

        void OnDrawGizmos()
        {
            if (points == null || points.Count == 0 || startIndex >= points.Count) return;
            Transform oldT = points[0].transform;
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            if (!Application.isPlaying)
            {
                transform.position = points[startIndex].transform.position;
                transform.eulerAngles = points[startIndex].transform.eulerAngles;
            }

            foreach (vPlatformPoint t in points)
            {
                if (t.transform != null && t.transform != oldT)
                {
                    Gizmos.DrawLine(oldT.position, t.transform.position);
                    oldT = t.transform;
                }
            }

            foreach (vPlatformPoint t in points)
            {
                if (t.transform)
                {
                    Matrix4x4 rotationMatrix = Matrix4x4.TRS(t.transform.position, t.transform.rotation, transform.lossyScale);
                    Gizmos.matrix = rotationMatrix;
                    Gizmos.DrawCube(Vector3.zero, Vector3.one);
                }
            }
        }

        void Start()
        {
            if (points.Count == 0 || startIndex >= points.Count) return;
            if (points.Count < 2) return;
            transform.position = points[startIndex].transform.position;
            transform.eulerAngles = points[startIndex].transform.eulerAngles;
            oldEuler = transform.eulerAngles;
            var targetIndex = startIndex;

            if (startIndex + 1 < points.Count) targetIndex++;
            else if (startIndex - 1 > 0)
            {
                targetIndex--; invert = true;
            }

            dist = Vector3.Distance(transform.position, points[targetIndex].transform.position);
            targetTransform = points[targetIndex].transform;
            currentTime = points[startIndex].useDefaultStayTime ? defaultStayTime : points[index].stayTime;
            currentSpeed = points[startIndex].useDefaultSpeed ? defaultSpeed : points[index].speedToNextPoint;
            index = targetIndex;
            canMove = true;
        }

        void FixedUpdate()
        {
            if (points.Count == 0 && !canMove) return;

            currentDist = Vector3.Distance(transform.position, targetTransform.position);

            if (currentTime <= 0)
            {
                var distFactor = (float)Mathf.Clamp((100f - ((float)(100f * currentDist) / dist)) * 0.01f, 0, 1f);
                // Движение
                transform.position = Vector3.MoveTowards(transform.position, targetTransform.position, currentSpeed * Time.deltaTime);

                // --- Новый поворот ---
                Quaternion targetRot;
                // Если rotation у точки (0,0,0), то поворачиваем по траектории
                if (targetTransform.eulerAngles == Vector3.zero)
                {
                    Vector3 dir = (targetTransform.position - transform.position).normalized;
                    if (dir.sqrMagnitude > 0.001f)
                        targetRot = Quaternion.LookRotation(dir, Vector3.up);
                    else
                        targetRot = transform.rotation;
                }
                else
                {
                    targetRot = Quaternion.Euler(targetTransform.eulerAngles);
                }
                // Плавный поворот
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                // --- Конец нового поворота ---
            }
            else
                currentTime -= Time.fixedDeltaTime;

            if (currentDist < 0.02f)
            {
                currentSpeed = points[index].useDefaultSpeed ? defaultSpeed : points[index].speedToNextPoint;
                currentTime = points[index].useDefaultStayTime ? defaultStayTime : points[index].stayTime;
                if (!invert)
                {
                    if (index + 1 < points.Count) index++;
                    else invert = true;
                }
                else
                {
                    if (index - 1 >= 0) index--;
                    else invert = false;
                }
                dist = Vector3.Distance(targetTransform.position, points[index].transform.position);
                targetTransform = points[index].transform;
                oldEuler = transform.eulerAngles;
            }
        }

        void OnTriggerStay(Collider other)
        {
            if (other.transform.parent != transform && other.transform.tag == "Player" && other.GetComponent<Invector.vCharacterController.vCharacter>() != null)
            {
                other.transform.parent = transform;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.transform.parent == transform && other.transform.tag == "Player")
            {
                other.transform.parent = null;
                other.transform.eulerAngles = new Vector3(0, other.transform.eulerAngles.y, 0);
            }
        }

        // Методы для управления точками
        public void AddPoint(vPlatformPoint point)
        {
            points.Add(point);
        }
        public void InsertPoint(int index, vPlatformPoint point)
        {
            if (index < 0 || index > points.Count) return;
            points.Insert(index, point);
        }
        public void RemovePointAt(int index)
        {
            if (index < 0 || index >= points.Count) return;
            points.RemoveAt(index);
        }

        [System.Serializable]
        public class vPlatformPoint
        {
            public Transform transform;
            public bool useDefaultStayTime = true;
            [vHideInInspector("useDefaultstayTime", true)]
            public float stayTime;
            public bool useDefaultSpeed = true;
            [vHideInInspector("useDefaultSpeed", true)]
            public float speedToNextPoint = 1f;
        }
    }
} 