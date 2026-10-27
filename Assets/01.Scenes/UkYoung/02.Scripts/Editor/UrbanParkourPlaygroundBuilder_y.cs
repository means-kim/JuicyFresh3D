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
            "Entry Bridge",
            "Training Lane",
            "Level 01 Jumping",
            "Level 02 Jumping 2",
            "Level 03 Jumping 3",
            "Level 04 Jump Over",
            "Level 05 Jump Over 2",
            "Level 06 Jump Over 3",
            "Left Course Guide",
            "Right Course Guide"
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
            CreateEastObstacleAlley(playground.transform, materials, prefabs);
            CreateWestWallRunAlley(playground.transform, materials, prefabs);
            CreateNorthServiceYard(playground.transform, materials, prefabs);
            CreateRooftopCircuit(playground.transform, materials, prefabs);
            CreateFutureTestYard(playground.transform, materials, prefabs);
            CreateCityBackdrop(playground.transform, materials, prefabs);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Scene could not be saved: {ScenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Urban parkour playground created. Removed duplicate roots: {removedDuplicateCount}");
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
            CreateBlock(root, "City Foundation", new Vector3(0f, -1.1f, 35f), new Vector3(90f, 2f, 110f), materials.Asphalt);
            CreateVisualBlock(root, "Main Loop West", new Vector3(-18f, -0.07f, 36f), new Vector3(0.18f, 0.04f, 82f), materials.PrimaryRoute);
            CreateVisualBlock(root, "Main Loop East", new Vector3(18f, -0.07f, 36f), new Vector3(0.18f, 0.04f, 82f), materials.PrimaryRoute);
            CreateVisualBlock(root, "Main Loop North", new Vector3(0f, -0.07f, 77f), new Vector3(36f, 0.04f, 0.18f), materials.PrimaryRoute);
            CreateVisualBlock(root, "Main Loop South", new Vector3(0f, -0.07f, -5f), new Vector3(36f, 0.04f, 0.18f), materials.PrimaryRoute);

            CreateVisualBlock(root, "Central Connector", new Vector3(0f, -0.065f, 30f), new Vector3(0.14f, 0.045f, 44f), materials.TechnicalRoute);
            CreateVisualBlock(root, "North Connector", new Vector3(0f, -0.065f, 62f), new Vector3(36f, 0.045f, 0.14f), materials.TechnicalRoute);
            CreateVisualBlock(root, "South Connector", new Vector3(0f, -0.065f, 10f), new Vector3(36f, 0.045f, 0.14f), materials.TechnicalRoute);
        }

        private static void CreateCentralPlaza(Transform parent, MaterialPalette materials)
        {
            Transform root = CreateGroup(parent, "01_CentralPlaza");
            CreateBlock(root, "Central Plaza", new Vector3(0f, -0.02f, -3f), new Vector3(24f, 0.12f, 18f), materials.Concrete);
            CreateVisualBlock(root, "Plaza Route Inlay", new Vector3(0f, 0.045f, -3f), new Vector3(1.1f, 0.015f, 14f), materials.PrimaryRoute);

            CreateBlock(root, "Landmark Base", new Vector3(-8.5f, 0.45f, -6f), new Vector3(2.6f, 0.9f, 2.6f), materials.Facade);
            GameObject landmark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            landmark.name = "Central Light Spire";
            landmark.transform.SetParent(root);
            landmark.transform.position = new Vector3(-8.5f, 4.8f, -6f);
            landmark.transform.localScale = new Vector3(0.65f, 4.4f, 0.65f);
            landmark.GetComponent<Renderer>().sharedMaterial = materials.PrimaryRoute;
            Object.DestroyImmediate(landmark.GetComponent<Collider>());
            SetStatic(landmark);

            CreateVisualBlock(root, "East District Marker", new Vector3(10.5f, 1.6f, 2.8f), new Vector3(0.2f, 3.2f, 1.1f), materials.TechnicalRoute);
            CreateVisualBlock(root, "West District Marker", new Vector3(-10.5f, 1.6f, 2.8f), new Vector3(0.2f, 3.2f, 1.1f), materials.PrimaryRoute);
        }

        private static void CreateEastObstacleAlley(Transform parent, MaterialPalette materials, PrefabPalette prefabs)
        {
            Transform root = CreateGroup(parent, "02_EastObstacleAlley");
            CreateBlock(root, "East Alley Floor", new Vector3(15f, -0.02f, 34f), new Vector3(12f, 0.12f, 58f), materials.Concrete);
            CreateVisualBlock(root, "East Alley Edge", new Vector3(9.2f, 0.05f, 34f), new Vector3(0.16f, 0.03f, 54f), materials.TechnicalRoute);

            CreateParkourObstacle(prefabs.VaultBlock, root, "Jumping 1", new Vector3(14f, 0f, 22f), new Vector3(3.2f, 1.16f, 0.24f), 1);
            CreateParkourObstacle(prefabs.VaultBlock, root, "Jumping 2", new Vector3(16.2f, 0f, 31f), new Vector3(3.2f, 1.16f, 0.24f), 2);
            CreateParkourObstacle(prefabs.VaultBlock, root, "Jumping 3", new Vector3(13.8f, 0f, 40f), new Vector3(3.2f, 1.16f, 0.24f), 3);
            CreateParkourObstacle(prefabs.VaultBlock, root, "Jump Over", new Vector3(15f, 0f, 50f), new Vector3(3.5f, 1.35f, 0.85f), 4);

            CreatePrefabInstance(prefabs.Wall, root, "East Alley Divider", new Vector3(21f, 0f, 34f), new Vector3(0.5f, 3.8f, 54f));
            CreateVisualBlock(root, "Obstacle Alley Sign", new Vector3(20.72f, 2.6f, 8f), new Vector3(0.04f, 1.1f, 2.8f), materials.TechnicalRoute);
        }

        private static void CreateWestWallRunAlley(Transform parent, MaterialPalette materials, PrefabPalette prefabs)
        {
            Transform root = CreateGroup(parent, "03_WestWallRunAlley");
            CreateBlock(root, "West Alley Floor", new Vector3(-17f, -0.02f, 33f), new Vector3(12f, 0.12f, 58f), materials.Concrete);
            CreatePrefabInstance(prefabs.Wall, root, "West Outer Wall", new Vector3(-22.5f, 0f, 34f), new Vector3(0.55f, 5.2f, 54f));

            CreatePrefabInstance(prefabs.Wall, root, "Wall Run A", new Vector3(-13.2f, 0f, 17f), new Vector3(0.55f, 4.5f, 9f));
            CreatePrefabInstance(prefabs.Wall, root, "Wall Run B", new Vector3(-17.5f, 0f, 30f), new Vector3(0.55f, 5f, 10f), new Vector3(0f, 90f, 0f));
            CreatePrefabInstance(prefabs.Wall, root, "Wall Run C", new Vector3(-13.2f, 0f, 43f), new Vector3(0.55f, 5.5f, 9f));
            CreatePrefabInstance(prefabs.Wall, root, "Wall Run Turn", new Vector3(-17.5f, 0f, 51f), new Vector3(0.55f, 5.5f, 8f), new Vector3(0f, 90f, 0f));

            CreateVisualBlock(root, "Wall Alley Entry Marker", new Vector3(-11.2f, 2.4f, 7f), new Vector3(0.08f, 1.4f, 1.8f), materials.PrimaryRoute);
        }

        private static void CreateNorthServiceYard(Transform parent, MaterialPalette materials, PrefabPalette prefabs)
        {
            Transform root = CreateGroup(parent, "04_NorthServiceYard");
            CreateBlock(root, "Service Yard Floor", new Vector3(0f, -0.02f, 75f), new Vector3(38f, 0.12f, 18f), materials.Concrete);

            CreatePrefabInstance(
                prefabs.SlideGate,
                root,
                "Slide Tunnel",
                new Vector3(-11f, 0f, 73f),
                new Vector3(1f, 1f, 14f));
            CreatePrefabInstance(prefabs.SlideGate, root, "Slide Gate B", new Vector3(-11f, 0f, 78f), Vector3.one);
            CreateVisualBlock(root, "Slide Approach", new Vector3(-11f, 0.05f, 66.5f), new Vector3(1.2f, 0.025f, 5f), materials.TechnicalRoute);

            CreateBlock(root, "Short Roll Divider Left", new Vector3(4f, 0.7f, 74f), new Vector3(0.35f, 1.4f, 12f), materials.Metal);
            CreateBlock(root, "Short Roll Divider Right", new Vector3(10f, 0.7f, 74f), new Vector3(0.35f, 1.4f, 12f), materials.Metal);
            CreateVisualBlock(root, "Short Roll Lane", new Vector3(7f, 0.05f, 74f), new Vector3(0.7f, 0.025f, 10f), materials.WarmLight);

            CreatePrefabInstance(prefabs.VaultBlock, root, "Service Vault", new Vector3(15f, 0f, 76f), new Vector3(2.8f, 0.75f, 0.7f));
        }

        private static void CreateRooftopCircuit(Transform parent, MaterialPalette materials, PrefabPalette prefabs)
        {
            Transform root = CreateGroup(parent, "05_RooftopCircuit");

            CreatePrefabInstance(prefabs.RoofUnit, root, "Roof Entry", new Vector3(26f, 0f, 12f), new Vector3(11f, 2.4f, 10f));
            CreateRamp(root, "Roof Entry Ramp", new Vector3(21.5f, 1.1f, 12f), new Vector3(10f, 0.7f, 5f), new Vector3(0f, 0f, 13f), materials.Concrete);

            CreatePrefabInstance(prefabs.RoofUnit, root, "Roof Two", new Vector3(28f, 0f, 24.5f), new Vector3(12f, 3f, 11f));
            CreatePrefabInstance(prefabs.RoofUnit, root, "Roof Three", new Vector3(26f, 0f, 38.5f), new Vector3(10f, 3.6f, 10f));
            CreatePrefabInstance(prefabs.RoofUnit, root, "Roof Four", new Vector3(30f, 0f, 54.5f), new Vector3(12f, 4.2f, 12f));

            CreateRamp(root, "Roof Exit Ramp", new Vector3(24f, 2.1f, 63f), new Vector3(14f, 0.7f, 5f), new Vector3(0f, 0f, 17f), materials.LightConcrete);
            CreateVisualBlock(root, "Roof Circuit Marker", new Vector3(36.2f, 4f, 42f), new Vector3(0.12f, 5f, 1.8f), materials.WarmLight);

            CreateBlock(root, "Low Drop Landing Pad", new Vector3(38f, -0.02f, 20f), new Vector3(9f, 0.12f, 9f), materials.Concrete);
            CreateBlock(root, "Medium Drop Landing Pad", new Vector3(38f, -0.02f, 39f), new Vector3(9f, 0.12f, 10f), materials.Concrete);
            CreateBlock(root, "High Drop Landing Pad", new Vector3(38f, -0.02f, 58f), new Vector3(9f, 0.12f, 12f), materials.Concrete);
        }

        private static void CreateFutureTestYard(Transform parent, MaterialPalette materials, PrefabPalette prefabs)
        {
            Transform root = CreateGroup(parent, "06_FutureTestYard");
            CreateBlock(root, "Future Yard Floor", new Vector3(-33f, -0.02f, 72f), new Vector3(17f, 0.12f, 23f), materials.Asphalt);
            CreatePrefabInstance(prefabs.Wall, root, "Wall Climb Test", new Vector3(-38f, 0f, 76f), new Vector3(4f, 1.85f, 5f));
            CreateVisualBlock(root, "Wall Climb Runway", new Vector3(-30.5f, 0.05f, 76f), new Vector3(7f, 0.025f, 0.45f), materials.PrimaryRoute);
            CreateVisualBlock(root, "Wall Climb Trigger Marker", new Vector3(-34.3f, 0.055f, 76f), new Vector3(0.08f, 0.03f, 5f), materials.TechnicalRoute);
            CreateBlock(root, "Tall Thin Hanging Wall", new Vector3(-38f, 1.125f, 68f), new Vector3(0.16f, 2.25f, 5f), materials.LightConcrete);
            CreateVisualBlock(root, "Tall Wall Runway", new Vector3(-30.5f, 0.05f, 68f), new Vector3(7f, 0.025f, 0.45f), materials.PrimaryRoute);
            CreatePrefabInstance(prefabs.VaultBlock, root, "Flip Test Block", new Vector3(-29f, 0f, 70f), new Vector3(4f, 1.2f, 1.2f));
            CreateVisualBlock(root, "Future Yard Boundary", new Vector3(-24.6f, 0.04f, 72f), new Vector3(0.16f, 0.025f, 21f), materials.WarmLight);
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

        private static void CreateRamp(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Vector3 rotation,
            Material material)
        {
            CreateBlock(parent, name, position, scale, material, rotation);
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
                if (!duplicateNames.Contains(rootObject.name))
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
