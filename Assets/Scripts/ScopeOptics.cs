using UnityEngine;

namespace GeoSniper
{
    public static class ScopeOptics
    {
        public static float Magnification(float hipFov,float opticFov)
            => Mathf.Tan(Mathf.Clamp(hipFov,5,120)*Mathf.Deg2Rad*.5f)/Mathf.Tan(Mathf.Clamp(opticFov,1,120)*Mathf.Deg2Rad*.5f);
        public static float FovForMagnification(float hipFov,float magnification)
            => Mathf.Clamp(2*Mathf.Atan(Mathf.Tan(Mathf.Clamp(hipFov,5,120)*Mathf.Deg2Rad*.5f)/Mathf.Max(1,magnification))*Mathf.Rad2Deg,1,120);
        public static float ZoomFov(WeaponConfig config,float value)
        {
            float hip=config!=null?config.hipFOV:65;
            var levels=config!=null?config.zoomLevels:null;
            float low=levels!=null && levels.Length>0?levels[0]:14;
            float high=levels!=null && levels.Length>1?levels[levels.Length-1]:low*.5f;
            float start=Mathf.Max(1,Magnification(hip,low)),end=Mathf.Max(start,Magnification(hip,high));
            // Equal slider travel changes optical magnification, rather than raw FOV degrees.
            return FovForMagnification(hip,Mathf.Lerp(start,end,Mathf.Clamp01(value)));
        }
        public static float SmoothFov(float current,float target,float speed,float delta)
            => Mathf.Lerp(current,target,1-Mathf.Exp(-Mathf.Max(.1f,speed)*Mathf.Max(0,delta)));
        public static float AimScale(float fov)
            => Mathf.Clamp(Magnification(65,14)/Mathf.Max(1,Magnification(65,fov)),.15f,1.4f);
        public static float Diameter(float width,float height)=>Mathf.Min(width,height)*.96f;
        public static Color LensPixel(float x,float y)
        {
            float r=Mathf.Sqrt(x*x+y*y),a=Mathf.Atan2(y,x);
            if(r>=.995f)return new Color(.006f,.008f,.012f,1);
            if(r>.88f)
            {
                float highlight=Mathf.Pow(Mathf.Max(0,-x*.65f+y*.76f)/Mathf.Max(r,.001f),12);
                float machining=(Mathf.Sin(a*160)*.5f+.5f)*.025f;
                float bevel=Mathf.Exp(-Mathf.Pow((r-.894f)*280,2))*.22f+Mathf.Exp(-Mathf.Pow((r-.96f)*180,2))*.08f;
                float metal=.025f+machining+bevel+highlight*.12f;
                return new Color(metal*.9f,metal,metal*1.08f,1);
            }
            float vignette=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.88f,r));
            return new Color(.025f,.055f,.075f,vignette*vignette*.42f);
        }
    }
}
