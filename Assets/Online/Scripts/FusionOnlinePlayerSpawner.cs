using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Спавнит сетевого игрока для каждого подключившегося клиента (Shared mode).
/// Спавн делает только хост; клиент только получает объекты от хоста.
/// Повесьте на объект с NetworkObject в сцене. Укажите Player Prefab и Spawn Point.
/// </summary>
public class FusionOnlinePlayerSpawner : NetworkBehaviour
{
    [Header("Префаб игрока (должен содержать NetworkObject и NetworkTransform)")]
    [SerializeField] private NetworkPrefabRef _playerPrefab;

    [Header("Точки спавна (разные для каждого игрока)")]
    [Tooltip("Если задано несколько — игрок 1 на 1-й точке, игрок 2 на 2-й и т.д. Если одна или пусто — используется одна точка для всех.")]
    [SerializeField] private Transform[] _spawnPoints;

    [Header("Одна точка (если не задан массив выше)")]
    [SerializeField] private Transform _spawnPoint;

    static float _hideMenuUntil = -1f;

    public override void Spawned()
    {
        _hideMenuUntil = Time.realtimeSinceStartup + 10f;
        HideBootstrapMenu();
        if (!Runner.IsServer)
            return;
        SpawnPlayerFor(Runner.LocalPlayer);
    }

    /// <summary>
    /// Скрывает все экраны выбора режима Fusion (Bootstrap GUI), чтобы была видна игровая сцена.
    /// </summary>
    private static void HideBootstrapMenu()
    {
        var guis = UnityEngine.Object.FindObjectsOfType<Fusion.FusionBootstrapDebugGUI>(true);
        foreach (var gui in guis)
        {
            if (gui != null && gui.enabled)
                gui.enabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Time.realtimeSinceStartup < _hideMenuUntil)
            HideBootstrapMenu();
        if (!Runner.IsServer || _playerPrefab.IsValid == false)
            return;
        foreach (var player in Runner.ActivePlayers)
        {
            if (Runner.GetPlayerObject(player) != null)
                continue;
            SpawnPlayerFor(player);
        }
    }

    private void SpawnPlayerFor(PlayerRef player)
    {
        if (_playerPrefab.IsValid == false)
        {
            Debug.LogWarning("[FusionOnlinePlayerSpawner] Player Prefab не назначен.");
            return;
        }

        int playerIndex = 0;
        foreach (var p in Runner.ActivePlayers)
        {
            if (p == player) break;
            playerIndex++;
        }

        Vector3 pos;
        Quaternion rot;
        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            var point = _spawnPoints[playerIndex % _spawnPoints.Length];
            pos = point != null ? point.position : Vector3.zero;
            rot = point != null ? point.rotation : Quaternion.identity;
        }
        else
        {
            pos = _spawnPoint != null ? _spawnPoint.position : Vector3.zero;
            rot = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.identity;
        }

        NetworkObject playerObj;
        try
        {
            playerObj = Runner.Spawn(_playerPrefab, pos, rot, inputAuthority: player);
        }
        catch (InvalidOperationException ex) when (ex.Message != null && ex.Message.Contains("failed to be translated into a prefab id"))
        {
            Debug.LogError(
                "[FusionOnlinePlayerSpawner] Префаб игрока не найден в таблице Fusion. " +
                "Tools → Fusion → Rebuild Prefab Table, затем перезапусти игру.");
            return;
        }

        Runner.SetPlayerObject(player, playerObj);

        if (player == Runner.LocalPlayer)
            RegisterPlayerWithGKC(playerObj.gameObject, pos, rot);

        Debug.Log($"[FusionOnlinePlayerSpawner] Спавн игрока для " + player);
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
