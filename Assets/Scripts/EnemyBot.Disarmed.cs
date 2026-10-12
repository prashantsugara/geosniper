using UnityEngine;
namespace GeoSniper
{
    public sealed partial class EnemyBot
    {
        Transform droppedWeapon,droppedWeaponParent,disarmedLeftHand,disarmedRightHand;
        bool droppedColliderAdded,droppedColliderWasEnabled;
        Vector3 droppedLocalPosition;Quaternion droppedLocalRotation;
        float disarmedAt,weaponPickupHold;
        public bool RecoveringWeapon=>isDisarmed && weaponPickupHold>0;
        void UpdateDisarmed(float dt)
        {
            if(droppedWeapon!=null && Time.time-disarmedAt>30){Destroy(droppedWeapon.gameObject);droppedWeapon=null;}
            bool pressured=Time.time<underFireUntil || suppressionTimer>0 || ClosePlayerVisible(12);
            bool canRecover=droppedWeapon!=null && Time.time-disarmedAt>=4 && !pressured && actor.Health>actor.maxHealth*.35f;
            if(canRecover)
            {
                Vector3 point=droppedWeapon.position;
                if(UnityEngine.AI.NavMesh.SamplePosition(point,out var hit,1.2f,UnityEngine.AI.NavMesh.AllAreas)
                    && Mathf.Abs(hit.position.y-point.y)<1 && (hit.position-transform.position).sqrMagnitude<144
                    && SafeStep(hit.position) && EnemyCoverGeometry.Reachable(transform.position,hit.position,transform,coverPathHits))
                {
                    currentState=AIState.TakeCover;
                    if(Vector3.Distance(transform.position,hit.position)>1.1f){weaponPickupHold=0;MoveTowards(hit.position,.85f,false);return;}
                    MoveTowards(transform.position,0f);weaponPickupHold+=dt;
                    if(weaponPickupHold>=1.4f)RecoverDroppedWeapon();
                    return;
                }
            }
            weaponPickupHold=0;currentState=AIState.TakeCover;
            UpdateTakeCover();
        }
        void RecoverDroppedWeapon()
        {
            if(droppedWeapon==null || actor.IsDead)return;
            var physics=droppedWeapon.GetComponent<Rigidbody>();
            if(physics!=null){physics.linearVelocity=Vector3.zero;physics.angularVelocity=Vector3.zero;physics.isKinematic=true;physics.useGravity=false;if(Application.isPlaying)Destroy(physics);else DestroyImmediate(physics);}
            var collision=droppedWeapon.GetComponent<Collider>();
            if(collision!=null){collision.enabled=droppedColliderWasEnabled;if(droppedColliderAdded){if(Application.isPlaying)Destroy(collision);else DestroyImmediate(collision);}}
            gun=droppedWeapon;droppedWeapon=null;gun.SetParent(droppedWeaponParent!=null?droppedWeaponParent:transform,true);
            gun.localPosition=droppedLocalPosition;gun.localRotation=droppedLocalRotation;
            foreach(var child in gun.GetComponentsInChildren<Transform>())if(child.name=="Enemy weapon muzzle")weaponMuzzle=child;
            isDisarmed=false;weaponPickupHold=0;
            var anchor=gun.GetComponent<EnemyWeaponAnchor>();if(anchor!=null)anchor.enabled=true;
            SetStagger(.35f);acquisitionRemaining=.5f;fireCooldown=Mathf.Max(fireCooldown,.65f);
            currentState=playerVisible?AIState.Combat:AIState.Suspicious;stateTimer=6;combatTargetReady=false;
            TriggerBark("WEAPON RECOVERED!",2);
        }
        void PoseDisarmedHands()
        {
            if(disarmedLeftHand==null)disarmedLeftHand=WeaponGripPose.Bone(transform,"lefthand","hand_l","left_hand","mixamoriglefthand");
            if(disarmedRightHand==null)disarmedRightHand=WeaponGripPose.Bone(transform,"righthand","hand_r","right_hand","mixamorigrighthand");
            float height=RecoveringWeapon?.48f:IsCrouched?.95f:1.15f;
            Vector3 center=transform.position+Vector3.up*height+transform.forward*(RecoveringWeapon?.4f:.14f);
            WeaponGripPose.SolveArm(disarmedLeftHand,Vector3.zero,center-transform.right*.22f,transform.rotation, -transform.right-transform.up,true);
            WeaponGripPose.SolveArm(disarmedRightHand,Vector3.zero,center+transform.right*.22f,transform.rotation,transform.right-transform.up,true);
        }
        void PlayEnemyReport(bool sniper)
        {
            if(enemyAudio==null)enemyAudio=gameObject.AddComponent<AudioSource>();
            if(sharedEnemyShotClip==null)sharedEnemyShotClip=BuildEnemyShotClip();
            enemyAudio.playOnAwake=false;enemyAudio.spatialBlend=1;enemyAudio.dopplerLevel=0;
            enemyAudio.rolloffMode=AudioRolloffMode.Logarithmic;enemyAudio.minDistance=sniper?10:7;
            enemyAudio.maxDistance=sniper?200:110;enemyAudio.volume=sniper?.85f:.65f;
            // Keep pitch in place for the whole report; resetting immediately changes its playback.
            enemyAudio.pitch=sniper?.94f:Random.Range(.98f,1.02f);
            enemyAudio.PlayOneShot(sharedEnemyShotClip);
        }
    }
}
