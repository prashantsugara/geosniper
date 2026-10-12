using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public enum GameButtonTone { Steel, Amber, Cyan, Danger, Disabled }
    public static class GameButtonChrome
    {
        static readonly Dictionary<int,GUIStyle> styles=new Dictionary<int,GUIStyle>();
        static readonly Dictionary<int,Texture2D> textures=new Dictionary<int,Texture2D>();
        public static GUIStyle Style(GameButtonTone tone,int fontSize,bool icon=false)
        {
            int key=(int)tone*1000+fontSize*2+(icon?1:0);
            if(styles.TryGetValue(key,out var style))return style;
            Color ink=tone==GameButtonTone.Amber?new Color(.13f,.08f,.02f):tone==GameButtonTone.Disabled?new Color(.57f,.62f,.66f):new Color(.96f,.98f,1f);
            style=new GUIStyle
            {
                border=new RectOffset(12,12,12,12),padding=new RectOffset(icon?34:10,10,3,6),
                alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=fontSize,wordWrap=true,richText=false,
                normal={background=Texture(tone,0),textColor=ink},hover={background=Texture(tone,1),textColor=ink},
                active={background=Texture(tone,2),textColor=ink},focused={background=Texture(tone,1),textColor=ink},
                onNormal={background=Texture(tone,1),textColor=ink},onHover={background=Texture(tone,1),textColor=ink},onActive={background=Texture(tone,2),textColor=ink}
            };
            styles.Add(key,style);return style;
        }
        static Texture2D Texture(GameButtonTone tone,int state)
        {
            int key=(int)tone*3+state;if(textures.TryGetValue(key,out var texture))return texture;
            const int width=128,height=64;var pixels=new Color[width*height];
            Color top,bottom,rim;
            switch(tone)
            {
                case GameButtonTone.Amber:top=new Color(1f,.77f,.32f);bottom=new Color(.83f,.39f,.055f);rim=new Color(1f,.88f,.55f);break;
                case GameButtonTone.Cyan:top=new Color(.12f,.65f,.79f);bottom=new Color(.025f,.28f,.43f);rim=new Color(.4f,.9f,1f);break;
                case GameButtonTone.Danger:top=new Color(.55f,.22f,.24f);bottom=new Color(.24f,.08f,.12f);rim=new Color(.87f,.43f,.42f);break;
                case GameButtonTone.Disabled:top=new Color(.18f,.21f,.24f);bottom=new Color(.1f,.12f,.14f);rim=new Color(.29f,.33f,.37f);break;
                default:top=new Color(.28f,.35f,.42f);bottom=new Color(.10f,.15f,.21f);rim=new Color(.46f,.56f,.64f);break;
            }
            if(state==1){top=Color.Lerp(top,Color.white,.12f);rim=Color.Lerp(rim,Color.white,.25f);}
            if(state==2){top*=.72f;bottom*=.88f;rim*=.72f;}
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                // Rounded signed-distance mask; the lower rail and offset shadow give the face depth.
                float px=x+.5f,py=height-y-.5f;
                float distance=RoundedDistance(px,py,64,30,62,27,9);
                float shadow=RoundedDistance(px,py,64,33,62,27,9);
                Color color=new Color(.015f,.025f,.04f,Mathf.Clamp01(1-shadow)*.75f);
                if(distance<1)
                {
                    float gradient=Mathf.Clamp01((py-4)/51);
                    Color face=Color.Lerp(top,bottom,gradient);
                    if(distance>-1.4f)face=rim;
                    else if(distance>-3 && py<30)face=Color.Lerp(face,Color.white,state==2?.03f:.25f);
                    else if(py>50)face=Color.Lerp(face,Color.black,state==2?.15f:.3f);
                    else if(py<15)face=Color.Lerp(face,Color.white,state==2?0:.055f);
                    face.a=Mathf.Clamp01(1-distance);color=face;
                }
                pixels[y*width+x]=color;
            }
            texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Button "+tone+" "+state,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            texture.SetPixels(pixels);texture.Apply(false,true);textures.Add(key,texture);return texture;
        }
        static float RoundedDistance(float x,float y,float cx,float cy,float hx,float hy,float radius)
        {
            float qx=Mathf.Abs(x-cx)-hx+radius,qy=Mathf.Abs(y-cy)-hy+radius;
            return new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
        }
        public static bool Draw(Rect rect,string text,GameButtonTone tone=GameButtonTone.Steel,int fontSize=16)
        {
            var skin=Style(GUI.enabled?tone:GameButtonTone.Disabled,fontSize);
            return GUI.Button(rect,text,skin);
        }
        public static bool Surface(Rect rect,bool selected=false)
            => GUI.Button(rect,GUIContent.none,Style(GUI.enabled?(selected?GameButtonTone.Cyan:GameButtonTone.Steel):GameButtonTone.Disabled,12));
    }
}

