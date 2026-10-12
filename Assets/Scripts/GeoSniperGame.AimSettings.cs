using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        bool showingAimSettings;
        Vector2 aimSettingsScroll;
        void DrawAimSettings(float w, float h)
        {
            CommandGUI.DrawPanel(new Rect(0,0,w,h),CommandGUI.ThemeCard,Color.clear,0);
            float width=Mathf.Min(640,w-32), x=(w-width)/2;
            var title=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
            var label=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true,normal={textColor=Color.white}};
            GUI.Label(new Rect(x,8,width,34),"TOUCH & GYRO AIMING",title);
            aimSettingsScroll=GUI.BeginScrollView(new Rect(x,50,width,Mathf.Max(100,h-116)),aimSettingsScroll,new Rect(0,0,width-20,600));
            float rowWidth=width-24;
            GUI.Label(new Rect(0,0,rowWidth,48),"Swipe to aim. Enable gyro to fine-tune scoped shots by moving your phone.",label);
            if(CommandGUI.DrawButton(new Rect(0,64,rowWidth,48),"CUSTOMIZE TOUCH CONTROLS",false,14)) BeginTouchControlLayout();
            AimSlider(new Rect(0,128,rowWidth,64),"LOOK SPEED","GeoSniper.LookSens",1f,.3f,2.5f,label);
            AimSlider(new Rect(0,204,rowWidth,64),"SCOPE SPEED","GeoSniper.ScopeSens",PlayerPrefs.GetFloat("GeoSniper.LookSens",1),.3f,2.5f,label);
            bool enabled=PlayerPrefs.GetInt("GeoSniper.GyroAim",0)==1;
            bool previous=GUI.enabled;
            GUI.enabled=previous && AndroidGyroAim.Available;
            if(CommandGUI.DrawButton(new Rect(0,282,rowWidth,48),AndroidGyroAim.Available?(enabled?"SCOPED GYRO: ON":"SCOPED GYRO: OFF"):"GYRO UNAVAILABLE ON THIS DEVICE",enabled,14))
            { enabled=!enabled; PlayerPrefs.SetInt("GeoSniper.GyroAim",enabled?1:0); PlayerPrefs.Save(); }
            GUI.enabled=previous && AndroidGyroAim.Available && enabled;
            AimSlider(new Rect(0,350,rowWidth,64),"GYRO SPEED","GeoSniper.GyroSpeed",1f,.2f,3f,label);
            GUI.enabled=previous;
            bool inverted=PlayerPrefs.GetInt("GeoSniper.InvertAimY",0)==1;
            if(CommandGUI.DrawButton(new Rect(0,438,rowWidth,48),inverted?"VERTICAL AIM: INVERTED":"VERTICAL AIM: NORMAL",inverted,14))
            { PlayerPrefs.SetInt("GeoSniper.InvertAimY",inverted?0:1); PlayerPrefs.Save(); }
            bool haptics=AndroidHitHaptics.Enabled;
            GUI.enabled=previous && Application.platform==RuntimePlatform.Android;
            if(CommandGUI.DrawButton(new Rect(0,506,rowWidth,48),haptics?"HIT HAPTICS: ON":"HIT HAPTICS: OFF",haptics,14))
            { PlayerPrefs.SetInt("GeoSniper.HitHaptics",haptics?0:1); PlayerPrefs.Save(); }
            GUI.enabled=previous;
            GUI.EndScrollView();
            if(CommandGUI.DrawButton(new Rect(x,h-56,width,48),"SAVE & BACK",true,16))
            { PlayerPrefs.Save(); showingAimSettings=false; }
        }
        static void AimSlider(Rect row,string label,string key,float fallback,float min,float max,GUIStyle style)
        {
            float value=Mathf.Clamp(PlayerPrefs.GetFloat(key,fallback),min,max);
            GUI.Label(new Rect(row.x,row.y,row.width,26),label+"  "+value.ToString("0.0"),style);
            float next=CommandGUI.HorizontalSlider(new Rect(row.x,row.y+32,row.width,28),value,min,max);
            if(next!=value) PlayerPrefs.SetFloat(key,next);
        }
    }
}
