using UnityEngine;
using UnityEngine.AI;

namespace GeoSniper
{
    public enum AIState { Idle, Patrol, Suspicious, Combat, TakeCover }
    public enum CombatRole { Assault, Suppressor, Flanker, Sniper }
    public enum CoverSubState { Sprinting, Crouching, Peeking }
    
    [DefaultExecutionOrder(100)]
    public sealed partial class EnemyBot : MonoBehaviour
    {
        public Transform player; public float speed=2.5f, detectionRange=65, engagementRange=50, health=100;
        Vector3 patrolTarget,lastPosition,combatTarget; float patrolTime,blockedTime,fireCooldown,vertical,combatTargetTime; int patrolIndex; CombatActor actor; CharacterController body; LineRenderer shotLine;
        float staggerTimer, combatPause;
        float provokedUntil;
        public float patrolRadius=6;
        Vector3 patrolAnchor;
        Vector3 lastSeenPosition;
        float lostSightTime;
        bool playerVisible, cachedVisibility, searching;
        float nextVisionCheck, nextBodyScan, searchTime;
        Vector3 searchAnchor, searchProbe;
        bool hasSearchProbe;
        Quaternion searchFacing;
        AIState lastUpdatedState;
        readonly RaycastHit[] coverPathHits=new RaycastHit[16];
        int combatStrafeSign=1;
        bool combatTargetReady;
        Transform gun, weaponMuzzle; NavMeshAgent navigationAgent;
        Vector3 lastNavDestination;
        float nextNavRepath;
        bool navDestinationSet;
        float aimPitch = 0f;
        Transform spineBone;
        Transform visualRoot;
        Vector3 visualRestLocalPos;
        
        public float detectionProgress = 0f;
        public string AwarenessLabel => IsReloading ? "RELOADING"
            : currentState == AIState.TakeCover ? "TAKING COVER"
            : currentState == AIState.Combat ? (playerVisible ? "SPOTTED" : "SEARCHING")
            : currentState == AIState.Suspicious ? (searching ? "SEARCHING" : "INVESTIGATING")
            : detectionProgress > 0 ? "SUSPICIOUS" : "";
        Camera indicatorCamera; float nextIndicatorCheck; bool indicatorVisible;
        public bool CanShowAwareness(Camera view)
        {
            if(view==null || actor==null || actor.IsDead)return false;
            if(indicatorCamera!=view || Time.unscaledTime>=nextIndicatorCheck)
            {
                indicatorCamera=view;nextIndicatorCheck=Time.unscaledTime+.15f;
                var target=transform.position+Vector3.up*(IsCrouched?1.05f:1.65f);
                var vp=view.WorldToViewportPoint(target);
                indicatorVisible=vp.z>0 && vp.x>0 && vp.x<1 && vp.y>0 && vp.y<1 && (target-view.transform.position).sqrMagnitude<14400;
                if(indicatorVisible && Physics.Linecast(view.transform.position,target,out var hit,~0,QueryTriggerInteraction.Ignore))
                    indicatorVisible=hit.collider.GetComponentInParent<EnemyBot>()==this;
            }
            return indicatorVisible;
        }

        
        public AIState currentState = AIState.Patrol;
        public CombatRole currentRole = CombatRole.Assault;
        public CoverSubState coverSubState = CoverSubState.Sprinting;
        public Vector3 coverTargetPos;
        public float coverTimer;
        Vector3 coverRestPos, coverPeekPos;
        bool returningToCover;
        public bool IsCrouchingInCover => currentState == AIState.TakeCover && coverSubState == CoverSubState.Crouching;
        public bool IsCrouched => currentState == AIState.TakeCover && coverSubState == CoverSubState.Crouching;
        public bool IsSprinting => currentState == AIState.TakeCover && coverSubState == CoverSubState.Sprinting;
        public bool IsAiming => !isDisarmed && !IsReloading && player != null && (currentState == AIState.Combat || (currentState == AIState.TakeCover && coverSubState == CoverSubState.Peeking) || isCounterSniper);
        const int MagazineCapacity=18;
        const float ReloadSeconds=2.0f;
        int magazineRounds=MagazineCapacity;
        float enemyReloadRemaining;
        public bool IsReloading => enemyReloadRemaining>0f;
        public float ReloadProgress => IsReloading ? 1f-enemyReloadRemaining/ReloadSeconds : 0f;
        void StartReload()
        {
            if(IsReloading || actor==null || actor.IsDead || isCounterSniper)return;
            enemyReloadRemaining=ReloadSeconds;
            combatTargetReady=false;
            if(currentState==AIState.Combat && player!=null)
            {
                currentState=AIState.TakeCover;
                stateTimer=Mathf.Max(stateTimer,4f);
                ChooseCoverTarget(lastSeenPosition-transform.position);
            }
            TriggerBark("RELOADING! COVER ME!",2f);
        }
        void TickReload(float delta)
        {
            if(enemyReloadRemaining<=0f || delta<=0f)return;
            enemyReloadRemaining=Mathf.Max(0f,enemyReloadRemaining-delta);
            if(enemyReloadRemaining==0f)magazineRounds=MagazineCapacity;
        }
        public string CombatBark { get; private set; }
        public float BarkTimer { get; private set; }

        static float lastAnyBarkAudioTime = -10f;
        public void TriggerBark(string text, float duration = 2.5f)
        {
            CombatBark = text;
            BarkTimer = duration;

            if (Time.time - lastAnyBarkAudioTime > 8.0f)
            {
                if (RadioCommsChannel.CanTransmit(RadioCommsChannel.Priority.Medium, 2.0f))
                {
                    if(PlayBarkAudio(text))lastAnyBarkAudioTime = Time.time;
                }
            }
        }

        void EnsureEnemyAudio()
        {
            if (enemyRadioAudio == null)
            {
                enemyRadioAudio = gameObject.AddComponent<AudioSource>();
                enemyRadioAudio.spatialBlend = 1f; enemyRadioAudio.minDistance = 3.5f; enemyRadioAudio.maxDistance = 55f;
                enemyRadioAudio.rolloffMode = AudioRolloffMode.Linear; enemyRadioAudio.playOnAwake = false;
            }
        }

        bool PlayBarkAudio(string text)
        {
            AudioClip clip = null;
            string upper = text.ToUpperInvariant();
            if (upper.Contains("AREA CLEAR") || upper.Contains("IN POSITION") || upper.Contains("COMPROMISED")
                || upper.Contains("REPOSITIONING") || upper.Contains("LOST VISUAL") || upper.Contains("MAN DOWN") || upper.Contains("FLANKING"))
                return false; // No matching recorded line: keep the on-screen bark truthful.
            if (upper.Contains("SNIPER") || upper.Contains("COVER") || upper.Contains("INCOMING") || upper.Contains("SUPPRESSING") || upper.Contains("PINNED"))
            {
                clip = ProceduralAudio.LoadAsset(upper.Contains("SUPPRESS") ? "EnemyCombat_1" : "EnemyCombat_0");
            }
            else
            {
                clip = ProceduralAudio.LoadAsset(upper.Contains("HOSTILE") || upper.Contains("SPOTTED") ? "EnemyAlert_2" : "EnemyAlert_1");
            }

            if (clip != null)
            {
                EnsureEnemyAudio();
                float pitch = Random.Range(0.95f, 1.05f);
                return RadioCommsChannel.PlayTransmission(enemyRadioAudio, clip, RadioCommsChannel.Priority.Medium, volume: 0.95f, pitch: pitch, silenceCooldownAfter: 5.0f);
            }
            return false;
        }

        public void RegisterWeapon(Transform gunTransform, Transform muzzleTransform)
        {
            if (gunTransform != null) gun = gunTransform;
            if (muzzleTransform != null) weaponMuzzle = muzzleTransform;
        }
        public float grenadeCooldown = 0f;
        public Vector3 suspiciousLocation;
        public float stateTimer;
        
        public bool Suspended;
        public bool ObservationPatrol;
        public float CurrentSpeed { get; private set; }
        public CombatActor Actor => actor;
        public float tagDurationTimer;
        public bool IsTagged => tagDurationTimer > 0f && actor != null && !actor.IsDead;
        public void TagEnemy(float duration = 45f)
        {
            tagDurationTimer = Mathf.Max(tagDurationTimer, duration);
        }
        public bool isVipAttacker;
        public CivilianBot targetVIP;
        Vector3 vipAmbushAnchor;
        public bool isFugitiveRunner;
        public float runnerStartDelay;
        public Vector3 escapeDestination;
        public System.Collections.Generic.List<Vector3> runnerWaypoints;
        public int runnerWaypointIndex;
        public bool isCounterSniper;
        public bool isDisarmed { get; private set; }
        public bool isHobbled { get; private set; }
        public float CounterSniperLockProgress => isCounterSniper ? Mathf.Clamp01(sniperAimTimer / SniperLock) : 0f;
        public float CounterSniperLockRemaining => isCounterSniper ? Mathf.Max(0f, SniperLock - sniperAimTimer) : 0f;
        public bool IsAimingAtPlayer => isCounterSniper && sniperAimTimer > 0.1f;
        public static EnemyBot ActiveAimingCounterSniper { get; private set; }
        public float suppressionTimer = 0f;
        GameObject scopeGlintObj;
        Light scopeGlintLight;
        // Bodies are a stealth-specific information source. Other contracts do
        // not turn a normal elimination into a global alarm event.
        public bool ReportBodies = true;
        float sniperAimTimer;
        LineRenderer sniperLaser;
        
