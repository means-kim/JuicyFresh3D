using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using JuicyFresh3D.Parkour;
using Object = UnityEngine.Object;

namespace JuicyFresh3D.Editor
{
    public static class UrbanParkourPlaygroundBuilder_y
    {
        private const string ScenePath = "Assets/01.Scenes/UkYoung/01.Scenes/UkYoungScene.unity";
        private const string MaterialFolder = "Assets/01.Scenes/UkYoung/09.Materials/Parkour";
        private const string PrefabFolder = "Assets/01.Scenes/UkYoung/03.Prefabs/UkYoung";
        private const string PlaygroundRootName = "Urban Parkour Playground";

        private static readonly string[] DuplicateRootNames =
        {
            "Parkour Course",
            "Sprint Wall Climb Test Setup_y",
            "Anim_SprintingForwardRoll(Clone)",
            "Entry Bridge",
            "Training Lane",
            "Level 01 Jumping",
            "Level 02 Jumping 2",
            "Level 03 Jumping 3",
            "Level 04 Jump Over",
            "Level 05 Jump Over 2",
            "Level 06 Jump Over 3",
            "Left Course Guide",
            "Right Course Guide",
            "Jump Variety Course",
            "Ledge Test Setup Version 2_y",
            "Ledge Test Setup Version 3_y",
            "Ledge Test Setup Version 4_y",
            "Ledge Test Setup Version 5_y"
        };

        [MenuItem("Tools/JuicyFresh3D/Build Urban Parkour Playground")]
        public static void BuildUrbanParkourPlayground()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureFolder("Assets/01.Scenes/UkYoung/03.Prefabs", "UkYoung");

            MaterialPalette materials = CreateMaterials();
            PrefabPalette prefabs = CreatePrefabs(materials);

            RemovePreviousPlayground();
            int removedDuplicateCount = RemoveDuplicateRootObjects();

            GameObject playground = new(PlaygroundRootName);
            CreateGround(playground.transform, materials);
            CreateCentralPlaza(playground.transform, materials);
            CreateSection1_VaultStreet(playground.transform, materials, prefabs);
            CreateSection2_WallRunTower(playground.transform, materials, prefabs);
            CreateSection3_SkybridgeSlide(playground.transform, materials, prefabs);
            CreateSection4_RooftopHangAndRoll(playground.transform, materials, prefabs);
            CreateCityBackdrop(playground.transform, materials, prefabs);

            RepositionPlayer();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Scene could not be saved: {ScenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Urban parkour playground created. Removed duplicate roots: {removedDuplicateCount}");
        }

        private static void RepositionPlayer()
        {
            GameObject player = GameObject.Find("PF_Player");
            if (player == null)
            {
                return;
            }

            player.transform.SetPositionAndRotation(new Vector3(0f, 0.04f, -5f), Quaternion.identity);
            EditorUtility.SetDirty(player.transform);
        }

        private static MaterialPalette CreateMaterials()
        {
            return new MaterialPalette
            {
                Asphalt = CreateOrUpdateMaterial("M_UrbanAsphalt", new Color(0.055f, 0.075f, 0.095f), 0.08f, 0f),
                Concrete = CreateOrUpdateMaterial("M_UrbanConcrete", new Color(0.42f, 0.49f, 0.54f), 0.2f, 0f),
                LightConcrete = CreateOrUpdateMaterial("M_UrbanLightConcrete", new Color(0.68f, 0.74f, 0.77f), 0.24f, 0f),
                Facade = CreateOrUpdateMaterial("M_UrbanFacade", new Color(0.09f, 0.14f, 0.19f), 0.28f, 0.05f),
                Window = CreateOrUpdateMaterial("M_UrbanWindow", new Color(0.05f, 0.22f, 0.29f), 0.78f, 0.45f),
                Metal = CreateOrUpdateMaterial("M_UrbanMetal", new Color(0.24f, 0.3f, 0.34f), 0.5f, 0.65f),
                PrimaryRoute = CreateOrUpdateMaterial(
                    "M_UrbanPrimaryRoute",
                    new Color(0.05f, 0.68f, 0.78f),
                    0.4f,
                    0.1f,
                    new Color(0.02f, 0.3f, 0.38f)),
                TechnicalRoute = CreateOrUpdateMaterial(
                    "M_UrbanTechnicalRoute",
                    new Color(0.95f, 0.26f, 0.4f),
                    0.36f,
                    0.05f,
                    new Color(0.32f, 0.04f, 0.08f)),
                WarmLight = CreateOrUpdateMaterial(
                    "M_UrbanWarmLight",
                    new Color(1f, 0.58f, 0.18f),
                    0.45f,
                    0f,
                    new Color(0.36f, 0.14f, 0.02f))
            };
        }

