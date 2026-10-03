using UnityEngine;
using System.Collections.Generic;

namespace GeoSniper
{
    public class TrafficVehicle : MonoBehaviour
    {
        static int nextPriority;
        readonly int crossingPriority=nextPriority++;
        List<Vector3> path;
        int currentWaypointIndex;
        float speed = 8f; // ~30 km/h
        float maxSpeed = 12f;
        float currentSpeed = 0f;
        float acceleration = 4f;
        bool isFleeing = false;
        
        int directionStep = 1;
        float laneOffset = 1.35f;
        float steeringAngle;
        TrafficBrakeLights brakeLights;
        public static readonly List<TrafficVehicle> ActiveVehicles = new List<TrafficVehicle>();
        
        public void Initialize(List<Vector3> roadPath, int startIndex, int step = 1, float offset = 1.35f)
        {
            path = roadPath;
            brakeLights=GetComponent<TrafficBrakeLights>();
            laneOffset=offset; routeEnded=false; steeringAngle=0;nextRouteRefresh=0;
            if (path == null || path.Count < 2)
            {
                Destroy(gameObject);
                return;
            }
            directionStep = step >= 0 ? 1 : -1;
            startIndex = Mathf.Clamp(startIndex, 0, path.Count - 1);
            if (directionStep > 0 && startIndex >= path.Count - 1) startIndex = path.Count - 2;
            if (directionStep < 0 && startIndex <= 0) startIndex = 1;

            currentWaypointIndex = startIndex + directionStep;
            Vector3 forwardDir = path[currentWaypointIndex] - path[startIndex];
            Vector3 horizForward = new Vector3(forwardDir.x, 0, forwardDir.z);
            Vector3 rightDir = horizForward.sqrMagnitude > 0.001f ? Vector3.Cross(Vector3.up, horizForward.normalized) : Vector3.right;
            Vector3 startPos = path[startIndex] + rightDir * laneOffset + Vector3.up * 0.05f;
            if(!TryRoadSurface(startPos,out var supported,out _))
            {gameObject.SetActive(false);Destroy(gameObject);return;}
            startPos=supported;
            foreach(var other in ActiveVehicles)
            {
                if(other==null || other==this || !other.gameObject.activeSelf || other.path==null)continue;
                Vector3 separation=other.transform.position-startPos;
                if(Mathf.Abs(separation.y)<3f && new Vector2(separation.x,separation.z).sqrMagnitude<64f)
                {gameObject.SetActive(false);Destroy(gameObject);return;}
            }
            transform.position = startPos;
            if (forwardDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(forwardDir.normalized);
            }
            currentSpeed = 0f;
        }
        
        void OnEnable()
        {
            if (!ActiveVehicles.Contains(this)) ActiveVehicles.Add(this);
            BallisticsSystem.OnShotNoise += HearGunshot;
        }

        void OnDisable()
        {
            ActiveVehicles.Remove(this);
            BallisticsSystem.OnShotNoise -= HearGunshot;
        }

        void OnDestroy()
        {
            ActiveVehicles.Remove(this);
            BallisticsSystem.OnShotNoise -= HearGunshot;
        }
        
        void HearGunshot(Vector3 source,float radius)
        {
            if (Vector3.Distance(transform.position, source) < Mathf.Min(100f,radius))
            {
                isFleeing = true;
            }
        }
        
