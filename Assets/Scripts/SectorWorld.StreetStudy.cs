using UnityEngine;
using System.Collections.Generic;
namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        Texture2D streetStudyRoadTexture;
        // Apply the reviewed pilot to one short street in the initial sector.
        // Streaming sectors retain their existing style until the broader art/performance gate.
        public static bool TryStyleRepresentativeStreet(List<MapFeature> features,Vector2 focus,out Vector2 start,out Vector2 direction)
        {
            start=focus;direction=Vector2.up;
            if(features.Exists(f=>MapFeatureStyle.Text(f,"street_study")=="true"))return false;
            float best=float.MaxValue;Vector2 segmentA=default,segmentB=default;
            var buildingCenters=new List<Vector2>();
            foreach(var feature in features)if(feature.Kind=="building" && !WorldDetailPlanner.IsBuildingPart(feature) && EnterableBuilding.FitLobby(feature,out _,out _,out _))buildingCenters.Add(SectorSignage.Center(feature.Points));
            foreach(var road in features)
            {
                if(road.Kind!="road" || MapFeatureStyle.RoadWidth(road)<6)continue;
                string bridge=MapFeatureStyle.Text(road,"is_bridge");if(bridge=="yes" || bridge=="true" || bridge=="1")continue;
                for(int i=1;i<road.Points.Count;i++)
                {
                    var a=road.Points[i-1];var b=road.Points[i];var delta=b-a;
                    if(delta.sqrMagnitude<25*25)continue;
                    var point=a+delta*Mathf.Clamp01(Vector2.Dot(focus-a,delta)/delta.sqrMagnitude);
                    bool authored=MapFeatureStyle.Text(road,"study_candidate")=="true";
                    if(authored)point=(a+b)*.5f;
                    float distance=(point-focus).sqrMagnitude;
                    if(distance>180*180)continue;
                    int frontage=0;
                    foreach(var center in buildingCenters)
                    {
                        var nearest=a+delta*Mathf.Clamp01(Vector2.Dot(center-a,delta)/delta.sqrMagnitude);
                        if((center-nearest).sqrMagnitude<=42*42 && (center-point).sqrMagnitude<=85*85)frontage++;
                    }
                    if(frontage<2)continue;
                    float score=distance+(12-Mathf.Min(frontage,12))*900;
                    if(authored)score-=100000;
                    if(score>=best)continue;
                    best=score;start=point;direction=delta.normalized;segmentA=a;segmentB=b;
                }
            }
            if(best==float.MaxValue)return false;
            var candidates=new List<MapFeature>();
            foreach(var feature in features)
            {
                if(feature.Kind!="building" || MapFeatureStyle.Text(feature,"scenery_only")=="true" || WorldDetailPlanner.IsBuildingPart(feature) || !EnterableBuilding.FitLobby(feature,out _,out _,out _))continue;
                var center=SectorSignage.Center(feature.Points);var delta=segmentB-segmentA;
                var nearest=segmentA+delta*Mathf.Clamp01(Vector2.Dot(center-segmentA,delta)/delta.sqrMagnitude);
                if((center-nearest).sqrMagnitude<=42*42 && (center-start).sqrMagnitude<=85*85)candidates.Add(feature);
            }
            if(candidates.Count<2)return false;
            var anchor=start;
            candidates.Sort((a,b)=>(SectorSignage.Center(a.Points)-anchor).sqrMagnitude.CompareTo((SectorSignage.Center(b.Points)-anchor).sqrMagnitude));
            for(int i=0;i<Mathf.Min(12,candidates.Count);i++)candidates[i].Details["street_study"]="true";
            return true;
        }
        void ApplyStreetStudyRoad(Material road)
        {
            if(!Features.Exists(f=>MapFeatureStyle.Text(f,"street_study")=="true"))return;
            const int size=256;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Street study asphalt aggregate",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=2};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                // Coarse resurfacing mottles and fine aggregate, with mipmaps so
                // oblique road pixels do not turn into a sparkling textile pattern.
                float patch=Mathf.PerlinNoise(x*.026f+12,y*.026f+31);
                float grain=AsphaltGrain(x,y);
                float value=.095f+patch*.045f+(grain-.5f)*.018f;
                pixels[y*size+x]=new Color(value,value*.99f,value*.95f,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,true);if(streetStudyRoadTexture!=null)Destroy(streetStudyRoadTexture);streetStudyRoadTexture=texture;
            var shader=Shader.Find("GeoSniper/StreetStudySurface");if(shader!=null)road.shader=shader;
            road.mainTexture=texture;road.color=new Color(.76f,.75f,.73f);
            road.SetFloat("_BumpScale",.12f);road.SetFloat("_Glossiness",.20f);
        }
        void BuildStudyForecourt(MapFeature building,List<MapFeature> features)
        {
            Vector2 min=building.Points[0],max=min;
            foreach(var p in building.Points){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            min-=Vector2.one*7;max+=Vector2.one*7;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            var exclusions=new List<MapFeature>();var samples=new Vector2[5];
            foreach(var feature in features)
            {
                if(feature.Points.Count<2 || (feature.Kind!="building" && feature.Kind!="park" && feature.Kind!="water" && feature.Kind!="road"))continue;
                var lo=feature.Points[0];var hi=lo;foreach(var point in feature.Points){lo=Vector2.Min(lo,point);hi=Vector2.Max(hi,point);}
                float margin=feature.Kind=="road"?MapFeatureStyle.RoadWidth(feature)*.5f+2.5f:0;
                lo-=Vector2.one*margin;hi+=Vector2.one*margin;
                if(lo.x<=max.x && hi.x>=min.x && lo.y<=max.y && hi.y>=min.y)exclusions.Add(feature);
            }
            for(float x=min.x;x<max.x;x+=2)for(float z=min.y;z<max.y;z+=2)
            {
                var center=new Vector2(x+1,z+1);bool reserved=false;
                samples[0]=new Vector2(x,z);samples[1]=new Vector2(x,z+2);samples[2]=new Vector2(x+2,z);samples[3]=new Vector2(x+2,z+2);samples[4]=center;
                foreach(var feature in exclusions)
                {
                    if(feature.Points.Count<2)continue;
                    if(feature.Kind=="building" || feature.Kind=="park" || feature.Kind=="water")
                    {
                        // Check all tile corners, so paving cannot cover an explicit green/water edge.
                        foreach(var corner in samples)
                            if(feature.Kind=="water" && feature.WaterRings.Count>0?WaterGeometry.Contains(feature.WaterRings,corner):EnterableBuilding.Inside(corner,feature.Points)){reserved=true;break;}
                    }
                    else if(feature.Kind=="road")for(int i=1;i<feature.Points.Count;i++)
                    {
                        var a=feature.Points[i-1];var delta=feature.Points[i]-a;if(delta.sqrMagnitude<.01f)continue;
                        var near=a+delta*Mathf.Clamp01(Vector2.Dot(center-a,delta)/delta.sqrMagnitude);
                        if((center-near).sqrMagnitude<Mathf.Pow(MapFeatureStyle.RoadWidth(feature)*.5f+2.5f,2)){reserved=true;break;}
                    }
                    if(reserved)break;
                }
                if(reserved)continue;
                int first=vertices.Count;
                foreach(var p in new[]{new Vector2(x+.018f,z+.018f),new Vector2(x+.018f,z+1.982f),new Vector2(x+1.982f,z+1.982f),new Vector2(x+1.982f,z+.018f)})
                {vertices.Add(new Vector3(p.x,Ground(p.x,p.y)+.042f,p.y));uv.Add(p*.65f);}
                triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
            }
            if(MapFeatureStyle.Text(building,"training_frontage")=="true")
            {
                // Authored training frontage has a reserved continuous apron between
                // its facade and the existing sidewalk. Real source plots use exclusions.
                float lo=building.Points[0].y,hi=lo,left=building.Points[0].x,right=left;
                foreach(var p in building.Points){lo=Mathf.Min(lo,p.y);hi=Mathf.Max(hi,p.y);left=Mathf.Min(left,p.x);right=Mathf.Max(right,p.x);}
                bool west=(left+right)*.5f<80;float front=west?right:left,curb=80+(west?-5.55f:5.55f);
                float near=Mathf.Min(front,curb),far=Mathf.Max(front,curb);
                for(float z=lo;z<hi;z+=1)
                {
                    int first=vertices.Count;float end=Mathf.Min(z+1,hi);
                    foreach(var p in new[]{new Vector2(near,z),new Vector2(near,end),new Vector2(far,end),new Vector2(far,z)})
                    {vertices.Add(new Vector3(p.x,Ground(p.x,p.y)+.045f,p.y));uv.Add(p*.65f);}
                    triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
                }
            }
            if(vertices.Count==0)return;
            var mesh=new Mesh{name="Pilot forecourt paving"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();meshes.Add(mesh);
            var obj=new GameObject("Bounded urban forecourt / no collision");obj.transform.SetParent(transform,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var paving=InteriorSurface("Pilot entrance paving",new Color(.34f,.32f,.28f),.16f);paving.mainTexture=BuildNeighborhoodPavingTexture();
            obj.AddComponent<MeshRenderer>().sharedMaterial=paving;
        }
        void BuildStudyApproach(EnterableBuilding interior,List<MapFeature> features)
        {
            var door=transform.InverseTransformPoint(interior.transform.TransformPoint(interior.Entrance));
            var start=new Vector2(door.x,door.z);Vector2 nearest=start;float distance=float.MaxValue,width=0;
            foreach(var road in features)
            {
                if(road.Kind!="road" || MapFeatureStyle.Path(road))continue;
                for(int i=1;i<road.Points.Count;i++)
                {
                    var a=road.Points[i-1];var delta=road.Points[i]-a;if(delta.sqrMagnitude<1)continue;
                    var point=a+delta*Mathf.Clamp01(Vector2.Dot(start-a,delta)/delta.sqrMagnitude);
                    float d=(point-start).sqrMagnitude;if(d>=distance)continue;
                    distance=d;nearest=point;width=MapFeatureStyle.RoadWidth(road);
                }
            }
            float length=Mathf.Sqrt(distance)-width*.5f-.15f;
            if(!float.IsFinite(length) || length<1 || length>25)return;
            var heading=(nearest-start).normalized;
            var outward=transform.InverseTransformDirection(interior.transform.TransformDirection(interior.EntranceOutward));
            if(Vector2.Dot(heading,new Vector2(outward.x,outward.z))<.65f)return;
            var side=new Vector2(-heading.y,heading.x)*1.45f;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            int steps=Mathf.CeilToInt(length/1.5f);
            for(int step=0;step<steps;step++)
            {
                var a=start+heading*(.25f+step*length/steps);var b=start+heading*(.25f+(step+1)*length/steps);
                int first=vertices.Count;
                foreach(var p in new[]{a-side,b-side,b+side,a+side}){vertices.Add(new Vector3(p.x,Ground(p.x,p.y)+.045f,p.y));uv.Add(p*.65f);}
                triangles.AddRange(new[]{first,first+2,first+1,first,first+3,first+2});
            }
            var mesh=new Mesh{name="Terrain fitted pilot entrance paving"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();meshes.Add(mesh);
            var obj=new GameObject("Pilot entrance approach / no collision");obj.transform.SetParent(transform,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var paving=InteriorSurface("Pilot entrance paving",new Color(.34f,.32f,.28f),.16f);paving.mainTexture=BuildNeighborhoodPavingTexture();
            obj.AddComponent<MeshRenderer>().sharedMaterial=paving;
        }
    }
}