        private static PrefabPalette CreatePrefabs(MaterialPalette materials)
        {
            return new PrefabPalette
            {
                Building = CreateBuildingPrefab(materials),
                Wall = CreateWallPrefab(materials),
                VaultBlock = CreateVaultBlockPrefab(materials),
                SlideGate = CreateSlideGatePrefab(materials),
                RoofUnit = CreateRoofUnitPrefab(materials)
            };
        }

        private static GameObject CreateBuildingPrefab(MaterialPalette materials)
        {
            GameObject root = new("PF_UrbanBuilding_y");
            CreateBlock(root.transform, "Building Mass", new Vector3(0f, 0.5f, 0f), Vector3.one, materials.Facade);
            CreateVisualBlock(root.transform, "Glass Band", new Vector3(0f, 0.58f, -0.506f), new Vector3(0.72f, 0.12f, 0.012f), materials.Window);
            CreateVisualBlock(root.transform, "Roof Light", new Vector3(0f, 1.02f, 0f), new Vector3(0.55f, 0.035f, 0.55f), materials.PrimaryRoute);
            return SavePrefab(root, "PF_UrbanBuilding_y");
        }

        private static GameObject CreateWallPrefab(MaterialPalette materials)
        {
            GameObject root = new("PF_UrbanWall_y");
            CreateBlock(root.transform, "Wall", new Vector3(0f, 0.5f, 0f), Vector3.one, materials.LightConcrete);
            CreateVisualBlock(root.transform, "Readable Edge", new Vector3(0f, 1.005f, 0f), new Vector3(1f, 0.018f, 1f), materials.PrimaryRoute);
            return SavePrefab(root, "PF_UrbanWall_y");
        }

        private static GameObject CreateVaultBlockPrefab(MaterialPalette materials)
        {
            GameObject root = new("PF_UrbanVaultBlock_y");
            CreateBlock(root.transform, "Vault Block", new Vector3(0f, 0.5f, 0f), Vector3.one, materials.Concrete);
            CreateVisualBlock(root.transform, "Vault Edge", new Vector3(0f, 1.005f, -0.42f), new Vector3(0.72f, 0.025f, 0.12f), materials.TechnicalRoute);
            return SavePrefab(root, "PF_UrbanVaultBlock_y");
        }

        private static GameObject CreateSlideGatePrefab(MaterialPalette materials)
        {
            GameObject root = new("PF_UrbanSlideGate_y");
            CreateBlock(root.transform, "Left Post", new Vector3(-1.85f, 1.25f, 0f), new Vector3(0.3f, 2.5f, 0.45f), materials.Metal);
            CreateBlock(root.transform, "Right Post", new Vector3(1.85f, 1.25f, 0f), new Vector3(0.3f, 2.5f, 0.45f), materials.Metal);
            CreateBlock(root.transform, "Low Beam", new Vector3(0f, 1.58f, 0f), new Vector3(3.4f, 0.5f, 0.45f), materials.LightConcrete);
            CreateVisualBlock(root.transform, "Beam Marker", new Vector3(0f, 1.835f, -0.235f), new Vector3(1.5f, 0.06f, 0.03f), materials.TechnicalRoute);
            return SavePrefab(root, "PF_UrbanSlideGate_y");
        }

        private static GameObject CreateRoofUnitPrefab(MaterialPalette materials)
        {
            GameObject root = new("PF_UrbanRoofUnit_y");
            CreateBlock(root.transform, "Roof Platform", new Vector3(0f, 0.5f, 0f), Vector3.one, materials.Concrete);
            CreateVisualBlock(root.transform, "Roof Edge", new Vector3(0f, 1.005f, -0.47f), new Vector3(0.72f, 0.025f, 0.06f), materials.WarmLight);
            return SavePrefab(root, "PF_UrbanRoofUnit_y");
        }

