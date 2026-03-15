# Онлайн (Fusion) — сцена OnlineTest

Используются **только стандартные средства Fusion** (меню Tools → Fusion, компоненты в сцене). Дополнительных своих меню или авто-скриптов нет.

## Что нужно в сцене

- **Network Starts** — объект с компонентами **Network Runner**, **Fusion Bootstrap**, **Fusion Bootstrap Debug GUI**. В Bootstrap в поле **Runner Prefab** — этот же объект (с Network Runner).
- **Fusion Player Spawner** — объект с **Network Object** и скриптом **Fusion Online Player Spawner**. В скрипте: **Player Prefab** = префаб **OnlinePlayer_A**, **Spawn Point** = дочерний Transform (точка спавна).
- Сцена **OnlineTest** добавлена в **File → Build Settings → Scenes In Build**.
- У **Player And Game Management** (GKC) отключено **Spawn Online Players On Start** — спавн только через Fusion.

## Регистрация префаба в Fusion

Fusion спавнит только префабы из своей таблицы. Чтобы в неё попал **OnlinePlayer_A**:

1. Выдели префаб **OnlinePlayer_A** в Project.
2. В Inspector внизу во вкладке **Labels** должна быть метка **FusionPrefab**. Если нет — **Add Label** → **FusionPrefab**.
3. В меню Unity: **Tools → Fusion → Rebuild Prefab Table** (стандартный пункт Fusion).
4. В консоли появится «Rebuild Prefab Table done.»
5. После этого запускай Play и **Start Host** / **Start Shared Client**.

Если ошибка **«failed to be translated into a prefab id»** — префаб не в таблице: проверь метку FusionPrefab и снова выполни **Tools → Fusion → Rebuild Prefab Table**, затем перезапусти игру (выйди из Play и зайди снова).

## Как запустить

- **Первый игрок:** Play → **Start Host**.
- **Второй игрок:** тот же экземпляр с **Client Count = 1** (режим Multiple Peers) или отдельный билд → **Start Shared Client** с тем же именем комнаты (Room).

**Host** = ты и сервер, и игрок. **Start Server** = только сервер без своего персонажа (для дедикейтед). Для «два игрока онлайн» первому — Host, второму — Start Shared Client.

---

**В проекте:**  
- **FusionOnlinePlayerSpawner** (скрипт) — спавнит префаб через `Runner.Spawn()` и регистрирует его в GKC через `addSpawnedPlayerOnSceneForMultiplayerWithCustomPositionRotation`.  
- **OnlinePlayer_A** — префаб с Network Object, Network Transform и GKC (playerComponentsManager, GKC_PlayerPrefabSpawner).
