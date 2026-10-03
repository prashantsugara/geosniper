using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GeoSniper
{
    public enum CivilianState { Idle, Patrol, Flee, Cower }
    
    public sealed class CivilianBot : MonoBehaviour
    {
        public float walkSpeed=2.0f, fleeSpeed=5.5f, health=30;
        Vector3 targetPos, lastPosition; 
        float stateTimer, blockedTime, vertical;
        Vector3 dangerPosition;
        CombatActor actor; CharacterController body; NavMeshAgent navigationAgent;
        Vector3 lastNavDestination;
        float nextNavRepath;
        bool navDestinationSet;
        
        public CivilianState currentState = CivilianState.Patrol;
        public bool Suspended;
        public float CurrentSpeed { get; private set; }
        public bool isVIP;
        public Vector3 vipDestination;
        public List<Vector3> waypoints;
        public int waypointIndex;
        public CombatActor Actor => actor;
        public CivilianBot SocialPartner {get;private set;}
        float nextSocialCheck;
        
        public void SetTarget(Vector3 pos) { targetPos = pos; currentState = CivilianState.Patrol; stateTimer = 999f; }

        public void Initialize(int index)
        {
            if(gameObject.activeInHierarchy && !AllBots.Contains(this)) AllBots.Add(this);
            ChoosePatrolTarget();
            lastPosition=transform.position;
            actor=gameObject.AddComponent<CombatActor>(); actor.Initialize(health);
            actor.OnDeath += HandleDeath;
            actor.OnDamaged += (dmg) => { if (isVIP) walkSpeed = 3.5f; else PanicFrom(transform.position - transform.forward * 3f); };
            body=GetComponent<CharacterController>();
            if(body==null) body=gameObject.AddComponent<CharacterController>();
            body.height=1.9f; body.center=Vector3.up*.95f; body.radius=.32f; body.stepOffset=.3f;
            navigationAgent = GetComponent<NavMeshAgent>();
            if (navigationAgent == null) navigationAgent = gameObject.AddComponent<NavMeshAgent>();
            navigationAgent.radius=.32f; navigationAgent.height=1.9f; navigationAgent.speed=fleeSpeed; navigationAgent.angularSpeed=720; navigationAgent.acceleration=18; navigationAgent.updatePosition=false; navigationAgent.updateRotation=false;
            if(NavMesh.SamplePosition(transform.position,out var navHit,3f,NavMesh.AllAreas)) navigationAgent.Warp(navHit.position);
        }
        
        public static readonly List<CivilianBot> AllBots=new List<CivilianBot>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => AllBots.Clear();
        void OnEnable() { if(!AllBots.Contains(this)) AllBots.Add(this); BallisticsSystem.OnShotNoise += HearGunshot; }
        void OnDisable() { AllBots.Remove(this); BallisticsSystem.OnShotNoise -= HearGunshot; }
        void OnDestroy() {AllBots.Remove(this);}
        
        void HearGunshot(Vector3 source,float radius)
        {
            if (actor == null || actor.IsDead || Suspended) return;
            if(Vector3.Distance(transform.position,source)>=Mathf.Min(80f,radius)) return;
            if (isVIP)
            {
                walkSpeed = 3.5f; // VIP accelerates towards extraction when shots ring out
                return;
            }
            PanicFrom(source);
        }

        void PanicFrom(Vector3 source)
        {
            dangerPosition=source;
            currentState=CivilianState.Flee;
            stateTimer=Random.Range(7f,11f);
            targetPos=ChooseSafeTarget(source,18f,42f);
            blockedTime=0;
        }

        Vector3 ChooseSafeTarget(Vector3 danger,float minDistance,float maxDistance)
        {
            Vector3 best=transform.position;
            float bestScore=float.NegativeInfinity;
            foreach(var sector in SectorWorld.LoadedWorlds)
            {
                if(sector==null)continue;
                foreach(var point in sector.SpawnPoints)
                {
                    float travel=Vector3.Distance(transform.position,point);
                    if(travel<minDistance || travel>maxDistance || Mathf.Abs(point.y-transform.position.y)>2f
                        || !SectorWorld.DryFootprint(point,.6f))continue;
                    float away=Vector3.Distance(point,danger)-Vector3.Distance(transform.position,danger);
                    if(away<3f)continue;
                    float score=away-travel*.3f+Random.Range(0f,2f);
                    if(score>bestScore){bestScore=score;best=point;}
                }
            }
            if(bestScore>float.NegativeInfinity)return best;
            // If no street point is safe, avoid inventing a destination in water.
            return transform.position;
        }
        
        void Update()
        {
            if(actor==null || actor.IsDead || Suspended) { SocialPartner=null;CurrentSpeed=0; lastPosition=transform.position; return; }
            
            if (isVIP)
            {
                if (waypoints != null && waypoints.Count > 0)
                {
                    if (waypointIndex < waypoints.Count)
                    {
                        Vector3 targetWp = waypoints[waypointIndex];
                        float dist = Vector3.Distance(transform.position, targetWp);
                        if (dist < 2.5f)
                        {
                            waypointIndex++;
                            if (waypointIndex < waypoints.Count) targetWp = waypoints[waypointIndex];
                        }
                        MoveTowards(targetWp, 1f);
                    }
                    else
                    {
                        CurrentSpeed = 0;
                        MoveTowards(transform.position, 0f);
                    }
                }
                else
                {
                    float dist = Vector3.Distance(transform.position, vipDestination);
                    if (dist > 1.2f) MoveTowards(vipDestination, 1f);
                    else CurrentSpeed = 0;
                }
                return;
            }

            switch (currentState)
            {
                case CivilianState.Idle: UpdateIdle(); break;
                case CivilianState.Patrol: UpdatePatrol(); break;
                case CivilianState.Flee: UpdateFlee(); break;
                case CivilianState.Cower: UpdateCower(); break;
            }
        }
        
        void MoveTowards(Vector3 dest, float currentSpeedMultiplier)
        {
            if(navigationAgent!=null && navigationAgent.isOnNavMesh)
            {
                navigationAgent.nextPosition=transform.position;
                navigationAgent.speed=((currentState==CivilianState.Flee)?fleeSpeed:walkSpeed)*currentSpeedMultiplier;
                bool moving=currentSpeedMultiplier>.01f && (dest-transform.position).sqrMagnitude>.25f;
                if(!moving)
                {
                    if(navDestinationSet) navigationAgent.ResetPath();
                    navDestinationSet=false;
                }
                else
                {
                    if(!navDestinationSet || Time.time>=nextNavRepath || (dest-lastNavDestination).sqrMagnitude>4f)
                    {
                        navigationAgent.SetDestination(dest);
                        lastNavDestination=dest;
                        nextNavRepath=Time.time+.4f;
                        navDestinationSet=true;
                    }
                    if(navigationAgent.hasPath && !navigationAgent.pathPending)
                        dest=navigationAgent.steeringTarget;
                }
            }
            Vector3 direction = dest - transform.position; direction.y = 0;
            float targetSpeed = (currentState == CivilianState.Flee) ? fleeSpeed : walkSpeed;
            targetSpeed *= currentSpeedMultiplier;

            if (direction.sqrMagnitude > 0.01f && targetSpeed > 0.1f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 360);
            
            Vector3 move = Vector3.zero;
            if (direction.magnitude > 0.4f && targetSpeed > 0.1f)
            {
                move = transform.forward * targetSpeed;
            }
            
            vertical = body.isGrounded ? -2 : Mathf.Max(-30, vertical - 18 * Time.deltaTime);
            body.Move((move + Vector3.up * vertical) * Time.deltaTime);
            
            float moved = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(lastPosition.x, 0, lastPosition.z));
            CurrentSpeed = Time.deltaTime > 0 ? moved / Time.deltaTime : 0;
            
            if (move.sqrMagnitude > .01f && moved < .01f) blockedTime += Time.deltaTime; else blockedTime = 0;
            lastPosition = transform.position;
        }

        void UpdateIdle()
        {
            if(Time.time>=nextSocialCheck)
            {
                nextSocialCheck=Time.time+Random.Range(3f,5f);
                SocialPartner=null;
                if(!isVIP)
                    foreach(var other in AllBots)
                        if(other!=this && other!=null && !other.isVIP && !other.Suspended && other.Actor!=null
                            && !other.Actor.IsDead && other.currentState==CivilianState.Idle
                            && (other.transform.position-transform.position).sqrMagnitude<20f)
                        {SocialPartner=other;break;}
            }
            stateTimer -= Time.deltaTime;
            MoveTowards(transform.position, 0f); // Stand still
            if (stateTimer <= 0)
            {
                ChoosePatrolTarget();
                SocialPartner=null;
                currentState = CivilianState.Patrol;
            }
        }
        
        void UpdatePatrol()
        {
            stateTimer -= Time.deltaTime;
            MoveTowards(targetPos, 1f);
            if (Vector3.Distance(transform.position, targetPos) < 2f || (blockedTime > 0.5f) || stateTimer <= 0)
            {
                blockedTime = 0;
                currentState = CivilianState.Idle;
                stateTimer = Random.Range(3f, 8f);
            }
        }
        
        void UpdateFlee()
        {
            stateTimer -= Time.deltaTime;
            MoveTowards(targetPos, 1f);
            
            if (targetPos==transform.position || blockedTime > 1.0f)
            {
                // Got stuck while fleeing, just cower
                currentState = CivilianState.Cower;
                stateTimer = Random.Range(5f, 10f);
            }
            else if (stateTimer <= 0 || Vector3.Distance(transform.position, targetPos) < 2f)
            {
                currentState = CivilianState.Cower;
                stateTimer = Random.Range(5f, 10f);
            }
        }
        
        void UpdateCower()
        {
            stateTimer -= Time.deltaTime;
            MoveTowards(transform.position, 0f); // Stand still
            if (stateTimer <= 0)
            {
                // Recover and resume walking
                currentState = CivilianState.Idle;
                stateTimer = Random.Range(1f, 3f);
            }
        }
        
        void ChoosePatrolTarget()
        {
            targetPos=ChooseSafeTarget(transform.position-transform.forward*5f,8f,30f);
            stateTimer = 15f; // Max time to reach it
        }

        void HandleDeath(CombatActor victim)
        {
            DamageSystem.NotifyCivilianKill(this);
            CurrentSpeed = 0;
            if (navigationAgent != null) navigationAgent.enabled = false;
            if (body != null) body.enabled = false;

            var walk = GetComponent<EnemyWalkAnimator>();
            if (walk != null) walk.enabled = false;

            var col = GetComponent<CapsuleCollider>();
            if (col == null) col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.75f;
            col.radius = 0.30f;
            col.center = Vector3.up * 0.88f;
            col.enabled = true;

            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 65f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            rb.AddForce(-transform.forward * Random.Range(3.5f, 5.0f) + Vector3.up * 1.8f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.Impulse);

            StartCoroutine(SettleCasualty(rb));
        }

        System.Collections.IEnumerator SettleCasualty(Rigidbody rb)
        {
            yield return new WaitForSeconds(2.0f);
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
    }
}
