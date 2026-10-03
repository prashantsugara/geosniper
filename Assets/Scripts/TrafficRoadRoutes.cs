using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    // Connect only coincident mapped nodes. Crossing lines alone do not prove a junction.
    public sealed class TrafficRoadRoutes
    {
        public sealed class Road
        {
            public List<Vector3> Points;
            public MapFeature Feature;
        }
        readonly List<Road> roads;
        public TrafficRoadRoutes(List<Road> roads){this.roads=roads;}
        public bool TryAppend(List<Vector3> current)
        {
            if(current==null || current.Count<2)return false;
            Vector3 end=current[current.Count-1];
            Vector3 incoming=end-current[current.Count-2];incoming.y=0;
            if(incoming.sqrMagnitude<.25f)return false;
            Road bestRoad=null;int bestIndex=0,bestStep=0;float bestScore=.55f;
            foreach(var road in roads)
            {
                if(road.Points==null || road.Points.Count<2 ||
                   (road.Feature!=null && (MapFeatureStyle.Path(road.Feature) || MapFeatureStyle.RoadWidth(road.Feature)<3f)))continue;
                int allowed=road.Feature!=null?MapFeatureStyle.TrafficDirection(road.Feature):0;
                float offset=road.Feature!=null?MapFeatureStyle.TrafficLaneOffset(road.Feature):1.35f;
                for(int i=0;i<road.Points.Count;i++)for(int step=-1;step<=1;step+=2)
                {
                    int next=i+step;if(next<0 || next>=road.Points.Count || allowed!=0 && allowed!=step)continue;
                    Vector3 tangent=road.Points[next]-road.Points[i];tangent.y=0;
                    if(tangent.sqrMagnitude<.25f)continue;
                    Vector3 lane=road.Points[i]+Vector3.Cross(Vector3.up,tangent.normalized)*offset;
                    Vector3 gap=lane-end;
                    if(Mathf.Abs(gap.y)>1.5f || gap.x*gap.x+gap.z*gap.z>9f)continue;
                    float score=Vector3.Dot(incoming.normalized,tangent.normalized)-gap.sqrMagnitude*.025f;
                    if(score>bestScore){bestScore=score;bestRoad=road;bestIndex=i;bestStep=step;}
                }
            }
            if(bestRoad==null)return false;
            var continuation=Build(bestRoad,bestIndex,bestStep);
            if(continuation.Count<2)return false;
            int originalCount=current.Count;
            foreach(var point in continuation)
                if((point-current[current.Count-1]).sqrMagnitude>.04f)current.Add(point);
            return current.Count>originalCount;
        }
        public List<Vector3> Build(Road start,int startIndex,int direction)
        {
            var result=new List<Vector3>();
            var visited=new HashSet<Road>();
            var road=start;int index=startIndex,step=direction;
            float length=0;
            for(int hop=0;hop<24 && road!=null && result.Count<512 && length<1500;hop++)
            {
                visited.Add(road);
                float offset=road.Feature!=null?MapFeatureStyle.TrafficLaneOffset(road.Feature):1.35f;
                for(int i=index;i>=0 && i<road.Points.Count;i+=step)
                {
                    int next=i+step,previous=i-step;
                    Vector3 tangent=next>=0 && next<road.Points.Count?road.Points[next]-road.Points[i]:road.Points[i]-road.Points[previous];
                    tangent.y=0;
                    Vector3 point=road.Points[i]+Vector3.Cross(Vector3.up,tangent.normalized)*offset;
                    if(result.Count==0 || (point-result[result.Count-1]).sqrMagnitude>.04f)
                    {
                        if(result.Count>0)length+=Vector3.Distance(result[result.Count-1],point);
                        result.Add(point);
                    }
                    if(result.Count>=512 || length>=1500)break;
                }
                int end=step>0?road.Points.Count-1:0;
                Vector3 node=road.Points[end],incoming=node-road.Points[end-step];incoming.y=0;
                Road selected=null;int selectedIndex=0,selectedStep=1;float best=-.5f;
                foreach(var candidate in roads)
                {
                    if(visited.Contains(candidate) || candidate.Points.Count<2)continue;
                    if(candidate.Feature!=null && (MapFeatureStyle.Path(candidate.Feature) || MapFeatureStyle.RoadWidth(candidate.Feature)<3))continue;
                    int allowed=candidate.Feature!=null?MapFeatureStyle.TrafficDirection(candidate.Feature):0;
                    for(int j=0;j<candidate.Points.Count;j++)
                    {
                        Vector3 gap=candidate.Points[j]-node;
                        if(Mathf.Abs(gap.y)>1f || gap.x*gap.x+gap.z*gap.z>2.25f)continue;
                        for(int d=-1;d<=1;d+=2)
                        {
                            if(allowed!=0 && d!=allowed || j+d<0 || j+d>=candidate.Points.Count)continue;
                            Vector3 outgoing=candidate.Points[j+d]-candidate.Points[j];outgoing.y=0;
                            if(outgoing.sqrMagnitude<.25f)continue;
                            float score=Vector3.Dot(incoming.normalized,outgoing.normalized);
                            if(score<=best)continue;
                            best=score;selected=candidate;selectedIndex=j;selectedStep=d;
                        }
                    }
                }
                road=selected;index=selectedIndex;step=selectedStep;
            }
            return result;
        }
    }
}
