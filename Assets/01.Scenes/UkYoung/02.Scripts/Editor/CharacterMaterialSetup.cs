using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JuicyFresh3D.Editor
{
    public static class CharacterMaterialSetup
    {
        private const string ModelPath = "Assets/01.Scenes/UkYoung/05.Models/Character_model.fbx";
        private const string TextureFolder = "Assets/01.Scenes/UkYoung/05.Models/Textures";
        private const string MaterialFolder = "Assets/01.Scenes/UkYoung/05.Models/Materials";
        private const string MaterialPath = MaterialFolder + "/M_Character.mat";

        [InitializeOnLoadMethod]
        private static void ScheduleAutomaticSetup()
        {
            EditorApplication.delayCall += RunAutomaticSetup;
        }

        [MenuItem("Tools/JuicyFresh3D/Repair Character Materials")]
        public static void RepairCharacterMaterials()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"Character model was not found: {ModelPath}");
                return;
            }

            EnsureFolder("Assets/01.Scenes/UkYoung/05.Models", "Textures");
            EnsureFolder("Assets/01.Scenes/UkYoung/05.Models", "Materials");

            importer.ExtractTextures(TextureFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Texture2D diffuse = FindTexture("Ch03_1001_Diffuse");
            Texture2D normal = FindTexture("Ch03_1001_Normal");
            Texture2D specular = FindTexture("Ch03_1001_Specular");
            Texture2D glossiness = FindTexture("Ch03_1001_Glossiness");
            if (diffuse == null)
            {
                Debug.LogError("Embedded character textures could not be extracted.");
                return;
            }

            ConfigureNormalMap(normal);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = "M_Character" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", diffuse);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_WorkflowMode", 1f);
            material.SetTexture("_SpecGlossMap", specular);
            material.SetFloat("_Smoothness", glossiness != null ? 0.55f : 0.35f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_SPECULAR_SETUP");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Material[] embeddedMaterials = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<Material>()
                .ToArray();
            foreach (Material embeddedMaterial in embeddedMaterials)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embeddedMaterial), material);
            }

            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            Debug.Log($"Character material repaired with {embeddedMaterials.Length} material remap(s).");
        }

        private static void RunAutomaticSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunAutomaticSetup;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
            {
                RepairCharacterMaterials();
            }
        }

        private static Texture2D FindTexture(string name)
        {
            string guid = AssetDatabase.FindAssets($"{name} t:Texture2D", new[] { TextureFolder })
                .FirstOrDefault();
            return string.IsNullOrEmpty(guid)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static void ConfigureNormalMap(Texture2D normal)
        {
            if (normal == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(normal);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
