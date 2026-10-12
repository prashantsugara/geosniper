using System;
using System.IO;
using System.Text;
using GeoSniper;
using UnityEngine;

public static class HudLayoutChecks
{
    public static void Run()
    {
        var report = new StringBuilder();
        Vector2[] sizes = { new Vector2(800,720),new Vector2(1200,720),new Vector2(1280,720),new Vector2(1600,720),new Vector2(800,1222) };
        foreach (var size in sizes)
        {
            foreach (bool notch in new[] { false,true })
            {
                Rect safe = notch ? new Rect(54,12,size.x-108,size.y-30) : new Rect(0,0,size.x,size.y);
                var panels = CombatHudPreferences.ScopeInstruments(size.x,size.y,safe);
                var aimingPoint = new Rect(size.x/2-60,size.y/2-60,120,120);
                var warning = new Rect(size.x/2-210,171,420,32);
                for (int i=0;i<panels.Length;i++)
                {
                    Require(Inside(panels[i],safe),"Scope reading escapes notch safe area");
                    Require(panels[i].width>=148 && panels[i].height>=52,"Scope readings shrink below readable dimensions");
                    Require(!panels[i].Overlaps(aimingPoint),"Scope reading covers the aiming point");
                    Require(!panels[i].Overlaps(warning),"Scope reading covers the rival lock warning");
                    for (int j=i+1;j<panels.Length;j++) Require(!panels[i].Overlaps(panels[j]),"Scope readings overlap each other");
                }
                float d=Mathf.Min(size.x,size.y)*.92f, scopeLeft=(size.x-d)/2;
                var zoomTouch=new Rect(Mathf.Max(scopeLeft-38,50)-35,(size.y-Mathf.Clamp(size.y*.46f,240,380))/2-25,90,Mathf.Clamp(size.y*.46f,240,380)+50);
                for(int i=0;i<panels.Length;i++) Require(!panels[i].Overlaps(zoomTouch),"Scope readings cover the zoom touch rail");
                var dock=CombatHudPreferences.UtilityDock(safe);
                Require(Inside(dock,safe),"Utility buttons escape safe area");
                Require(!dock.Overlaps(new Rect(safe.center.x-210,171,420,32)),"Utility buttons cover rival warning");
                foreach (Rect weapons in new[] { new Rect(safe.center.x-175,safe.yMax-48,350,40),new Rect(safe.x+30,safe.y+130,300,40),new Rect(safe.xMax-330,safe.yMax-160,300,40) })
                {
                    var status=CombatHudPreferences.PlayerStatus(safe,weapons);
                    Require(Inside(status,safe),"Health/ammo strip escapes safe area");
                    Require(!status.Overlaps(weapons),"Health/ammo strip overlaps the customized weapon dock");
                }
                report.AppendLine("PASS: scope essentials, aiming clearance, zoom rail, warning and custom weapon dock at "+size+(notch?" with notch":""));
            }
        }

        // Measure real font glyphs against the actual content widths, rather than trusting fixed labels.
        var font=Font.CreateDynamicFontFromOSFont("Arial",CombatHudPreferences.ScopeValueFont);
        try
        {
            foreach(string text in new[]{"1200 m","◀ -12.4 mil","RELOADING","30 / 30","+8.4 mil"})
            {
                int size=text.Length>16?16:CombatHudPreferences.ScopeValueFont;
                var style=new GUIStyle {font=font,fontSize=size,fontStyle=FontStyle.Bold};
                Require(style.CalcSize(new GUIContent(text)).x<=130,"Scope value clips at minimum panel width: "+text);
            }
            var dropLabel = new GUIStyle {font=font,fontSize=CombatHudPreferences.ScopeLabelFont,fontStyle=FontStyle.Bold};
            Require(dropLabel.CalcSize(new GUIContent("DROP 1.75 m")).x<=130,"Detailed drop distance label clips");
            foreach(string text in new[]{"LOW HP 35%","AMMO 20 / 20","RELOADING"})
            {
                var style=new GUIStyle {font=font,fontSize=CombatHudPreferences.StatusFont,fontStyle=FontStyle.Bold};
                Require(style.CalcSize(new GUIContent(text)).x<=130,"Essential status clips: "+text);
            }
            report.AppendLine("PASS: range, wind, drop, reload and critical-health typography fit their smallest real content widths");
        }
        finally {if(font!=null) UnityEngine.Object.DestroyImmediate(font);}
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/HudLayoutChecks.txt",report.ToString());
        WriteDiagnostic();
        Debug.Log(report.ToString());
    }

    static bool Inside(Rect item,Rect safe) => item.x>=safe.x && item.y>=safe.y && item.xMax<=safe.xMax+.01f && item.yMax<=safe.yMax+.01f;
    static void Require(bool success,string message) {if(!success)throw new Exception(message);}

    static void WriteDiagnostic()
    {
        var svg=new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1280\" height=\"720\" viewBox=\"0 0 1280 720\"><rect width=\"1280\" height=\"720\" fill=\"#050a10\"/><circle cx=\"640\" cy=\"360\" r=\"330\" fill=\"#273b48\" stroke=\"#61717a\" stroke-width=\"12\"/><path d=\"M350 360H930 M640 70V650\" stroke=\"#9eb3b6\"/><circle cx=\"640\" cy=\"360\" r=\"3\" fill=\"#e55145\"/>");
        var panels=CombatHudPreferences.ScopeInstruments(1280,720,new Rect(0,0,1280,720));
        string[] tags={"RANGE","WIND 1.8 m/s","BULLET DROP","AMMO · 8.5×"}, values={"216 m","◀ -0.7 mil","+1.7 mil","17 / 20"};
        for(int i=0;i<panels.Length;i++)
        {
            var rect=panels[i];
            svg.Append($"<rect x=\"{rect.x}\" y=\"{rect.y}\" width=\"{rect.width}\" height=\"{rect.height}\" fill=\"#11202c\"/><text x=\"{rect.x+10}\" y=\"{rect.y+17}\" fill=\"#24d1e6\" font-family=\"Arial\" font-size=\"12\" font-weight=\"bold\">{tags[i]}</text><text x=\"{rect.x+10}\" y=\"{rect.y+42}\" fill=\"white\" font-family=\"Arial\" font-size=\"18\" font-weight=\"bold\">{values[i]}</text>");
        }
        svg.Append("<text x=\"640\" y=\"690\" text-anchor=\"middle\" fill=\"#91a7b0\" font-family=\"Arial\" font-size=\"14\">Scope layout diagnostic · geometry preview</text></svg>");
        File.WriteAllText("Logs/hud-scope-layout.svg",svg.ToString());
    }
}