        private static void CreateGround(Transform parent, MaterialPalette materials)
        {
            Transform root = CreateGroup(parent, "00_Ground");
            // Lower City Foundation by 0.06m (top surface at y = -0.06m) so it never fights with any floor slab at y = 0.00m
            CreateBlock(root, "City Foundation", new Vector3(0f, -0.56f, 45f), new Vector3(60f, 1f, 130f), materials.Asphalt);

            CreateVisualBlock(root, "Main Perimeter West", new Vector3(-25f, -0.045f, 45f), new Vector3(0.2f, 0.02f, 119.6f), materials.Metal);
            CreateVisualBlock(root, "Main Perimeter East", new Vector3(25f, -0.045f, 45f), new Vector3(0.2f, 0.02f, 119.6f), materials.Metal);
            CreateVisualBlock(root, "Main Perimeter South", new Vector3(0f, -0.045f, -15f), new Vector3(49.8f, 0.02f, 0.2f), materials.Metal);
            CreateVisualBlock(root, "Main Perimeter North", new Vector3(0f, -0.045f, 105f), new Vector3(49.8f, 0.02f, 0.2f), materials.Metal);
        }

        private static void CreateCentralPlaza(Transform parent, MaterialPalette materials)
        {
            Transform root = CreateGroup(parent, "01_CentralPlaza");
            // Plaza floor spans z = -13m to 3m, x = -10m to 10m, top at y = 0.00m
            CreateBlock(root, "Plaza Floor", new Vector3(0f, -0.05f, -5f), new Vector3(20f, 0.1f, 16f), materials.Concrete);

            // High Drop landing zone floor on East side (z = 3m to 8m, x = 8m to 12m) at y = 0.00m
            CreateBlock(root, "Plaza East Landing Zone", new Vector3(10f, -0.05f, 5.5f), new Vector3(4f, 0.1f, 5f), materials.Concrete);

            // Visual markers elevated 0.005m above the y = 0.00m floor (top at y = 0.010m)
            CreateVisualBlock(root, "Plaza Start Line", new Vector3(0f, 0.005f, -5f), new Vector3(3f, 0.01f, 0.25f), materials.PrimaryRoute);
            CreateVisualBlock(root, "Plaza Departure Guide Left", new Vector3(-1.6f, 0.005f, -1f), new Vector3(0.12f, 0.01f, 8f), materials.TechnicalRoute);
            CreateVisualBlock(root, "Plaza Departure Guide Right", new Vector3(1.6f, 0.005f, -1f), new Vector3(0.12f, 0.01f, 8f), materials.TechnicalRoute);

            CreateBlock(root, "Plaza Monolith West", new Vector3(-8.5f, 2.5f, -6f), new Vector3(1.6f, 5f, 1.6f), materials.Facade);
            CreateVisualBlock(root, "Monolith West Light", new Vector3(-8.5f, 5.05f, -6f), new Vector3(1.2f, 0.1f, 1.2f), materials.WarmLight);

            CreateBlock(root, "Plaza Monolith East", new Vector3(8.5f, 2.5f, -6f), new Vector3(1.6f, 5f, 1.6f), materials.Facade);
            CreateVisualBlock(root, "Monolith East Light", new Vector3(8.5f, 5.05f, -6f), new Vector3(1.2f, 0.1f, 1.2f), materials.PrimaryRoute);
        }

        private static void CreateSection1_VaultStreet(Transform parent, MaterialPalette materials, PrefabPalette prefabs)
        {
            Transform root = CreateGroup(parent, "02_Section1_VaultStreet");
            // Floor from z = 3m to 37m (center z = 20m, size z = 34m), x = -3.2m to 3.2m, top at y = 0.00m
            CreateBlock(root, "Vault Street Floor", new Vector3(0f, -0.05f, 20f), new Vector3(6.4f, 0.1f, 34f), materials.Concrete);

            CreateParkourObstacle(prefabs.VaultBlock, root, "Vault 1 Low", new Vector3(0f, 0f, 14f), new Vector3(3.2f, 0.90f, 0.25f), 1);
            CreateParkourObstacle(prefabs.VaultBlock, root, "Vault 2 Mid", new Vector3(0f, 0f, 23f), new Vector3(3.2f, 1.16f, 0.25f), 2);
            CreateParkourObstacle(prefabs.VaultBlock, root, "Vault 3 High Over", new Vector3(0f, 0f, 33f), new Vector3(3.5f, 1.35f, 0.85f), 4);

            CreateBlock(root, "Street Curb Left", new Vector3(-3.2f, 0.25f, 20f), new Vector3(0.4f, 0.5f, 34f), materials.Metal);
            CreateBlock(root, "Street Curb Right", new Vector3(3.2f, 0.25f, 20f), new Vector3(0.4f, 0.5f, 34f), materials.Metal);
            CreateVisualBlock(root, "Street Guideline", new Vector3(0f, 0.005f, 20f), new Vector3(0.12f, 0.01f, 32f), materials.PrimaryRoute);
        }

