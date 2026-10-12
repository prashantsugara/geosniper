using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        void BuildDecorativeSkyline()
        {
            // Beyond the playable 640m sector: scenery cannot block an objective or road.
            var source=GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.SetActive(false);
            var cube=source.GetComponent<MeshFilter>().sharedMesh;
            var random=new System.Random(7319);
            for(int district=0;district<4;district++)
            {
                var pieces=new List<CombineInstance>();
                for(int index=0;index<18;index++)
                {
                    // Separate clustered districts and depth layers, rather than a uniform ring.
                    float angle=(district*90+32+random.Next(-26,27))*Mathf.Deg2Rad;
                    float radius=480+(index%3)*95+random.Next(0,65);
                    float x=Mathf.Cos(angle)*radius,z=Mathf.Sin(angle)*radius;
                    // Skip beyond-sector water; no invented islands or floating towers.
                    if(WaterAt(transform.TransformPoint(new Vector3(x,0,z))))continue;
                    float height=index%7==0?110+random.Next(0,65):index%3==0?50+random.Next(0,45):18+random.Next(0,35),width=12+random.Next(0,28);
                    bool setback=index%3==0;float bodyHeight=setback?height*.72f:height;
                    float ground=Ground(x,z);
                    var skin=Instantiate(cube);skin.name="Skyline physical-scale windows";
                    var uv=skin.uv;
                    for(int vertex=0;vertex<uv.Length;vertex++)uv[vertex]=Vector2.Scale(uv[vertex],new Vector2(width/8f,bodyHeight/8f))+new Vector2(index*3,0);
                    skin.uv=uv;meshes.Add(skin);
                    pieces.Add(new CombineInstance{mesh=skin,transform=Matrix4x4.TRS(new Vector3(x,ground+bodyHeight*.5f,z),Quaternion.Euler(0,district*90,0),new Vector3(width,bodyHeight,width*.8f))});
                    if(setback)
                    {
                        var upper=Instantiate(cube);var upperUV=upper.uv;
                        for(int vertex=0;vertex<upperUV.Length;vertex++)upperUV[vertex]=Vector2.Scale(upperUV[vertex],new Vector2(width*.68f/8,(height-bodyHeight)/8))+new Vector2(index*3,bodyHeight/8);
                        upper.uv=upperUV;meshes.Add(upper);
                        pieces.Add(new CombineInstance{mesh=upper,transform=Matrix4x4.TRS(new Vector3(x,ground+bodyHeight+(height-bodyHeight)*.5f,z),Quaternion.Euler(0,district*90,0),new Vector3(width*.68f,height-bodyHeight,width*.54f))});
                        pieces.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(new Vector3(x,ground+height+1.5f,z),Quaternion.Euler(0,district*90,0),new Vector3(width*.32f,3,width*.28f))});
                    }
                }
                if(pieces.Count==0)continue;
                var mesh=new Mesh{name="Fictional skyline district"};mesh.CombineMeshes(pieces.ToArray(),true,true);meshes.Add(mesh);
                var style=new MapFeature{Kind="building",Height=48};
                style.Details["facade_material"]=district%2==0?"concrete":"metal";
                style.Details["street_study"]="true";
                style.Details["scenery_only"]="true";
                var material=FacadeStyled(style,new Color(.23f+district*.018f,.27f+district*.018f,.31f+district*.018f),true);
                material.SetFloat("_WindowLightScale",.7f);
                var obj=new GameObject("Decorative distant city / no gameplay collision");obj.transform.SetParent(transform,false);
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            Destroy(source);
        }
    }
}
