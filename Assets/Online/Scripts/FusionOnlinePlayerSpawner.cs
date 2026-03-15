using Fusion;
using UnityEngine;

/// <summary>
/// Спавнит сетевого игрока для каждого подключившегося клиента (Shared mode).
/// Повесьте на объект с NetworkObject в сцене. Укажите Player Prefab и Spawn Point.
/// </summary>
public class FusionOnlinePlayerSpawner : NetworkBehaviour
{
    [Header("Префаб игрока (должен содержать NetworkObject и NetworkTransform)")]
    [SerializeField] private NetworkPrefabRef _playerPrefab;

    [Header("Точка спавна (опционально)")]
    [SerializeField] private Transform _spawnPoint;

    public override void Spawned()
    {
        if (_playerPrefab.IsValid == false)
        {
            Debug.LogWarning("[FusionOnlinePlayerSpawner] Player Prefab не назначен. Назначьте префаб в инспекторе.");
            return;
        }

        Vector3 pos = _spawnPoint != null ? _spawnPoint.position : Vector3.zero;
        Quaternion rot = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.identity;

        var playerObj = Runner.Spawn(_playerPrefab, pos, rot);
        Runner.SetPlayerObject(Runner.LocalPlayer, playerObj);

        RegisterPlayerWithGKC(playerObj.gameObject, pos, rot);

        Debug.Log($"[FusionOnlinePlayerSpawner] Спавн игрока для " + Runner.LocalPlayer + ", зарегистрирован в GKC.");
    }

    /// <summary>
    /// Регистрирует заспавненного игрока в playerCharactersManager (GKC).
    /// </summary>
    private static void RegisterPlayerWithGKC(GameObject spawnedPlayerRoot, Vector3 position, Quaternion rotation)
    {
        var gkcManager = playerCharactersManager.Instance;
        if (gkcManager == null)
            gkcManager = UnityEngine.Object.FindObjectOfType<playerCharactersManager>();
        if (gkcManager == null)
        {
            Debug.LogWarning("[FusionOnlinePlayerSpawner] playerCharactersManager не найден. Игрок заспавнен, но GKC его не видит.");
            return;
        }

        if (spawnedPlayerRoot.GetComponentInChildren<playerComponentsManager>() == null)
            Debug.LogWarning("[FusionOnlinePlayerSpawner] У префаба нет playerComponentsManager. Используйте префаб GKC-игрока (OnlinePlayer_A).");

        gkcManager.addSpawnedPlayerOnSceneForMultiplayerWithCustomPositionRotation(spawnedPlayerRoot, position, rotation);
    }
}
