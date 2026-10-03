using UnityEngine;

namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        readonly HitConfirmation confirmedHit=new HitConfirmation();
        void DrawConfirmedHit(float w,float h)
        {
            float remaining=confirmedHit.Remaining(Time.unscaledTime);
            if(remaining<=0) return;
            Color color=confirmedHit.Headshot?new Color(1f,.75f,.25f):Color.white;
            color.a=Mathf.Clamp01(remaining/.1f);
            float inner=confirmedHit.Headshot?12:9, outer=confirmedHit.Headshot?23:18;
            var center=new Vector2(w*.5f,h*.5f);
            for(int x=-1;x<=1;x+=2)
                for(int y=-1;y<=1;y+=2)
                    TacticalGUI.DrawLine(center+new Vector2(x*inner,y*inner),center+new Vector2(x*outer,y*outer),color,3);
            var style=new GUIStyle(GUI.skin.label){fontSize=confirmedHit.Headshot?18:15,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=color}};
            GUI.Label(new Rect(w/2-100,h/2+29,200,28),confirmedHit.Headshot?"HEADSHOT":"HIT",style);
        }
    }
}