        public static readonly System.Collections.Generic.List<EnemyBot> AllBots = new System.Collections.Generic.List<EnemyBot>();
        public DifficultyProfile Difficulty;
        [System.NonSerialized] public MissionAlertState Alerts=new MissionAlertState();
        readonly System.Collections.Generic.HashSet<EnemyBot> observedBodies=new System.Collections.Generic.HashSet<EnemyBot>();
        float ReactionScale => Difficulty!=null?Difficulty.reactionTime:1f;
        float DamageScale => Difficulty!=null?Difficulty.enemyDamage:1f;
        float SpeedScale => Difficulty!=null?Difficulty.enemySpeed:1f;
        float SniperLock => Difficulty!=null?Difficulty.counterSniperLockSeconds:2.2f;
        public void SetHealth(float value) { health=value; actor?.Initialize(value*(Difficulty!=null?Difficulty.enemyHealth:1f)); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() { AllBots.Clear(); ActiveAimingCounterSniper = null; }

        public void Initialize(Transform target,int index)
        {
            player=target; patrolIndex=index;
            speed = Random.Range(2.0f, 2.7f);
            patrolRadius = Random.Range(8f, 16f);
            patrolAnchor=transform.position;
            patrolTime=Random.Range(2.5f, 6.0f);
            lastPosition=transform.position;
            // Diverse initial facing & patrol target so bots never walk in single-file line
            float initialAngle = (index * 68f + Random.Range(-25f, 25f)) % 360f;
            transform.rotation = Quaternion.Euler(0, initialAngle, 0);
            patrolTarget = transform.position + transform.forward * Random.Range(6f, 14f);
            if (index % 2 == 1)
            {
                currentState = AIState.Idle;
                stateTimer = Random.Range(1.2f, 3.0f);
            }
            fireCooldown=Random.Range(2.8f,4.5f)*ReactionScale;
            combatPause=Random.Range(1.8f,3.2f)*ReactionScale;
            actor = gameObject.AddComponent<CombatActor>(); SetHealth(health);
            actor.OnDeath += HandleDeath;
            actor.OnDamaged += OnTakeDamage;
            body = GetComponent<CharacterController>();
            if (body == null) body = gameObject.AddComponent<CharacterController>();
            body.height = 1.9f; body.center = Vector3.up * .95f; body.radius = .32f; body.stepOffset = .4f;
            navigationAgent = GetComponent<NavMeshAgent>();
            if (navigationAgent == null) navigationAgent = gameObject.AddComponent<NavMeshAgent>();
            navigationAgent.radius=.32f; navigationAgent.height=1.9f; navigationAgent.speed=speed; navigationAgent.angularSpeed=720; navigationAgent.acceleration=18; navigationAgent.updatePosition=false; navigationAgent.updateRotation=false;
            if(NavMesh.SamplePosition(transform.position,out var navHit,3f,NavMesh.AllAreas)) navigationAgent.Warp(navHit.position);
            shotLine = gameObject.AddComponent<LineRenderer>(); shotLine.positionCount = 2; shotLine.enabled = false; shotLine.startWidth = .012f; shotLine.endWidth = .004f;
            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var tracer = new Material(shader); tracer.color = new Color(1f, .25f, .06f);
                if (tracer.HasProperty("_EmissionColor")) { tracer.EnableKeyword("_EMISSION"); tracer.SetColor("_EmissionColor", new Color(1f, .25f, .06f)); }
                shotLine.sharedMaterial = tracer;
            }
            visualRoot = transform.Find("Visual") ?? (transform.childCount > 0 ? transform.GetChild(0) : null);
            if (visualRoot != null) visualRestLocalPos = visualRoot.localPosition;

            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Enemy weapon muzzle") weaponMuzzle = child;
                string n = child.name.ToLowerInvariant().Replace("_", "").Replace(".", "").Replace(":", "").Replace(" ", "");
                if (spineBone == null && (n.Contains("spine1") || n.Contains("spine01") || n.Contains("chest") || n.Contains("spine") || n.Contains("torso") || n.Contains("upperbody")))
                    spineBone = child;
                if (gun == null && (n.Contains("gun") || n.Contains("weapon") || n.Contains("rifle") || n.Contains("ak47") || n.Contains("m16"))) gun = child;
            }

            enemyAudio = GetComponent<AudioSource>();
            if (enemyAudio == null) enemyAudio = gameObject.AddComponent<AudioSource>();
            enemyAudio.spatialBlend = 1f;
            enemyAudio.minDistance = 3.5f;
            enemyAudio.maxDistance = 55f;
            enemyAudio.rolloffMode = AudioRolloffMode.Linear;
            enemyAudio.playOnAwake = false;
        }
        
        void OnEnable()
        {
            BallisticsSystem.OnShotNoise += HearGunshot; AllBots.Add(this);
            if(actor!=null) actor.OnDamaged+=OnTakeDamage;
        }
        void OnDisable()
        {
            BallisticsSystem.OnShotNoise -= HearGunshot;
            if (actor != null) actor.OnDamaged -= OnTakeDamage;
            if (ActiveAimingCounterSniper == this) ActiveAimingCounterSniper = null;
            AllBots.Remove(this);
        }
        
        public static void NotifyMissedShot(Vector3 muzzle, Vector3 impactPoint)
        {
            Vector3 rayDir = impactPoint - muzzle;
            float rayLen = rayDir.magnitude;
            if (rayLen > 0.001f) rayDir /= rayLen;

            for (int i = 0; i < AllBots.Count; i++)
            {
                var bot = AllBots[i];
                if (bot == null || bot.actor == null || bot.actor.IsDead || bot.Suspended) continue;

                Vector3 botPos = bot.transform.position;
                float distToImpact = Vector3.Distance(botPos, impactPoint);

                float distToTraj = float.MaxValue;
                float proj = Vector3.Dot(botPos - muzzle, rayDir);
                if (proj > 0f && proj < rayLen)
                {
                    Vector3 closestOnRay = muzzle + rayDir * proj;
                    distToTraj = Vector3.Distance(botPos, closestOnRay);
                }

                if (distToImpact < 28f || distToTraj < 14f)
                {
                    PlayRadioChirpAt(botPos);
                    bool needsCover=bot.currentState!=AIState.TakeCover || (bot.suspiciousLocation-muzzle).sqrMagnitude>36f;
                    bot.detectionProgress = 1.0f;
                    bot.suspiciousLocation = muzzle;
                    bot.lastSeenPosition = muzzle;
                    bot.lostSightTime=0;
                    bot.provokedUntil = Time.time + 18f;
                    bot.stateTimer = 10f;
                    bot.currentState = AIState.TakeCover;
                    if(needsCover) bot.ChooseCoverTarget(muzzle - botPos);
                    bot.coverTimer = Mathf.Min(bot.coverTimer, 1.8f);
                    bot.fireCooldown = Mathf.Min(bot.fireCooldown, 1.2f);
                    bot.TriggerBark("SNIPER FIRE! TAKE COVER!", 3.0f);

                    Vector3 faceDir = muzzle - botPos; faceDir.y = 0;
                    if (faceDir.sqrMagnitude > 0.01f)
                        bot.transform.rotation = Quaternion.LookRotation(faceDir);

                    // High-intensity near-miss suppression (bullet passes <= 3.8m or strikes <= 4.2m)
                    if (distToTraj < 3.8f || distToImpact < 4.2f)
                    {
                        bot.suppressionTimer = Random.Range(1.4f, 2.2f);
                        bot.TriggerBark("SNIPER! SPRINT TO COVER!", 3.2f);
                    }
                }
                else if (distToImpact < 45f)
                {
                    // Audible impact thud nearby causes alert suspicion without mission-fail alarms
                    bot.detectionProgress = Mathf.Max(bot.detectionProgress, 0.65f);
                    bot.suspiciousLocation = impactPoint;
                    bot.stateTimer = 6f;
                    bot.currentState = AIState.Suspicious;
                    bot.TriggerBark("INCOMING ROUND!", 2.2f);

                    Vector3 faceDir = impactPoint - botPos; faceDir.y = 0;
                    if (faceDir.sqrMagnitude > 0.01f)
                        bot.transform.rotation = Quaternion.LookRotation(faceDir);
                }
            }
        }

