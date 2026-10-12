using System.Collections;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class UrbanPlayer
    {
        readonly Collider[] vaultContacts=new Collider[24];
        bool windowVaulting;
        bool VaultClear(Vector3 feet)
        {
            int count=Physics.OverlapCapsuleNonAlloc(feet+Vector3.up*.43f,feet+Vector3.up*1.47f,.34f,vaultContacts,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==vaultContacts.Length)return false;
            for(int i=0;i<count;i++)if(!vaultContacts[i].transform.IsChildOf(transform))return false;
            return true;
        }
        public bool TryWindowVault(Vector3 direction)
        {
            if(body==null || climbing || InputBlocked || Health!=null && Health.IsDead)return false;
            direction.y=0;if(direction.sqrMagnitude<.01f)return false;direction.Normalize();
            Physics.SyncTransforms();
            foreach(var building in EnterableBuilding.All)
                foreach(var window in building.OpenWindows)
                {
                    Vector3 center=building.transform.TransformPoint(window.Center);
                    Vector3 outward=building.transform.TransformDirection(window.Outward).normalized;
                    Vector3 offset=transform.position-center;float side=Vector3.Dot(offset,outward);
                    Vector3 across=Vector3.Cross(Vector3.up,outward);
                    if(Mathf.Abs(side)>.95f || Mathf.Abs(side)<.12f || Mathf.Abs(offset.y)>.5f ||
                        Mathf.Abs(Vector3.Dot(offset,across))>window.Width*.5f-.4f)continue;
                    Vector3 through=side>0?-outward:outward;
                    if(Vector3.Dot(direction,through)<.6f)continue;
                    Vector3 destination=center+through*1.05f;
                    if(!Physics.Raycast(destination+Vector3.up*1.6f,Vector3.down,out var floor,3.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || floor.normal.y<.7f || SectorWorld.WaterAt(floor.point))continue;
                    destination=floor.point+Vector3.up*.1f;
                    if(Mathf.Abs(destination.y-transform.position.y)>1.25f)continue;
                    var path=new[]{transform.position,center-through*.55f+Vector3.up*(window.Sill+.12f),center+through*.55f+Vector3.up*(window.Sill+.12f),destination};
                    bool clear=true;
                    for(int segment=0;segment<3 && clear;segment++)
                        for(int step=1;step<=10;step++)if(!VaultClear(Vector3.Lerp(path[segment],path[segment+1],step/10f))){clear=false;break;}
                    if(!clear)continue;
                    StartCoroutine(WindowVault(path));return true;
                }
            return false;
        }
        IEnumerator WindowVault(Vector3[] path)
        {
            climbing=true;windowVaulting=true;scope=false;BinocularsMode=false;vaultTimer=0;vertical=0;
            body.height=1.8f;body.center=Vector3.up*.9f;IsCrouching=false;if(Controls!=null)Controls.IsCrouching=false;
            try
            {
                float elapsed=0;
                while(elapsed<.65f)
                {
                    if(InputBlocked || Health!=null && Health.IsDead)yield break;
                    elapsed+=Time.deltaTime;float progress=Mathf.Clamp01(elapsed/.65f)*3;
                    int segment=Mathf.Min(2,Mathf.FloorToInt(progress));float t=Mathf.SmoothStep(0,1,progress-segment);
                    Vector3 next=Vector3.Lerp(path[segment],path[segment+1],t);
                    if(!VaultClear(next))yield break;
                    body.Move(next-transform.position);SpeedMetersPerSecond=3;
                    if(Vector3.Distance(transform.position,next)>.25f)yield break;
                    yield return null;
                }
                lastGrounded=transform.position;lastDryPosition=transform.position;hasDryPosition=true;
            }
            finally{climbing=false;windowVaulting=false;vertical=-1;prevVertical=-1;SpeedMetersPerSecond=0;}
        }
    }
}
