using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CaptureVisualReview
{
    [MenuItem("Geo Sniper/Visual Review/Capture Running Game")]
    public static void Capture()
    {
        if(!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Enter Play mode and load a GPS mission before capturing the visual review.");
            return;
        }
        string folder=Path.GetFullPath("Logs/VisualReview");
        Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"Gameplay-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log("Gameplay capture requested (saved after frame end): "+path);
    }
}