        static float lastRadioChirpTime = -10f;
        public static void PlayRadioChirpAt(Vector3 position)
        {
            if (Time.time - lastRadioChirpTime < 5.0f) return;
            lastRadioChirpTime = Time.time;
            AudioClip clip = ProceduralAudio.CreateRadioChirp();
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position, 0.45f);
            }
        }

        public void AlertNearbyTeammates(Vector3 threatPos, Vector3 casualtyPos)
        {
            PlayRadioChirpAt(casualtyPos);
            TriggerBark("MAN DOWN! GET TO COVER!", 3.2f);
            float alertRadius = 35f;
            foreach (var bot in AllBots)
            {
                if (bot == null || bot == this || bot.actor == null || bot.actor.IsDead || bot.Suspended || bot.Alerts != Alerts) continue;
                float d = Vector3.Distance(bot.transform.position, casualtyPos);
                if (d > alertRadius) continue;

                bot.detectionProgress = 1f;
                bot.suspiciousLocation = threatPos;
                bot.lastSeenPosition = threatPos;
                bot.lostSightTime=0;
                bot.provokedUntil = Time.time + 18f;
                bot.stateTimer = 10f;
                bool needsCover=bot.currentState!=AIState.TakeCover;
                bot.currentState = AIState.TakeCover;
                if(needsCover) bot.ChooseCoverTarget(threatPos - bot.transform.position);

                Vector3 faceDir = threatPos - bot.transform.position; faceDir.y = 0;
                if (faceDir.sqrMagnitude > 0.01f)
                    bot.transform.rotation = Quaternion.LookRotation(faceDir);
            }
        }

        public void BroadcastRadioAlert(Vector3 location, AIState level)
        {
            Alerts.Raise();
            PlayRadioChirpAt(location);
            float maxDist = (level == AIState.Combat) ? 120f : 80f;
            foreach (var bot in AllBots)
            {
                if (bot == null || bot.actor==null || bot.actor.IsDead || bot.Suspended || bot.Alerts!=Alerts || bot.currentState == AIState.Combat) continue;
                if (Vector3.Distance(bot.transform.position, location) > maxDist) continue;
                bot.currentState = level;
                bot.suspiciousLocation = location;
                bot.lastSeenPosition = location;
                bot.lostSightTime=0;
                bot.searching=false;
                bot.provokedUntil = Time.time + 14f;
                bot.stateTimer = 10f;
            }
        }

        public float StaggerProgress => staggerTimer > 0 ? staggerTimer / 0.35f : 0f;
        public Vector3 FlinchDirection { get; private set; }

        public void Disarm()
        {
            if (isDisarmed || actor == null || actor.IsDead) return;
            isDisarmed = true;
            if(shotLine!=null)shotLine.enabled=false;
            if(sniperLaser!=null)sniperLaser.enabled=false;
            if(scopeGlintObj!=null)scopeGlintObj.SetActive(false);
            if(muzzleFlash!=null){muzzleFlash.intensity=0f;muzzleFlashTimer=0f;}
            if(ActiveAimingCounterSniper==this)ActiveAimingCounterSniper=null;
            sniperAimTimer=0f;

            if (gun == null)
            {
                foreach (var child in GetComponentsInChildren<Transform>(true))
                {
                    string n = child.name.ToLowerInvariant();
                    if (n.Contains("enemy tactical rifle") || n.Contains("ak47") || n.Contains("attached_ak47") || n.Contains("rifle") || n.Contains("gun"))
                    {
                        gun = child;
                        break;
                    }
                }
            }

            if (gun != null)
            {
                var anchor = gun.GetComponent<EnemyWeaponAnchor>();
                if (anchor != null) anchor.enabled = false;
                weaponMuzzle = null;
                gun.SetParent(null, true);
                var rb = gun.gameObject.GetComponent<Rigidbody>();
                if (rb == null) rb = gun.gameObject.AddComponent<Rigidbody>();
                rb.mass = 3.2f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var col = gun.gameObject.GetComponent<Collider>();
                if (col == null)
                {
                    var box = gun.gameObject.AddComponent<BoxCollider>();
                    var bounds = ImportedVisual.LocalBounds(gun);
                    box.center = bounds.center;
                    box.size = bounds.size;
                    col = box;
                }
                col.enabled = true;
                rb.AddForce(transform.forward * 2.2f + Vector3.up * 2.5f + Random.insideUnitSphere * 0.4f, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
                if (Application.isPlaying) Destroy(gun.gameObject, 15f);
                gun = null;
            }

            staggerTimer = 0.55f;
            FlinchDirection = player != null ? (transform.position - player.position).normalized : -transform.forward;
            currentState = AIState.TakeCover;
            stateTimer = 12f;
            Vector3 threatPos = lastSeenPosition;
            ChooseCoverTarget(threatPos - transform.position);
        }

        public void Hobble()
        {
            if (isHobbled || actor == null || actor.IsDead) return;
            isHobbled = true;
            speed *= 0.45f;
            if (navigationAgent != null && navigationAgent.isOnNavMesh)
            {
                navigationAgent.speed = speed;
            }
            staggerTimer = 0.7f;
            FlinchDirection = Vector3.down * 0.5f + (player != null ? (transform.position - player.position).normalized * 0.5f : -transform.forward * 0.5f);
            coverSubState = CoverSubState.Crouching;
            stateTimer = 10f;
        }

        public void RegisterAnatomicalHit(DamageInfo info)
        {
            if (actor == null || actor.IsDead) return;

            string part = info.bodyPart != null ? info.bodyPart.ToLowerInvariant() : "";
            if (part.Contains("arm") || part.Contains("hand"))
            {
                Disarm();
            }
            else if (part.Contains("leg") || part.Contains("foot"))
            {
                Hobble();
            }
        }

        void OnTakeDamage(float damage)
        {
            if (actor == null || actor.IsDead || Suspended) return;
            detectionProgress = 1f;
            if(player!=null) { lastSeenPosition=player.position; lostSightTime=0; provokedUntil=Time.time+18f; }
            staggerTimer = 0.35f;
            FlinchDirection = player != null ? (transform.position - player.position).normalized : -transform.forward;
            
            bool needsCover=currentState!=AIState.TakeCover;
            currentState = AIState.TakeCover;
            stateTimer = 8f;
            Vector3 threatPos = lastSeenPosition;
            if(needsCover) ChooseCoverTarget(threatPos - transform.position);
            fireCooldown = Mathf.Min(fireCooldown, 1.2f);
            
            Vector3 faceDir = threatPos - transform.position; faceDir.y = 0;
            if (faceDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(faceDir);

            AlertNearbyTeammates(threatPos, transform.position);
        }

        void HearGunshot(Vector3 source, float radius)
        {
            if (actor == null || actor.IsDead || Suspended || currentState == AIState.Combat) return;
            float dist = Vector3.Distance(transform.position, source);
            if (dist < Mathf.Min(detectionRange * 2.8f, radius))
            {
                bool needsCover=currentState!=AIState.TakeCover || (suspiciousLocation-source).sqrMagnitude>36f;
                suspiciousLocation = source;
                lastSeenPosition = source;
                lostSightTime=0;
                provokedUntil = Time.time + 18f;
                stateTimer = 8f;
                Vector3 aimDir = source - transform.position; aimDir.y = 0;
                if (aimDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(aimDir);

                if (dist < 110f)
                {
                    detectionProgress = Mathf.Max(detectionProgress, 0.95f);
                    currentState = AIState.TakeCover;
                    if(needsCover) ChooseCoverTarget(source - transform.position);
                    coverTimer = Mathf.Min(coverTimer, 1.8f);
                    fireCooldown = Mathf.Min(fireCooldown, 1.2f);
                }
                else
                {
                    detectionProgress = Mathf.Max(detectionProgress, 0.95f);
                    currentState = AIState.Suspicious;
                }
            }
        }
        
        void Update()
        {
            if (tagDurationTimer > 0f) tagDurationTimer -= Time.deltaTime;
            if(actor==null || actor.IsDead || player==null || Suspended) { CurrentSpeed=0; lastPosition=transform.position; return; }
            TickReload(Time.deltaTime);
            if(ObservationPatrol) {currentState=AIState.Patrol;UpdatePatrol();return;}
            
            if (staggerTimer > 0)
            {
                staggerTimer -= Time.deltaTime;
                CurrentSpeed = 0;
                lastPosition = transform.position;
                return; // skip movement and firing while staggered
            }

            if (isFugitiveRunner)
            {
                if(runnerStartDelay>0f)
                {
                    runnerStartDelay=Mathf.Max(0f,runnerStartDelay-Time.deltaTime);
                    MoveTowards(transform.position,0f);
                    return;
                }
                if (runnerWaypoints != null && runnerWaypoints.Count > 0)
                {
                    if (runnerWaypointIndex < runnerWaypoints.Count)
                    {
                        Vector3 targetWp = runnerWaypoints[runnerWaypointIndex];
                        float dist = Vector3.Distance(transform.position, targetWp);
                        if (dist < 2.5f)
                        {
                            runnerWaypointIndex++;
                            if (runnerWaypointIndex < runnerWaypoints.Count) targetWp = runnerWaypoints[runnerWaypointIndex];
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
                    float distToGoal = Vector3.Distance(transform.position, escapeDestination);
                    if (distToGoal > 1.5f) MoveTowards(escapeDestination, 1f);
                    else CurrentSpeed = 0;
                }
                return;
            }

            if (isVipAttacker && targetVIP != null && targetVIP.Actor != null && !targetVIP.Actor.IsDead)
            {
                UpdateVipAttack();
                return;
            }
            
            Vector3 delta=player.position-transform.position; float d=delta.magnitude;
            var urbanPlayer = player.GetComponent<UrbanPlayer>();
            bool isCrouching = urbanPlayer != null && urbanPlayer.IsCrouching;
            Vector3 eyes=transform.position+Vector3.up*1.65f, target=player.position+Vector3.up*(isCrouching ? 0.85f : 1.55f);
            
            float visionRange=Time.time<provokedUntil?Mathf.Max(detectionRange,150f):detectionRange;
            if(Time.time>=nextVisionCheck)
            {
                cachedVisibility=d<visionRange && (!Physics.Linecast(eyes,target,out var hit,~0,QueryTriggerInteraction.Ignore) || hit.transform==player || hit.transform.IsChildOf(player));
                nextVisionCheck=Time.time+.12f+(patrolIndex&3)*.01f;
            }
            bool visible=d<visionRange && cachedVisibility;

            if (isCounterSniper)
            {
                playerVisible=visible;
                if(visible) lastSeenPosition=player.position;
                UpdateCounterSniper(visible, delta, d, eyes, target);
                return;
            }

            // Vision cone check (140 degrees)
            if (visible && currentState!=AIState.Combat && currentState!=AIState.TakeCover && Vector3.Angle(transform.forward, delta) > 70f) visible = false;
            playerVisible=visible;
            if(visible) {lastSeenPosition=player.position;lostSightTime=0;}
            else lostSightTime+=Time.deltaTime;
            UpdateSquadRole(Time.deltaTime);
            if(currentState!=AIState.Combat) flankActive=false;
            
            if (visible && currentState != AIState.Combat && currentState != AIState.TakeCover)
            {
                float previousDetection=detectionProgress;
                detectionProgress=EnemyAwarenessPolicy.Detection(detectionProgress,true,d/visionRange,isCrouching,Difficulty!=null?Difficulty.detectionSpeed:1f,Time.deltaTime);
                if(previousDetection<.35f && detectionProgress>=.35f && currentState!=AIState.Suspicious)
                {
                    currentState=AIState.Suspicious;
                    suspiciousLocation=lastSeenPosition;
                    stateTimer=8f;
                    searching=false;
                    TriggerBark("MOVEMENT! CHECKING IT OUT.",2f);
                }

                if (detectionProgress >= 1f)
                {
                    currentState = AIState.Combat;
                    combatTargetReady=false;
                    combatPause=1.2f*ReactionScale;
                    BroadcastRadioAlert(lastSeenPosition, AIState.Combat);
                }
            }
            else if (!visible && detectionProgress > 0f && currentState != AIState.Combat)
            {
                detectionProgress=EnemyAwarenessPolicy.Detection(detectionProgress,false,1,false,1,Time.deltaTime);
            }
            else if (currentState == AIState.Combat && actor.Health <= actor.maxHealth * 0.35f && stateTimer <= 0)
            {
                currentState = AIState.TakeCover;
                stateTimer = 5f;
                ChooseCoverTarget(lastSeenPosition-transform.position);
            }
            
            if (currentState != AIState.Combat && currentState != AIState.TakeCover && Time.time>=nextBodyScan)
            {
                nextBodyScan=Time.time+.75f;
                foreach (var bot in AllBots)
                {
                    if (ReportBodies && bot != null && bot != this && bot.actor!=null && bot.actor.IsDead && bot.Alerts==Alerts && !observedBodies.Contains(bot))
                    {
                        float distToBody = Vector3.Distance(transform.position, bot.transform.position);
                        if (distToBody < 20f && Vector3.Angle(transform.forward, bot.transform.position - transform.position) < 70f)
                        {
                            if (!Physics.Linecast(eyes, bot.transform.position + Vector3.up * 0.5f,out var corpseHit,~0, QueryTriggerInteraction.Ignore)
                                || corpseHit.transform==bot.transform || corpseHit.transform.IsChildOf(bot.transform))
                            {
                                observedBodies.Add(bot);
                                currentState = AIState.Suspicious;
                                suspiciousLocation = bot.transform.position;
                                stateTimer = 8f;
                                BroadcastRadioAlert(bot.transform.position, AIState.Suspicious);
                                break;
                            }
                        }
                    }
                }
            }

            if(currentState!=AIState.Suspicious || lastUpdatedState!=AIState.Suspicious) searching=false;
            lastUpdatedState=currentState;
            switch (currentState)
            {
                case AIState.Idle: UpdateIdle(); break;
                case AIState.Patrol: UpdatePatrol(); break;
                case AIState.Suspicious: UpdateSuspicious(); break;
                case AIState.Combat: UpdateCombat(visible, delta, d, eyes, target); break;
                case AIState.TakeCover: UpdateTakeCover(); break;
            }
            
            bool isCrouched = (currentState == AIState.TakeCover && coverSubState == CoverSubState.Crouching);
            if (body != null)
            {
                body.height = Mathf.Lerp(body.height, isCrouched ? 1.15f : 1.9f, Time.deltaTime * 8f);
                body.center = Vector3.Lerp(body.center, isCrouched ? Vector3.up * 0.58f : Vector3.up * 0.95f, Time.deltaTime * 8f);
            }
            if (visualRoot != null)
            {
                bool hasArmyAnim = GetComponent<ArmyAnimation>() != null && GetComponent<ArmyAnimation>().Ready;
                Vector3 targetLocalPos = (isCrouched && !hasArmyAnim) ? visualRestLocalPos + Vector3.down * 0.55f : visualRestLocalPos;
                visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, targetLocalPos, Time.deltaTime * 8f);
            }
            if (BarkTimer > 0f) BarkTimer -= Time.deltaTime;

            // Fade muzzle flash
            if(muzzleFlash!=null && muzzleFlashTimer>0)
            {
                muzzleFlashTimer-=Time.deltaTime;
                muzzleFlash.intensity=Mathf.Lerp(0,3.5f,Mathf.Max(0,muzzleFlashTimer)/.06f);
            }
        }
        
        void MoveTowards(Vector3 targetPos, float moveSpeedMultiplier = 1f)
        {
            moveSpeedMultiplier*=SpeedScale;
            if(navigationAgent!=null && navigationAgent.isOnNavMesh)
            {
                navigationAgent.nextPosition = transform.position;
                navigationAgent.speed=speed*moveSpeedMultiplier;
                bool moving=moveSpeedMultiplier>.01f && (targetPos-transform.position).sqrMagnitude>.25f;
                if(!moving)
                {
                    if(navDestinationSet) navigationAgent.ResetPath();
                    navDestinationSet=false;
                }
                else
                {
                    // SetDestination queues pathfinding. Repeating it every frame for
                    // every bot caused Android stalls during movement and alerts.
                    if(!navDestinationSet || Time.time>=nextNavRepath || (targetPos-lastNavDestination).sqrMagnitude>4f)
                    {
                        navigationAgent.SetDestination(targetPos);
                        lastNavDestination=targetPos;
                        nextNavRepath=Time.time+.35f+(patrolIndex&3)*.05f;
                        navDestinationSet=true;
                    }
                    if(navigationAgent.hasPath && !navigationAgent.pathPending)
                        targetPos=navigationAgent.steeringTarget;
                }
            }
            Vector3 direction = targetPos - transform.position; direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 6f);
            }
            
            Vector3 move = Vector3.zero;
            if (direction.magnitude > 0.5f)
            {
                float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, direction.normalized) + 0.3f);
                move = transform.forward * speed * moveSpeedMultiplier * alignment;
            }
            
            bool unsafeStep=move.sqrMagnitude>0 && !SafeStep(transform.position+move.normalized*.8f);
            if(unsafeStep)
            {
                Vector3 detour1 = Quaternion.Euler(0, 40, 0) * move;
                Vector3 detour2 = Quaternion.Euler(0, -40, 0) * move;
                if (SafeStep(transform.position + detour1.normalized * .8f)) move = detour1 * 0.85f;
                else if (SafeStep(transform.position + detour2.normalized * .8f)) move = detour2 * 0.85f;
                else move = Vector3.zero;
            }
            vertical=body.isGrounded?-2:Mathf.Max(-30,vertical-18*Time.deltaTime);
            body.Move((move+Vector3.up*vertical)*Time.deltaTime);
            
            float moved = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(lastPosition.x, 0, lastPosition.z));
            float rawSpeed = Time.deltaTime > 0 ? moved / Time.deltaTime : 0;
            CurrentSpeed = Mathf.Lerp(CurrentSpeed, rawSpeed, Time.deltaTime * 8f);
            
            if (unsafeStep || (move.sqrMagnitude > .01f && moved < .01f)) blockedTime += Time.deltaTime; else blockedTime = 0;
            lastPosition = transform.position;
        }

        static float lastSquadChatterTime = -10f;
        void CheckPatrolChatter()
        {
            if (currentState != AIState.Idle && currentState != AIState.Patrol) return;
            if (player == null || actor == null || actor.IsDead || Suspended) return;

            float d = Vector3.Distance(transform.position, player.position);
            if (d > 28f) return;

            if (Time.time - lastSquadChatterTime < 32f) return;

            if (!RadioCommsChannel.CanTransmit(RadioCommsChannel.Priority.Low, 3.0f)) return;

            lastSquadChatterTime = Time.time + Random.Range(4f, 16f);
            EnsureEnemyAudio();
            if (enemyRadioAudio != null)
            {
                var clip = RadioCommsChannel.GetNextPatrolChatterClip();
                if (clip != null)
                {
                    float pitch = Random.Range(0.95f, 1.05f);
                    RadioCommsChannel.PlayTransmission(enemyRadioAudio, clip, RadioCommsChannel.Priority.Low, volume: 0.82f, pitch: pitch, silenceCooldownAfter: 15f);
                }
            }
        }

        void UpdateIdle()
        {
            CheckPatrolChatter();
            stateTimer -= Time.deltaTime;
            MoveTowards(transform.position); // Stand still
            if (stateTimer <= 0)
            {
                ChoosePatrolTarget();
                currentState = AIState.Patrol;
            }
        }
        
        void UpdatePatrol()
        {
            CheckPatrolChatter();
            patrolTime -= Time.deltaTime;
            float speedMult = Alerts.Level > 0 ? 0.65f : 0.45f;
            MoveTowards(patrolTarget, speedMult);
            if (Vector3.Distance(transform.position, patrolTarget) < 1f || blockedTime > 1.2f || patrolTime<=0)
            {
                blockedTime = 0;
                if (Alerts.Level > 0)
                {
                    ChoosePatrolTarget();
                }
                else
                {
                    currentState = AIState.Idle;
                    stateTimer = Random.Range(.5f, 1.4f);
                }
            }
        }
        
        void UpdateSuspicious()
        {
            if(playerVisible) { suspiciousLocation=lastSeenPosition; stateTimer=8f; searching=false; }
            if(searching && (searchAnchor-suspiciousLocation).sqrMagnitude>4f) searching=false;
            if(!searching)
            {
                stateTimer-=Time.deltaTime;
                MoveTowards(suspiciousLocation,.65f);
                if(Vector3.Distance(transform.position,suspiciousLocation)>2f && blockedTime<=1.2f && stateTimer>0) return;
                blockedTime=0;
                searching=true;
                searchAnchor=suspiciousLocation;
                searchFacing=transform.rotation;
                searchTime=EnemyAwarenessPolicy.SearchDuration;
                hasSearchProbe=TryChooseSearchProbe(out searchProbe);
                TriggerBark("LOST VISUAL. SEARCHING.",2.5f);
            }
            searchTime-=Time.deltaTime;
            if(hasSearchProbe && searchTime<EnemyAwarenessPolicy.SearchDuration-1.3f && searchTime>1.1f
                && Vector3.Distance(transform.position,searchProbe)>1f && blockedTime<.6f)
            {
                MoveTowards(searchProbe,.55f);
                return;
            }
            MoveTowards(transform.position,0);
            float sweep=Mathf.Sin((EnemyAwarenessPolicy.SearchDuration-searchTime)*1.8f)*65f;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,searchFacing*Quaternion.Euler(0,sweep,0),Time.deltaTime*95f);
            if(searchTime<=0)
            {
                searching=false;
                detectionProgress=0;
                currentState=AIState.Patrol;
                ChoosePatrolTarget();
                TriggerBark("AREA CLEAR. RESUMING PATROL.",2f);
            }
        }

        bool TryChooseSearchProbe(out Vector3 probe)
        {
            probe=transform.position;
            Vector3 forward=searchAnchor-transform.position;
            forward.y=0f;
            if(forward.sqrMagnitude<.01f)forward=transform.forward;
            forward.Normalize();
            float side=(patrolIndex++&1)==0?1f:-1f;
            for(int i=0;i<4;i++)
            {
                Vector3 direction=Quaternion.Euler(0f,side*(55f+i*35f),0f)*forward;
                Vector3 candidate=transform.position+direction*3f;
                if(!SafeStep(candidate) || !EnemyCoverGeometry.Reachable(transform.position,candidate,transform,coverPathHits))continue;
                probe=candidate;
                return true;
            }
            return false;
        }

        void UpdateTakeCover()
        {
            stateTimer -= Time.deltaTime;
            fireCooldown -= Time.deltaTime;

            if(!playerVisible && lostSightTime>EnemyAwarenessPolicy.LostSightGrace+3f)
            {
                currentState=AIState.Suspicious; suspiciousLocation=lastSeenPosition; stateTimer=6f; searching=false; return;
            }
            switch (coverSubState)
            {
                case CoverSubState.Sprinting:
                    MoveTowards(coverTargetPos, 1.85f);
                    coverTimer -= Time.deltaTime;
                    if (BarkTimer <= 0f && Random.value < 0.05f) TriggerBark("TAKING COVER!", 2.2f);

                    float distToCover = Vector3.Distance(transform.position, coverTargetPos);
                    if (distToCover < 1.35f || blockedTime > 1.5f || coverTimer <= 0f)
                    {
                        if(!EnemyCoverGeometry.Protected(transform.position,lastSeenPosition+Vector3.up*1.65f,transform,player))
                        {
                            currentState=isDisarmed?AIState.Suspicious:AIState.Combat;
                            suspiciousLocation=lastSeenPosition; stateTimer=6f;
                            searching=false; blockedTime=0; TriggerBark("COVER COMPROMISED!",2f); break;
                        }
                        blockedTime = 0;
                        coverSubState = CoverSubState.Crouching;
                        coverRestPos=transform.position;
                        returningToCover=false;
                        coverTimer = suppressionTimer > 0f ? 0.8f : Random.Range(1.2f, 2.2f);
                        TriggerBark(suppressionTimer > 0f ? "PINNED DOWN!" : "IN POSITION!", 2.5f);
                    }
                    break;

                case CoverSubState.Crouching:
                    if(returningToCover && Vector3.Distance(transform.position,coverRestPos)>0.65f)
                    {
                        if(blockedTime>=.8f){LeaveUnusableCover();break;}
                        MoveTowards(coverRestPos,.8f);
                        break;
                    }
                    returningToCover=false;
                    MoveTowards(transform.position, 0f);
                    if(IsReloading){coverTimer=Mathf.Max(coverTimer,.5f);break;}
                    if (player != null)
                    {
                        Vector3 faceDir = lastSeenPosition - transform.position; faceDir.y = 0;
                        if (faceDir.sqrMagnitude > 0.01f)
                            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), Time.deltaTime * 5f);
                    }

                    if (suppressionTimer > 0f)
                    {
                        suppressionTimer -= Time.deltaTime;
                        coverTimer = Mathf.Max(coverTimer, 0.7f);
                    }
                    coverTimer -= Time.deltaTime;

                    if (coverTimer <= 0f)
                    {
                        if(TryChooseCoverPeek(out coverPeekPos))
                        {
                            coverSubState = CoverSubState.Peeking;
                            coverTimer = Random.Range(2.2f, 3.5f);
                        }
                        else LeaveUnusableCover();
                    }
                    break;

                case CoverSubState.Peeking:
                    if(Vector3.Distance(transform.position,coverPeekPos)>0.65f)
                    {
                        MoveTowards(coverPeekPos,.85f);
                        coverTimer-=Time.deltaTime;
                        if(blockedTime>.8f || coverTimer<=0f)LeaveUnusableCover();
                        break;
                    }
                    MoveTowards(transform.position,0f);
                    if (player != null)
                    {
                        Vector3 faceDir = lastSeenPosition - transform.position; faceDir.y = 0;
                        if (faceDir.sqrMagnitude > 0.01f)
                            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(faceDir), Time.deltaTime * 360f);

                        float distance=Vector3.Distance(transform.position,player.position);
                        float fireRange=Time.time<provokedUntil?Mathf.Max(engagementRange,140f):engagementRange;
                        Vector3 targetEye=player.position+Vector3.up*((player.GetComponent<UrbanPlayer>()?.IsCrouching??false)?.85f:1.55f);
                        bool clearShot=playerVisible && distance<fireRange
                            && EnemyCoverGeometry.ClearShot(transform.position+Vector3.up*1.65f,targetEye,transform,player,coverPathHits)
                            && EnemyCoverGeometry.ClearShot(transform.position+Vector3.up*1.35f,targetEye,transform,player,coverPathHits);
                        if (fireCooldown <= 0f && !IsReloading && clearShot)
                        {
                            fireCooldown = Random.Range(2.8f, 4.2f) * ReactionScale;
                            StartCoroutine(ExecuteBurst(targetEye, distance));
                        }
                    }
                    coverTimer -= Time.deltaTime;
                    if (coverTimer <= 0f || stateTimer <= 0f)
                    {
                        if(stateTimer<=0f)LeaveUnusableCover();
                        else
                        {
                            coverSubState=CoverSubState.Crouching;
                            returningToCover=true;
                            coverTimer=Random.Range(1.1f,1.8f);
                        }
                    }
                    break;
            }
        }

        bool TryChooseCoverPeek(out Vector3 peek)
        {
            peek=transform.position;
            if(player==null)return false;
            Vector3 remembered=playerVisible?player.position:lastSeenPosition;
            Vector3 toward=remembered-transform.position;toward.y=0f;
            if(toward.sqrMagnitude<.01f)return false;
            Vector3 side=Vector3.Cross(Vector3.up,toward.normalized);
            Vector3 targetEye=remembered+Vector3.up*(playerVisible && (player.GetComponent<UrbanPlayer>()?.IsCrouching??false)?.85f:1.55f);
            float sign=(patrolIndex++&1)==0?1f:-1f;
            for(int i=0;i<6;i++)
            {
                float distance=1.2f+(i/2)*1.1f;
                Vector3 candidate=transform.position+side*(sign*((i&1)==0?1f:-1f)*distance);
                if(!SafeStep(candidate) || !EnemyCoverGeometry.Reachable(transform.position,candidate,transform,coverPathHits))continue;
                if(!EnemyCoverGeometry.ClearShot(candidate+Vector3.up*1.65f,targetEye,transform,player,coverPathHits))continue;
                if(!EnemyCoverGeometry.ClearShot(candidate+Vector3.up*1.35f,targetEye,transform,player,coverPathHits))continue;
                peek=candidate;
                return true;
            }
            return false;
        }

        void LeaveUnusableCover()
        {
            currentState=isDisarmed?AIState.Suspicious:AIState.Combat;
            suspiciousLocation=lastSeenPosition;
            searching=false;
            returningToCover=false;
            stateTimer=6f;
            combatTargetReady=false;
            combatPause=.35f*ReactionScale;
            blockedTime=0f;
            TriggerBark("COVER COMPROMISED!",2f);
        }
        
        void ChooseCoverTarget(Vector3 deltaToPlayer)
        {
            coverSubState = CoverSubState.Sprinting;
            Vector3 playerEyePos = transform.position + deltaToPlayer + Vector3.up * 1.65f;
            Vector3 awayFromPlayer = -deltaToPlayer.normalized; awayFromPlayer.y = 0;
            if (awayFromPlayer.sqrMagnitude < 0.01f) awayFromPlayer = -transform.forward;
            else awayFromPlayer.Normalize();

            Vector3 bestCoverPos = transform.position;
            float bestScore = float.MaxValue;
            bool foundCover = false;

            // Search multi-radius walkable ground points around the enemy
            float[] testRadii = Application.isMobilePlatform ? new float[] { 2.5f, 5.5f, 9f } : new float[] { 2.5f, 4.5f, 7.0f, 10.0f };
            int angleSteps = Application.isMobilePlatform ? 8 : 16;

            for (int r = 0; r < testRadii.Length; r++)
            {
                float radius = testRadii[r];
                for (int a = 0; a < angleSteps; a++)
                {
                    float angle = a * (360f / angleSteps);
                    Vector3 testDir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                    float playerDot = Vector3.Dot(testDir, awayFromPlayer);

                    Vector3 candidate = transform.position + testDir * radius;
                    candidate.y = transform.position.y;

                    // 1. Must be on walkable, safe floor/roof (never fall off edge)
                    if (!SafeStep(candidate)) continue;

                    if(!EnemyCoverGeometry.Reachable(transform.position,candidate,transform,coverPathHits)) continue;
                    bool blocksLOS=EnemyCoverGeometry.Protected(candidate,playerEyePos,transform,player);
                    if(!blocksLOS) continue;

                    float distFromBot = Vector3.Distance(transform.position, candidate);
                    bool clumped = false;
                    for (int b = 0; b < AllBots.Count; b++)
                    {
                        var other = AllBots[b];
                        if (other != null && other != this && other.actor != null && !other.actor.IsDead)
                        {
                            if (Vector3.Distance(other.transform.position, candidate) < 2.2f ||
                                (other.currentState == AIState.TakeCover && Vector3.Distance(other.coverTargetPos, candidate) < 2.0f))
                            {
                                clumped = true;
                                break;
                            }
                        }
                    }

                    if(clumped) continue;
                    float score = distFromBot 
                                + (blocksLOS ? 0f : 45f) 
                                + (clumped ? 30f : 0f)
                                - (playerDot * 6f);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCoverPos = candidate;
                        foundCover = true;
                    }
                }

                // If we found a great cover position that completely breaks sniper line of sight, take it!
                if (foundCover && bestScore < 30f) break;
            }

            // Fallback: If no candidate blocked LOS, find any safe step 2m away from line of fire
            if (!foundCover)
            {
                for (int a = 0; a < 8; a++)
                {
                    Vector3 fallbackDir = Quaternion.Euler(0, a * 45f, 0) * awayFromPlayer;
                    Vector3 testPos = transform.position + fallbackDir * 2.2f;
                    testPos.y = transform.position.y;
                    if (SafeStep(testPos) && EnemyCoverGeometry.Reachable(transform.position,testPos,transform,coverPathHits))
                    {
                        bestCoverPos = testPos;
                        break;
                    }
                }
            }

            if(!foundCover)
            {
                TriggerBark("NO COVER! REPOSITIONING.",2f);
            }
            coverTargetPos = bestCoverPos;
            patrolTarget = bestCoverPos;
            coverTimer = foundCover?Random.Range(1.2f, 2.2f):.75f;
            blockedTime = 0f;
        }

        void ChooseCombatStrafeTarget(Vector3 deltaToPlayer)
        {
            Vector3 flat=deltaToPlayer; flat.y=0;
            if(flat.sqrMagnitude<.01f) return;
            flat.Normalize();
            combatStrafeSign = (patrolIndex++ % 2 == 0) ? 1 : -1;
            Vector3 side=Vector3.Cross(Vector3.up,flat);
            Vector3 forwardOffset=flat*Random.Range(-1.5f,1.5f);
            combatTarget=transform.position;
            for(int attempt=0;attempt<2;attempt++)
            {
                float sign=attempt==0?combatStrafeSign:-combatStrafeSign;
                Vector3 candidate=transform.position+side*(sign*Random.Range(4f,7f))+forwardOffset;
                candidate.y=transform.position.y;
                if(!SafeStep(candidate) || !EnemyCoverGeometry.Reachable(transform.position,candidate,transform,coverPathHits))continue;
                bool reserved=false;
                foreach(var ally in AllBots)
                {
                    if(ally==null || ally==this || ally.Alerts!=Alerts || ally.actor==null || ally.actor.IsDead)continue;
                    if((ally.currentState==AIState.Combat && ally.combatTargetReady && (ally.combatTarget-candidate).sqrMagnitude<6.25f)
                        || (ally.transform.position-candidate).sqrMagnitude<4f){reserved=true;break;}
                }
                if(reserved)continue;
                combatTarget=candidate;
                break;
            }
            combatTargetReady=true;
            combatTargetTime=Random.Range(2.5f,4f);
            blockedTime=0;
        }

        void UpdateCombat(bool visible, Vector3 delta, float d, Vector3 eyes, Vector3 target)
        {
            if(IsReloading){MoveTowards(transform.position,0f);return;}
            if (isDisarmed)
            {
                currentState = AIState.TakeCover;
                stateTimer = 10f;
                Vector3 threatPos = lastSeenPosition;
                ChooseCoverTarget(threatPos - transform.position);
                return;
            }

            fireCooldown -= Time.deltaTime;
            stateTimer -= Time.deltaTime;
            flankCooldown=Mathf.Max(0,flankCooldown-Time.deltaTime);
            if(UpdateFlank(visible,Time.deltaTime))return;
            if (!visible)
            {
                MoveTowards(lastSeenPosition, 0.8f);
                if (blockedTime > 0.5f || lostSightTime > EnemyAwarenessPolicy.LostSightGrace) { currentState = AIState.Suspicious; stateTimer = 6f; suspiciousLocation = lastSeenPosition; }
                return;
            }
            
            // Aim towards player
            Vector3 aimDir = delta; aimDir.y = 0;
            if (aimDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aimDir), Time.deltaTime * 360f);

            if (grenadeCooldown > 0) grenadeCooldown -= Time.deltaTime;

            bool coveringAdvance=currentRole==CombatRole.Suppressor && HasAdvancingTeammate();
            if (currentRole == CombatRole.Suppressor && !coveringAdvance && grenadeCooldown <= 0 && d > 15f && d < 40f)
            {
                grenadeCooldown = Random.Range(10f, 15f);
                StartCoroutine(ThrowGrenade(lastSeenPosition));
                return;
            }

            if(coveringAdvance)
            {
                combatTargetReady=false;
                MoveTowards(transform.position);
            }
            if (!coveringAdvance && !combatTargetReady && combatPause <= 0)
            {
                ChooseCombatStrafeTarget(delta);
                Vector3 flat = delta; flat.y = 0;
                if (d > 45 || d < 12)
                {
                    var candidate = transform.position + flat.normalized * (d > 45 ? 6f : -4f);
                    if (SafeStep(candidate) && !Physics.Linecast(eyes, candidate + Vector3.up * 1.65f, ~0, QueryTriggerInteraction.Ignore)) combatTarget = candidate;
                }
            }
            if (!coveringAdvance && combatTargetReady && combatPause <= 0)
            {
                MoveTowards(combatTarget, 0.75f);
                combatTargetTime -= Time.deltaTime;
                if (Vector3.Distance(transform.position, combatTarget) < 1.1f || combatTargetTime <= 0 || blockedTime > 0.35f)
                {
                    combatTargetReady = false;
                    combatPause = Random.Range(0.6f, 1.2f)*ReactionScale;
                }
            }
            else
            {
                MoveTowards(transform.position);
                combatPause -= Time.deltaTime;
            }

            float fireRange=Time.time<provokedUntil?Mathf.Max(engagementRange,140f):engagementRange;
            if (d < fireRange && fireCooldown <= 0 && !IsReloading && Vector3.Angle(transform.forward, aimDir) < 32f)
            {
                float fireRateMult = coveringAdvance ? 0.5f : (currentRole == CombatRole.Suppressor) ? 0.7f : 1f;
                fireCooldown = Random.Range(3.0f, 5.0f) * fireRateMult * ReactionScale;
                combatPause = Random.Range(1.8f, 3.2f) * ReactionScale;
                StartCoroutine(ExecuteBurst(target, d));
            }
        }

        System.Collections.IEnumerator ThrowGrenade(Vector3 targetPos)
        {
            combatPause = 1.5f*ReactionScale;
            if (enemyAudio == null) { enemyAudio = gameObject.AddComponent<AudioSource>(); enemyAudio.spatialBlend = 1; enemyAudio.maxDistance = 80; enemyAudio.rolloffMode = AudioRolloffMode.Linear; enemyAudio.volume = 1f; }
            
            // Spawn grenade
            GameObject grenadeObj = null;
            var grenadePrefab = ModelLibrary.Load("Grenade");
            if (grenadePrefab != null && grenadePrefab.Length > 0)
            {
                grenadeObj = Instantiate(grenadePrefab[0]);
                var gMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                gMat.color = new Color(0.22f, 0.28f, 0.18f);
                gMat.SetFloat("_Metallic", 0.75f);
                gMat.SetFloat("_Glossiness", 0.35f);
                foreach (var r in grenadeObj.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.sharedMaterial == null || r.sharedMaterial.mainTexture == null || r.sharedMaterial.color == Color.white)
                        r.sharedMaterial = gMat;
                }
            }
            else
            {
                grenadeObj = new GameObject("TacticalGrenade");
                var mf = grenadeObj.AddComponent<MeshFilter>();
                var mr = grenadeObj.AddComponent<MeshRenderer>();
                var gMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                gMat.color = new Color(0.22f, 0.28f, 0.18f);
                mr.sharedMaterial = gMat;
                var m = new Mesh { name = "GrenadeMesh" };
                m.vertices = new Vector3[] { Vector3.up * 0.1f, Vector3.down * 0.1f, Vector3.left * 0.08f, Vector3.right * 0.08f, Vector3.forward * 0.08f, Vector3.back * 0.08f };
                m.triangles = new int[] { 0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 4, 2, 1, 3, 4, 1, 5, 3, 1, 2, 5 };
                m.RecalculateNormals();
                mf.sharedMesh = m;
            }
            grenadeObj.transform.position = transform.position + Vector3.up * 1.5f + transform.forward * 0.5f;
            
            var rb = grenadeObj.GetComponent<Rigidbody>();
            if (rb == null) rb = grenadeObj.AddComponent<Rigidbody>();
            rb.mass = 1f; rb.interpolation = RigidbodyInterpolation.Interpolate;
            
            Vector3 throwDir = (targetPos - grenadeObj.transform.position);
            float dist = throwDir.magnitude;
            throwDir.y = 0; throwDir.Normalize();
            throwDir.y = 1f; 
            rb.linearVelocity = throwDir * Mathf.Min(dist * 0.7f, 18f);
            
            yield return new WaitForSeconds(3.0f);
            
            if (grenadeObj != null)
            {
                Vector3 blastPos = grenadeObj.transform.position;
                Destroy(grenadeObj);
                
                // Damage in radius if line of sight is not blocked by solid walls
                if (player != null && Vector3.Distance(player.position, blastPos) < 6f)
                {
                    Vector3 playerCenter = player.position + Vector3.up * 1.0f;
                    if (!Physics.Linecast(blastPos, playerCenter, out var hit, ~0, QueryTriggerInteraction.Ignore) || hit.transform == player || hit.transform.IsChildOf(player))
                    {
                        player.GetComponent<CombatActor>()?.Damage(40f*DamageScale, blastPos);
                    }
                }
            }
        }

        System.Collections.IEnumerator ExecuteBurst(Vector3 target, float d)
        {
            int burstCount = (currentRole == CombatRole.Suppressor) ? Random.Range(4, 7) : Random.Range(2, 4);
            for (int b = 0; b < burstCount; b++)
            {
                if (actor == null || actor.IsDead || player == null || Suspended || isDisarmed || IsFlanking || IsReloading || (currentState!=AIState.Combat && !(currentState==AIState.TakeCover && coverSubState==CoverSubState.Peeking) && !isCounterSniper)) yield break;
                if(magazineRounds<=0){StartReload();yield break;}
                var eyes = transform.position + Vector3.up * 1.65f;
                target = player.position + Vector3.up * ((player.GetComponent<UrbanPlayer>()?.IsCrouching??false)?.85f:1.55f);
                d=Vector3.Distance(transform.position,player.position);
                bool blocked = false;
                Vector3 impactPt = target;
                if (!EnemyCoverGeometry.ClearShot(eyes,target,transform,player,coverPathHits)
                    || !EnemyCoverGeometry.ClearShot(transform.position+Vector3.up*1.35f,target,transform,player,coverPathHits))
                {
                    yield break;
                }
                magazineRounds--;

                GetComponent<ArmyAnimation>()?.Fire();

                if (muzzleFlash == null)
                {
                    var flashObj = new GameObject("Enemy muzzle flash");
                    if (weaponMuzzle != null)
                    {
                        flashObj.transform.SetParent(weaponMuzzle, false);
                    }
                    else if (gun != null)
                    {
                        flashObj.transform.SetParent(gun, false);
                        flashObj.transform.localPosition = new Vector3(0.58f, 0f, 0f);
                    }
                    else
                    {
                        flashObj.transform.SetParent(transform, false);
                        flashObj.transform.localPosition = Vector3.up * 1.35f + Vector3.forward * .65f;
                    }
                    muzzleFlash = flashObj.AddComponent<Light>();
                    muzzleFlash.type = LightType.Point; muzzleFlash.range = 12; muzzleFlash.color = new Color(1f, .7f, .25f);
                }
                muzzleFlash.intensity = 4.0f; muzzleFlashTimer = .06f;
                var muzzle = (weaponMuzzle != null) ? weaponMuzzle.position : (gun != null ? gun.position : muzzleFlash.transform.position);
                shotLine.SetPosition(0, muzzle);
                shotLine.SetPosition(1, Vector3.MoveTowards(muzzle, impactPt, Mathf.Min(3, d * .35f)));
                shotLine.enabled = true;
                StartCoroutine(HideShot());

                if (blocked)
                {
                    SniperPresentation.SpawnImpactSparks(impactPt);
                }
                else
                {
                    Vector3 scatter = Random.insideUnitSphere * 1.5f;
                    float hitChance = Mathf.Lerp(0.55f, 0.12f, Mathf.InverseLerp(10f, 65f, d));
                    if (currentRole == CombatRole.Sniper) hitChance = Mathf.Lerp(0.75f, 0.35f, Mathf.InverseLerp(20f, 150f, d));
                    
                    bool directHit = Random.value < hitChance;
                    if (directHit)
                    {
                        var hp = player.GetComponent<CombatActor>();
                        if (hp != null) hp.Damage(Random.Range(4f, 7.5f) * Mathf.Min(1.2f, DamageScale), transform.position);
                    }
                    else
                    {
                        SniperPresentation.SpawnImpactSparks(target + scatter);
                        // Suppression effect: bullet whizzing past player causes a slight flinch
                        var uPlayer = player.GetComponent<UrbanPlayer>();
                        if (uPlayer != null && !uPlayer.Scoped) 
                        {
                            uPlayer.ApplyRecoil(Random.Range(20f, 60f));
                        }
                    }
                }

                if (enemyAudio == null) { enemyAudio = gameObject.AddComponent<AudioSource>(); enemyAudio.spatialBlend = 1; enemyAudio.maxDistance = 80; enemyAudio.rolloffMode = AudioRolloffMode.Linear; enemyAudio.volume = .35f; }
                if (sharedEnemyShotClip == null) sharedEnemyShotClip = BuildEnemyShotClip();
                enemyAudio.PlayOneShot(sharedEnemyShotClip);

                if(magazineRounds==0)StartReload();

                yield return new WaitForSeconds(0.08f);
            }
        }

        public void ConfigureAsCounterSniper()
        {
            try
            {
                isCounterSniper = true;
                currentRole = CombatRole.Sniper;
                detectionRange = 220f;
                engagementRange = 200f;
                health = 100f;
                SetHealth(health);
                if (sniperLaser == null)
                {
                    var laserObj = new GameObject("SniperLaser");
                    laserObj.transform.SetParent(transform, false);
                    sniperLaser = laserObj.AddComponent<LineRenderer>();
                    sniperLaser.positionCount = 2;
                    sniperLaser.startWidth = 0.025f;
                    sniperLaser.endWidth = 0.015f;
                    sniperLaser.enabled = false;
                    var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                    if (sh != null)
                    {
                        if (cachedLaserMat == null)
                        {
                            cachedLaserMat = new Material(sh);
                            cachedLaserMat.color = new Color(1f, 0.08f, 0.08f, 0.85f);
                        }
                        sniperLaser.sharedMaterial = cachedLaserMat;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[GeoSniper] ConfigureAsCounterSniper recovered: " + ex.Message);
            }
        }

        public void ConfigureAsVipAmbusher(CivilianBot vip, float initialDelay = 2.0f)
        {
            try
            {
                isVipAttacker = true;
                targetVIP = vip;
                vipAmbushAnchor = transform.position;
                detectionRange = 140f;
                engagementRange = 65f;
                speed = 3.8f;
                fireCooldown = initialDelay;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[GeoSniper] ConfigureAsVipAmbusher recovered: " + ex.Message);
            }
        }

        public void RelocateVipAmbushPost(Vector3 position)
        {
            transform.position = position;
            vipAmbushAnchor = position;
        }

        void UpdateScopeGlint(Vector3 muzzle, Vector3 target, bool active)
        {
            if (active)
            {
                if (scopeGlintObj == null)
                {
                    scopeGlintObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    scopeGlintObj.name = "CounterSniperScopeGlint";
                    var col = scopeGlintObj.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    scopeGlintObj.transform.SetParent(transform, true);

                    var mr = scopeGlintObj.GetComponent<MeshRenderer>();
                    var sh = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Unlit/Color");
                    if (sh != null)
                    {
                        var mat = new Material(sh);
                        mat.color = new Color(1f, 0.94f, 0.70f, 0.95f);
                        mr.sharedMaterial = mat;
                    }
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    scopeGlintLight = scopeGlintObj.AddComponent<Light>();
                    scopeGlintLight.type = LightType.Point;
                    scopeGlintLight.range = 10f;
                    scopeGlintLight.color = new Color(1f, 0.94f, 0.65f);
                }

                scopeGlintObj.SetActive(true);
                scopeGlintObj.transform.position = muzzle + (target - muzzle).normalized * 0.15f;
                if (Camera.main != null) scopeGlintObj.transform.LookAt(Camera.main.transform);
                else scopeGlintObj.transform.LookAt(target);

                float pulse = 1f + 0.35f * Mathf.Sin(Time.time * 24f);
                scopeGlintObj.transform.localScale = Vector3.one * (1.1f * pulse);
                if (scopeGlintLight != null) scopeGlintLight.intensity = 5.5f * pulse;
            }
            else
            {
                if (scopeGlintObj != null && scopeGlintObj.activeSelf)
                    scopeGlintObj.SetActive(false);
            }
        }

        void UpdateCounterSniper(bool visible, Vector3 delta, float d, Vector3 eyes, Vector3 target)
        {
            CurrentSpeed = 0;
            Vector3 aimDir = delta; aimDir.y = 0;
            if (aimDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aimDir), Time.deltaTime * 180f);

            fireCooldown -= Time.deltaTime;

            if (!visible || d > engagementRange || isDisarmed)
            {
                if (sniperLaser != null) sniperLaser.enabled = false;
                sniperAimTimer = 0f;
                UpdateScopeGlint(transform.position, target, false);
                if (ActiveAimingCounterSniper == this) ActiveAimingCounterSniper = null;
                return;
            }

            sniperAimTimer += Time.deltaTime;
            ActiveAimingCounterSniper = this;
            var muzzle = (weaponMuzzle != null) ? weaponMuzzle.position : (gun != null ? gun.position : eyes);

            // Counter-Sniper Scope Glint Warning: Flash optic flare 1.2s before firing
            bool showGlint = sniperAimTimer >= Mathf.Max(0.1f, SniperLock - 1.25f);
            UpdateScopeGlint(muzzle, target, showGlint);

            if (sniperLaser != null)
            {
                sniperLaser.enabled = true;
                sniperLaser.SetPosition(0, muzzle);
                float wobble = Mathf.Max(0f, (SniperLock - sniperAimTimer) * 0.35f);
                Vector3 laserEnd = target + Random.insideUnitSphere * wobble;
                sniperLaser.SetPosition(1, laserEnd);
                float t = Mathf.Clamp01(sniperAimTimer / SniperLock);
                sniperLaser.startWidth = Mathf.Lerp(0.012f, 0.038f, t);
                sniperLaser.endWidth = Mathf.Lerp(0.008f, 0.022f, t);
            }

            if (sniperAimTimer >= SniperLock && fireCooldown <= 0f)
            {
                UpdateScopeGlint(muzzle, target, false);
                ExecuteCounterSniperShot(target, d);
                sniperAimTimer = 0f;
                fireCooldown = Random.Range(2.8f, 3.8f)*ReactionScale;
            }
        }

        void ExecuteCounterSniperShot(Vector3 target, float d)
        {
            if (isDisarmed) return;
            GetComponent<ArmyAnimation>()?.Fire();
            if (muzzleFlash != null)
            {
                muzzleFlash.intensity = 6.0f;
                muzzleFlashTimer = 0.12f;
            }

            var urbanPlayer = player != null ? player.GetComponent<UrbanPlayer>() : null;
            bool isCrouching = urbanPlayer != null && urbanPlayer.IsCrouching;

            float hitChance = Mathf.Lerp(0.68f, 0.42f, Mathf.InverseLerp(30f, 180f, d));
            bool isHit = Random.value < hitChance;

            // Player dodge window: If player crouched / ducked during lock warning, round misses with loud supersonic crack!
            if (isCrouching)
            {
                isHit = false;
                var mission = FindObjectOfType<UrbanCombatMission>();
                if (mission != null) mission.NotifySniperShotDodged();
            }

            Vector3 shotDestination = isHit ? target : (isCrouching ? player.position + Vector3.up * 1.85f + Random.insideUnitSphere * 0.35f : target + Random.insideUnitSphere * Random.Range(1.2f, 2.5f));

            var muzzle = (weaponMuzzle != null) ? weaponMuzzle.position : (gun != null ? gun.position : transform.position + Vector3.up * 1.65f);
            shotLine.SetPosition(0, muzzle);
            shotLine.SetPosition(1, shotDestination);
            shotLine.enabled = true;
            StartCoroutine(HideShot());

            if (enemyAudio == null) { enemyAudio = gameObject.AddComponent<AudioSource>(); enemyAudio.spatialBlend = 1; enemyAudio.maxDistance = 200; enemyAudio.rolloffMode = AudioRolloffMode.Linear; enemyAudio.volume = 0.85f; }
            if (sharedEnemyShotClip == null) sharedEnemyShotClip = BuildEnemyShotClip();
            enemyAudio.pitch = 0.72f;
            enemyAudio.PlayOneShot(sharedEnemyShotClip);
            enemyAudio.pitch = 1.0f;

            if (Physics.Linecast(muzzle, shotDestination, out var hit, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == player || hit.transform.IsChildOf(player))
                {
                    if (isHit)
                    {
                        var hp = player.GetComponent<CombatActor>();
                        if (hp != null) hp.Damage(Random.Range(22f, 32f) * Mathf.Min(1.2f, DamageScale), transform.position);
                    }
                    else
                    {
                        SniperPresentation.SpawnImpactSparks(hit.point);
                        if (isCrouching && Camera.main != null)
                        {
                            var camAudio = Camera.main.GetComponent<AudioSource>();
                            if (camAudio != null) camAudio.PlayOneShot(ProceduralAudio.CreateRicochetMiss(), 0.95f);
                        }
                    }
                }
                else
                {
                    SniperPresentation.SpawnImpactSparks(hit.point);
                    if (isCrouching && Camera.main != null)
                    {
                        var camAudio = Camera.main.GetComponent<AudioSource>();
                        if (camAudio != null) camAudio.PlayOneShot(ProceduralAudio.CreateRicochetMiss(), 0.95f);
                    }
                }
            }
            else
            {
                if (isHit)
                {
                    var hp = player.GetComponent<CombatActor>();
                    if (hp != null) hp.Damage(Random.Range(35f, 50f)*DamageScale);
                }
                else
                {
                    SniperPresentation.SpawnImpactSparks(shotDestination);
                    if (isCrouching && Camera.main != null)
                    {
                        var camAudio = Camera.main.GetComponent<AudioSource>();
                        if (camAudio != null) camAudio.PlayOneShot(ProceduralAudio.CreateRicochetMiss(), 0.95f);
                    }
                }
            }
        }

        void UpdateVipAttack()
        {
            if(isDisarmed)
            {
                currentState=AIState.TakeCover;
                MoveTowards(coverTargetPos,1f);
                return;
            }
            if (targetVIP == null || targetVIP.Actor == null || targetVIP.Actor.IsDead) return;

            Vector3 delta = targetVIP.transform.position - transform.position;
            float d = delta.magnitude;
            Vector3 eyes = transform.position + Vector3.up * 1.65f + transform.forward * 0.5f;
            Vector3 target = targetVIP.transform.position + Vector3.up * 1.1f;
            int mask = ~(1 << 2);
            bool blocked = Physics.Linecast(eyes, target, out var hit, mask, QueryTriggerInteraction.Ignore)
                && hit.transform != targetVIP.transform && !hit.transform.IsChildOf(targetVIP.transform)
                && hit.transform != transform && !hit.transform.IsChildOf(transform);
            bool visible = d < 70f && !blocked;

            fireCooldown -= Time.deltaTime;

            Vector3 aimDir = delta; aimDir.y = 0;
            if (aimDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aimDir), Time.deltaTime * 360f);

            // Guard a section of the route instead of chasing the VIP's exact
            // position. A short peek is enough to clear nearby cover, and the
            // leash prevents the whole ambush squad from forming one pile.
            Vector3 postDelta = transform.position - vipAmbushAnchor;
            postDelta.y = 0f;
            if (postDelta.sqrMagnitude > 36f)
                MoveTowards(vipAmbushAnchor, 0.8f);
            else if (!visible && d < 65f)
            {
                Vector3 peek = targetVIP.transform.position - vipAmbushAnchor;
                peek.y = 0f;
                Vector3 peekPoint = vipAmbushAnchor + (peek.sqrMagnitude > 0.01f ? peek.normalized * 4f : Vector3.zero);
                if (SafeStep(peekPoint)) MoveTowards(peekPoint, 0.55f);
                else MoveTowards(transform.position, 0f);
            }
            else MoveTowards(transform.position, 0f);

            if (!isDisarmed && fireCooldown <= 0f && visible && d <= 55f)
            {
                if (Vector3.Angle(transform.forward, aimDir) < 40f)
                {
                    fireCooldown = Random.Range(4.0f, 5.5f) * ReactionScale;
                    StartCoroutine(ExecuteBurstAtVIP(targetVIP, d));
                }
            }
        }

        System.Collections.IEnumerator ExecuteBurstAtVIP(CivilianBot vip, float d)
        {
            int burstCount = Random.Range(2, 4);
            for (int b = 0; b < burstCount; b++)
            {
                if (actor == null || actor.IsDead || isDisarmed || IsReloading || vip == null || vip.Actor == null || vip.Actor.IsDead || Suspended) yield break;
                var eyes = transform.position + Vector3.up * 1.65f + transform.forward * 0.5f;
                var target = vip.transform.position + Vector3.up * 1.1f;
                int mask = ~(1 << 2);
                if (Physics.Linecast(eyes, target, out var obstruction, mask, QueryTriggerInteraction.Ignore)
                    && obstruction.transform != vip.transform && !obstruction.transform.IsChildOf(vip.transform)
                    && obstruction.transform != transform && !obstruction.transform.IsChildOf(transform)) yield break;

                GetComponent<ArmyAnimation>()?.Fire();

                if (muzzleFlash == null)
                {
                    var flashObj = new GameObject("Enemy muzzle flash");
                    if (weaponMuzzle != null) flashObj.transform.SetParent(weaponMuzzle, false);
                    else if (gun != null) { flashObj.transform.SetParent(gun, false); flashObj.transform.localPosition = new Vector3(0.58f, 0f, 0f); }
                    else { flashObj.transform.SetParent(transform, false); flashObj.transform.localPosition = Vector3.up * 1.35f + Vector3.forward * .65f; }
                    muzzleFlash = flashObj.AddComponent<Light>();
                    muzzleFlash.type = LightType.Point; muzzleFlash.range = 12; muzzleFlash.color = new Color(1f, .7f, .25f);
                }
                muzzleFlash.intensity = 4.0f; muzzleFlashTimer = .06f;
                var muzzle = (weaponMuzzle != null) ? weaponMuzzle.position : (gun != null ? gun.position : muzzleFlash.transform.position);
                shotLine.SetPosition(0, muzzle);
                shotLine.SetPosition(1, Vector3.MoveTowards(muzzle, target, Mathf.Min(3, d * .35f)));
                shotLine.enabled = true;
                StartCoroutine(HideShot());

                Vector3 scatter = Random.insideUnitSphere * 0.35f;
                bool directHit = Random.value > 0.50f;
                if (directHit)
                {
                    vip.Actor.Damage(Random.Range(6f, 10f) * Mathf.Min(1.1f, DamageScale), transform.position);
                }
                else
                {
                    SniperPresentation.SpawnImpactSparks(target + scatter);
                }

                if (enemyAudio == null) { enemyAudio = gameObject.AddComponent<AudioSource>(); enemyAudio.spatialBlend = 1; enemyAudio.maxDistance = 80; enemyAudio.rolloffMode = AudioRolloffMode.Linear; enemyAudio.volume = .45f; }
                if (sharedEnemyShotClip == null) sharedEnemyShotClip = BuildEnemyShotClip();
                enemyAudio.PlayOneShot(sharedEnemyShotClip);

                yield return new WaitForSeconds(0.11f);
            }
        }

        Light muzzleFlash; float muzzleFlashTimer;
        AudioSource enemyAudio, enemyRadioAudio;
        static AudioClip sharedEnemyShotClip;
        static Material cachedLaserMat;
        static AudioClip BuildEnemyShotClip()
        {
            const int rate=22050; int len=(int)(.35f*rate); var samples=new float[len]; var rng=new System.Random(77);
            for(int i=0;i<len;i++) { float t=i/(float)rate,n=(float)rng.NextDouble()*2-1; samples[i]=n*.7f*Mathf.Exp(-t*40)+Mathf.Sin(t*2*Mathf.PI*65)*.3f*Mathf.Exp(-t*12); }
            var clip=AudioClip.Create("Enemy shot",len,1,rate,false); clip.SetData(samples,0); return clip;
        }
        System.Collections.IEnumerator HideShot()
        {
            yield return new WaitForSeconds(.06f);
            if(shotLine!=null) shotLine.enabled=false;
        }
        public void Damage(float amount)
        { 
            actor?.Damage(amount); 
            if (actor != null && !actor.IsDead) 
            {
                staggerTimer = 0.4f;
                if (!isFugitiveRunner && currentState != AIState.Combat && !actor.IsDead)
                {
                    // Enter combat
                    currentState = AIState.Combat;
                    stateTimer = 12f;
                    combatTargetReady = false;
                    combatPause = 0.5f*ReactionScale;
                    grenadeCooldown = Random.Range(3f, 8f);
                    
                    squadRoleTimer=0; // The same squad assignment applies to sight, radio and damage alerts.
                }
            }
        }
        
        void HandleDeath(CombatActor actor)
        {
            if (ActiveAimingCounterSniper == this) ActiveAimingCounterSniper = null;
            if (scopeGlintObj != null) scopeGlintObj.SetActive(false);
            if (sniperLaser != null) sniperLaser.enabled = false;
            if (navigationAgent != null) navigationAgent.enabled = false;
            if (body != null) body.enabled = false;

            var walk = GetComponent<EnemyWalkAnimator>();
            if (walk != null) walk.enabled = false;

            Vector3 threatPos = lastSeenPosition;
            if (currentState == AIState.Combat)
            {
                AlertNearbyTeammates(threatPos, transform.position);
            }
            else
            {
                // Stealth assassination: only alert teammates within 12m if they have direct line of sight to the casualty dropping
                foreach (var ally in AllBots)
                {
                    if (ally == null || ally == this || ally.actor == null || ally.actor.IsDead || ally.Suspended || ally.Alerts != Alerts) continue;
                    float d = Vector3.Distance(ally.transform.position, transform.position);
                    if (d <= 12f)
                    {
                        Vector3 eyePos = ally.transform.position + Vector3.up * 1.6f;
                        Vector3 targetEye = transform.position + Vector3.up * 1.2f;
                        if (!Physics.Linecast(eyePos, targetEye, ~(1 << 2), QueryTriggerInteraction.Ignore))
                        {
                            ally.suspiciousLocation = transform.position;
                            ally.stateTimer = 8f;
                            ally.currentState = AIState.Suspicious;
                            PlayRadioChirpAt(transform.position);
                            break;
                        }
                    }
                }
            }

            Vector3 impactDir = player != null ? (transform.position - player.position).normalized : -transform.forward;
            impactDir.y = 0;
            if (impactDir.sqrMagnitude < 0.01f) impactDir = -transform.forward;

            // Trigger physical skeletal ragdoll on EnemyWalkAnimator so body folds realistically over railings
            bool ragdollActive = walk != null && walk.EnablePhysicalRagdoll(impactDir, transform.position);

            if (!ragdollActive)
            {
                // Ground physical collider so character lands on rooftop/street rather than sinking through
                var col = GetComponent<CapsuleCollider>();
                if (col == null) col = gameObject.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.radius = 0.32f;
                col.center = Vector3.up * 0.9f;
                col.enabled = true;

                var rb = GetComponent<Rigidbody>();
                if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
                rb.mass = 75f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // Heavy ballistic impact momentum: backward force + torso torque
                rb.AddForce(impactDir * Random.Range(260f, 380f) + Vector3.up * Random.Range(70f, 120f), ForceMode.Impulse);
                Vector3 torqueAxis = Vector3.Cross(impactDir, Vector3.up) + Random.insideUnitSphere * 0.4f;
                rb.AddTorque(torqueAxis * Random.Range(160f, 280f), ForceMode.Impulse);
                StartCoroutine(SettleCasualty(rb));
            }

            DamageSystem.NotifyKill(this);
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
        void ChoosePatrolTarget()
        {
            patrolTarget=transform.position;
            float baseAngle = Random.Range(-180f, 180f);
            for(int attempt=0;attempt<16;attempt++)
            {
                float angle = baseAngle + attempt * (360f / 16f);
                Vector3 direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
                float dist = Random.Range(4f, Mathf.Max(patrolRadius, 10f));
                var candidate=patrolAnchor+direction*dist;
                candidate.y=transform.position.y;
                if(!SafeStep(candidate)) continue;
                if(Physics.Linecast(transform.position+Vector3.up*.8f,candidate+Vector3.up*.8f,~0,QueryTriggerInteraction.Ignore)) continue;
                patrolTarget=candidate; break;
            }
            blockedTime=0;
            patrolTime=Random.Range(3.5f, 7.5f);
        }
        bool SafeStep(Vector3 point)
        {
            if(SectorWorld.WaterAt(point))return false;
            if(!Physics.Raycast(point+Vector3.up*.85f,Vector3.down,out var support,1.6f,~0,QueryTriggerInteraction.Ignore)) return false;
            return support.collider.gameObject.name!="water" && support.normal.y>.45f && !support.transform.IsChildOf(transform)
                && Mathf.Abs(support.point.y-transform.position.y)<.85f;
        }
        public void InitializePatrol(){ patrolAnchor=transform.position; ChoosePatrolTarget(); lastPosition=transform.position; }
        void LateUpdate()
        {
            if (actor == null || actor.IsDead || Suspended) return;

            bool isAiming = IsAiming;
            if (isAiming)
            {
                Vector3 eyePos = transform.position + Vector3.up * 1.55f;
                Vector3 targetPos = (playerVisible?player.position:lastSeenPosition) + Vector3.up * 1.35f;
                Vector3 dir = targetPos - eyePos;
                float flatDist = new Vector2(dir.x, dir.z).magnitude;
                if (flatDist > 0.4f)
                {
                    float desiredPitch = Mathf.Atan2(dir.y, flatDist) * Mathf.Rad2Deg;
                    desiredPitch = Mathf.Clamp(desiredPitch, -35f, 45f);
                    aimPitch = Mathf.Lerp(aimPitch, desiredPitch, Time.deltaTime * 10f);
                }
            }
            else
            {
                aimPitch = Mathf.Lerp(aimPitch, 0f, Time.deltaTime * 6f);
            }

            if (Mathf.Abs(aimPitch) > 0.5f)
            {
                float spineFlex = Mathf.Clamp(aimPitch * 0.35f, -8f, 10f);
                if (spineBone != null)
                {
                    spineBone.rotation = Quaternion.AngleAxis(-spineFlex, transform.right) * spineBone.rotation;
                }
                if (gun != null && gun.GetComponent<EnemyWeaponAnchor>() == null)
                {
                    gun.rotation = Quaternion.AngleAxis(-(aimPitch - spineFlex), transform.right) * gun.rotation;
                }
            }
        }

        void OnDestroy()
        {
            if(shotLine!=null && shotLine.sharedMaterial!=null)
            { if(Application.isPlaying) Destroy(shotLine.sharedMaterial); else DestroyImmediate(shotLine.sharedMaterial); }
            if (scopeGlintObj != null) Destroy(scopeGlintObj);
            // Counter-snipers share cachedLaserMat; removing one must not erase other lasers.
        }
    }
}
