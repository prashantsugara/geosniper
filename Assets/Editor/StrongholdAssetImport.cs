using UnityEditor;
public sealed class StrongholdAssetImport:AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if(!assetPath.StartsWith("Assets/Resources/Models/Stronghold/"))return;
        var importer=(ModelImporter)assetImporter;importer.isReadable=true;importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
    }
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/Models/Stronghold/"))return;
        var importer=(TextureImporter)assetImporter;importer.maxTextureSize=1024;importer.mipmapEnabled=true;
        if(assetPath.Contains("_normal"))importer.textureType=TextureImporterType.NormalMap;
        if(assetPath.Contains("_orm") || assetPath.Contains("_metallic") || assetPath.Contains("_occlusion"))importer.sRGBTexture=false;
    }
}
