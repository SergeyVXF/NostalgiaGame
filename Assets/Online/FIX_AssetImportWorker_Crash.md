# Падение AssetImportWorker (2D Sprite AIIntegration)

Если при запуске игры или при импорте вылетает **AssetImportWorker** с ошибкой в  
`UnityEditor.U2D.Sprites.AIIntegration.AIIntegration:Install2DEnhancerPackage` —  
это баг пакета **2D Sprite** (интеграция с AI/2D Enhancer). Из-за краша редактор может выкидывать обратно на экран выбора режима Fusion.

## Решение 1: Отключить пакет 2D Sprite (рекомендуется для теста)

1. В Unity открой **Window → Package Manager**.
2. Слева в выпадающем списке выбери **Built In** (встроенные пакеты).
3. В списке найди **2D Sprite**.
4. Нажми **Disable** в правой панели.
5. Дождись окончания процесса (галочка с пакета исчезнет).
6. Перезапусти Unity и снова **Play → Start Host**.

**Важно:** после отключения компоненты, зависящие от 2D Sprite (например, Sprite Renderer в 2D режиме), могут стать недоступны или подсвечены серым. Для сцены OnlineTest и 3D-игры это обычно не мешает. Если нужны спрайты — используй решение 2 или 3.

## Решение 2: Кастомизировать пакет и убрать вызов, который крашит

1. **Window → Package Manager**.
2. Найди пакет **2D Sprite** (список **In Project** или **Built In**).
3. В правой панели нажми **⋮** (три точки) → **Customize** (или **Manage → Customize**).  
   Unity скопирует пакет в `Packages/com.unity.2d.sprite/`.
4. В Project открой  
   `Packages/com.unity.2d.sprite/Editor/AIIntegration/AIIntegration.cs`.
5. В методе `Install2DEnhancerPackage()` оберни тело в `try/catch` или закомментируй вызов `Debug.LogError(...)`, чтобы воркер не падал при логировании.
6. Сохрани файл и перезапусти Unity.

После этого можно снова включить пакет 2D Sprite, если он был отключён.

## Решение 3: Обновить Unity

Часть падений AssetImportWorker исправлена в **Unity 6000.2.8f1** и новее.  
Обнови редактор до 6000.2.8+ через **Unity Hub → Installs → Add** или **Check for updates**.

---

После любого из решений проверь запуск: **Play → Start Host**. Экран выбора режима не должен возвращаться из-за краша воркера.
