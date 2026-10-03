using UnityEngine;

namespace GeoSniper
{
    [DefaultExecutionOrder(-100)]
    public sealed class UrbanPlayer : MonoBehaviour
    {
        public float walkSpeed=5, sprintSpeed=9, jumpSpeed=7.2f;
        Vector3 vaultBoost; float vaultTimer;
        CharacterController body; Camera view; float vertical; float yaw; bool scope, touchSprint;
        public static UrbanPlayer Instance { get; private set; }
        public bool BinocularsMode { get; private set; }
        public float BinocularZoom { get; private set; } = 6f;
        public void ToggleBinoculars() => SetBinoculars(!BinocularsMode);
        public void SetBinoculars(bool active)
        {
            BinocularsMode = active;
            if (BinocularsMode) scope = false;
        }
        public CombatActor Health { get; private set; }
        public MobileCombatInput Controls { get; private set; }
        public float SpeedMetersPerSecond { get; private set; }
        public bool Sprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsHoldingBreath { get; private set; }
        public string MovementLabel => climbing ? "CLIMB" : IsCrouching ? "CROUCH" : Sprinting ? "SPRINT" : "WALK";
        float pitch;
        bool jumpQueued;
        float landingPunch, landingSpeedMultiplier = 1f, prevVertical;
        bool wasGrounded = true;
        public bool InputBlocked;
        bool climbing;
        SectorWorld.LadderRoute nearby;
        bool descend;
        Vector3 lastGrounded;
        Vector3 lastDryPosition;
        bool hasDryPosition;
        float groundedTimer;
        AndroidGyroAim gyroAim;
        public void Initialize(Camera camera)
        {
            Instance = this;
            view = camera != null ? camera : Camera.main;
            if (view == null)
            {
                var camObj = new GameObject("Player Main Camera");
                camObj.tag = "MainCamera";
                view = camObj.AddComponent<Camera>();
            }
            body = gameObject.GetComponent<CharacterController>();
            if (body == null) body = gameObject.AddComponent<CharacterController>();
            body.height = 1.8f; body.radius = .35f; body.center = Vector3.up * .9f;
            body.stepOffset = .45f; body.slopeLimit = 50f; body.skinWidth = .08f;
            Health = gameObject.GetComponent<CombatActor>();
            if (Health == null) Health = gameObject.AddComponent<CombatActor>();
            Health.canRegenerate = true;
            // Start on the rooftop traversal platform; the player can descend the generated stair route.
            transform.position = new Vector3(0, 10.1f, -105);
            if (view != null)
            {
                view.transform.SetParent(transform);
                view.transform.localPosition = new Vector3(0, 1.55f, 0);
                view.transform.localRotation = Quaternion.identity;
                view.fieldOfView = 65;
                view.allowHDR = true;
                MobilePostProcess.Ensure(view);
            }
            yaw = 180; pitch = 0;
            transform.rotation = Quaternion.Euler(0, yaw, 0); lastGrounded = transform.position;
            Controls = gameObject.GetComponent<MobileCombatInput>() ?? gameObject.AddComponent<MobileCombatInput>();
            gyroAim = gameObject.GetComponent<AndroidGyroAim>() ?? gameObject.AddComponent<AndroidGyroAim>();

            footstepAudio = gameObject.GetComponent<AudioSource>();
            if (footstepAudio == null) footstepAudio = gameObject.AddComponent<AudioSource>();
            footstepAudio.playOnAwake = false;
            footstepAudio.spatialBlend = 0f;

            if (dustParticleSystem == null)
            {
                var dustObj = new GameObject("PlayerFootstepDust");
                dustObj.transform.SetParent(transform, false);
                dustObj.transform.localPosition = Vector3.zero;
                dustParticleSystem = dustObj.AddComponent<ParticleSystem>();
                var dMain = dustParticleSystem.main;
                dMain.loop = false;
                dMain.playOnAwake = false;
                dMain.simulationSpace = ParticleSystemSimulationSpace.World;
                dMain.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.95f);
                dMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                dMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
                dMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.68f, 0.62f, 0.52f, 0.28f));
                dMain.maxParticles = 80;

                var dEmission = dustParticleSystem.emission;
                dEmission.enabled = false;

                var dShape = dustParticleSystem.shape;
                dShape.shapeType = ParticleSystemShapeType.Circle;
                dShape.radius = 0.25f;
                dShape.rotation = new Vector3(90f, 0f, 0f);

