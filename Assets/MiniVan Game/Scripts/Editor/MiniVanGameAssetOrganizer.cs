using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.Editor
{
    public static class MiniVanGameAssetOrganizer
    {
        private const string Root = "Assets/MiniVan Game";

        [MenuItem("MiniVan Game/Assets/Organize All Resources")]
        public static void OrganizeAllResources()
        {
            MoveBulkFolders();
            MoveRootPrefabs();
            MoveRootMaterials();
            MoveMixedGeneratedAssets();
            MoveResourceAssets();
            MoveDocumentationAndSettings();
            OrganizeByTypeCategories();

            DeleteAssetIfPresent(Root + "/Materials/Resources/FuelSystem/Materials/.keep");
            DeleteAssetIfPresent(Root + "/Textures/Vehicles/FuelSystem/.keep");
            RemoveEmptyFolderTree(Root + "/Generated");
            RemoveEmptyFolderTree(Root + "/Art/Cosmetics");
            RemoveEmptyFolderTree(Root + "/Art/Equipment");
            RemoveEmptyFolderTree(Root + "/Art/EquipmentIcons");
            RemoveEmptyFolderTree(Root + "/Prefabs/DamObstacle/Materials");
            RemoveEmptyFolderTree(Root + "/Prefabs/Vehicles/MiniVan/Materials");
            RemoveEmptyFolderTree(Root + "/Prefabs/AntonRadar/Concepts");
            RemoveEmptyFolderTree(Root + "/Prefabs/Cosmetics/Concepts");
            RemoveEmptyFolderTree(Root + "/Prefabs/DamObstacle/Concepts");
            RemoveEmptyFolderTree(Root + "/Prefabs/FacePainting/Concepts");
            RemoveEmptyFolderTree(Root + "/Audio/Engine");
            RemoveEmptyFolderTree(Root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[MiniVan Asset Organizer] Resource organization completed.");
        }

        /// <summary>
        /// Puts same-type assets into typed folders, with categories when a type is large.
        /// Does not relocate Resources.* load paths (keep Resources/ tree intact).
        /// </summary>
        private static void OrganizeByTypeCategories()
        {
            // Cosmetics: materials + meshes out of Art
            MoveAssetsByExtension(Root + "/Art/Cosmetics", ".mat", Root + "/Materials/Cosmetics", false);
            MoveAssetsByExtension(Root + "/Art/Cosmetics", ".asset", Root + "/Models/Cosmetics", false);

            // Equipment materials
            MoveAssetsByExtension(
                Root + "/Art/Equipment",
                ".mat",
                Root + "/Materials/Equipment/FireExtinguisher",
                false);

            // UI source icons / concept art
            MoveFolderContents(Root + "/Art/EquipmentIcons", Root + "/Art/UI/EquipmentIcons");
            MoveAsset(
                Root + "/Art/UI/rpm-gauge-dashboard-concept.png",
                Root + "/Art/Concepts/UI/rpm-gauge-dashboard-concept.png");
            MoveAsset(
                Root + "/Art/UI/rpm-sweetspot-dashboard-concept.png",
                Root + "/Art/Concepts/UI/rpm-sweetspot-dashboard-concept.png");
            MoveAsset(
                Root + "/Art/UI/rpm-sweetspot-vertical-concept.png",
                Root + "/Art/Concepts/UI/rpm-sweetspot-vertical-concept.png");

            // Concept art that lived next to prefabs
            MoveFolderContents(Root + "/Prefabs/AntonRadar/Concepts", Root + "/Art/Concepts/AntonRadar");
            MoveFolderContents(Root + "/Prefabs/Cosmetics/Concepts", Root + "/Art/Concepts/Cosmetics");
            MoveFolderContents(Root + "/Prefabs/DamObstacle/Concepts", Root + "/Art/Concepts/DamObstacle");
            MoveFolderContents(Root + "/Prefabs/FacePainting/Concepts", Root + "/Art/Concepts/FacePainting");

            // DamObstacle: materials / terrain settings out of Prefabs
            MoveAssetsByExtension(
                Root + "/Prefabs/DamObstacle",
                ".mat",
                Root + "/Materials/World/DamObstacle",
                true);
            MoveAssetsByExtension(
                Root + "/Prefabs/DamObstacle/Materials",
                ".mat",
                Root + "/Materials/World/DamObstacle",
                false);
            MoveAsset(
                Root + "/Prefabs/DamObstacle/DamObstacle_LocalTerrainData.asset",
                Root + "/Settings/World/DamObstacle/DamObstacle_LocalTerrainData.asset");
            MoveAsset(
                Root + "/Prefabs/DamObstacle/Dam_Channel_WetGrassTerrainLayer.terrainlayer",
                Root + "/Settings/World/DamObstacle/Dam_Channel_WetGrassTerrainLayer.terrainlayer");
            MoveAsset(
                Root + "/Prefabs/DamObstacle/Dam_Channel_WetGrassTerrainTexture.asset",
                Root + "/Textures/World/DamObstacle/Dam_Channel_WetGrassTerrainTexture.asset");

            // MiniVan screenshot assets out of Prefabs
            MoveAsset(
                Root + "/Prefabs/Vehicles/MiniVan/Screenshot_1.png",
                Root + "/Screenshots/Vehicles/MiniVan/Screenshot_1.png");
            MoveAsset(
                Root + "/Prefabs/Vehicles/MiniVan/Materials/Screenshot_1.mat",
                Root + "/Materials/Vehicles/MiniVan/Screenshot_1.mat");

            // FX materials / textures that lived under Shaders
            MoveAsset(Root + "/Shaders/CarFire.mat", Root + "/Materials/FX/CarFire.mat");
            MoveAsset(Root + "/Shaders/EngineSmoke.mat", Root + "/Materials/FX/EngineSmoke.mat");
            MoveAsset(Root + "/Shaders/Fire.png", Root + "/Textures/FX/Fire.png");
            MoveAsset(Root + "/Shaders/flowmap.psd", Root + "/Textures/FX/flowmap.psd");
            MoveAsset(Root + "/Shaders/fx_a_noise_003 1.png", Root + "/Textures/FX/fx_a_noise_003 1.png");
            MoveAsset(Root + "/Shaders/smoke 1.png", Root + "/Textures/FX/smoke 1.png");
            MoveAsset(Root + "/Shaders/smoke.png", Root + "/Textures/FX/smoke.png");
            MoveAsset(Root + "/Shaders/SMOKE_flowmap.psd", Root + "/Textures/FX/SMOKE_flowmap.psd");
            MoveAsset(Root + "/Shaders/smoke_LIGHT.png", Root + "/Textures/FX/smoke_LIGHT.png");

            // ScriptableObject / trigger settings out of Prefabs
            MoveAsset(
                Root + "/Prefabs/World/Hazards/EH_CircularWaterDamageTrigger.asset",
                Root + "/Settings/World/Hazards/EH_CircularWaterDamageTrigger.asset");

            // Audio categories
            MoveFolderContents(Root + "/Audio/Engine", Root + "/Audio/Vehicles/MiniVan");
            MoveAsset(
                Root + "/Audio/Voice/Pizza_Radio_Request_RU.wav",
                Root + "/Audio/Voice/Pizza/Pizza_Radio_Request_RU.wav");
        }

        private static void MoveBulkFolders()
        {
            MoveFolder(
                Root + "/Editor",
                Root + "/Scripts/Editor/Builders");

            MoveFolder(
                Root + "/Generated/Panelka/Prefabs/Furniture",
                Root + "/Prefabs/Panelka/Interiors/Furniture");
            MoveFolder(
                Root + "/Generated/Panelka/Prefabs/HouseProps",
                Root + "/Prefabs/Panelka/Building/HouseProps");
            MoveFolder(
                Root + "/Prefabs/GameMode",
                Root + "/Prefabs/World/GameMode");

            MoveFolder(
                Root + "/Materials/PanelkaPB",
                Root + "/Materials/Panelka/LegacyPB");
            MoveFolder(
                Root + "/Materials/PanelkaCell",
                Root + "/Materials/Panelka/Cells");
            MoveFolder(
                Root + "/Materials/PizzaLoop",
                Root + "/Materials/Items/Pizza/Legacy");
            MoveFolder(
                Root + "/Generated/Panelka/Materials",
                Root + "/Materials/Panelka/Generated");
            MoveFolder(
                Root + "/Generated/PanelkaStage1/Materials",
                Root + "/Materials/Panelka/Stage1");
            MoveFolder(
                Root + "/Generated/GameMode/Materials",
                Root + "/Materials/World/GameMode");
            MoveFolder(
                Root + "/Generated/TexturePack_LowPoly/Materials",
                Root + "/Materials/Panelka/Interior/LowPolyPack");
            MoveFolder(
                Root + "/Generated/Materials",
                Root + "/Materials/Shared/Generated");

            MoveFolder(
                Root + "/Generated/Panelka/Textures",
                Root + "/Textures/Panelka/Procedural");
            MoveFolder(
                Root + "/Generated/TexturePack_LowPoly/Doors",
                Root + "/Textures/Panelka/Interior/LowPolyPack/Doors");
            MoveFolder(
                Root + "/Generated/TexturePack_LowPoly/Floor_Apartment",
                Root + "/Textures/Panelka/Interior/LowPolyPack/Floors");
            MoveFolder(
                Root + "/Generated/TexturePack_LowPoly/Stairwell",
                Root + "/Textures/Panelka/Interior/LowPolyPack/Stairwell");
            MoveFolder(
                Root + "/Generated/TexturePack_LowPoly/Tile_Bathroom",
                Root + "/Textures/Panelka/Interior/LowPolyPack/Bathroom");
            MoveFolder(
                Root + "/Generated/TexturePack_LowPoly/Wallpaper_Kitchen",
                Root + "/Textures/Panelka/Interior/LowPolyPack/Kitchen");
            MoveFolder(
                Root + "/Generated/TexturePack_LowPoly/Wallpaper_Room",
                Root + "/Textures/Panelka/Interior/LowPolyPack/Rooms");
        }

        private static void MoveRootPrefabs()
        {
            MovePrefab("MiniVan", "Vehicles/MiniVan");
            MovePrefab("Skateboard", "Vehicles/Rideables");
            MovePrefab("hoverboardM", "Vehicles/Rideables");
            MovePrefab("MiniVanPlayer", "Characters/Players");
            MovePrefab("Zombie", "Characters/Zombies");
            MovePrefab("Zombie_SpawnerAny", "Characters/Zombies");
            MovePrefab("HotPotatoDummy", "Characters/Test");
            MovePrefab("BatPickup", "Weapons/Melee");
            MovePrefab("HotPotatoBomb", "Weapons/Explosives");
            MovePrefab("HotPotatoPoop", "Items/HotPotato");
            MovePrefab("HotPotatoPoopLowPoly", "Items/HotPotato");
            MovePrefab("CoffeeMugVisual", "Items/Props");
            MovePrefab("Ramp_01", "World/Props");
            MovePrefab("Bunker", "World/Rescue");
            MovePrefab("SavePlace", "World/Rescue");
            MovePrefab("Rescue_StopZone_Bunker", "World/Rescue");
            MovePrefab("Rescue_StopZone_SavePlace", "World/Rescue");

            MoveAsset(
                Root + "/Generated/GameMode/Game_v01_Network.prefab",
                Root + "/Prefabs/Network/GameMode/Game_v01_Network.prefab");
        }

        private static void MoveRootMaterials()
        {
            MoveMaterial("Bat_Grip", "Weapons/Bat");
            MoveMaterial("Bat_Wood", "Weapons/Bat");
            MoveMaterial("Zombie_DarkSide", "Characters/Zombie");
            MoveMaterial("Zombie_Green", "Characters/Zombie");
            MoveMaterial("Zombie_Lime", "Characters/Zombie");
            MoveMaterial("Zombie_Red", "Characters/Zombie");
            MoveMaterial("MVG_Player", "Characters/Player");
            MoveMaterial("MVG_Bomb", "Weapons/Explosives");
            MoveMaterial("MVG_Poop", "Items/HotPotato");
            MoveMaterial("MVG_Asphalt", "World/Shared");
            MoveMaterial("MVG_Grass", "World/Shared");
            MoveMaterial("MVG_MenuGray", "UI");
            MoveMaterial("KnockableCube_Mat", "World/Props");

            string[] vehicleMaterials =
            {
                "MiniVan Steering Black", "MiniVan Steering Dark Grey",
                "MVG_CarLights_01", "MVG_Dark", "MVG_Door", "MVG_Frame",
                "MVG_Glass", "MVG_Ladder_DarkMetal", "MVG_Ladder_Rungs",
                "MVG_Seat", "MVG_SeatTrigger", "MVG_VanBody"
            };
            for (int i = 0; i < vehicleMaterials.Length; i++)
                MoveMaterial(vehicleMaterials[i], "Vehicles/MiniVan");

            string[] mugMaterials =
            {
                "CoffeeMug_Coffee", "CoffeeMug_Cream", "CoffeeMug_DarkTop",
                "CoffeeMug_Grey"
            };
            for (int i = 0; i < mugMaterials.Length; i++)
                MoveMaterial(mugMaterials[i], "Items/Props");

            MoveAsset(
                Root + "/Prefabs/CoffeeMug_DarkCoffee.mat",
                Root + "/Materials/Items/Props/CoffeeMug_DarkCoffee.mat");
            MoveAsset(
                Root + "/Prefabs/CoffeeMug_Mug.mat",
                Root + "/Materials/Items/Props/CoffeeMug_Mug.mat");
            MoveAsset(
                Root + "/Prefabs/EngineStatusLight.mat",
                Root + "/Materials/Vehicles/MiniVan/EngineStatusLight.mat");
            MoveAsset(
                Root + "/Prefabs/HotPotatoPoopLowPoly_Body.mat",
                Root + "/Materials/Items/HotPotato/HotPotatoPoopLowPoly_Body.mat");
            MoveAsset(
                Root + "/Prefabs/HotPotatoPoopLowPoly_Eye.mat",
                Root + "/Materials/Items/HotPotato/HotPotatoPoopLowPoly_Eye.mat");
            MoveAsset(
                Root + "/Prefabs/HotPotatoPoopLowPoly_Pupil.mat",
                Root + "/Materials/Items/HotPotato/HotPotatoPoopLowPoly_Pupil.mat");

            MoveAsset(
                Root + "/Generated/AG2_Missing_Unlit_Gray.mat",
                Root + "/Materials/Shared/AG2_Missing_Unlit_Gray.mat");
            MoveAsset(
                Root + "/Generated/MiniVanPrefab/MVG_Dark.mat",
                Root + "/Materials/Vehicles/MiniVan/Generated/MVG_Dark.mat");
            MoveAsset(
                Root + "/Generated/MiniVanPrefab/MVG_VanBody.mat",
                Root + "/Materials/Vehicles/MiniVan/Generated/MVG_VanBody.mat");
            MoveAsset(
                Root + "/Generated/Skateboard/SkateboardDeck.mat",
                Root + "/Materials/Vehicles/Rideables/Skateboard/SkateboardDeck.mat");
            MoveAsset(
                Root + "/Generated/Skateboard/SkateboardGrip.mat",
                Root + "/Materials/Vehicles/Rideables/Skateboard/SkateboardGrip.mat");
            MoveAsset(
                Root + "/Generated/Skateboard/SkateboardWheel.mat",
                Root + "/Materials/Vehicles/Rideables/Skateboard/SkateboardWheel.mat");
        }

        private static void MoveMixedGeneratedAssets()
        {
            MoveAssetsByType(
                Root + "/Generated",
                typeof(Mesh),
                Root + "/Models/Items/HotPotato",
                false);
            MoveAssetsByType(
                Root + "/Generated/MiniVanPrefab",
                typeof(Mesh),
                Root + "/Models/Vehicles/MiniVan",
                false);

            MoveAsset(
                Root + "/Generated/GameMode/Game_v01_GrassTexture.asset",
                Root + "/Textures/World/GameMode/Game_v01_GrassTexture.asset");
            MoveAsset(
                Root + "/Generated/GameMode/Game_v01_RoadTexture.asset",
                Root + "/Textures/World/GameMode/Game_v01_RoadTexture.asset");
            MoveAsset(
                Root + "/Generated/GameMode/Game_v01_RockTexture.asset",
                Root + "/Textures/World/GameMode/Game_v01_RockTexture.asset");
            MoveAsset(
                Root + "/Generated/GameMode/Game_v01_Window_Glare.asset",
                Root + "/Textures/World/GameMode/Game_v01_Window_Glare.asset");

            MoveAsset(
                Root + "/Generated/Skateboard/SkateboardLowFriction.asset",
                Root + "/PhysicsMaterials/Vehicles/Rideables/SkateboardLowFriction.asset");
            MoveAsset(
                Root + "/Generated/Skateboard/SkateboardLowFriction.physicsMaterial",
                Root + "/PhysicsMaterials/Vehicles/Rideables/SkateboardLowFriction.physicsMaterial");
        }

        private static void MoveResourceAssets()
        {
            MoveFolder(
                Root + "/Resources/FuelSystem/Materials",
                Root + "/Materials/Resources/FuelSystem/Materials");
            MoveFolder(
                Root + "/Resources/FuelSystem/Textures",
                Root + "/Textures/Vehicles/FuelSystem");
            MoveAsset(
                Root + "/Resources/Panelka_WorldTextDepth.mat",
                Root + "/Materials/Resources/Panelka_WorldTextDepth.mat");

            string pizzaRoot = Root + "/Resources/PizzaLoop";
            if (!AssetDatabase.IsValidFolder(pizzaRoot))
                return;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { pizzaRoot });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string source = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                MoveAsset(
                    source,
                    Root + "/Prefabs/Resources/PizzaLoop/" + Path.GetFileName(source));
            }

            string generated = pizzaRoot + "/LowPolyGenerated";
            MoveAssetsByType(generated, typeof(Material),
                Root + "/Materials/Items/Pizza/Generated", true);
            MoveAssetsByType(generated, typeof(Texture2D),
                Root + "/Textures/Items/Pizza", true);
            MoveAssetsByType(generated, typeof(Mesh),
                Root + "/Models/Items/Pizza", true);
        }

        private static void MoveDocumentationAndSettings()
        {
            MoveAsset(
                Root + "/Generated/TexturePack_LowPoly/README.md",
                Root + "/Documentation/Textures/LowPolyInteriorTexturePack.md");
            MoveAsset(
                Root + "/Generated/Panelka/Panelka_WorldTextDepth.shader",
                Root + "/Shaders/Panelka/PanelkaWorldTextDepth.shader");

            MoveAsset(
                Root + "/Generated/GameMode/Game_v01_TerrainData.asset",
                Root + "/Settings/World/GameMode/Terrain/Game_v01_TerrainData.asset");
            MoveAsset(
                Root + "/Generated/GameMode/Game_v01_Grass.terrainlayer",
                Root + "/Settings/World/GameMode/Terrain/Game_v01_Grass.terrainlayer");
            MoveAsset(
                Root + "/Generated/GameMode/Game_v01_Road.terrainlayer",
                Root + "/Settings/World/GameMode/Terrain/Game_v01_Road.terrainlayer");
            MoveAsset(
                Root + "/Generated/GameMode/Game_v01_Rock.terrainlayer",
                Root + "/Settings/World/GameMode/Terrain/Game_v01_Rock.terrainlayer");
        }

        private static void MovePrefab(string name, string category)
        {
            MoveAsset(
                Root + "/Prefabs/" + name + ".prefab",
                Root + "/Prefabs/" + category + "/" + name + ".prefab");
        }

        private static void MoveMaterial(string name, string category)
        {
            MoveAsset(
                Root + "/Materials/" + name + ".mat",
                Root + "/Materials/" + category + "/" + name + ".mat");
        }

        private static void MoveAssetsByType(
            string sourceFolder, Type type, string destinationFolder, bool includeSubfolders)
        {
            if (!AssetDatabase.IsValidFolder(sourceFolder))
                return;

            string filter = "t:" + type.Name;
            string[] guids = AssetDatabase.FindAssets(filter, new[] { sourceFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string source = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!includeSubfolders &&
                    !string.Equals(Path.GetDirectoryName(source)?.Replace('\\', '/'),
                        sourceFolder, StringComparison.Ordinal))
                    continue;

                MoveAsset(source, destinationFolder + "/" + Path.GetFileName(source));
            }
        }

        private static void MoveAssetsByExtension(
            string sourceFolder, string extension, string destinationFolder, bool includeSubfolders)
        {
            if (!AssetDatabase.IsValidFolder(sourceFolder))
                return;

            string[] guids = AssetDatabase.FindAssets("", new[] { sourceFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string source = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetDatabase.IsValidFolder(source))
                    continue;
                if (!source.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    continue;

                string parent = Path.GetDirectoryName(source)?.Replace('\\', '/');
                if (!includeSubfolders &&
                    !string.Equals(parent, sourceFolder, StringComparison.Ordinal))
                    continue;

                MoveAsset(source, destinationFolder + "/" + Path.GetFileName(source));
            }
        }

        private static void MoveFolderContents(string source, string destination)
        {
            if (!AssetDatabase.IsValidFolder(source))
                return;

            EnsureFolder(destination);
            string[] guids = AssetDatabase.FindAssets("", new[] { source });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetDatabase.IsValidFolder(assetPath))
                    continue;

                string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                if (!string.Equals(parent, source, StringComparison.Ordinal))
                    continue;

                MoveAsset(assetPath, destination + "/" + Path.GetFileName(assetPath));
            }
        }

        private static void MoveFolder(string source, string destination)
        {
            if (!AssetDatabase.IsValidFolder(source))
                return;
            if (AssetDatabase.IsValidFolder(destination))
            {
                MoveFolderContents(source, destination);
                return;
            }

            EnsureParentFolder(destination);
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
                Debug.LogError("[MiniVan Asset Organizer] " + source + " -> " + destination + ": " + error);
        }

        private static void MoveAsset(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(source) == null)
                return;
            if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
            {
                Debug.LogWarning("[MiniVan Asset Organizer] Destination already exists: " + destination);
                return;
            }

            EnsureParentFolder(destination);
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
                Debug.LogError("[MiniVan Asset Organizer] " + source + " -> " + destination + ": " + error);
        }

        private static void EnsureParentFolder(string assetPath)
        {
            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void DeleteAssetIfPresent(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        private static bool RemoveEmptyFolderTree(string assetFolder)
        {
            if (!AssetDatabase.IsValidFolder(assetFolder))
                return true;

            string[] subfolders = AssetDatabase.GetSubFolders(assetFolder);
            for (int i = 0; i < subfolders.Length; i++)
                RemoveEmptyFolderTree(subfolders[i]);

            string absolute = Path.GetFullPath(assetFolder);
            if (!Directory.Exists(absolute))
                return true;

            string[] entries = Directory.GetFileSystemEntries(absolute);
            for (int i = 0; i < entries.Length; i++)
            {
                if (!entries[i].EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            AssetDatabase.DeleteAsset(assetFolder);
            return true;
        }
    }
}