        private static void CreateSection2_WallRunTower(Transform parent, MaterialPalette materials, PrefabPalette prefabs)
        {
            Transform root = CreateGroup(parent, "03_Section2_WallRunTower");
            // Floor from z = 37m to 61m (center z = 49m, size z = 24m), x = -16m to 2m, top at y = 0.00m
            CreateBlock(root, "Wall Run Floor", new Vector3(-7f, -0.05f, 49f), new Vector3(18f, 0.1f, 24f), materials.Concrete);

            // Right wall for Wall Run Right
            CreateBlock(root, "Wall Run Right", new Vector3(-2.0f, 2.75f, 45f), new Vector3(0.45f, 5.5f, 10f), materials.LightConcrete);
            CreateVisualBlock(root, "Wall Run Right Edge", new Vector3(-2.0f, 5.52f, 45f), new Vector3(0.5f, 0.04f, 10f), materials.PrimaryRoute);
            CreateVisualBlock(root, "Wall Run Right Marker", new Vector3(-2.24f, 2.2f, 45f), new Vector3(0.02f, 0.18f, 9.5f), materials.TechnicalRoute);

            // Left wall for Wall Run Left
            CreateBlock(root, "Wall Run Left", new Vector3(-8.0f, 2.9f, 54f), new Vector3(0.45f, 5.8f, 12f), materials.LightConcrete);
            CreateVisualBlock(root, "Wall Run Left Edge", new Vector3(-8.0f, 5.82f, 54f), new Vector3(0.5f, 0.04f, 12f), materials.PrimaryRoute);
            CreateVisualBlock(root, "Wall Run Left Marker", new Vector3(-7.76f, 2.6f, 54f), new Vector3(0.02f, 0.18f, 11.5f), materials.TechnicalRoute);

            // Wall Tower Backing Mass
            CreateBlock(root, "Wall Tower Backing", new Vector3(-5.0f, 3.5f, 62f), new Vector3(7f, 7f, 0.5f), materials.Facade);
        }

        private static void CreateSection3_SkybridgeSlide(Transform parent, MaterialPalette materials, PrefabPalette prefabs)
        {
            Transform root = CreateGroup(parent, "04_Section3_SkybridgeSlide");

            // Upper Skybridge Platform at height y = 4.0m (z = 58m to 70m)
            CreateBlock(root, "Skybridge Upper Platform", new Vector3(-14f, 2.0f, 64f), new Vector3(4f, 4.0f, 12f), materials.Concrete);
            CreateBlock(root, "Upper Railing Left", new Vector3(-16.0f, 4.3f, 64f), new Vector3(0.2f, 0.6f, 12f), materials.Metal);
            CreateBlock(root, "Upper Railing Right", new Vector3(-12.0f, 4.3f, 64f), new Vector3(0.2f, 0.6f, 12f), materials.Metal);

            // Step-Down edge at z = 70m dropping 0.35m to y = 3.65m. Raised 0.005m above floor so top is at y = 4.01m
            CreateVisualBlock(root, "Step Down Marker", new Vector3(-14f, 4.005f, 70f), new Vector3(3.6f, 0.01f, 0.2f), materials.WarmLight);

            // Lower Skybridge Platform at height y = 3.65m (z = 70m to 86m, exactly touching Turn Platform at z = 86m)
            CreateBlock(root, "Skybridge Lower Platform", new Vector3(-14f, 1.825f, 78f), new Vector3(4f, 3.65f, 16f), materials.Concrete);
            CreateBlock(root, "Lower Railing Left", new Vector3(-16.0f, 3.95f, 78f), new Vector3(0.2f, 0.6f, 16f), materials.Metal);
            CreateBlock(root, "Lower Railing Right", new Vector3(-12.0f, 3.95f, 78f), new Vector3(0.2f, 0.6f, 16f), materials.Metal);

            // Slide Gate at z = 77m with 1.25m clearance (bottom of beam at 4.90m)
            CreateBlock(root, "Slide Gate Left Post", new Vector3(-15.82f, 4.25f, 77f), new Vector3(0.26f, 1.6f, 0.45f), materials.Metal);
            CreateBlock(root, "Slide Gate Right Post", new Vector3(-12.18f, 4.25f, 77f), new Vector3(0.26f, 1.6f, 0.45f), materials.Metal);
            CreateBlock(root, "Slide Gate Low Beam", new Vector3(-14f, 5.15f, 77f), new Vector3(3.8f, 0.5f, 0.6f), materials.LightConcrete);
            CreateVisualBlock(root, "Slide Gate Marker", new Vector3(-14f, 5.41f, 77f), new Vector3(1.6f, 0.04f, 0.04f), materials.TechnicalRoute);
            CreateVisualBlock(root, "Slide Approach Marker", new Vector3(-14f, 3.655f, 74f), new Vector3(1.2f, 0.01f, 4f), materials.TechnicalRoute);

            // North Corner Turn Platform at z = 86m to 90m (x = -16m to -12m)
            CreateBlock(root, "North West Turn Platform", new Vector3(-14f, 1.825f, 88f), new Vector3(4f, 3.65f, 4f), materials.Concrete);
        }

