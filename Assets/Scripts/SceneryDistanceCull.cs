using UnityEngine;
namespace GeoSniper
{
    [RequireComponent(typeof(Renderer))]
    public sealed class SceneryDistanceCull:MonoBehaviour
    {
        public float MinDistance;
        public float MaxDistance=120f;
        Renderer targetRenderer;
        float nextCheck;
        void Awake(){targetRenderer=GetComponent<Renderer>();}
        void LateUpdate()
        {
            if(Time.unscaledTime<nextCheck)return;
            nextCheck=Time.unscaledTime+.25f;
            var camera=Camera.main;
            if(camera!=null)Refresh(camera);
        }
        public void Refresh(Camera camera)
        {
            if(camera==null)return;
            if(targetRenderer==null)targetRenderer=GetComponent<Renderer>();
            if(targetRenderer==null)return;
            float quality=MobileGraphics.Selected==MobileGraphicsPreset.Performance?.75f:
                MobileGraphics.Selected==MobileGraphicsPreset.High?1.25f:1f;
            float multiplier=Application.isMobilePlatform?quality:1.7f;
            float min=MinDistance*multiplier,max=MaxDistance*multiplier;
            float squared=targetRenderer.bounds.SqrDistance(camera.transform.position);
            targetRenderer.enabled=squared>=min*min && squared<max*max;
        }
    }
}