                var dSize = dustParticleSystem.sizeOverLifetime;
                dSize.enabled = true;
                dSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.8f));

                var dColor = dustParticleSystem.colorOverLifetime;
                dColor.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(0.7f, 0.65f, 0.55f), 0f), new GradientColorKey(new Color(0.6f, 0.55f, 0.48f), 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.28f, 0.15f), new GradientAlphaKey(0f, 1f) }
                );
                dColor.color = grad;

                var dRend = dustObj.GetComponent<ParticleSystemRenderer>();
                var particleShader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
                if (particleShader != null)
                {
                    dRend.material = new Material(particleShader);
                }
            }
        }
        float stepDistance;
        AudioSource footstepAudio;
        ParticleSystem dustParticleSystem;
        float recoilPitch;
        float recoilVelocity;
        float recoilYaw;
        float recoilRoll;
        float recoilYawVelocity;
        float recoilRollVelocity;
        float cameraBank;

        public void ApplyRecoil(float magnitude)
        {
            recoilVelocity += magnitude;
            recoilYawVelocity += magnitude * UnityEngine.Random.Range(-0.25f, 0.25f);
            recoilRollVelocity += magnitude * UnityEngine.Random.Range(-0.2f, 0.2f);
        }

        void Update()
        {
            SpeedMetersPerSecond = 0;
            bool blocked = body == null || (Health != null && Health.IsDead) || BallisticsSystem.isBulletCamActive || InputBlocked || climbing;
            if (blocked) { gyroAim?.Read(false, true); return; }
            nearby=null;
            foreach(var ladder in SectorWorld.Ladders)
            {
                if(ladder.Owner==null) continue;
                bool atTop=Vector3.Distance(transform.position,ladder.Landing)<7f;
                bool atBottom=Vector3.Distance(transform.position,ladder.Bottom)<5f;
                if((atTop || atBottom) && Mathf.Abs(transform.position.y-(atTop?ladder.Landing.y:ladder.Bottom.y))<3f)
                { nearby=ladder; descend=atTop; break; }
            }
            // Jump must remain a jump even beside a ladder. Climbing is explicit:
            // desktop uses E and mobile uses the contextual climb prompt below.
            if(nearby!=null && Input.GetKeyDown(KeyCode.E)) { StartCoroutine(Climb(nearby,descend)); return; }
            bool mobile=Application.isMobilePlatform || Input.touchCount>0;
            if((!mobile && Input.GetMouseButtonDown(1)) || (Controls != null && Controls.ScopePressed))
            {
                if (BinocularsMode) SetBinoculars(false);
                else scope = !scope;
            }

            if (Input.GetKeyDown(KeyCode.B)) ToggleBinoculars();

            if (BinocularsMode)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    BinocularZoom = Mathf.Clamp(BinocularZoom + scroll * 10f, 3f, 18f);
                }
            }
            
            // The player owns stance; all input devices issue the same command.
            if (Input.GetKeyDown(KeyCode.C) || (Controls != null && Controls.CrouchPressed))
                SetCrouching(!IsCrouching);
            if (Controls != null && Controls.Sprint && IsCrouching) SetCrouching(false);

            bool sprint = !scope && !BinocularsMode && (Input.GetKey(KeyCode.LeftShift) || touchSprint || (Controls != null && Controls.Sprint)) && !IsCrouching;
            if (sprint)
            {
                IsCrouching = false;
                if (Controls != null) Controls.IsCrouching = false;
            }
            Sprinting = sprint;

            if (body != null)
            {
                body.height = Mathf.Lerp(body.height, IsCrouching ? 1.0f : 1.8f, Time.deltaTime * 12f);
                body.center = Vector3.up * Mathf.Lerp(body.center.y, IsCrouching ? 0.5f : 0.9f, Time.deltaTime * 12f);
            }
            landingPunch = Mathf.MoveTowards(landingPunch, 0f, Time.deltaTime * 3.2f);
            if (view != null)
            {
                float eyeY = (IsCrouching ? 0.85f : 1.55f) - landingPunch;
                view.transform.localPosition = Vector3.Lerp(view.transform.localPosition, new Vector3(0, eyeY, 0), Time.deltaTime * 14f);
                if (BinocularsMode)
                {
                    view.fieldOfView = Mathf.Lerp(view.fieldOfView, 65f / BinocularZoom, Time.deltaTime * 14f);
                }
            }
            
            // Handle Hold Breath
            if (Controls != null) Controls.ShowHoldBreath = scope;
            IsHoldingBreath = scope && (Input.GetKey(KeyCode.LeftShift) || (Controls != null && Controls.HoldBreathHeld));
            // WeaponSway owns stamina and heartbeat playback, including exhaustion.

            Vector2 input=mobile?Controls.Move:Vector2.ClampMagnitude(new Vector2(Input.GetAxis("Horizontal"),Input.GetAxis("Vertical")),1);
            Vector2 look = mobile ? Controls.Look * (scope ? .06f : .13f) : ((Controls != null && Controls.IsPointerOverUI) ? Controls.Look * (scope ? .06f : .13f) : new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * (scope ? .65f : 2.2f));
            if (BinocularsMode) look *= (1f / Mathf.Max(1f, BinocularZoom * 0.35f));
            float lookSensitivity=PlayerPrefs.GetFloat("GeoSniper.LookSens",1f);
            float sensitivity=scope?PlayerPrefs.GetFloat("GeoSniper.ScopeSens",lookSensitivity):lookSensitivity;
            look*=Mathf.Clamp(sensitivity,.3f,2.5f);
            if (gyroAim != null) look += gyroAim.Read(scope, false);
            if (PlayerPrefs.GetInt("GeoSniper.InvertAimY",0)==1) look.y=-look.y;
            yaw+=look.x; pitch=Mathf.Clamp(pitch-look.y,-80,80);
            transform.rotation=Quaternion.Euler(0,yaw,0); 
            
            float recoilDecay = Mathf.Exp(-30f * Time.deltaTime);
            recoilPitch += recoilVelocity * (1f - recoilDecay) / 30f;
            recoilVelocity *= recoilDecay;
            recoilPitch *= Mathf.Exp(-4f * Time.deltaTime);

            recoilYaw += recoilYawVelocity * (1f - recoilDecay) / 30f;
            recoilYawVelocity *= recoilDecay;
            recoilYaw *= Mathf.Exp(-6f * Time.deltaTime);

            recoilRoll += recoilRollVelocity * (1f - recoilDecay) / 30f;
            recoilRollVelocity *= recoilDecay;
            recoilRoll *= Mathf.Exp(-6f * Time.deltaTime);

            float targetBank = !scope ? Mathf.Clamp(-input.x * 2.4f - look.x * 0.32f, -3.5f, 3.5f) : 0f;
            cameraBank = Mathf.Lerp(cameraBank, targetBank, Time.deltaTime * 9.5f);
            if (view != null) view.transform.localRotation = Quaternion.Euler(pitch - recoilPitch, recoilYaw, recoilRoll + cameraBank);
            landingSpeedMultiplier = Mathf.MoveTowards(landingSpeedMultiplier, 1f, Time.deltaTime * 2.2f);
            float speed=(IsCrouching?2.2f:(sprint?sprintSpeed:walkSpeed)) * landingSpeedMultiplier;
            Vector3 move=(transform.right*input.x+transform.forward*input.y)*speed;
            if (vaultTimer > 0f)
            {
                vaultTimer -= Time.deltaTime;
                move += vaultBoost;
            }
            // Debounce isGrounded when moving on surfaces so flat roofs don't flicker.
            // When jumping (vertical > 0.1f), player is actively airborne so grounded is strictly false.
            if (vertical > 0.1f)
            {
                groundedTimer = 0f;
            }
            else if (body.isGrounded)
            {
                groundedTimer = 0.12f;
            }
            else
            {
                groundedTimer = Mathf.MoveTowards(groundedTimer, 0, Time.deltaTime);
            }
            bool grounded = groundedTimer > 0 && vertical <= 0.1f;
            if (!wasGrounded && grounded && prevVertical < -8.5f)
            {
                float severity = Mathf.Clamp01((-prevVertical - 8.5f) / 14f);
                landingPunch = severity * 0.42f;
                landingSpeedMultiplier = Mathf.Lerp(1f, 0.55f, severity);
                var speaker = view != null ? view.GetComponent<AudioSource>() : null;
                if (speaker != null) speaker.PlayOneShot(ProceduralAudio.CreateBodyHitThud(), 0.85f * severity);
                AndroidHitHaptics.ConfirmHit();
            }
            wasGrounded = grounded;
            prevVertical = vertical;
            if(grounded)
            {
                vertical = -1f;
                if(Input.GetKeyDown(KeyCode.Space)||(Controls!=null && Controls.JumpPressed)||jumpQueued)
                {
                    vertical = jumpSpeed;
                    groundedTimer = 0f;
                    if(IsCrouching)
                    {
                        IsCrouching = false;
                        if(Controls != null) Controls.IsCrouching = false;
                    }

                    // Vault assist: If jumping against a waist-height barrier/railing, boost up and forward
                    Vector3 moveDir = (transform.right * input.x + transform.forward * input.y).normalized;
                    if (moveDir.sqrMagnitude < 0.01f) moveDir = transform.forward;
                    Vector3 hip = transform.position + Vector3.up * 0.65f;
                    Vector3 chest = transform.position + Vector3.up * 1.45f;
                    if (Physics.Raycast(hip, moveDir, out _, 1.35f, ~0, QueryTriggerInteraction.Ignore) &&
                        !Physics.Raycast(chest, moveDir, 1.35f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        vertical = 8.5f;
                        vaultBoost = moveDir * 6.5f;
                        vaultTimer = 0.4f;
                    }
                }
            }
            else vertical -= 18 * Time.deltaTime;
            vertical = Mathf.Max(vertical, -28f);
            jumpQueued=false;
            Vector3 beforeMove=transform.position;
            if(SectorWorld.WaterAt(beforeMove+move*Time.deltaTime)) move=Vector3.zero;
            body.Move((move+Vector3.up*vertical)*Time.deltaTime);
            if(SectorWorld.WaterAt(transform.position))
            {
                if(hasDryPosition)Place(lastDryPosition);
                else foreach(var sector in SectorWorld.LoadedWorlds)
                    if(sector!=null && sector.TryFindGeographicSpawn(transform.position,out var dry)) {Place(dry);break;}
            }
            else if(grounded) {lastDryPosition=transform.position;hasDryPosition=true;}
            Vector3 displacement=transform.position-beforeMove;
            displacement.y=0;
            SpeedMetersPerSecond=Time.deltaTime>0?displacement.magnitude/Time.deltaTime:0;
            if(grounded) lastGrounded=transform.position;
            else if(transform.position.y<lastGrounded.y-45) Place(lastGrounded+Vector3.up*.2f);
            if (SectorWorld.LoadedWorlds.Count > 0 && SectorWorld.LoadedWorlds[0] != null)
            {
                float localGround = SectorWorld.LoadedWorlds[0].Ground(transform.position.x, transform.position.z);
                if (transform.position.y < localGround - 0.25f)
                    Place(new Vector3(transform.position.x, localGround + 0.25f, transform.position.z));
            }

            if (grounded && !climbing && SpeedMetersPerSecond > 0.35f)
            {
                float strideLength = Sprinting ? 1.85f : (IsCrouching ? 1.05f : 1.35f);
                stepDistance += displacement.magnitude;
                if (stepDistance >= strideLength)
                {
                    stepDistance = 0f;
                    TriggerFootstepAndDust();
                }
            }
            else
            {
                stepDistance = Mathf.Min(stepDistance, 0.35f);
            }
        }

        void TriggerFootstepAndDust()
        {
            string surface = "Asphalt";
            bool isDusty = false;

            if (Physics.Raycast(transform.position + Vector3.up * 0.4f, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                string colName = hit.collider.gameObject.name;
                if (colName.Contains("Mud")) { surface = "Mud"; isDusty = true; }
                else if (colName.Contains("Gravel")) { surface = "Gravel"; isDusty = true; }
                else if (colName.Contains("Cobblestone")) { surface = "Cobblestone"; }
                else if (colName.Contains("terrain") || colName.Contains("Terrain")) { surface = "dirt"; isDusty = true; }
                else if (colName.Contains("curb") || colName.Contains("Concrete") || colName.Contains("building") || colName.Contains("roof")) { surface = "Concrete"; }
                else if (colName.Contains("ladder") || colName.Contains("Ladder")) { surface = "Metal"; }
            }

            if (footstepAudio != null)
            {
                var clip = ProceduralAudio.CreateFootstep(surface, Sprinting);
                if (clip != null)
                {
                    footstepAudio.pitch = UnityEngine.Random.Range(0.93f, 1.07f);
                    float vol = Sprinting ? 0.82f : (IsCrouching ? 0.32f : 0.58f);
                    footstepAudio.PlayOneShot(clip, vol);
                }
            }

            if (dustParticleSystem != null && !SectorWorld.WaterAt(transform.position))
            {
                int count = Sprinting ? (isDusty ? 6 : 3) : (isDusty ? 3 : 1);
                var emitParams = new ParticleSystem.EmitParams();
                Vector3 footPos = transform.position + Vector3.up * 0.05f + UnityEngine.Random.insideUnitSphere * 0.12f;
                footPos.y = transform.position.y + 0.05f;
                emitParams.position = footPos;
                Vector3 driftDir = -transform.forward * UnityEngine.Random.Range(0.2f, 0.5f) + Vector3.up * UnityEngine.Random.Range(0.1f, 0.25f);
                emitParams.velocity = driftDir;
                Color dustCol = isDusty ? new Color(0.68f, 0.60f, 0.48f, 0.35f) : new Color(0.75f, 0.73f, 0.70f, 0.14f);
                emitParams.startColor = dustCol;
                emitParams.startSize = UnityEngine.Random.Range(0.22f, 0.38f);
                dustParticleSystem.Emit(emitParams, count);
            }
        }
        void OnDisable() { gyroAim?.Read(false,true); }

        public void SetCrouching(bool crouched)
        {
            IsCrouching = crouched;
            if (Controls != null)
            {
                Controls.IsCrouching = crouched;
                if (crouched) Controls.Sprint = false;
            }
        }
        public void Jump(){ jumpQueued=true; }
        void OnGUI()
        {
            if(InputBlocked || climbing || nearby==null) return;
            var previous=GUI.matrix; GUI.matrix=Matrix4x4.identity;
            bool mobile=Application.isMobilePlatform || Input.touchSupported;
            string action=descend?"CLIMB DOWN":"CLIMB UP";
            if(!mobile) action+="  [E]";
            if(TacticalGUI.DrawButton(new Rect(Screen.width/2-100,Screen.height*.65f,200,46),"ðŸªœ "+action,true,13))
                StartCoroutine(Climb(nearby,descend));
            GUI.matrix=previous;
        }
        System.Collections.IEnumerator Climb(SectorWorld.LadderRoute route,bool down)
        {
            if(climbing || route==null || route.Owner==null) yield break;
            climbing=true; body.enabled=false;
            Vector3 start=transform.position;
            try {
            var stops=down?new[]{route.Top,route.Bottom}:new[]{route.Bottom,route.Top,route.Landing};
            foreach(var target in stops)
                while(Vector3.Distance(transform.position,target)>.02f)
                {
                    if(route.Owner==null) { transform.position=start; yield break; }
                    transform.position=Vector3.MoveTowards(transform.position,target,4*Time.deltaTime);
                    yield return null;
                }
            } finally { vertical=0; if(body!=null) body.enabled=true; climbing=false; nearby=null; }
        }
        public void Place(Vector3 position)
        {
            if(SectorWorld.WaterAt(position))
            {
                bool found=false;
                foreach(var sector in SectorWorld.LoadedWorlds)
                    if(sector!=null && sector.TryFindGeographicSpawn(position,out var dry))
                    {position=dry;found=true;break;}
                if(!found){Debug.LogWarning("Player placement rejected: no dry supported surface.");return;}
            }
            SpeedMetersPerSecond=0;
            if (body != null) body.enabled=false;
            if (SectorWorld.LoadedWorlds.Count > 0 && SectorWorld.LoadedWorlds[0] != null)
            {
                float groundH = SectorWorld.LoadedWorlds[0].Ground(position.x, position.z);
                if (position.y < groundH + 0.15f) position.y = groundH + 0.25f;
            }
            transform.position=position; 
            lastGrounded=position;
            lastDryPosition=position;hasDryPosition=true;
            vertical=0; 
            if (body != null) body.enabled=true;
        }
        public void Face(Vector3 point)
        {
            var direction=point-transform.position;direction.y=0;
            if(direction.sqrMagnitude<.01f) return;
            yaw=Quaternion.LookRotation(direction).eulerAngles.y;pitch=0;
            transform.rotation=Quaternion.Euler(0,yaw,0);
            if(view!=null) view.transform.localRotation=Quaternion.identity;
        }
        public void ToggleScope(){ scope=!scope; }
        public void ToggleSprint(){ touchSprint=!touchSprint; }
        public bool Scoped=>scope;
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
