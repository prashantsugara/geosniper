using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public static class SniperHitQuery
    {
        public struct ShotHit { public Collider Collider; public Vector3 Point; public float Distance; }
        static int queryVersion;
        public static Collider Cast(Ray ray,IEnumerable<EnemyBot> enemies,Transform shooter,Transform view,float maxDistance=1000f)
            => TryCast(ray,enemies,shooter,view,out var hit,maxDistance)?hit.Collider:null;
        public static bool TryCast(Ray ray,IEnumerable<EnemyBot> enemies,Transform shooter,Transform view,out ShotHit result,float maxDistance=1000f)
            => Query(ray,enemies,shooter,view,out result,maxDistance,false);
        public static bool TryCastAimAssist(Ray ray,IEnumerable<EnemyBot> enemies,Transform shooter,Transform view,out ShotHit result,float maxDistance=1000f)
            => Query(ray,enemies,shooter,view,out result,maxDistance,true);

        static bool Query(Ray ray,IEnumerable<EnemyBot> enemies,Transform shooter,Transform view,out ShotHit result,float range,bool assist)
        {
            result=default;
            if(range<=0 || ray.direction.sqrMagnitude<.000001f) return false;
            ray.direction=ray.direction.normalized;
            var targets=new List<CombatActor>();
            if(enemies!=null) foreach(var enemy in enemies)
                if(enemy!=null && enemy.gameObject.activeInHierarchy && enemy.Actor!=null && !enemy.Actor.IsDead) targets.Add(enemy.Actor);
            foreach(var civilian in CivilianBot.AllBots)
                if(civilian!=null && civilian.gameObject.activeInHierarchy && civilian.Actor!=null && !civilian.Actor.IsDead) targets.Add(civilian.Actor);
            int version=++queryVersion;
            try
            {
                foreach(var actor in targets) actor.GetComponent<EnemyHitboxes>()?.Refresh();
                Physics.SyncTransforms();
                bool found=CastPrepared(ray,targets,shooter,view,range,version,out result);
                // Never redirect a shot off an intervening wall, civilian or thin surface.
                if(found || !assist) return found;
                Vector3 right=Vector3.Cross(Vector3.up,ray.direction).normalized;
                if(right.sqrMagnitude<.01f) right=Vector3.Cross(Vector3.forward,ray.direction).normalized;
                Vector3 up=Vector3.Cross(ray.direction,right).normalized;
                float angle=Mathf.Min(.003f,.12f/Mathf.Max(1f,range));
                foreach(var offset in new[]{right*angle,-right*angle,up*angle,-up*angle})
                    if(CastPrepared(new Ray(ray.origin,(ray.direction+offset).normalized),targets,shooter,view,range,version,out var candidate)
                        && candidate.Collider.GetComponentInParent<EnemyBot>()!=null)
                    {result=candidate;return true;}
                return false;
            }
            finally { foreach(var actor in targets) if(actor!=null) actor.GetComponent<EnemyHitboxes>()?.Disable(); }
        }

        static bool CastPrepared(Ray ray,List<CombatActor> targets,Transform shooter,Transform view,float range,int version,out ShotHit result)
        {
            result=default;
            var hits=Physics.RaycastAll(ray,range,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            float nearest=range;
            bool found=false;
            foreach(var hit in hits)
            {
                if(Ignore(hit.collider,shooter,view) || hit.collider.isTrigger || hit.collider.GetComponentInParent<CombatActor>()!=null) continue;
                nearest=hit.distance;
                result=new ShotHit {Collider=hit.collider,Point=hit.point,Distance=hit.distance};found=true;break;
            }
            foreach(var actor in targets)
            {
                var volumes=actor.GetComponent<EnemyHitboxes>();
                if(volumes!=null && volumes.HasRaycastGeometry)
                {
                    if(volumes.TryRaycastVisual(ray,nearest,out var point,out var distance,out var collider,version))
                    {nearest=distance;result=new ShotHit {Collider=collider,Point=point,Distance=distance};found=true;}
                    continue; // A mesh miss must not become a hit on an oversized controller.
                }
                foreach(var hit in hits)
                {
                    if(hit.distance>=nearest) break;
                    if(Ignore(hit.collider,shooter,view) || hit.collider.GetComponentInParent<CombatActor>()!=actor) continue;
                    nearest=hit.distance;result=new ShotHit {Collider=hit.collider,Point=hit.point,Distance=hit.distance};found=true;break;
                }
            }
            return found;
        }

        public static bool IsPenetrable(Collider collider)
        {
            if(collider==null) return false;
            string name=collider.gameObject.name.ToLowerInvariant();
            return name.Contains("glass") || name.Contains("window") || name.Contains("wire fence") || name.Contains("chainlink");
        }
        public static bool TryPenetrate(ShotHit hit,Vector3 direction,out Vector3 exit)
        {
            exit=hit.Point;
            if(!IsPenetrable(hit.Collider)) return false;
            direction.Normalize();
            const float maxThickness=.25f;
            if(!hit.Collider.Raycast(new Ray(hit.Point+direction*(maxThickness+.01f),-direction),out var back,maxThickness+.02f)) return false;
            float thickness=Vector3.Dot(back.point-hit.Point,direction);
            if(thickness<0 || thickness>maxThickness) return false;
            foreach(var obstruction in Physics.RaycastAll(hit.Point-direction*.001f,direction,thickness+.003f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if(obstruction.collider!=hit.Collider) return false;
            exit=back.point+direction*.002f;
            return true;
        }
        static bool Ignore(Collider c,Transform shooter,Transform view) => c==null ||
            (shooter!=null && (c.transform==shooter || c.transform.IsChildOf(shooter))) ||
            (view!=null && (c.transform==view || c.transform.IsChildOf(view)));
    }
}
