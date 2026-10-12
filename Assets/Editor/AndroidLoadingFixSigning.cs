using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;

// Use credentials already entered in this project's Unity publishing settings.
// Passwords stay in memory and the child signer environment, never in files/logs.
[InitializeOnLoad]
public static class AndroidLoadingFixSigning
{
    const string Request=".utmp/sign-loading-fix.request",Result="output/phone-logs/signing-result.txt";
    static AndroidLoadingFixSigning(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        File.Delete(Request);
        Directory.CreateDirectory("output/phone-logs");
        string storePass=Environment.GetEnvironmentVariable("GEOSNIPER_KEYSTORE_PASSWORD")??PlayerSettings.Android.keystorePass;
        string aliasPass=Environment.GetEnvironmentVariable("GEOSNIPER_KEY_ALIAS_PASSWORD")??PlayerSettings.Android.keyaliasPass;
        if(string.IsNullOrEmpty(storePass) || string.IsNullOrEmpty(aliasPass))
        {File.WriteAllText(Result,"NEEDS_UNITY_SIGNING_PASSWORDS");return;}
        try
        {
            string android=Path.Combine(EditorApplication.applicationContentsPath,"PlaybackEngines/AndroidPlayer");
            string signer=Path.Combine(android,"SDK/build-tools/36.0.0/lib/apksigner.jar");
            var start=new ProcessStartInfo(Path.Combine(android,"OpenJDK/bin/java.exe"))
            {UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
            start.Arguments="-jar \""+signer+"\" sign --ks \""+PlayerSettings.Android.keystoreName+
                "\" --ks-key-alias \""+PlayerSettings.Android.keyaliasName+
                "\" --ks-pass env:GEO_LOADING_STORE_PASS --key-pass env:GEO_LOADING_ALIAS_PASS --out \""+
                Path.GetFullPath("Builds/GeoSniper-loading-fix-signed.apk")+"\" \""+
                Path.GetFullPath("Builds/GeoSniper-loading-fix.apk")+"\"";
            start.EnvironmentVariables["GEO_LOADING_STORE_PASS"]=storePass;
            start.EnvironmentVariables["GEO_LOADING_ALIAS_PASS"]=aliasPass;
            using(var process=Process.Start(start))
            {
                var output=process.StandardOutput.ReadToEndAsync();
                var error=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(30000)){process.Kill();throw new Exception("Signing timed out");}
                // Avoid persisting signer diagnostics that might contain credential details.
                File.WriteAllText(Result,process.ExitCode==0?"SIGNED":"SIGNING_FAILED; check Unity publishing settings");
            }
        }
        catch(Exception){File.WriteAllText(Result,"SIGNING_UNAVAILABLE; check Unity publishing settings");}
    }
}
