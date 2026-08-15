# MiniVan Game Asset Structure

- `Prefabs`: gameplay prefabs grouped by Vehicles, Characters, Weapons, Items,
  Panelka, World, Network, and runtime Resources.
- `Materials`: materials grouped by the same gameplay domains. Runtime-loaded
  materials remain inside nested `Resources` folders.
- `Textures`: source and generated textures grouped by gameplay domain.
- `Models`: generated mesh assets grouped by gameplay domain.
- `PhysicsMaterials`: vehicle and movement physics materials.
- `Shaders`: custom shaders.
- `Settings`: terrain data, terrain layers, and other configuration assets.
- `Scenes`: Unity scenes and scene-owned navigation data.
- `Scripts`: runtime code plus editor builders under `Scripts/Editor`.
- `Audio`: music, voices, and sound effects.
- `Documentation`: asset notes and authoring instructions.

All reorganizations are performed through Unity `AssetDatabase.MoveAsset`, so
asset GUIDs and serialized prefab/scene references are preserved.
