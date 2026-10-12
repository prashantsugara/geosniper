using UnityEngine;

namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        public int MappedShopFronts { get; private set; }
        public int MappedShopKiosks { get; private set; }
        bool ShopDetailAvailable(Vector2 point)
        {
            Vector3 focus=transform.InverseTransformPoint(DetailFocus==Vector3.zero?transform.position:DetailFocus);
            return MappedShopFronts+MappedShopKiosks<(Application.isMobilePlatform?24:64) && (point-new Vector2(focus.x,focus.z)).sqrMagnitude<240*240;
        }
        public void BuildMappedShopFront(MapFeature building,Vector3 signPosition,Quaternion rotation,float width,Material color)
        {
            Vector2 point=new Vector2(signPosition.x,signPosition.z);
            if(!ShopDetailAvailable(point))return;
            var front=new GameObject("Mapped shop storefront");front.transform.SetParent(transform,false);
            front.transform.localPosition=new Vector3(point.x,Ground(point.x,point.y)+.05f,point.y);front.transform.localRotation=rotation;
            float span=Mathf.Clamp(width,2,7);
            var glass=Material(new Color(.08f,.2f,.25f),.8f,.35f);
            var frame=Material(new Color(.2f,.22f,.24f),.45f,.55f);
            var cream=Material(new Color(.88f,.84f,.72f));
            bool playable=Buildings.TryGetValue(building,out var host) && host.GetComponentInChildren<EnterableBuilding>()!=null;
            if(!playable)
            {
            DressingBox(front.transform,"Shop display glazing",new Vector3(-span*.16f,1.25f,-.14f),new Vector3(span*.6f,1.85f,.08f),glass,false);
            DressingBox(front.transform,"Shop door",new Vector3(span*.34f,1.15f,-.1f),new Vector3(span*.22f,2.2f,.1f),frame,false);
            DressingBox(front.transform,"Door glass",new Vector3(span*.34f,1.35f,-.025f),new Vector3(span*.16f,1.55f,.04f),glass,false);
            DressingBox(front.transform,"Door handle",new Vector3(span*.26f,1.05f,.025f),new Vector3(.045f,.3f,.04f),cream,false);
            }
            DressingBox(front.transform,"Shop canopy",new Vector3(0,2.35f,.38f),new Vector3(span+.2f,.14f,1.2f),color,false);
            for(int stripe=0;stripe<6;stripe++)DressingBox(front.transform,"Awning stripe",new Vector3(-span*.42f+stripe*span/5,2.43f,.38f),new Vector3(span/12,.025f,1.2f),cream,false);
            DressingBox(front.transform,"Awning valance",new Vector3(0,2.26f,.96f),new Vector3(span+.2f,.25f,.05f),color,false);
            if(!playable)
            {
            DressingBox(front.transform,"Display sill",new Vector3(-span*.16f,.3f,-.05f),new Vector3(span*.62f,.14f,.18f),cream,false);
            foreach(float bay in new[]{-.46f,-.16f,.14f})
                DressingBox(front.transform,"Display mullion",new Vector3(span*bay,1.25f,-.08f),new Vector3(.055f,1.9f,.09f),frame,false);
            DressingBox(front.transform,"Door transom",new Vector3(span*.34f,2.06f,-.025f),new Vector3(span*.22f,.075f,.12f),cream,false);
            }
            foreach(float edge in new[]{-.5f,.5f})
                DressingBox(front.transform,"Shop facade trim",new Vector3(span*edge,1.15f,-.09f),new Vector3(.12f,2.2f,.13f),cream,false);
            CombineDressing(front.transform);
            front.transform.SetParent(Buildings[building].transform,true);
            MappedShopFronts++;
        }
        public void BuildMappedShopKiosk(MapFeature shop,Material color,Material frame)
        {
            Vector2 point=SectorSignage.Center(shop.Points);
            if(!ShopDetailAvailable(point) || IsWaterAt(point.x,point.y))return;
            Vector2 direction=SectorSignage.NearestRoad(Features,point)-point;if(direction.sqrMagnitude<.01f)direction=Vector2.up;direction.Normalize();
            if(!StreetPlacement.Clear(Features,null,point,direction,3.4f,2.6f))return;
            Vector2 side=new Vector2(direction.y,-direction.x);
            foreach(float x in new[]{-1.7f,1.7f})foreach(float z in new[]{-1.3f,1.3f})
            {var corner=point+side*x+direction*z;if(IsWaterAt(corner.x,corner.y) || Mathf.Abs(Ground(corner.x,corner.y)-Ground(point.x,point.y))>.4f)return;}
            var kiosk=new GameObject("Mapped shop kiosk");kiosk.transform.SetParent(transform,false);
            kiosk.transform.localPosition=new Vector3(point.x,Ground(point.x,point.y)+.05f,point.y);kiosk.transform.localRotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
            var wall=Styled(new Color(.64f,.61f,.52f),false);
            DressingBox(kiosk.transform,"Kiosk back",new Vector3(0,1.25f,-1),new Vector3(3.2f,2.5f,.18f),wall,true);
            foreach(float x in new[]{-1.5f,1.5f})DressingBox(kiosk.transform,"Kiosk side",new Vector3(x,1.25f,0),new Vector3(.18f,2.5f,2.2f),wall,true);
            DressingBox(kiosk.transform,"Shop counter",new Vector3(0,.55f,.9f),new Vector3(3f,1.1f,.45f),frame,true);
            DressingBox(kiosk.transform,"Kiosk canopy",new Vector3(0,2.6f,.1f),new Vector3(3.5f,.16f,2.7f),color,false);
            string caption=(string.IsNullOrWhiteSpace(shop.Name)?MapFeatureStyle.Category(shop):shop.Name.Trim()+"\n"+MapFeatureStyle.Category(shop));
            WorldSign.Create(kiosk.transform,caption,new Vector3(0,2.18f,1.2f),Quaternion.identity,new Vector2(2.8f,.6f),color,false);
            CombineDressing(kiosk.transform);
            MappedShopKiosks++;
        }
    }
}
