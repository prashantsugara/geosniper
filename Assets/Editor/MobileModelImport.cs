using UnityEditor;

// These textures are shared by many instances; retaining CPU copies wastes RAM.
public sealed class MobileModelImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/Models/")) return;
        var importer = assetImporter as TextureImporter;
        if (importer == null) return;

        if(assetPath.EndsWith("/Barrier_normal.png")) importer.textureType = TextureImporterType.NormalMap;
        if(assetPath.Contains("/Weapons/Authored/"))
        {
            importer.textureType=assetPath.EndsWith("_Normal.png")?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=assetPath.EndsWith("_BaseColor.png");
            importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=false;
        }
        if (importer.isReadable) importer.isReadable = false;
        if (!importer.mipmapEnabled) importer.mipmapEnabled = true;

        var android = importer.GetPlatformTextureSettings("Android");
        if (!android.overridden || android.maxTextureSize != 1024 || android.format != TextureImporterFormat.ASTC_6x6)
        {
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
        }
    }
    void OnPreprocessModel()
    {
        if(!assetPath.StartsWith("Assets/Resources/Models/")) return;
        var importer = assetImporter as ModelImporter;
        if (importer == null) return;

        bool readable = NeedsRuntimeMeshData(assetPath);
        if (importer.isReadable != readable) importer.isReadable = readable;
        if (importer.addCollider) importer.addCollider = false;

        if(assetPath.StartsWith("Assets/Resources/Models/Enemies/army_character_"))
        {
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false;
        }
    }
    internal static bool NeedsRuntimeMeshData(string path)
    {
        // Player builds discard non-readable mesh data. Runtime MeshCollider cooking
        // needs it for these small, shared location meshes as well as vegetation.
        return path.StartsWith("Assets/Resources/Models/Enemies/")
            || path.StartsWith("Assets/Resources/Models/Civilians/")
            || path.StartsWith("Assets/Resources/Models/Vegetation/")
            || path.StartsWith("Assets/Resources/Models/Stronghold/")
            || path=="Assets/Resources/Models/LevelSites/BrickApartments.fbx"
            || path=="Assets/Resources/Models/LevelSites/MilitaryTent.fbx"
            || path=="Assets/Resources/Models/LevelSites/Warehouse.fbx"
            || path=="Assets/Resources/Models/LevelSites/ShopShelf.fbx"
            || path=="Assets/Resources/Models/LevelSites/ShopFridge.fbx";
    }
    [MenuItem("Geo Sniper/Optimize Mobile Model Imports")]
    static void Reimport()
    {
        foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Models"}))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),ImportAssetOptions.ForceUpdate);
    }
}
