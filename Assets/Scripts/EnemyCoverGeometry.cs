using UnityEngine;
namespace GeoSniper
{
    public static class EnemyCoverGeometry
    {
        public static bool Protected(Vector3 candidate,Vector3 threatEye,Transform owner,Transform player)
        {
            if(!Physics.Linecast(candidate+Vector3.up*.85f,threatEye,out var hit,~0,QueryTriggerInteraction.Ignore)) return false;
            return hit.transform!=owner && !hit.transform.IsChildOf(owner)
                && (player==null || (hit.transform!=player && !hit.transform.IsChildOf(player)))
                && hit.collider.GetComponentInParent<CombatActor>()==null;
        }
        public static bool Reachable(Vector3 from,Vector3 to,Transform owner,RaycastHit[] hits)
        {
            Vector3 delta=to-from;delta.y=0;
            float distance=delta.magnitude;
            if(distance<.1f) return true;
            int count=Physics.SphereCastNonAlloc(from+Vector3.up*.65f,.3f,delta/distance,hits,distance,~0,QueryTriggerInteraction.Ignore);
            if(count>=hits.Length) return false;
            for(int i=0;i<count;i++)
                if(hits[i].transform!=owner && !hits[i].transform.IsChildOf(owner)) return false;
            int steps=Mathf.CeilToInt(distance/.65f);
            for(int i=1;i<=steps;i++)
            {
                Vector3 point=Vector3.Lerp(from,to,(float)i/steps);
                if(!Physics.Raycast(point+Vector3.up*.85f,Vector3.down,out var support,1.6f,~0,QueryTriggerInteraction.Ignore)
                    || support.normal.y<=.45f || support.transform==owner || support.transform.IsChildOf(owner)
                    || Mathf.Abs(support.point.y-from.y)>=.85f) return false;
            }
            return true;
        }

        public static bool ClearShot(Vector3 origin,Vector3 target,Transform owner,Transform player,RaycastHit[] hits)
        {
            Vector3 delta=target-origin;
            float distance=delta.magnitude;
            if(distance<.1f)return true;
            int count=Physics.RaycastNonAlloc(origin,delta/distance,hits,distance,~0,QueryTriggerInteraction.Ignore);
            if(count>=hits.Length)return false;
            float closestBlock=float.MaxValue, closestPlayer=float.MaxValue;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(hit.transform==owner || hit.transform.IsChildOf(owner))continue;
                if(player!=null && (hit.transform==player || hit.transform.IsChildOf(player)))
                    closestPlayer=Mathf.Min(closestPlayer,hit.distance);
                else closestBlock=Mathf.Min(closestBlock,hit.distance);
            }
            return closestBlock>=closestPlayer || closestBlock==float.MaxValue;
        }
    }
}