        readonly RaycastHit[] probeHits=new RaycastHit[24];
        bool routeEnded;
        float nextRouteRefresh;
        public float CurrentSpeed=>currentSpeed;
        public static float SafeSpeed(float clearDistance,float braking=6f)
            =>Mathf.Sqrt(2f*braking*Mathf.Max(0,clearDistance-2.5f));
        // Predict crossing paths before side traffic enters the forward obstacle probe.
        // Stable priority avoids both cars repeatedly yielding and restarting together.
        public static float CrossingSpeed(Vector3 position,Vector3 forward,float velocity,int priority,
            Vector3 otherPosition,Vector3 otherForward,float otherVelocity,int otherPriority)
        {
            if(Mathf.Abs(position.y-otherPosition.y)>2f)return float.PositiveInfinity;
            Vector2 a=new Vector2(forward.x,forward.z).normalized;
            Vector2 b=new Vector2(otherForward.x,otherForward.z).normalized;
            float cross=a.x*b.y-a.y*b.x;
            if(Mathf.Abs(cross)<.25f)return float.PositiveInfinity;
            Vector2 gap=new Vector2(otherPosition.x-position.x,otherPosition.z-position.z);
            float distance=(gap.x*b.y-gap.y*b.x)/cross;
            float otherDistance=(gap.x*a.y-gap.y*a.x)/cross;
            if(distance<0 || distance>35 || otherDistance< -3 || otherDistance>35)return float.PositiveInfinity;
            float arrival=distance/Mathf.Max(velocity,2f);
            float otherArrival=Mathf.Max(0,otherDistance)/Mathf.Max(otherVelocity,2f);
            if(arrival>5 || otherArrival>5 || Mathf.Abs(arrival-otherArrival)>2.5f)return float.PositiveInfinity;
            bool otherCommitted=otherDistance<3;
            bool ownCommitted=distance<3;
            if(!otherCommitted && (ownCommitted || priority<otherPriority))return float.PositiveInfinity;
            return SafeSpeed(distance-2f);
        }
        float CrossingLimit()
        {
            float limit=float.PositiveInfinity;
            foreach(var other in ActiveVehicles)
            {
                if(other==null || other==this || other.path==null || other.routeEnded || !other.isActiveAndEnabled)continue;
                if((other.transform.position-transform.position).sqrMagnitude>2500)continue;
                limit=Mathf.Min(limit,CrossingSpeed(transform.position,transform.forward,currentSpeed,crossingPriority,
                    other.transform.position,other.transform.forward,other.currentSpeed,other.crossingPriority));
            }
            return limit;
        }
        bool TryRoadSurface(Vector3 point,out Vector3 surface,out Vector3 normal)
        {
            surface=point;normal=Vector3.up;
            if(SectorWorld.WaterAt(point))return false;
            int count=Physics.RaycastNonAlloc(point+Vector3.up*5f,Vector3.down,probeHits,12f,~0,QueryTriggerInteraction.Ignore);
            float nearest=float.MaxValue;
            for(int i=0;i<count;i++)
            {
                var hit=probeHits[i];
                if(hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<TrafficVehicle>()!=null)continue;
                string label=hit.collider.gameObject.name;
                if(label!="Local terrain" && label!="Sloped road")continue;
                if(hit.normal.y<.65f || hit.distance>=nearest)continue;
                nearest=hit.distance;surface=hit.point+Vector3.up*.05f;normal=hit.normal;
            }
            return nearest<float.MaxValue;
        }
        void Update()=>Simulate(Time.deltaTime);
        void Simulate(float deltaTime)
        {
            if(path==null || path.Count<2)return;
            var explosive=GetComponent<ExplosiveProp>();
            if(explosive!=null && explosive.isDetonated){currentSpeed=0;brakeLights?.SetState(false,false);return;}
            float dt=Mathf.Clamp(deltaTime,0,.05f);
            int index=Mathf.Clamp(currentWaypointIndex,0,path.Count-1);
            int previous=Mathf.Clamp(index-directionStep,0,path.Count-1);
            Vector3 segment=path[index]-path[previous];segment.y=0;
            Vector3 right=segment.sqrMagnitude>.01f?Vector3.Cross(Vector3.up,segment.normalized):transform.right;
            Vector3 target=path[index]+right*laneOffset;
            Vector3 toEnd=target-transform.position;toEnd.y=0;
            float remaining=toEnd.magnitude;
            if(currentWaypointIndex==path.Count-1 && remaining<30f && Time.time>=nextRouteRefresh)
            {
                nextRouteRefresh=Time.time+5f;
                if(TryRefreshRoute())
                {
                    routeEnded=false;
                    if(currentWaypointIndex>32)
                    {int trim=currentWaypointIndex-2;path.RemoveRange(0,trim);currentWaypointIndex-=trim;}
                    index=currentWaypointIndex;
                }
            }
            // Follow a point a few metres ahead on the lane instead of aiming at a distant endpoint.
            Vector3 laneStart=path[previous]+right*laneOffset;
            float fraction=Mathf.Clamp01(Vector3.Dot(transform.position-laneStart,segment)/Mathf.Max(.01f,segment.sqrMagnitude));
            float lookDistance=Mathf.Clamp(3f+currentSpeed*.45f,3f,7f);
            Vector3 aim=laneStart+segment*Mathf.Clamp01(fraction+lookDistance/Mathf.Max(.1f,segment.magnitude));
            Vector3 direction=aim-transform.position;direction.y=0;
            float desired=isFleeing?maxSpeed:speed;
            int following=index+directionStep;
            bool final=following<0 || following>=path.Count;
            if(final)desired=Mathf.Min(desired,SafeSpeed(remaining+.6f));
            else
            {
                Vector3 next=path[following]-path[index];next.y=0;
                float bend=Vector3.Angle(segment,next);
                float cornerSpeed=Mathf.Lerp(speed,2.2f,Mathf.Clamp01(bend/90f));
                desired=Mathf.Min(desired,Mathf.Sqrt(cornerSpeed*cornerSpeed+8f*Mathf.Max(0,remaining-4f)));
            }
            if(routeEnded)desired=0;
            desired=Mathf.Min(desired,CrossingLimit());
            // Speed-dependent headway applies during panic as well as normal driving.
            float lookAhead=Mathf.Max(8f,currentSpeed*1.4f+currentSpeed*currentSpeed/12f);
            int hits=Physics.BoxCastNonAlloc(transform.position+Vector3.up*.8f,new Vector3(.8f,.45f,.45f),
                transform.forward,probeHits,transform.rotation,lookAhead,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<hits;i++)
            {
                var hit=probeHits[i];if(hit.collider.transform.IsChildOf(transform))continue;
                string label=hit.collider.gameObject.name;
                if(label=="Sloped road" || label=="Local terrain" || label=="park")continue;
                desired=Mathf.Min(desired,SafeSpeed(hit.distance));
            }
            if(direction.sqrMagnitude>.04f)
            {
                float headingError=Vector3.Angle(transform.forward,direction);
                desired*=Mathf.Clamp01(1f-headingError/110f);
                float signedError=Vector3.SignedAngle(transform.forward,direction,Vector3.up)*Mathf.Deg2Rad;
                float requested=Mathf.Atan2(2f*2.6f*Mathf.Sin(signedError),Mathf.Max(1f,direction.magnitude))*Mathf.Rad2Deg;
                steeringAngle=Mathf.MoveTowards(steeringAngle,Mathf.Clamp(requested,-38f,38f),90f*dt);
            }
            brakeLights?.SetState(desired<currentSpeed-.15f || desired<.2f,true);
            currentSpeed=Mathf.MoveTowards(currentSpeed,desired,(desired<currentSpeed?7f:acceleration)*dt);
            // Bicycle steering: stationary wheels cannot turn the vehicle body in place.
            float yaw=currentSpeed/2.6f*Mathf.Tan(steeringAngle*Mathf.Deg2Rad)*Mathf.Rad2Deg*dt;
            transform.rotation=Quaternion.AngleAxis(yaw,Vector3.up)*transform.rotation;
            Vector3 step=transform.position+transform.forward*Mathf.Min(remaining,currentSpeed*dt);
            if(TryRoadSurface(step,out var supported,out var normal))
            {
                transform.position=supported;
                Vector3 forward=Vector3.ProjectOnPlane(transform.forward,normal).normalized;
                if(forward.sqrMagnitude>.01f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(forward,normal),dt*5f);
            }
            else currentSpeed=0;
            if(remaining<2.2f)
            {
                if(final){routeEnded=true;currentSpeed=0;}
                else currentWaypointIndex+=directionStep;
            }
        }

        bool TryRefreshRoute()
        {
            var roads=new List<TrafficRoadRoutes.Road>();
            foreach(var world in SectorWorld.LoadedWorlds)
            {
                if(world==null)continue;
                foreach(var local in world.RoadPaths)
                {
                    if(local==null || local.Count<2)continue;
                    world.RoadPathFeatures.TryGetValue(local,out var feature);
                    var points=new List<Vector3>(local.Count);
                    foreach(var point in local)points.Add(world.transform.TransformPoint(point));
                    roads.Add(new TrafficRoadRoutes.Road{Points=points,Feature=feature});
                }
            }
            return new TrafficRoadRoutes(roads).TryAppend(path);
        }
    }
}
