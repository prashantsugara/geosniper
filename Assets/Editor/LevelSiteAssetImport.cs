using UnityEditor;
public sealed class LevelSiteAssetImport:AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.Contains("/Models/LevelSites/"))return;
        var importer=(TextureImporter)assetImporter;importer.maxTextureSize=1024;importer.mipmapEnabled=true;
        importer.textureCompression=TextureImporterCompression.Compressed;
        if(assetPath.EndsWith("_normal.png"))importer.textureType=TextureImporterType.NormalMap;
        if(assetPath.EndsWith("_metallic.png") || assetPath.EndsWith("_orm.png"))importer.sRGBTexture=false;
        var mobile=importer.GetPlatformTextureSettings("Android");mobile.overridden=true;mobile.maxTextureSize=1024;mobile.format=TextureImporterFormat.ASTC_6x6;importer.SetPlatformTextureSettings(mobile);
    }
    void OnPreprocessModel()
    {
        if(!assetPath.Contains("/Models/LevelSites/"))return;
        var importer=(ModelImporter)assetImporter;importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=MobileModelImport.NeedsRuntimeMeshData(assetPath);
    }
}
