using UnityEngine;
namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        bool streetStudyToolsOpen;
        bool StreetStudyActive=>SectorWorld.ActiveFeatures!=null && SectorWorld.ActiveFeatures.Exists(f=>MapFeatureStyle.Text(f,"street_study")=="true");
        void DrawStreetStudyTools(Rect safe,float width,float height)
        {
            float x=safe.x+14,y=safe.y+62;
            if(TacticalGUI.DrawButton(new Rect(x,y,98,30),streetStudyToolsOpen?"TOOLS −":"TOOLS +",streetStudyToolsOpen,11))streetStudyToolsOpen=!streetStudyToolsOpen;
            if(streetStudyToolsOpen)
            {
                if(TacticalGUI.DrawButton(new Rect(x,y+36,94,30),"FLIR",weapon!=null && weapon.ThermalMode,11))ToggleThermalMode();
                if(TacticalGUI.DrawButton(new Rect(x+100,y+36,94,30),"UAV",uavTimer>0,11))TriggerUAVRecon();
                if(TacticalGUI.DrawButton(new Rect(x,y+72,94,30),"WEATHER",false,11))CycleEnvironment();
                if(TacticalGUI.DrawButton(new Rect(x+100,y+72,94,30),"RECON",player.BinocularsMode,11))player.ToggleBinoculars();
                if(mapClosedCooldown<=0 && TacticalGUI.DrawButton(new Rect(x,y+108,94,30),"ABORT",false,11))ExitToLevelMap();
            }
            if(uavTimer>0)DrawUAVMarkers(width,height);
        }
    }
}
