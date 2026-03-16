# Онлайн (Fusion) — сцена OnlineTest

Используются **только стандартные средства Fusion** (меню Tools → Fusion, компоненты в сцене). Дополнительных своих меню или авто-скриптов нет.

## Что нужно в сцене

- **Network Starts** — объект с компонентами **Network Runner**, **Fusion Bootstrap**, **Fusion Bootstrap Debug GUI**. В Bootstrap в поле **Runner Prefab** — этот же объект (с Network Runner).
- **Fusion Player Spawner** — объект с **Network Object** и скриптом **Fusion Online Player Spawner**. В скрипте: **Player Prefab** = префаб **OnlinePlayer_A**, **Spawn Point** = дочерний Transform (точка спавна).
- Сцена **OnlineTest** добавлена в **File → Build Settings → Scenes In Build**.
- У **Player And Game Management** (GKC) отключено **Spawn Online Players On Start** — спавн только через Fusion.

## Регистрация префаба в Fusion

Fusion спавнит только префабы из своей таблицы. В проекте поправлен **Rebuild Prefab Table**: он теперь подхватывает префабы с **NetworkObject** на дочернем объекте (не только на корне), так что **OnlinePlayer_A** попадает в таблицу.

**Что сделать один раз:**

1. В Unity: **Tools → Fusion → Rebuild Prefab Table**.
2. В консоли: «Rebuild Prefab Table done.»
3. Останови Play (если был запущен), затем снова **Play** → **Start Host** (или **Start Shared Client**).

**Обходной путь — проверить, что сеть вообще работает:**

1. В сцене выбери объект **Fusion Player Spawner**.
2. В Inspector в поле **Player Prefab** вместо **OnlinePlayer_A** перетащи префаб **OnlinePlayerAvatar** (он в той же папке Prefabs, маленький префаб с капсулой; у него уже есть метка FusionPrefab).
3. В меню: **Tools → Fusion → Rebuild Prefab Table**.
4. Выйди из Play (если был запущен), затем **Play** → **Start Host** (при необходимости **Client Count = 1**).

Должны появиться два игрока-капсулы. Значит, Fusion и спавн работают, проблема именно в регистрации **OnlinePlayer_A**. Чтобы снова использовать GKC-игрока, потом верни в **Player Prefab** префаб **OnlinePlayer_A** и разберись с ним через **Tools → Fusion → Network Prefabs Inspector** (посмотреть, есть ли префаб в списке) или упрости префаб (например, дубликат с минимумом компонентов).

## Как запустить

1. Запусти игру (**Play**). Должно появиться окно с кнопками и статусом **«Fusion Status: Disconnected»**.
2. Если видишь только **«Fusion Status: Starting Up»** и кнопок нет — останови Play. Проверь: сцена **OnlineTest** (или та, где висит Bootstrap) добавлена в **File → Build Settings → Scenes In Build**; у **Fusion Bootstrap** в Inspector: **Start Mode** = **User Interface**, **Runner Prefab** назначен. Запусти Play снова.
3. Нажми **«Start Host»**. Подожди 5–15 секунд (первое подключение к Photon может быть долгим). Статус сменится, GUI скроется, загрузится игра.
4. **Если висит «Starting Up» больше 1–2 минут** — подключение к Photon не проходит. Проверь: **Tools → Fusion → Hub** (или Photon Dashboard): в проекте должен быть указан **Fusion App Id** (PhotonAppSettings). Файрвол/антивирус не должен блокировать UDP. Для теста без облака: в **Fusion Project Config** включи режим **Multiple Peers**, в GUI укажи **Client Count: 1** и нажми **Start Host** (хост и клиент запустятся в одном процессе).
5. Второй игрок: в том же редакторе — **Client Count: 1** и снова **Start Host**. Либо отдельный билд — введи то же имя комнаты (Room) и нажми **«Start Shared Client»**.

**Host** = ты и сервер, и игрок. **Start Server** = только сервер без своего персонажа. Для «два игрока онлайн» первому — Host, второму — Start Shared Client.

---

**В проекте:**  
- **FusionOnlinePlayerSpawner** (скрипт) — спавнит префаб через `Runner.Spawn()` и регистрирует его в GKC через `addSpawnedPlayerOnSceneForMultiplayerWithCustomPositionRotation`.  
- **OnlinePlayer_A** — префаб с Network Object, Network Transform и GKC (playerComponentsManager, GKC_PlayerPrefabSpawner).
