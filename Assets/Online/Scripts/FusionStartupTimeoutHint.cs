using UnityEngine;

/// <summary>
/// Если Fusion Bootstrap долго висит на "Starting Up", выводит подсказку в консоль (App Id, сеть, Multi-Peer).
/// Повесь на любой объект в сцене с Fusion Bootstrap (например, на тот же объект, что и Bootstrap).
/// </summary>
public class FusionStartupTimeoutHint : MonoBehaviour
{
    [Tooltip("Через сколько секунд показать подсказку")]
    public float hintAfterSeconds = 20f;

    float _startingUpTime = -1f;
    bool _hintShown;

    void Update()
    {
        var bootstrap = FindObjectOfType<Fusion.FusionBootstrap>();
        if (bootstrap == null) return;

        if (bootstrap.CurrentStage == Fusion.FusionBootstrap.Stage.StartingUp)
        {
            if (_startingUpTime < 0f)
                _startingUpTime = Time.realtimeSinceStartup;
            else if (!_hintShown && Time.realtimeSinceStartup - _startingUpTime >= hintAfterSeconds)
            {
                _hintShown = true;
                Debug.LogWarning(
                    "[Fusion] Подключение к Photon не завершается.\n" +
                    "Проверь: 1) Tools → Fusion → Hub или Photon Dashboard — указан ли Fusion App Id в PhotonAppSettings. " +
                    "2) Файрвол/антивирус не блокирует UDP. " +
                    "3) Для теста в одном редакторе: включи в Fusion Project Config режим Multiple Peers, задай Client Count: 1 и нажми Start Host (запустятся хост и клиент в одном процессе).");
            }
        }
        else
        {
            _startingUpTime = -1f;
            _hintShown = false;
        }
    }
}
