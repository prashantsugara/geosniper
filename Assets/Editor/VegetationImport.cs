using UnityEditor;
public sealed class VegetationImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if(!assetPath.Contains("/Models/Vegetation/"))return;
        var model=(ModelImporter)assetImporter;
        model.isReadable=true;model.importAnimation=false;model.importCameras=false;model.importLights=false;
    }
    void OnPreprocessTexture()
    {
        if(!assetPath.Contains("/Models/Vegetation/"))return;
        var texture=(TextureImporter)assetImporter;
        texture.maxTextureSize=1024;texture.alphaIsTransparency=true;texture.mipmapEnabled=true;
    }
}
