using Fusion;
using UnityEngine;

/// <summary>
/// Камера следует за локальным игроком Fusion (за кого играет этот экземпляр).
/// Если в сцене нет Main Camera — создаёт её при старте.
/// Повесь на любой объект в сцене (например, пустой GameObject "Gameplay Camera").
/// </summary>
public class FusionLocalPlayerCamera : MonoBehaviour
{
    [Header("Смещение камеры за игроком (мир)")]
    public Vector3 offset = new Vector3(0f, 5f, -8f);

    [Header("Плавность следования (0 = мгновенно)")]
    [Range(0f, 1f)]
    public float smoothTime = 0.15f;

    Camera _cam;
    Transform _target;
    Vector3 _velocity;

    void Start()
    {
        if (Camera.main != null)
        {
            _cam = Camera.main;
        }
        else
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            _cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 1000f;
            _cam.fieldOfView = 60f;
        }
    }

    void LateUpdate()
    {
        if (_cam == null) return;

        if (_target == null)
        {
            TryFindLocalPlayer();
            return;
        }

        var desired = _target.position + offset;
        _cam.transform.position = smoothTime > 0.001f
            ? Vector3.SmoothDamp(_cam.transform.position, desired, ref _velocity, smoothTime)
            : desired;
        _cam.transform.LookAt(_target.position + Vector3.up * 1.5f);
    }

    void TryFindLocalPlayer()
    {
        foreach (var runner in NetworkRunner.Instances)
        {
            if (runner == null || !runner.IsRunning) continue;
            var playerObj = runner.GetPlayerObject(runner.LocalPlayer);
            if (playerObj != null)
            {
                _target = playerObj.transform;
                return;
            }
        }
    }
}
