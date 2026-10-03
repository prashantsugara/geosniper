using UnityEngine;

namespace GeoSniper
{
    public static class WeaponGeometry
    {
        public static float Length(int index) => index==0?1.25f:index==2?1.02f:1.08f;
        // One metre-based calibration for both the loadout showcase and the first-person model.
        public static bool Configure(Transform model,Transform holder,int index)
        {
            ImportedVisual.SanitizeWeaponModel(model);
            if (model.name.Contains("FPSSniperRifle"))
            {
                model.localRotation = Quaternion.identity;
            }
            else
            {
                model.localRotation=index==0?Quaternion.Euler(-90,180,0):index==1?Quaternion.Euler(0,90,0):Quaternion.Euler(0,180,0);
            }
            var bounds=GunBounds(model, holder);
            if(!Usable(bounds)) { model.gameObject.SetActive(false); Debug.LogError("Invalid weapon geometry: "+model.name); return false; }
            model.localScale*=Length(index)/bounds.size.z;
            bounds=GunBounds(model, holder);
            var gripMarker=AssetCalibration.Marker(model,"Grip");
            Renderer gripPart=null,barrelPart=null;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                string name=renderer.name.ToLowerInvariant();
                if(name=="trigger" || name=="trigger_trigger_0" || name=="trigger_sniper_0"
                    || (index==2 && name=="polysurface7_lambert1_0")) gripPart=renderer;
                if(name.StartsWith("barrel") || (index==2 && name=="pcylinder1_lambert1_0")) barrelPart=renderer;
            }
            float gripRatio=index==0?.42f:index==2?.43f:.44f;
            Vector3 grip=gripMarker!=null?holder.InverseTransformPoint(gripMarker.position)
                :gripPart!=null?ImportedVisual.RendererBounds(holder,gripPart).center + (index==2?Vector3.zero:new Vector3(0,-.018f,-.018f))
                :new Vector3(bounds.center.x,bounds.min.y+bounds.size.y*.32f,bounds.min.z+bounds.size.z*gripRatio);
            model.localPosition-=grip;
            bounds=GunBounds(model,holder);
            float boreY=barrelPart!=null?ImportedVisual.RendererBounds(holder,barrelPart).center.y:bounds.center.y;
            WeaponGripPose.Markers(holder,index==0?.30f:index==1?.27f:.24f,Mathf.Abs(bounds.min.z),
                new Vector3(bounds.center.x,boreY,bounds.max.z));
            return true;
        }
        static Bounds GunBounds(Transform model,Transform holder)
        {
            if (!model.name.Contains("FPSSniperRifle")) return ImportedVisual.LocalBounds(holder);
            bool found=false; Bounds bounds=default;
            // This named asset contains separate skinned arms. They do not define rifle dimensions.
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                var next=ImportedVisual.RendererBounds(holder,renderer);
                if(!found) {bounds=next;found=true;} else bounds.Encapsulate(next);
            }
            return found?bounds:ImportedVisual.LocalBounds(holder);
        }

        public static bool Usable(Bounds bounds)
        {
            var s=bounds.size; var c=bounds.center;
            return float.IsFinite(s.x)&&float.IsFinite(s.y)&&float.IsFinite(s.z)&&float.IsFinite(c.x)&&float.IsFinite(c.y)&&float.IsFinite(c.z)
                &&s.x>.00001f&&s.y>.00001f&&s.z>.00001f;
        }
    }
}
