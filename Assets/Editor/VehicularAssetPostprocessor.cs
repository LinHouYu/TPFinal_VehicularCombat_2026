using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VehicularCombat.EditorTools
{
    /// <summary>
    /// Enforces pixel-art TextureImporter settings (Point, exact MaxSize 32/64/128/256,
    /// Compression=None, MipMaps=false) and wires URP/Lit materials to every FBX under Assets/Models.
    /// </summary>
    public class VehicularAssetPostprocessor : AssetPostprocessor
    {
        private const string ModelsRoot = "Assets/Models";
        private const string UiArtRoot = "Assets/Art/UI";
        private const string MaterialsRoot = "Assets/Materials/Models";

        private static readonly int[] ValidMaxSizes = { 32, 64, 128, 256, 512, 1024, 2048 };

        private void OnPreprocessTexture()
        {
            var ti = assetImporter as TextureImporter;
            if (ti == null)
                return;

            if (assetPath.StartsWith(UiArtRoot, StringComparison.OrdinalIgnoreCase))
            {
                ApplyPixelUiSpriteSettings(ti, assetPath);
                return;
            }

            if (assetPath.StartsWith(ModelsRoot, StringComparison.OrdinalIgnoreCase))
            {
                ApplyPixelTextureSettings(ti, assetPath);
            }
        }

        public static void ApplyPixelUiSpriteSettings(TextureImporter ti, string path)
        {
            int targetSize = ResolveExactMaxSize(ti, path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 16f;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = targetSize;
            ti.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            if (path.EndsWith("UI_PixelPanel.png", StringComparison.OrdinalIgnoreCase) || path.EndsWith("UI_PixelButton.png", StringComparison.OrdinalIgnoreCase))
                settings.spriteBorder = new Vector4(4, 4, 4, 4);
            else if (path.EndsWith("UI_PixelBarFrame.png", StringComparison.OrdinalIgnoreCase))
                settings.spriteBorder = new Vector4(2, 2, 2, 2);
            else if (path.EndsWith("UI_SpeedometerNeedle.png", StringComparison.OrdinalIgnoreCase))
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0.125f);
            }
            ti.SetTextureSettings(settings);

            var defSettings = ti.GetDefaultPlatformTextureSettings();
            defSettings.maxTextureSize = targetSize;
            defSettings.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SetPlatformTextureSettings(defSettings);
        }

        public static int ResolveExactMaxSize(TextureImporter ti, string path)
        {
            int w = 0, h = 0;
            ti.GetSourceTextureWidthAndHeight(out w, out h);
            int maxDim = Mathf.Max(w, h);
            if (maxDim <= 0)
            {
                // Fallback by folder convention if metadata not yet cached
                if (path.IndexOf("/Pickups/", StringComparison.OrdinalIgnoreCase) >= 0)
                    return 32;
                return 64;
            }

            foreach (int candidate in ValidMaxSizes)
            {
                if (maxDim <= candidate)
                    return candidate;
            }
            return 2048;
        }

        public static bool ApplyPixelTextureSettings(TextureImporter ti, string path)
        {
            int targetSize = ResolveExactMaxSize(ti, path);
            bool changed = false;

            if (ti.textureType != TextureImporterType.Default)
            {
                ti.textureType = TextureImporterType.Default;
                changed = true;
            }
            if (ti.filterMode != FilterMode.Point)
            {
                ti.filterMode = FilterMode.Point;
                changed = true;
            }
            if (ti.mipmapEnabled)
            {
                ti.mipmapEnabled = false;
                changed = true;
            }
            if (ti.textureCompression != TextureImporterCompression.Uncompressed)
            {
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }
            if (ti.npotScale != TextureImporterNPOTScale.None)
            {
                ti.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }
            if (ti.maxTextureSize != targetSize)
            {
                ti.maxTextureSize = targetSize;
                changed = true;
            }
            if (ti.wrapMode != TextureWrapMode.Clamp && path.IndexOf("Env_FloorTile", StringComparison.OrdinalIgnoreCase) < 0)
            {
                ti.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }
            if (path.IndexOf("Env_FloorTile", StringComparison.OrdinalIgnoreCase) >= 0 && ti.wrapMode != TextureWrapMode.Repeat)
            {
                ti.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            var defSettings = ti.GetDefaultPlatformTextureSettings();
            if (defSettings.maxTextureSize != targetSize || defSettings.textureCompression != TextureImporterCompression.Uncompressed)
            {
                defSettings.maxTextureSize = targetSize;
                defSettings.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SetPlatformTextureSettings(defSettings);
                changed = true;
            }

            var standalone = ti.GetPlatformTextureSettings("Standalone");
            if (standalone.overridden)
            {
                standalone.overridden = false;
                ti.SetPlatformTextureSettings(standalone);
                changed = true;
            }

            return changed;
        }

        [MenuItem("Tools/Vehicular Combat/1. Configure Textures & URP Materials")]
        public static string ConfigureAllTexturesAndMaterials()
        {
            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialsRoot);

            int texCount = 0;
            var texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { ModelsRoot });
            foreach (string guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                ApplyPixelTextureSettings(ti, path);
                ti.SaveAndReimport();
                texCount++;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                throw new InvalidOperationException("Could not find 'Universal Render Pipeline/Lit' shader.");
            }

            int fbxCount = 0;
            int missingMats = 0;
            var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot });
            var reportLines = new List<string>();

            foreach (string guid in fbxGuids)
            {
                string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!fbxPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;

                string modelName = Path.GetFileNameWithoutExtension(fbxPath);
                string dir = Path.GetDirectoryName(fbxPath).Replace("\\", "/");
                string pngPath = $"{dir}/{modelName}.png";
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                if (tex == null)
                {
                    Debug.LogWarning($"[VehicularAssetPostprocessor] Texture not found for {fbxPath} at {pngPath}");
                    continue;
                }

                string matPath = $"{MaterialsRoot}/{modelName}_Mat.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(urpLit);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else if (mat.shader != urpLit)
                {
                    mat.shader = urpLit;
                }

                mat.name = $"{modelName}_Mat";
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.15f);
                mat.SetFloat("_Metallic", 0.0f);
                EditorUtility.SetDirty(mat);

                var mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (mi != null)
                {
                    mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                    mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    mi.optimizeGameObjects = false;
                    mi.importCameras = false;
                    mi.importLights = false;
                    mi.generateSecondaryUV = fbxPath.IndexOf("/Environment/", StringComparison.OrdinalIgnoreCase) >= 0;

                    // Remap every embedded material in this FBX to our clean URP/Lit material
                    var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
                    bool remappedAny = false;
                    foreach (var sub in subAssets)
                    {
                        if (sub is Material embeddedMat)
                        {
                            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(embeddedMat), mat);
                            remappedAny = true;
                        }
                    }

                    // Fallback remap by common Blockbench material names
                    if (!remappedAny)
                    {
                        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "material_0"), mat);
                        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), modelName), mat);
                    }

                    mi.SaveAndReimport();
                    fbxCount++;

                    // Verify imported renderers
                    var rootGo = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                    int rendererCount = 0;
                    if (rootGo != null)
                    {
                        foreach (var r in rootGo.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            rendererCount++;
                            var sm = r.sharedMaterial;
                            if (sm == null || sm.shader == null || sm.shader.name != "Universal Render Pipeline/Lit" || sm.GetTexture("_BaseMap") == null)
                                missingMats++;
                        }
                    }
                    reportLines.Add($"{modelName}: tex={tex.width}x{tex.height}, renderers={rendererCount}, mat={mat.name}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string summary = $"Configured {texCount} textures (Point, Exact MaxSize, Uncompressed, No MipMaps) and {fbxCount} FBX models with URP/Lit materials (missing/invalid materials: {missingMats}).\n" + string.Join("\n", reportLines);
            Debug.Log("[VehicularAssetPostprocessor] " + summary);
            return summary;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
            string name = Path.GetFileName(folderPath);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
