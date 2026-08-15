# Defibrillator models (MiniVan)

Две модели, каждая — **один меш** (все детали склеены):

| Файл | Что |
|------|-----|
| `MiniVan_Defib_Suitcase.obj` (+ `.mtl`) | Открытый чемодан: корпус, бамперы, ручка, консоль, экран, кнопка, синяя/жёлтая лопатки в крышке |
| `MiniVan_Defib_Tube.obj` (+ `.mtl`) | Ручная трубка: голова с электродом, рукоять, гофра, разъём |

## Как пересобрать

**Без Blender (OBJ):**
```bat
python generate_defib_obj.py
```

**Через Blender (bevel + FBX + .blend):**
```bat
blender --background --python build_defibrillator_blender.py
```
Получишь `MiniVan_Defibrillator.blend`, `MiniVan_Defib_Suitcase.fbx`, `MiniVan_Defib_Tube.fbx`.

Импорт в Unity: перетащи OBJ/FBX в `Assets/MiniVan Game/Art/Defibrillator/` (или Prefabs/Defibrillator). Материалы по слотам MTL / FBX.