        private static void CreateSection4_RooftopHangAndRoll(Transform parent, MaterialPalette materials, PrefabPalette prefabs)
        {
            Transform root = CreateGroup(parent, "05_Section4_RooftopHangAndRoll");

            // West Crossover Platform (x = -12m to -2m, touching Turn Platform at x = -12m) at height y = 3.65m
            CreateBlock(root, "West Crossover Platform", new Vector3(-7.0f, 1.825f, 88f), new Vector3(10f, 3.65f, 4f), materials.Concrete);
            CreateVisualBlock(root, "Takeoff Edge Marker", new Vector3(-2.05f, 3.655f, 88f), new Vector3(0.15f, 0.01f, 3.96f), materials.PrimaryRoute);

            // East Crossover Platform (x = 1.2m to 8.0m, touching East High Runway at x = 8.0m) across 3.2m gap
            CreateBlock(root, "East Crossover Platform", new Vector3(4.6f, 1.825f, 88f), new Vector3(6.8f, 3.65f, 4f), materials.Concrete);
            CreateVisualBlock(root, "Ledge Grab Edge Marker", new Vector3(1.22f, 3.655f, 88f), new Vector3(0.08f, 0.01f, 3.96f), materials.PrimaryRoute);

            // East High Runway running South along x = 10m (x = 8.0m to 12.0m) from z = 90m down to z = 8m
            CreateBlock(root, "East High Runway", new Vector3(10.0f, 1.825f, 49.0f), new Vector3(4.0f, 3.65f, 82.0f), materials.Concrete);
            CreateBlock(root, "Runway Parapet East", new Vector3(12.1f, 3.95f, 49.0f), new Vector3(0.2f, 0.6f, 82.0f), materials.Metal);
            CreateBlock(root, "Runway Parapet West", new Vector3(7.9f, 3.95f, 47.0f), new Vector3(0.2f, 0.6f, 78.0f), materials.Metal);
            CreateVisualBlock(root, "Runway Guideline", new Vector3(10.0f, 3.655f, 49.0f), new Vector3(0.2f, 0.01f, 80.0f), materials.PrimaryRoute);

            // High Drop Edge at z = 8m (drop from y = 3.65m to y = 0.00m)
            CreateVisualBlock(root, "High Drop Edge Marker", new Vector3(10.0f, 3.655f, 8.05f), new Vector3(3.6f, 0.01f, 0.15f), materials.WarmLight);
        }

