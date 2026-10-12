using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper.Duel
{
    public sealed class DuelOpponentAvatar : MonoBehaviour
    {
        public string Callsign { get; set; } = "RIVAL";
        public float CurrentHealth { get; private set; } = 100f;
        public bool IsDead => CurrentHealth <= 0;
        public bool IsAimingAtPlayer { get; private set; }
        public Vector3 LocatorPosition => transform.position + Vector3.up * (remoteIsCrouching ? .95f : 1.65f);

        private GameObject visualRoot;
        private GameObject rifleObject;
        private LineRenderer opticGlint;
        private LineRenderer tracerLine;
        private DuelAudioFeedback audioFeedback;
        private CombatActor actor;
        private CapsuleCollider bodyHitbox;
        private SphereCollider headHitbox;
        private bool hitboxCrouched;
        private Animation characterAnimation;
        private string currentAnimation;
        private Transform hips, rightHand, leftHand;
        private Vector3 hipsRestPosition, rightPalm, leftPalm;
        private Quaternion rightGripRotation, leftGripRotation;
        private Vector3 visualRestPosition;
        private Quaternion visualRestRotation;
        private float moveSpeed, animationActionUntil, tracerUntil, turnLean, previousYaw;
        private string actionAnimation;
        private bool moving, running;
        private static readonly Dictionary<string, AnimationClip> animationClips = new Dictionary<string, AnimationClip>();
        private readonly List<Material> ownedMaterials = new List<Material>();

        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private float targetPitch;
        private float displayPitch, aimBlend;
        private bool remoteIsAiming;
        private bool remoteIsCrouching;

        private const float LerpSpeed = 16f;

        public event Action<byte, float, Vector3> OnLocallyDamaged; // hitZone, damage, point

        public void Initialize(string callsign, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            Callsign = callsign;
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
            targetPosition = spawnPosition;
            targetRotation = spawnRotation;
            CurrentHealth = 100f;

            BuildVisuals();
            BuildHitboxes();
            BuildOpticGlint();
            BuildTracer();

            audioFeedback = gameObject.AddComponent<DuelAudioFeedback>();
            audioFeedback.Initialize();
            previousYaw = transform.eulerAngles.y;
        }

        private void BuildVisuals()
        {
            // Use the same calibrated character hierarchy as the campaign. Raw
            // FBX roots use source units and have no imported SWAT materials.
            GameObject prefab = Resources.Load<GameObject>("Models/Enemies/swat") ?? Resources.Load<GameObject>("Models/swat");
            if (prefab == null)
            {
                var models = ModelLibrary.Load("Enemies");
                if (models != null && models.Length > 0) prefab = models[0];
            }

            if (prefab != null)
            {
                visualRoot = ImportedVisual.CreateEnemy(prefab, transform);
                ApplyCharacterMaterials(prefab.name);
                ImportedVisual.ConfigureCharacterSurfaces(visualRoot.transform);
                BuildCharacterAnimation();
                // SWAT is imported without animation; its external idle can
                // change the initial pose. Normalize that sampled pose in the
                // placement wrapper so clips cannot undo size or foot contact.
                var bounds = ImportedVisual.PosedBounds(visualRoot.transform);
                var placement = visualRoot.transform.Find("Model placement");
                if (placement != null && float.IsFinite(bounds.size.y) && bounds.size.y > .001f)
                {
                    AssetCalibration.TryGet(prefab.name, out var calibration);
                    float height = calibration != null && calibration.height > 0 ? calibration.height : 1.85f;
                    placement.localScale *= height / bounds.size.y;
                    bounds = ImportedVisual.PosedBounds(visualRoot.transform);
                    placement.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                }
            }
            else
            {
                visualRoot = new GameObject("Fallback duel operative");
                visualRoot.transform.SetParent(transform, false);
                var uniform = CreateMaterial("Duel uniform", new Color(.20f, .25f, .30f));
                var skin = CreateMaterial("Duel face", new Color(.65f, .52f, .43f));
                AddFallbackPart("Torso", PrimitiveType.Capsule, new Vector3(0, 1.12f, 0), new Vector3(.48f, .42f, .32f), uniform);
                AddFallbackPart("Head", PrimitiveType.Sphere, new Vector3(0, 1.68f, 0), Vector3.one * .32f, skin);
                foreach (float side in new[] { -1f, 1f })
                {
                    AddFallbackPart("Arm", PrimitiveType.Capsule, new Vector3(side * .31f, 1.13f, .08f), new Vector3(.15f, .30f, .15f), uniform);
                    AddFallbackPart("Leg", PrimitiveType.Capsule, new Vector3(side * .14f, .44f, 0), new Vector3(.19f, .44f, .19f), uniform);
                }
            }
            foreach (var collider in visualRoot.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            visualRestPosition = visualRoot.transform.localPosition;
            visualRestRotation = visualRoot.transform.localRotation;

            EquipWeapon(1);
        }
        int displayedWeapon=-1;
        Material rivalGunMaterial;
        public void EquipWeapon(int weaponIndex)
        {
            if(!WeaponCatalog.Valid(weaponIndex)||visualRoot==null||displayedWeapon==weaponIndex)return;
            displayedWeapon=weaponIndex;
            if(rifleObject!=null){rifleObject.SetActive(false);if(Application.isPlaying)Destroy(rifleObject);else DestroyImmediate(rifleObject);}
            // Render-only gun geometry must be stripped of exporter helper
            // spheres and first-person arms, then sized in world metres.
            GameObject riflePrefab = Resources.Load<GameObject>("Models/Weapons/"+WeaponCatalog.Get(weaponIndex).Model)
                ?? Resources.Load<GameObject>("Models/Weapons/Barrett50")
                ?? Resources.Load<GameObject>("Models/SniperRifle");

            if (riflePrefab != null)
            {
                rifleObject = new GameObject("Duel rival sniper rifle");
                rifleObject.transform.SetParent(transform, false);
                var model = Instantiate(riflePrefab, rifleObject.transform, false);
                ImportedVisual.SanitizeWeaponModel(model.transform);
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.gameObject.SetActive(false);

                if (WeaponGeometry.Configure(model.transform, rifleObject.transform, weaponIndex))
                {
                    var metal = rivalGunMaterial ?? (rivalGunMaterial=CreateMaterial("Duel rifle metal", new Color(.15f, .17f, .19f)));
                    if (metal.HasProperty("_Metallic")) metal.SetFloat("_Metallic", .65f);
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    {
                        var materials = renderer.sharedMaterials;
                        if (materials.Length == 0) materials = new[] { metal };
                        for (int i = 0; i < materials.Length; i++)
                            if (materials[i] == null || (materials[i].mainTexture == null && !materials[i].name.StartsWith("WeaponFinish/"))) materials[i] = metal;
                        renderer.sharedMaterials = materials;
                    }
                    rightHand = WeaponGripPose.Bone(visualRoot.transform, "righthand", "handr", "rwrist");
                    leftHand = WeaponGripPose.Bone(visualRoot.transform, "lefthand", "handl", "lwrist");
                    rightPalm = WeaponGripPose.PalmLocal(rightHand);
                    leftPalm = WeaponGripPose.PalmLocal(leftHand);
                    rightGripRotation = rightHand != null ? Quaternion.Inverse(transform.rotation) * rightHand.rotation : Quaternion.identity;
                    leftGripRotation = leftHand != null ? Quaternion.Inverse(transform.rotation) * leftHand.rotation : Quaternion.identity;
                    UpdateRiflePose();
                }
            }
        }

        private Material CreateMaterial(string name, Color color, Texture2D texture = null, Texture2D normal = null)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = name, color = color, mainTexture = texture };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .16f);
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            ownedMaterials.Add(material);
            return material;
        }

        private void ApplyCharacterMaterials(string modelName)
        {
            if (modelName.ToLowerInvariant().Contains("ch35"))
            {
                var materials = new Material[3];
                for (int i = 0; i < materials.Length; i++)
                {
                    string path = "Models/Enemies/Textures/Ch35_100" + (i + 1);
                    materials[i] = CreateMaterial("Duel operative " + (i + 1), Color.white,
                        Resources.Load<Texture2D>(path + "_Diffuse"), Resources.Load<Texture2D>(path + "_Normal"));
                }
                foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterials = materials;
                return;
            }
            if (!modelName.ToLowerInvariant().Contains("swat")) return;
            const string textures = "Models/Enemies/Textures/";
            var body = CreateMaterial("Duel operative uniform", new Color(.32f, .35f, .38f),
                Resources.Load<Texture2D>(textures + "Soldier_Body_diffuse"), Resources.Load<Texture2D>(textures + "Soldier_Body_normal"));
            var head = CreateMaterial("Duel operative face", new Color(.72f, .67f, .62f),
                Resources.Load<Texture2D>(textures + "Soldier_head_diffuse"), Resources.Load<Texture2D>(textures + "Soldier_head_normal"));
            foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                bool isHead = renderer.name.ToLowerInvariant().Contains("head");
                if (isHead && renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null && skin.sharedMesh.subMeshCount >= 2)
                    renderer.sharedMaterials = new[] { body, head };
                else renderer.sharedMaterial = isHead ? head : body;
            }
        }

        private void AddFallbackPart(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(visualRoot.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void BuildCharacterAnimation()
        {
            var root = ImportedVisual.CharacterAnimationRoot(visualRoot.transform);
            characterAnimation = root.GetComponent<Animation>() ?? root.AddComponent<Animation>();
            characterAnimation.playAutomatically = false;
            characterAnimation.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (string name in new[] { "idle", "walk", "run", "crouch_idle", "fire", "hit", "death" })
            {
                if (characterAnimation.GetClip(name) == null)
                {
                    if (!animationClips.TryGetValue(name, out var clip))
                    {
                        foreach (var candidate in Resources.LoadAll<AnimationClip>("Models/Enemies/Animations/swat_" + name))
                        {
                            if (candidate == null || candidate.name.Contains("__preview__")) continue;
                            clip = candidate;
                            break;
                        }
                        animationClips[name] = clip;
                    }
                    if (clip != null) { clip.legacy = true; characterAnimation.AddClip(clip, name); }
                }
                if (characterAnimation[name] != null)
                    characterAnimation[name].wrapMode = name == "fire" || name == "hit" || name == "death" ? WrapMode.ClampForever : WrapMode.Loop;
            }
            // Recoil and flinches blend over locomotion. Limiting the action to
            // the spine also keeps a crouched shooter's hips and legs lowered.
            var spine = WeaponGripPose.Bone(visualRoot.transform, "spine");
            if (spine != null)
                foreach (string name in new[] { "fire", "hit" })
                {
                    var clip = characterAnimation.GetClip(name);
                    if (clip == null) continue;
                    string upper = name + "_upper";
                    characterAnimation.AddClip(clip, upper);
                    characterAnimation[upper].AddMixingTransform(spine, true);
                    characterAnimation[upper].layer = 2;
                    characterAnimation[upper].wrapMode = WrapMode.ClampForever;
                }
            PlayCharacterAnimation("idle", true);
            characterAnimation.Sample();
            hips = WeaponGripPose.Bone(visualRoot.transform, "hips", "pelvis");
            if (hips != null) hipsRestPosition = hips.localPosition;
        }

        private void PlayCharacterAnimation(string name, bool snap = false)
        {
            if (characterAnimation == null || characterAnimation[name] == null || (!snap && currentAnimation == name)) return;
            string previous = currentAnimation;
            currentAnimation = name;
            if (snap)
            {
                characterAnimation.Stop();
                characterAnimation[name].time = 0;
                characterAnimation.Play(name);
            }
            else
            {
                if ((name == "walk" || name == "run") && (previous == "walk" || previous == "run") && characterAnimation[previous] != null)
                    characterAnimation[name].normalizedTime = Mathf.Repeat(characterAnimation[previous].normalizedTime, 1f);
                characterAnimation.CrossFade(name, name == "death" ? .09f : .18f);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearAnimationCache() => animationClips.Clear();

        private void PlayCharacterAction(string name, float duration)
        {
            if (IsDead || characterAnimation == null || characterAnimation[name] == null) return;
            string upper = name + "_upper";
            if (characterAnimation[upper] != null)
            {
                actionAnimation = upper;
                characterAnimation[upper].time = 0;
                characterAnimation.CrossFade(upper, .06f);
            }
            else
            {
                // Missing spine bindings use the full body clip as a fallback.
                characterAnimation[name].time = 0;
                currentAnimation = null;
                PlayCharacterAnimation(name);
                actionAnimation = name;
            }
            animationActionUntil = Time.time + duration;
        }

        private void LateUpdate()
        {
            // The remote transform drives movement; imported locomotion must
            // not drift away from the network position or damage colliders.
            if (hips != null && !IsDead)
            {
                var position = hips.localPosition;
                position.x = hipsRestPosition.x;
                position.z = hipsRestPosition.z;
                hips.localPosition = position;
            }
            if (visualRoot != null && !IsDead)
            {
                float yaw = transform.eulerAngles.y;
                float turnRate = Mathf.DeltaAngle(previousYaw, yaw) / Mathf.Max(.001f, Time.deltaTime);
                previousYaw = yaw;
                float desired = Mathf.Clamp(-turnRate * .007f, -4f, 4f) * Mathf.Clamp01(moveSpeed / 2f);
                turnLean = Mathf.MoveTowards(turnLean, desired, Time.deltaTime * 30f);
                visualRoot.transform.localRotation = visualRestRotation * Quaternion.Euler(0, 0, turnLean);
            }
            if (!IsDead) UpdateRiflePose();
        }

        private void UpdateRiflePose()
        {
            if (rifleObject == null) return;
            var facing = transform.rotation * Quaternion.Euler(Mathf.Lerp(8f, -displayPitch, aimBlend), 0, 0);
            var grip = rightHand != null ? rightHand.TransformPoint(rightPalm)
                : transform.TransformPoint(new Vector3(.2f, remoteIsCrouching ? .95f : 1.35f, .18f));
            var stock = rifleObject.transform.Find("WeaponStock");
            if (aimBlend > .001f && stock != null && rightHand != null && rightHand.parent != null && rightHand.parent.parent != null)
            {
                var shoulder = rightHand.parent.parent.position + transform.forward * .06f - transform.up * .04f;
                grip = Vector3.Lerp(grip, shoulder - facing * stock.localPosition, aimBlend);
            }
            rifleObject.transform.SetPositionAndRotation(grip, facing);
            WeaponGripPose.SolveArm(rightHand, rightPalm, grip, facing * rightGripRotation, transform.right - transform.up);
            var support = rifleObject.transform.Find("WeaponSupport");
            if (support != null) WeaponGripPose.SolveArm(leftHand, leftPalm, support.position, facing * leftGripRotation, -transform.right - transform.up);
        }

        private void BuildHitboxes()
        {
            actor = gameObject.GetComponent<CombatActor>();
            if (actor == null) actor = gameObject.AddComponent<CombatActor>();
            actor.Initialize(100);
            actor.canRegenerate = false;

            // Main body collider
            var bodyCol = gameObject.GetComponent<CapsuleCollider>();
            if (bodyCol == null) bodyCol = gameObject.AddComponent<CapsuleCollider>();
            bodyCol.height = 1.45f;
            bodyCol.radius = 0.38f;
            bodyCol.center = Vector3.up * .74f;
            bodyHitbox = bodyCol;

            // Head hitbox for 1-shot headshots
            var headObj = new GameObject("Hitbox_Head");
            headObj.transform.SetParent(transform);
            headObj.transform.localPosition = Vector3.up * 1.65f;
            var headCol = headObj.AddComponent<SphereCollider>();
            headCol.radius = 0.24f;
            headHitbox = headCol;
            // Shot queries and damage routing identify the DuelOpponentAvatar
            // component on the parent; no custom Unity tag is required.
        }

        private void SetHitboxStance(bool crouched)
        {
            if (bodyHitbox == null || headHitbox == null || hitboxCrouched == crouched) return;
            hitboxCrouched = crouched;
            bodyHitbox.height = crouched ? .8f : 1.45f;
            bodyHitbox.center = Vector3.up * (crouched ? .4f : .74f);
            headHitbox.transform.localPosition = Vector3.up * (crouched ? .95f : 1.65f);
        }

        private void BuildOpticGlint()
        {
            var glintGo = new GameObject("OpticGlint");
            glintGo.transform.SetParent(transform);
            glintGo.transform.localPosition = new Vector3(0.25f, 1.48f, 0.9f);

            opticGlint = glintGo.AddComponent<LineRenderer>();
            opticGlint.positionCount = 2;
            opticGlint.startWidth = 0.065f;
            opticGlint.endWidth = 0.065f;
            opticGlint.useWorldSpace = true;

            var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            mat.color = new Color(.90f, .42f, .08f, .85f);
            ownedMaterials.Add(mat);
            opticGlint.sharedMaterial = mat;
            opticGlint.enabled = false;
        }

        private void BuildTracer()
        {
            var trGo = new GameObject("BulletTracer");
            trGo.transform.SetParent(transform);
            tracerLine = trGo.AddComponent<LineRenderer>();
            tracerLine.positionCount = 2;
            tracerLine.startWidth = 0.08f;
            tracerLine.endWidth = 0.03f;
            tracerLine.useWorldSpace = true;

            var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            mat.color = new Color(1.0f, 0.45f, 0.1f, 0.9f);
            ownedMaterials.Add(mat);
            tracerLine.sharedMaterial = mat;
            tracerLine.enabled = false;
        }

        private void Update()
        {
            if (tracerLine != null && tracerLine.enabled && Time.unscaledTime >= tracerUntil) tracerLine.enabled = false;
            if (IsDead) return;

            // Smooth position & rotation interpolation
            Vector3 previousPosition = transform.position;
            float blend = 1f - Mathf.Exp(-Time.deltaTime * LerpSpeed);
            transform.position = Vector3.Lerp(transform.position, targetPosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
            displayPitch = Mathf.LerpAngle(displayPitch, targetPitch, blend);
            aimBlend = Mathf.Lerp(aimBlend, remoteIsAiming ? 1f : 0f, 1f - Mathf.Exp(-Time.deltaTime * 12f));
            Vector3 movement = transform.position - previousPosition;
            movement.y = 0;
            moveSpeed = Mathf.Lerp(moveSpeed, movement.magnitude / Mathf.Max(.001f, Time.deltaTime), Time.deltaTime * 10f);
            // Hysteresis prevents packet jitter switching idle/walk and
            // walk/run every frame while the player steadies their aim.
            moving = moveSpeed > (moving ? .08f : .18f);
            running = moveSpeed > (running ? 2.3f : 2.8f);
            if (Time.time >= animationActionUntil && actionAnimation != null)
            {
                if (characterAnimation != null && actionAnimation.EndsWith("_upper")) characterAnimation.Blend(actionAnimation, 0, .12f);
                actionAnimation = null;
            }
            if (actionAnimation == null || actionAnimation.EndsWith("_upper"))
            {
                string clip = remoteIsCrouching ? "crouch_idle" : running ? "run" : moving ? "walk" : "idle";
                PlayCharacterAnimation(clip);
                if (characterAnimation != null && characterAnimation[clip] != null && (clip == "walk" || clip == "run"))
                    characterAnimation[clip].speed = Mathf.Clamp(moveSpeed / (clip == "run" ? 2.6f : 1.4f), .15f, 2f);
            }
            audioFeedback?.AdvanceFootsteps(transform.position, movement.magnitude, moveSpeed, remoteIsCrouching);
            if (visualRoot != null && (characterAnimation == null || characterAnimation["crouch_idle"] == null))
                visualRoot.transform.localScale = Vector3.Lerp(visualRoot.transform.localScale,
                    new Vector3(1f, remoteIsCrouching ? .65f : 1f, 1f), Time.deltaTime * 12f);

            // Scope Glint Evaluation
            UpdateOpticGlint(Camera.main);
        }

        private void UpdateOpticGlint(Camera mainCam)
        {
            if (opticGlint == null) return;

            if (mainCam == null || !remoteIsAiming || IsDead)
            {
                opticGlint.enabled = false;
                IsAimingAtPlayer = false;
                return;
            }

            Vector3 toPlayer = (mainCam.transform.position - transform.position).normalized;
            float facingDot = Vector3.Dot(transform.forward, toPlayer);

            // Opponent is aiming and facing local player within ~25 degrees
            if (facingDot > 0.88f)
            {
                IsAimingAtPlayer = true;
                Vector3 opticPos = transform.position + Vector3.up * (remoteIsCrouching ? 1.0f : 1.45f) + transform.forward * 0.5f;

                // Pulsing flash effect
                float pulse = 0.6f + Mathf.PingPong(Time.time * 6f, 0.4f);
                // Lens-sized glint, rather than a luminous panel over the torso.
                opticGlint.startWidth = 0.065f * pulse;
                opticGlint.endWidth = 0.065f * pulse;

                Vector3 camRight = mainCam.transform.right * (0.032f * pulse);
                opticGlint.SetPosition(0, opticPos - camRight);
                opticGlint.SetPosition(1, opticPos + camRight);
                opticGlint.enabled = true;
            }
            else
            {
                IsAimingAtPlayer = false;
                opticGlint.enabled = false;
            }
        }

        public void ApplyNetworkState(Vector3 pos, float yaw, float pitch, bool isAiming, bool isCrouching, float hp)
        {
            if (!Finite(pos) || !float.IsFinite(yaw) || !float.IsFinite(pitch) || !float.IsFinite(hp)) return;
            bool wasDead = IsDead;
            float previousHealth = CurrentHealth;
            targetPosition = pos;
            targetRotation = Quaternion.Euler(0, yaw, 0);
            targetPitch = Mathf.Clamp(pitch, -89f, 89f);
            remoteIsAiming = isAiming;
            remoteIsCrouching = isCrouching;
            SetHitboxStance(isCrouching);
            // Movement packets cannot undo damage; Respawn resets health each round.
            CurrentHealth = Mathf.Min(CurrentHealth, Mathf.Clamp(hp, 0, 100));
            if (!wasDead && IsDead) Die();
            else if (CurrentHealth < previousHealth) PlayCharacterAction("hit", .28f);
        }

        public void PlayRemoteFire(Vector3 muzzle, Vector3 dir, int weaponIndex, bool soundMasked = false)
        {
            if (IsDead || !Finite(muzzle) || !Finite(dir) || !float.IsFinite(dir.sqrMagnitude) || dir.sqrMagnitude < .001f) return;
            EquipWeapon(weaponIndex);
            dir.Normalize();
            PlayCharacterAction("fire", .22f);
            Vector3 end = TraceEndpoint(muzzle, dir);
            // Tracer ray effect
            if (tracerLine != null)
            {
                tracerLine.SetPosition(0, muzzle);
                tracerLine.SetPosition(1, end);
                tracerLine.enabled = true;
                tracerUntil = Time.unscaledTime + .075f;
            }
            audioFeedback?.PlayGunshot(muzzle, weaponIndex, soundMasked);
            audioFeedback?.PlayNearMiss(muzzle, end);
        }

        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        readonly RaycastHit[] shotHits = new RaycastHit[24];
        private Vector3 TraceEndpoint(Vector3 muzzle, Vector3 direction)
        {
            float distance = 900f;
            int count = Physics.RaycastNonAlloc(muzzle, direction, shotHits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = shotHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                distance = Mathf.Min(distance, hit.distance);
            }
            return muzzle + direction * distance;
        }

        public void TakeLocalDamage(byte hitZone, float damage, Vector3 point)
        {
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            OnLocallyDamaged?.Invoke(hitZone, damage, point);

            if (CurrentHealth <= 0)
            {
                Die();
            }
            else if (damage > 0) PlayCharacterAction("hit", .28f);
        }

        public void Die()
        {
            CurrentHealth = 0;
            actionAnimation = null;
            audioFeedback?.ResetFootsteps();
            if (opticGlint != null) opticGlint.enabled = false;
            if (tracerLine != null) tracerLine.enabled = false;

            if (rifleObject != null) rifleObject.SetActive(false);
            // Use the authored collapse when available, preserving calibrated placement.
            if (visualRoot != null)
            {
                if (characterAnimation != null && characterAnimation["death"] != null)
                {
                    characterAnimation.Stop("fire_upper");
                    characterAnimation.Stop("hit_upper");
                    currentAnimation = null;
                    characterAnimation["death"].time = 0;
                    PlayCharacterAnimation("death");
                }
                else
                {
                    visualRoot.transform.localPosition = visualRestPosition + Vector3.down * .3f;
                    visualRoot.transform.localRotation = visualRestRotation * Quaternion.Euler(85f, 0, 0);
                }
            }
        }

        public void Respawn(Vector3 newPos, Quaternion newRot)
        {
            CurrentHealth = 100f;
            if (actor != null) actor.Initialize(100);
            transform.position = newPos;
            transform.rotation = newRot;
            targetPosition = newPos;
            targetRotation = newRot;
            remoteIsAiming = remoteIsCrouching = false;
            SetHitboxStance(false);
            targetPitch = displayPitch = aimBlend = 0;
            moveSpeed = animationActionUntil = 0;
            actionAnimation = null;
            moving = running = false;
            turnLean = 0;
            previousYaw = transform.eulerAngles.y;
            audioFeedback?.ResetFootsteps();
            if (tracerLine != null) tracerLine.enabled = false;

            if (visualRoot != null)
            {
                visualRoot.transform.localPosition = visualRestPosition;
                visualRoot.transform.localRotation = visualRestRotation;
                visualRoot.transform.localScale = Vector3.one;
            }
            PlayCharacterAnimation("idle", true);
            if (characterAnimation != null) characterAnimation.Sample();
            if (rifleObject != null) rifleObject.SetActive(true);
            UpdateRiflePose();
        }

        private void OnDestroy()
        {
            foreach (var material in ownedMaterials)
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material);
                    else DestroyImmediate(material);
                }
        }
    }
}
