# Префаб OnlinePlayer_A — структура и скрипты

Краткий разбор префаба **OnlinePlayer_A** для Fusion и GKC. Используй при доработке онлайн-игрока.

---

## Корневой объект (GameObject "OnlinePlayer_A")

Один корневой GameObject с **множеством компонентов** (десятки MonoBehaviours). Основные для сети и GKC:

### Fusion (сеть)
- **Fusion.NetworkObject** (Fusion.Runtime) — сетевая сущность, обязателен для спавна через Fusion.
- **Fusion.NetworkTransform** (Fusion.Runtime) — синхронизация позиции/вращения; висит на **дочернем** объекте (не на корне), чтобы синхронизировать модель/контроллер.

### GKC (Game Kit Controller)
- **playerComponentsManager** (`Assets/SORT/Game Kit Controller/Scripts/Player/playerComponentsManager.cs`)  
  Центральный компонент игрока: ссылки на playerController, playerCamera, inputManager, health, inventory, weapons, abilities, HUD и т.д. По нему FusionOnlinePlayerSpawner вызывает `addSpawnedPlayerOnSceneForMultiplayerWithCustomPositionRotation` и передаёт управление GKC.

- **GKC_PlayerPrefabSpawner** (`Assets/SORT/Game Kit Controller/Scripts/Online System/GKC_PlayerPrefabSpawner.cs`)  
  Опция спавна игрока при старте сцены. В префабе для Fusion важно:
  - **checkPlayerPrefabSpawnOnStartEnabled** = false (спавн делаем через Fusion, не при Start).
  - Может вызывать `checkPlayerPrefabSpawnOnAwake` / `checkPlayerPrefabSpawn` в Awake/Start, если флаг включён — для онлайн оставляем выключенным.

- **playerCharactersManager** (или аналог на корне)  
  В префабе есть компонент с полями:
  - `spawnOnlinePlayersOnStart: 0`, `numberOfOnlinePlayersToSpawnOnStart: 0`
  - `mainDynamicSplitScreenSystem`, `cameraStatesListString`, `currentPlayeraInfoActive`, `auxPlayerList`
  - Управляет списком персонажей, камерами, split screen. Для онлайн спавна через Fusion не должен сам спавнить игроков при старте.

### Прочее на корне
- Настройки тегов/слоёв (tagList, layerList, player и т.д.).
- Огромный **inputManager** (оси, кнопки, привязки клавиш/джойстика).
- Менеджеры камеры, split screen, смена персонажа, сохранения и т.д.
- FPS counter (часто отключён).

---

## Иерархия и дочерние объекты

- Префаб **очень большой**: тысячи строк YAML, сотни объектов (модель, скелет, оружие, UI, камера, партиклы, MagicaCloth, URP и т.д.).
- **NetworkTransform** висит на дочернем GameObject (не на корне), чтобы синхронизировать нужный Transform (например, модель/контроллер).
- Есть ветки: Camera And Canvas, оружие, инвентарь, устройства, транспорт, анимации, VFX.

---

## Важные моменты для Fusion

1. **Спавн только через Fusion**  
   В префабе: `spawnOnlinePlayersOnStart: 0`, `checkPlayerPrefabSpawnOnStartEnabled` отключён. Игрок создаётся только через `FusionOnlinePlayerSpawner` и `Runner.Spawn(OnlinePlayer_A)`.

2. **Регистрация в GKC после спавна**  
   После `Runner.Spawn` и `Runner.SetPlayerObject` спавнер вызывает  
   `playerCharactersManager.addSpawnedPlayerOnSceneForMultiplayerWithCustomPositionRotation(spawnedPlayerRoot, position, rotation)`.  
   Нужен **playerComponentsManager** на заспавненном объекте (на корне OnlinePlayer_A он есть).

3. **Камера и ввод**  
   Управление и камера привязываются через GKC после `addSpawnedPlayerOnSceneForMultiplayerWithCustomPositionRotation`. На сцене должен быть **playerCharactersManager** (обычно на отдельном объекте менеджера, не в префабе игрока).

4. **Метка FusionPrefab**  
   В `OnlinePlayer_A.prefab.meta` должна быть метка **FusionPrefab**, после изменений — **Tools → Fusion → Rebuild Prefab Table**.

---

## Типичные скрипты в префабе (по GUID из префаба)

Используются в том числе:
- Unity UI (Image, кнопки, изменение цвета при нажатии).
- URP (UniversalAdditionalCameraData, UniversalAdditionalLightData).
- MagicaCloth2 (MagicaCloth, MagicaSphereCollider).
- Post Processing (PostProcessVolume).
- Множество скриптов GKC: оружие, инвентарь, транспорт, камера, ввод, способности, здоровье, сохранения, карта, пауза и т.д.

Корневой объект — единый «хаб» GKC-игрока с ссылками на все подсистемы; для Fusion важны только NetworkObject, NetworkTransform и интеграция с playerCharactersManager/playerComponentsManager.
