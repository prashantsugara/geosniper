using UnityEngine;
using UnityEditor;
using System.IO;

namespace GeoSniper.Editor
{
    public class MixamoEnemyPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var importer = assetImporter as TextureImporter;
            if (importer == null) return;
            string path = assetPath.Replace('\\', '/').ToLowerInvariant();
            if (path.Contains("resources/models/enemies/textures/") && path.Contains("normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
        }

        void OnPreprocessModel()
        {
            var importer = assetImporter as ModelImporter;
            if (importer == null) return;
            string path = assetPath.Replace('\\', '/').ToLowerInvariant();

            if (path.Contains("resources/models/enemies/animations/"))
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.importConstraints = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
            }
            else if (path.EndsWith("/swat.fbx") || path.EndsWith("/ch35.fbx"))
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
            }
        }

        void OnPreprocessAnimation()
        {
            var importer = assetImporter as ModelImporter;
            if (importer == null) return;
            string path = assetPath.Replace('\\', '/').ToLowerInvariant();

            if (path.Contains("resources/models/enemies/animations/"))
            {
                string fileName = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
                string clipName = "anim";
                bool loop = false;

                if (fileName.Contains("crouch")) { clipName = "crouch_idle"; loop = true; }
                else if (fileName.Contains("run")) { clipName = "run"; loop = true; }
                else if (fileName.Contains("idle")) { clipName = "idle"; loop = true; }
                else if (fileName.Contains("walk")) { clipName = "walk"; loop = true; }
                else if (fileName.Contains("fire")) { clipName = "fire"; loop = false; }
                else if (fileName.Contains("hit")) { clipName = "hit"; loop = false; }
                else if (fileName.Contains("death")) { clipName = "death"; loop = false; }

                var defaultClips = importer.defaultClipAnimations;
                if (defaultClips != null && defaultClips.Length > 0)
                {
                    var clip = defaultClips[0];
                    clip.name = clipName;
                    clip.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
                    clip.loopTime = loop;
                    clip.lockRootPositionXZ = true;
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.keepOriginalPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    importer.clipAnimations = new[] { clip };
                }
            }
        }

        [MenuItem("GeoSniper/Setup Mixamo SWAT Enemies")]
        public static void SetupAll()
        {
            Debug.Log("[MixamoEnemyPostprocessor] Configuring SWAT & Mixamo models and mocap animations...");
            string baseDir = "Assets/Resources/Models/Enemies";
            
            // Reimport swat & ch35 base models
            foreach (var charFile in new[] { "/swat.fbx", "/ch35.fbx" })
            {
                string charPath = baseDir + charFile;
                if (File.Exists(charPath))
                {
                    var imp = AssetImporter.GetAtPath(charPath) as ModelImporter;
                    if (imp != null)
                    {
                        imp.animationType = ModelImporterAnimationType.Legacy;
                        imp.materialImportMode = ModelImporterMaterialImportMode.None;
                        imp.SaveAndReimport();
                        Debug.Log("[MixamoEnemyPostprocessor] Configured " + charFile);
                    }
                }
            }

            // Reimport animation clips
            string animsDir = baseDir + "/Animations";
            if (Directory.Exists(animsDir))
            {
                string[] files = Directory.GetFiles(animsDir, "*.fbx");
                foreach (var f in files)
                {
                    string unityPath = f.Replace('\\', '/');
                    var imp = AssetImporter.GetAtPath(unityPath) as ModelImporter;
                    if (imp != null)
                    {
                        imp.animationType = ModelImporterAnimationType.Legacy;
                        imp.importAnimation = true;
                        imp.materialImportMode = ModelImporterMaterialImportMode.None;
                        
                        string fileName = Path.GetFileNameWithoutExtension(unityPath).ToLowerInvariant();
                        string clipName = fileName;
                        bool loop = false;
                        if (fileName.Contains("crouch")) { clipName = "crouch_idle"; loop = true; }
                        else if (fileName.Contains("run")) { clipName = "run"; loop = true; }
                        else if (fileName.Contains("idle")) { clipName = "idle"; loop = true; }
                        else if (fileName.Contains("walk")) { clipName = "walk"; loop = true; }
                        else if (fileName.Contains("fire")) { clipName = "fire"; loop = false; }
                        else if (fileName.Contains("hit")) { clipName = "hit"; loop = false; }
                        else if (fileName.Contains("death")) { clipName = "death"; loop = false; }

                        var defaultClips = imp.defaultClipAnimations;
                        if (defaultClips != null && defaultClips.Length > 0)
                        {
                            var clip = defaultClips[0];
                            clip.name = clipName;
                            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
                            clip.loopTime = loop;
                            clip.lockRootPositionXZ = true;
                            clip.lockRootRotation = true;
                            clip.lockRootHeightY = true;
                            imp.clipAnimations = new[] { clip };
                        }
                        imp.SaveAndReimport();
                        Debug.Log("[MixamoEnemyPostprocessor] Configured " + fileName + " -> clip " + clipName);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MixamoEnemyPostprocessor] All SWAT assets setup complete!");
        }
    }
}