        private static void CreateCityBackdrop(Transform parent, MaterialPalette materials, PrefabPalette prefabs)
        {
            Transform root = CreateGroup(parent, "90_CityBackdrop");
            CreateBuilding(root, prefabs.Building, "South West Building", new Vector3(-35f, -0.1f, -8f), new Vector3(16f, 14f, 18f));
            CreateBuilding(root, prefabs.Building, "South East Building", new Vector3(34f, -0.1f, -9f), new Vector3(18f, 18f, 16f));
            CreateBuilding(root, prefabs.Building, "West Mid Building", new Vector3(-36f, -0.1f, 27f), new Vector3(18f, 20f, 20f));
            CreateBuilding(root, prefabs.Building, "East Mid Building", new Vector3(50f, -0.1f, 30f), new Vector3(14f, 16f, 20f));
            CreateBuilding(root, prefabs.Building, "North West Building", new Vector3(-34f, -0.1f, 94f), new Vector3(20f, 22f, 18f));
            CreateBuilding(root, prefabs.Building, "North Centre Building", new Vector3(0f, -0.1f, 97f), new Vector3(22f, 24f, 14f));
            CreateBuilding(root, prefabs.Building, "North East Building", new Vector3(34f, -0.1f, 94f), new Vector3(18f, 19f, 18f));

            CreateVisualBlock(root, "West Skyline Fin", new Vector3(-43f, 9f, 58f), new Vector3(0.8f, 18f, 8f), materials.PrimaryRoute);
            CreateVisualBlock(root, "East Skyline Fin", new Vector3(50f, 8f, 70f), new Vector3(0.8f, 16f, 10f), materials.WarmLight);
        }

        private static void CreateBuilding(Transform parent, GameObject prefab, string name, Vector3 position, Vector3 scale)
        {
            CreatePrefabInstance(prefab, parent, name, position, scale);
        }

        private static GameObject CreatePrefabInstance(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Vector3 rotation = default)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Prefab could not be instantiated: {prefab.name}");
            }

            instance.name = name;
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(rotation);
            instance.transform.localScale = scale;
            SetStaticRecursively(instance);
            return instance;
        }

        private static GameObject CreateParkourObstacle(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            int jumpTier)
        {
            GameObject obstacle = CreatePrefabInstance(prefab, parent, name, position, scale);
            ParkourObstacleProfile_y profile = obstacle.GetComponent<ParkourObstacleProfile_y>();
            if (profile == null)
            {
                profile = obstacle.AddComponent<ParkourObstacleProfile_y>();
            }

            profile.SetJumpTier(jumpTier);
            return obstacle;
        }

        private static GameObject CreateBlock(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Vector3 rotation = default)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.rotation = Quaternion.Euler(rotation);
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            SetStatic(block);
            return block;
        }

        private static GameObject CreateVisualBlock(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Vector3 rotation = default)
        {
            GameObject block = CreateBlock(parent, name, position, scale, material, rotation);
            Object.DestroyImmediate(block.GetComponent<Collider>());
            return block;
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject group = new(name);
            group.transform.SetParent(parent);
            return group.transform;
        }

        private static GameObject SavePrefab(GameObject root, string name)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Prefab could not be saved: {path}");
            }

            return prefab;
        }

        private static Material CreateOrUpdateMaterial(
            string name,
            Color color,
            float smoothness,
            float metallic,
            Color? emission = null)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException("Universal Render Pipeline Lit shader was not found.");
                }

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void RemovePreviousPlayground()
        {
            GameObject previous = GameObject.Find(PlaygroundRootName);
            if (previous != null && previous.transform.parent == null)
            {
                Object.DestroyImmediate(previous);
            }
        }

        private static int RemoveDuplicateRootObjects()
        {
            HashSet<string> duplicateNames = new(DuplicateRootNames, StringComparer.Ordinal);
            int removedCount = 0;
            foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!duplicateNames.Contains(rootObject.name) && !rootObject.name.Contains("(Clone)"))
                {
                    continue;
                }

                Object.DestroyImmediate(rootObject);
                removedCount++;
            }

            return removedCount;
        }

        private static void SetStaticRecursively(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                SetStatic(child.gameObject);
            }
        }

        private static void SetStatic(GameObject gameObject)
        {
            GameObjectUtility.SetStaticEditorFlags(
                gameObject,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private sealed class MaterialPalette
        {
            public Material Asphalt { get; set; }
            public Material Concrete { get; set; }
            public Material LightConcrete { get; set; }
            public Material Facade { get; set; }
            public Material Window { get; set; }
            public Material Metal { get; set; }
            public Material PrimaryRoute { get; set; }
            public Material TechnicalRoute { get; set; }
            public Material WarmLight { get; set; }
        }

        private sealed class PrefabPalette
        {
            public GameObject Building { get; set; }
            public GameObject Wall { get; set; }
            public GameObject VaultBlock { get; set; }
            public GameObject SlideGate { get; set; }
            public GameObject RoofUnit { get; set; }
        }
    }
}
