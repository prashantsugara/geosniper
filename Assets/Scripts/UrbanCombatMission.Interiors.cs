using UnityEngine;
namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        void DrawInteriorGuide(float w,float h,Rect safe)
        {
            if(player==null || player.Scoped || isPvPDuel || StoryFieldMode && stageMode)return;
            EnterableBuilding nearest=null;float distance=float.MaxValue;
            foreach(var interior in EnterableBuilding.All)
                {
                    float d=Vector3.Distance(player.transform.position,interior.transform.TransformPoint(interior.Entrance));
                    if(d<distance){distance=d;nearest=interior;}
                }
            if(nearest==null)return;
            float top=CombatHudPreferences.UtilityDock(safe).yMax+(stageMode?10:60);
            if(CommandGUI.DrawButton(new Rect(safe.x+14,top,220,36),"FIND OPEN BUILDING",false,11))
                SetWaypoint(nearest.transform.TransformPoint(nearest.Entrance+nearest.EntranceOutward*.8f),"OPEN // ENTER");
            if(distance<18 && cameraView!=null)
            {
                var screen=cameraView.WorldToViewportPoint(nearest.transform.TransformPoint(nearest.Entrance+Vector3.up*2.6f));
                if(screen.z>0 && screen.x>.05f && screen.x<.85f && screen.y>.15f && screen.y<.85f)
                    DrawTacticalScreenBadge(new Vector2(screen.x*w,(1-screen.y)*h),"OPEN // ENTER "+Mathf.RoundToInt(distance)+"m",new Color(.15f,.9f,.45f),w,h);
            }
        }
    }
}
