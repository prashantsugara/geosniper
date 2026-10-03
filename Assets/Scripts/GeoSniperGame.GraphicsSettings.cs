using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        bool showingGraphicsSettings;
        static readonly string[] GfxPresetNames = { "PERFORMANCE", "BALANCED", "HIGH" };
        static readonly string[] GfxPresetDescs = { "Shorter, simpler shadows and lighter rain effects.", "Moderate shadows and weather. Recommended starting point.", "Longer shadows, richer lighting and full rain effects." };

        void DrawGraphicsSettings(float w,float h)
        {
            CommandGUI.DrawPanel(new Rect(0,0,w,h),CommandGUI.ThemeCard,Color.clear,0);
            float width=Mathf.Min(640,w-32),x=(w-width)/2,y=Mathf.Max(8,(h-590)/2);
            var title=GUIStyleCache.Get(22,FontStyle.Bold,TextAnchor.UpperLeft,Color.white);
            var label=GUIStyleCache.Get(16,FontStyle.Normal,TextAnchor.UpperLeft,Color.white,true);
            GUI.Label(new Rect(x,y,width,32),"ANDROID GRAPHICS",title);
            GUI.Label(new Rect(x,y+42,width,46),"Choose lighter effects for smoother play, or richer lighting and weather.",label);
            for(int i=0;i<3;i++)
            {
                float row=y+106+i*104;
                if(CommandGUI.DrawButton(new Rect(x,row,width,48),GfxPresetNames[i]+(MobileGraphics.Selected==(MobileGraphicsPreset)i?"  [SELECTED]":""),MobileGraphics.Selected==(MobileGraphicsPreset)i,16))
                    MobileGraphics.Select((MobileGraphicsPreset)i);
                GUI.Label(new Rect(x,row+54,width,40),GfxPresetDescs[i],label);
            }
            int fps=MobileQualityPolicy.FrameRate(PlayerPrefs.GetInt("GeoSniper.TargetFPS",30));
            if(CommandGUI.DrawButton(new Rect(x,y+424,width,48),"FRAME RATE: "+fps+" FPS",true,16))
                MobileGraphics.SetFrameRate(fps==30?60:30);
            GUI.Label(new Rect(x,y+480,width,48),"30 FPS reduces rendering work. 60 FPS targets smoother motion when your phone can sustain it.",label);
            if(CommandGUI.DrawButton(new Rect(x,y+540,width,48),"BACK TO SETTINGS",true,16)) showingGraphicsSettings=false;
        }
    }
}
