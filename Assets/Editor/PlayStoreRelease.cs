using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GeoSniper;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Release checks and signed Android build helpers. Secrets are read from Unity or environment variables.
public sealed class PlayStoreRelease : IPreprocessBuildWithReport
{
    public int callbackOrder=>10000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if(report.summary.platform==BuildTarget.Android && (report.summary.options&BuildOptions.Development)==0)
            RequireReady();
    }

    public static List<string> CollectBlockers()
    {
        var blockers=new List<string>();
        try
        {
            var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(Application.dataPath)));
            const long recommendedFreeBytes=5L*1024L*1024L*1024L;
            if(drive.AvailableFreeSpace<recommendedFreeBytes)
                blockers.Add($"Free at least 5 GB on {drive.Name} before an Android release build (currently {drive.AvailableFreeSpace/(1024d*1024d*1024d):0.0} GB free).");
        }
        catch(Exception ex) { Debug.LogWarning("Could not inspect free disk space: "+ex.Message); }

        var config=JsonUtility.FromJson<ReleaseConfiguration>(File.ReadAllText("Assets/Resources/ReleaseConfiguration.json"));
        if(!ReleaseConfiguration.ValidHttps(config.privacyPolicyUrl)) blockers.Add("A public HTTPS privacy-policy URL is required.");
        if(string.IsNullOrWhiteSpace(config.publisherName)) blockers.Add("Set the public publisher/developer name.");
        if(!Regex.IsMatch(config.supportEmail??"",@"^[^\s@]+@[^\s@]+\.[^\s@]+$")) blockers.Add("Set a valid public support email.");
        if(!config.assetRightsVerified) blockers.Add("Verify commercial distribution rights for every shipped asset (Store/ASSET_RIGHTS.md).");
        if(!config.privacyAndDataSafetyReviewed) blockers.Add("Review the privacy policy and Play Console Data safety answers.");
        if(!config.deviceTestingCompleted) blockers.Add("Complete Android device, 16 KB page-size and Play pre-launch tests.");
        if(!config.audienceAndContentRatingReviewed) blockers.Add("Complete target-audience and content-rating declarations.");
        if(config.liveMapsEnabled && !config.mapProviderUsageReviewed) blockers.Add("Confirm production capacity, usage terms and attribution for map providers.");
        if(config.adsEnabled)
        {
            if(config.useTestAds) blockers.Add("Replace test ad configuration with production AdMob IDs.");
            if(string.IsNullOrWhiteSpace(config.rewardedAdUnitId) || string.IsNullOrWhiteSpace(config.interstitialAdUnitId)) blockers.Add("Configure rewarded and interstitial AdMob unit IDs.");
        }
        if((PlayerSettings.Android.targetArchitectures&AndroidArchitecture.ARM64)==0) blockers.Add("Enable ARM64.");
        if(!PlayerSettings.Android.useCustomKeystore || string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName) || !File.Exists(PlayerSettings.Android.keystoreName)) blockers.Add("Configure your upload signing keystore and alias. Never commit signing secrets.");
        if(string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName)) blockers.Add("Configure the upload key alias.");
        if(PlayerSettings.Android.bundleVersionCode<1) blockers.Add("Set a positive version code higher than the last uploaded release.");
        string idValue=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        if(!Regex.IsMatch(idValue??"",@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*){2,}$") || idValue.Contains("DefaultCompany"))
            blockers.Add("Set the final permanent Android package identifier (Unity currently resolves Android to '"+idValue+"'). Enable Override Default Package Name and use the package name already registered in Play Console.");
        string symbols=RemoveAndroidDebugSymbolsInternal();
        if(symbols.Contains("UNITY_MCP_READY") || symbols.Contains("UNITY_MCP_DEPS")) blockers.Add("Remove runtime MCP/debug bridge compilation symbols from Android.");
        if(!EditorBuildSettings.scenes.Any(s=>s.enabled && File.Exists(s.path))) blockers.Add("No valid enabled build scene.");
        if(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Branding/GeoSniperLogo.jpg")==null) blockers.Add("Game logo is missing.");
        if(!File.Exists("Store/icon-512.png")) blockers.Add("Export the 512px store icon (Geo Sniper > Release > Export Store Icon).");
        return blockers;
    }

    [MenuItem("Geo Sniper/Release/Check Play Store Readiness")]
    public static void Check()
    {
        Directory.CreateDirectory("Logs");
        var blockers=CollectBlockers();
        string report=DateTime.UtcNow.ToString("O")+"\n"+(blockers.Count==0?"READY FOR SIGNED BUILD; final Play review still required":"NOT READY TO PUBLISH")+"\n"+string.Join("\n",blockers.Select(s=>"BLOCKER: "+s));
        File.WriteAllText("Logs/PlayStoreReadiness.txt",report);
        Debug.Log(report);
    }

    [MenuItem("Geo Sniper/Release/Remove Android Debug Symbols")]
    public static void RemoveAndroidDebugSymbols()=>Debug.Log("Android scripting symbols cleaned: "+RemoveAndroidDebugSymbolsInternal());

    static string RemoveAndroidDebugSymbolsInternal()
    {
        string symbols=PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
        string cleaned=string.Join(";",symbols.Split(';').Where(s=>!string.Equals(s,"UNITY_MCP_READY",StringComparison.OrdinalIgnoreCase) && !s.StartsWith("UNITY_MCP_DEPS",StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s)));
        if(cleaned!=symbols)
        {
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android,cleaned);
            AssetDatabase.SaveAssets();
        }
        return cleaned;
    }

    [MenuItem("Geo Sniper/Release/Apply GameLogo As Android Icon")]
    public static void ApplyGameLogoAsAndroidIcon()
    {
        const string logoPath="Assets/Resources/Branding/GeoSniperLogo.jpg";
        var logo=AssetDatabase.LoadAssetAtPath<Texture2D>(logoPath);
        if(logo==null) throw new FileNotFoundException("Import the game logo first: "+logoPath);
        PlayerSettings.SetIcons(NamedBuildTarget.Android,new[]{logo},IconKind.Application);
        AssetDatabase.SaveAssets();
        Debug.Log("Applied GameLogo.jpg as the Android application icon.");
    }

    static void RequireReady()
    {
        Check();
        var blockers=CollectBlockers();
        if(blockers.Count>0) throw new BuildFailedException("Release blocked:\n"+string.Join("\n",blockers));
    }

    static void BuildAndroid(string outputPath,bool bundle)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Stop Play mode and existing builds first.");
        RequireReady();
        bool previousBundle=EditorUserBuildSettings.buildAppBundle;
        string oldStorePass=PlayerSettings.Android.keystorePass,oldAliasPass=PlayerSettings.Android.keyaliasPass;
        try
        {
            PlayerSettings.Android.keystorePass=Environment.GetEnvironmentVariable("GEOSNIPER_KEYSTORE_PASSWORD")??oldStorePass;
            PlayerSettings.Android.keyaliasPass=Environment.GetEnvironmentVariable("GEOSNIPER_KEY_ALIAS_PASSWORD")??oldAliasPass;
            if(string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass)) throw new BuildFailedException("Configure signing passwords in Unity or GEOSNIPER_KEYSTORE_PASSWORD and GEOSNIPER_KEY_ALIAS_PASSWORD.");
            EditorUserBuildSettings.buildAppBundle=bundle;
            Directory.CreateDirectory("Builds");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),target=BuildTarget.Android,options=BuildOptions.None,locationPathName=outputPath});
            if(report.summary.result!=BuildResult.Succeeded) throw new BuildFailedException($"Android build failed with result {report.summary.result} and {report.summary.totalErrors} errors. Inspect the Unity Editor.log for the first Gradle/IL2CPP error.");
        }
        finally { EditorUserBuildSettings.buildAppBundle=previousBundle; PlayerSettings.Android.keystorePass=oldStorePass; PlayerSettings.Android.keyaliasPass=oldAliasPass; }
    }

    [MenuItem("Geo Sniper/Release/Build Signed Play Store AAB")]
    public static void BuildBundle()=>BuildAndroid("Builds/GeoSniper-release.aab",true);

    [MenuItem("Geo Sniper/Release/Build Signed Release APK")]
    public static void BuildSignedApk()=>BuildAndroid("Builds/GeoSniper-release.apk",false);

    [MenuItem("Geo Sniper/Release/Export Store Icon")]
    public static void ExportIcon()
    {
        var source=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Branding/GeoSniperLogo.jpg");
        if(source==null) throw new FileNotFoundException("Import the game logo first.");
        var target=RenderTexture.GetTemporary(512,512,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active; var pixels=new Texture2D(512,512,TextureFormat.RGB24,false);
        try { Graphics.Blit(source,target); RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,512,512),0,0); pixels.Apply(); Directory.CreateDirectory("Store"); File.WriteAllBytes("Store/icon-512.png",pixels.EncodeToPNG()); }
        finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(pixels); }
    }
}
