using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public sealed class SniperPresentation : MonoBehaviour
    {
        const string ImportedRiflePath="Models/GeoSniperRifleProvided";
        const string LegacyRiflePath="Models/GeoSniperRifle";
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Transform rifle;
        Vector3 muzzleLocal;
        AudioSource speaker;
        AudioClip shot, bolt, hit, empty;
        Texture2D scope;
        float kick;
        float kickVelocity,boltCycleTime;
        float boltCycleDuration;
        int boltSoundPhase,reloadSoundPhase;
        bool reloadAudioActive;
        readonly AudioClip[] handlingClicks=new AudioClip[3];
        AudioClip casingClink;
        Material casingMaterial;
        float movementBlend;
        FirstPersonGripPose handlingPose;
        public bool Muted { get; private set; }
        // Muzzle flash
        Light muzzleLight;
        GameObject muzzleFlash;
        float flashTimer;
        // Bullet tracer
        LineRenderer tracerLine;
        float tracerTimer;
        // Camera shake
        Camera cam;
        float shakeIntensity;
        Vector3 shakeOffset;
        Vector3 weaponViewPosition=new Vector3(.22f,-.24f,.62f);
        Quaternion weaponViewRotation=Quaternion.Euler(0,-5,0);
        
        
        // Phase 1 Additions
        WeaponSway sway;
        public WeaponConfig Config;
        
        float currentFOV;
        int currentZoomIndex = 0;
        
        float hitMarkerTimer;
        bool hitMarkerKill;int lastKillFrame=-1;
        
        float killTimer;
        float targetDistance;
        float targetElevation;
        Vector3 lastMissImpactPos;
        float lastMissImpactTimer;
        float lastMissDistance;
        CombatActor currentTargetActor;
        GeoSniper.Duel.DuelOpponentAvatar currentTargetRival;
        string currentTargetClassification = "";
        ThermalVisionOverlay thermalOverlay;
        GUIStyle scopeTagStyle, scopeValueStyle, scopeCompassStyle, scopeRangeStyle, scopeFlirStyle, scopeKillStyle;
        float reloadDuration=1.4f;
        float scopeBlend;
        float breathPhase;
        float shotKick;
        Transform combatKnife;
        float knifeSlashTimer;
        bool knifeIsLethal;
        bool knifeHitEffectPlayed;
        float knifeBloodTimer;
        Transform leftArmBone;
        
        // Call of Duty Locomotion & Weapon Kinematics
        float tacticalSprintBlend;
        float crouchBlend;
        float stridePhase;
        Vector3 inertiaPos;
        Vector3 inertiaRot;
        Quaternion lastCamRot = Quaternion.identity;
        bool hasLastCamRot;
        
        public void ConfirmHit() {hitMarkerKill=lastKillFrame==Time.frameCount;hitMarkerTimer=hitMarkerKill?.3f:.2f;lastMissImpactTimer=0f;}
        public void ConfirmKill() {lastKillFrame=Time.frameCount;hitMarkerKill=true;killTimer=2f;hitMarkerTimer=.3f;lastMissImpactTimer=0f;}
        public void DrawHitFeedback(float width,float height)
        {
            if(hitMarkerTimer<=0)return;
            var previousColor=GUI.color;var previousMatrix=GUI.matrix;
            float lifetime=hitMarkerKill?.3f:.2f;
            GUI.color=hitMarkerKill?new Color(1,.75f,.25f,Mathf.Clamp01(hitMarkerTimer/lifetime)):new Color(1,1,1,Mathf.Clamp01(hitMarkerTimer/lifetime));
            GUIUtility.RotateAroundPivot(45,new Vector2(width*.5f,height*.5f));
            float pulse=1-Mathf.Clamp01(hitMarkerTimer/lifetime);
            float gap=Mathf.Lerp(8,5,pulse),size=hitMarkerKill?9:7;
            GUI.DrawTexture(new Rect(width*.5f-gap-size,height*.5f-1,size,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width*.5f+gap,height*.5f-1,size,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width*.5f-1,height*.5f-gap-size,2,size),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width*.5f-1,height*.5f+gap,2,size),Texture2D.whiteTexture);
            GUI.matrix=previousMatrix;GUI.color=previousColor;
        }
        public void RecordMissImpact(Vector3 impactWorldPos)
        {
            lastMissImpactPos = impactWorldPos;
            lastMissImpactTimer = 8.5f;
            lastMissDistance = cam != null ? Vector3.Distance(cam.transform.position, impactWorldPos) : 0f;
            var speaker = cam != null ? cam.GetComponent<AudioSource>() : null;
            if (speaker != null) speaker.PlayOneShot(ProceduralAudio.CreateRicochetMiss(), 0.85f);
        }
        public void TriggerXRayHitCam(Transform target, Vector3 impactPos)
        {
            if (target == null) return;
            var renderers = target.GetComponentsInChildren<Renderer>();
            var originalMats = new System.Collections.Generic.Dictionary<Renderer, Material[]>();
            var xrayShader = Shader.Find("GeoSniper/XRayAnatomical");
            if (xrayShader != null)
            {
                var xrayMat = new Material(xrayShader);
                xrayMat.SetVector("_HitPoint", impactPos);
                xrayMat.SetFloat("_HitRadius", 0.55f);
                foreach (var r in renderers)
                {
                    if (r == null || r.gameObject.name.Contains("XRay")) continue;
                    originalMats[r] = r.sharedMaterials;
                    var newMats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < newMats.Length; i++) newMats[i] = xrayMat;
                    r.sharedMaterials = newMats;
                }
            }

            GameObject xrayOverlay = BuildXRaySkullSpineOverlay(target, impactPos);

            // Hit audio is owned by the confirmed damage path.
            TimeScaleController.SetHitCam(0.08f);
            // TimeScaleController sets fixedDeltaTime automatically
            StartCoroutine(RestoreXRayTimeScale(2.2f, renderers, originalMats, xrayOverlay));
        }

        static GameObject BuildXRaySkullSpineOverlay(Transform target, Vector3 impactPos)
        {
            var root = new GameObject("XRayAnatomicalOverlay");
            root.transform.SetParent(target, false);

            Vector3 headPos = impactPos;
            var bones = target.GetComponentsInChildren<Transform>();
            Transform headBone = null;
            foreach (var b in bones)
            {
                string n = b.name.ToLowerInvariant();
                if (n.Contains("head") || n.Contains("skull")) { headBone = b; break; }
            }
            if (headBone != null) headPos = headBone.position;
            else headPos = target.position + Vector3.up * 1.62f;

            var boneMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            boneMat.color = new Color(0.98f, 0.96f, 0.88f, 0.95f);

            // 3D Skull Mesh
            var skull = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skull.name = "XRaySkullMesh";
            skull.transform.SetParent(root.transform, false);
            skull.transform.position = headPos;
            skull.transform.localScale = new Vector3(0.20f, 0.24f, 0.22f);
            var skullCol = skull.GetComponent<Collider>();
            if (skullCol != null) UnityEngine.Object.Destroy(skullCol);
            skull.GetComponent<Renderer>().sharedMaterial = boneMat;

            // 3D Spine Vertebrae
            Vector3 spineBase = target.position + Vector3.up * 0.9f;
            int count = 6;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                Vector3 vertPos = Vector3.Lerp(spineBase, headPos - Vector3.up * 0.12f, t);
                var vert = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                vert.name = "XRaySpineMesh";
                vert.transform.SetParent(root.transform, false);
                vert.transform.position = vertPos;
                vert.transform.localScale = new Vector3(0.08f, 0.04f, 0.08f);
                var c = vert.GetComponent<Collider>();
                if (c != null) UnityEngine.Object.Destroy(c);
                vert.GetComponent<Renderer>().sharedMaterial = boneMat;
            }

            return root;
        }

        System.Collections.IEnumerator RestoreXRayTimeScale(float duration, Renderer[] renderers, System.Collections.Generic.Dictionary<Renderer, Material[]> originalMats, GameObject overlay)
        {
            yield return new WaitForSecondsRealtime(duration);
            TimeScaleController.ClearHitCam();
            Time.fixedDeltaTime = 0.02f;

            if (renderers != null && originalMats != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null && originalMats.TryGetValue(r, out var mats))
                    {
                        r.sharedMaterials = mats;
                    }
                }
            }
            if (overlay != null) UnityEngine.Object.Destroy(overlay);
        }
        public void SetTargetDistance(float distance) { targetDistance = distance; }
        public void OnShotFired()
        {
            // Different rifles communicate their weight without requiring a second
            // animation rig: the heavy Barrett kicks hardest, the MK12 settles fastest.
            shotKick = Mathf.Clamp(WeaponCatalog.Get(CurrentWeaponIndex).Recoil/95f,.2f,1.5f);
            shotKick *= Config!=null ? Config.recoilPitch/9f : 1;
            kickVelocity=Mathf.Min(kickVelocity+shotKick*52f,100f);
            if(WeaponHandlingMotion.BoltAction(CurrentWeaponIndex))
            {
                boltCycleDuration=WeaponHandlingMotion.BoltDuration(CurrentWeaponIndex);
                boltCycleTime=boltCycleDuration;boltSoundPhase=0;
            }
            shakeIntensity = Mathf.Max(shakeIntensity,shotKick*.12f);
        }
        public void Initialize(Camera camera)
        {
            if(rifle!=null) return;
            cam=camera;
            thermalOverlay=ThermalVisionOverlay.Ensure(cam);
            cam.nearClipPlane = 0.05f;
            currentFOV = Config != null ? Config.hipFOV : 65f;
            cam.fieldOfView = currentFOV;
            if(FindAnyObjectByType<AudioListener>()==null) camera.gameObject.AddComponent<AudioListener>();
            speaker=gameObject.AddComponent<AudioSource>(); speaker.spatialBlend=0; speaker.volume=.55f;
            shot=Sound("Rifle report",.65f,0); bolt=Sound("Bolt and magazine",1.4f,1);
            hit=Sound("Target confirmation",.18f,2); empty=Sound("Empty chamber",.09f,3);
            casingClink=ProceduralAudio.CreateShellClink();
            if(ProceduralAudio.LoadAsset("ShellClink")!=casingClink)owned.Add(casingClink);
            for(int i=0;i<handlingClicks.Length;i++)handlingClicks[i]=BuildHandlingClick(i);
            casingMaterial=new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
            casingMaterial.color=new Color(.95f,.78f,.28f);
            casingMaterial.SetFloat("_Metallic",.9f);casingMaterial.SetFloat("_Glossiness",.75f);owned.Add(casingMaterial);
            BuildMuzzleFlash(camera);
            BuildTracerLine();
            BuildCombatKnife(camera);
            
            EquipWeaponModel(CurrentWeaponIndex);
            
            int lensSize=MobileGraphics.Selected==MobileGraphicsPreset.Performance?512:1024;
            scope=new Texture2D(lensSize,lensSize,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp}; owned.Add(scope);
            var pixels=new Color32[lensSize*lensSize];
            for(int y=0;y<lensSize;y++) for(int x=0;x<lensSize;x++)
            {
                pixels[y*lensSize+x]=ScopeOptics.LensPixel((x+.5f)*2/lensSize-1,(y+.5f)*2/lensSize-1);
            }
            scope.SetPixels32(pixels); scope.Apply(false,true);
        }
        Material Mat(Color color,float metallic)
        {
            var shader=Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse");
            var m=new Material(shader!=null?shader:Shader.Find("Sprites/Default")); m.color=color; m.SetFloat("_Metallic",metallic); m.SetFloat("_Glossiness",.3f); owned.Add(m); return m;
        }
        void ApplyRifleMaterials(Transform root)
        {
            var metal=Mat(new Color(.09f,.105f,.12f),.82f);
            metal.SetFloat("_Glossiness",.42f);
            var polymer=Mat(new Color(.13f,.145f,.115f),.05f);
            polymer.SetFloat("_Glossiness",.22f);
            var coatedShader = Shader.Find("GeoSniper/CoatedOptic") ?? Shader.Find("Standard");
            var glass = new Material(coatedShader) { color = new Color(.035f, .14f, .22f) };
            if (glass.HasProperty("_Metallic")) glass.SetFloat("_Metallic", 0.90f);
            if (glass.HasProperty("_Glossiness")) glass.SetFloat("_Glossiness", 0.98f);
            var steel=Mat(new Color(.32f,.35f,.37f),.9f);steel.SetFloat("_Glossiness",.75f);
            var rubber=Mat(new Color(.025f,.03f,.032f),0);rubber.SetFloat("_Glossiness",.22f);
            if(CurrentWeaponIndex==1)
            {
                polymer.color=new Color(.24f,.22f,.16f);
            }
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                // Preserve imported textured submeshes, rather than collapsing the entire
                // renderer to one material when only its first slot has no texture.
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    if(slots[i]!=null && (slots[i].mainTexture!=null || slots[i].name.StartsWith("WeaponFinish/"))) continue;
                    string name=(renderer.name+" "+(slots[i]!=null?slots[i].name:"")).ToLowerInvariant();
                    if(name.Contains("glass") || name.Contains("lens") || name.Contains("optic")) slots[i]=glass;
                    else if(CurrentWeaponIndex==1 && (name.Contains("bolt silver") || name.Contains("bolt_silver") || name.Contains("screw"))) slots[i]=steel;
                    else if(CurrentWeaponIndex==1 && (name.Contains("second color") || name.Contains("second_color"))) slots[i]=rubber;
                    else slots[i]=name.Contains("stock") || name.Contains("grip") || name.Contains("cheek") || (CurrentWeaponIndex==1 && (name.Contains("sniper body") || name.Contains("sniper_body"))) ? polymer : metal;
                }
                renderer.sharedMaterials=slots;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        void NormalizeImportedRifle(Transform root)
        {
            var holder=root.parent;
            var bounds=ImportedVisual.LocalBounds(holder);
            if(bounds.size.y>bounds.size.z*1.5f) root.localRotation=Quaternion.Euler(90,0,0)*root.localRotation;
            else if(bounds.size.x>bounds.size.z*1.5f) root.localRotation=Quaternion.Euler(0,-90,0)*root.localRotation;
        }
        

        static Mesh quadMeshShared;
        static Mesh GetSharedQuadMesh()
        {
            if (quadMeshShared != null) return quadMeshShared;
            var m = new Mesh { name = "ProceduralFlashQuad" };
            m.vertices = new Vector3[] {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0)
            };
            m.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            quadMeshShared = m;
            return m;
        }

        AudioClip suppressedShot;

        AudioClip BuildHandlingClick(int kind)
        {
            const int rate=22050;
            var data=new float[Mathf.RoundToInt(rate*(kind==1?.14f:.09f))];
            var random=new System.Random(821+kind);float filtered=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate;filtered=Mathf.Lerp(filtered,(float)random.NextDouble()*2-1,.35f);
                data[i]=(filtered*.5f+Mathf.Sin(t*2*Mathf.PI*(kind==2?1850:720))*.22f)*Mathf.Exp(-t*(kind==1?26:55));
            }
            var clip=AudioClip.Create("Weapon mechanical "+kind,data.Length,1,rate,false);
            clip.SetData(data,0);owned.Add(clip);return clip;
        }
        void HandlingClick(int kind,float volume=.55f)
        {
            if(speaker!=null && handlingClicks[kind]!=null)speaker.PlayOneShot(handlingClicks[kind],volume);
        }

        AudioClip Sound(string name,float seconds,int kind)
        {
            if (kind == 0) return ProceduralAudio.CreateHeavyRifleShot();
            if (kind == 1) return ProceduralAudio.CreateBoltCycle();
            const int rate=44100; var samples=new float[(int)(seconds*rate)]; var random=new System.Random(41+kind);
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate, noise=(float)random.NextDouble()*2-1;
                if(kind==2) samples[i]=Mathf.Sin(t*2*Mathf.PI*880)*.25f*Mathf.Exp(-t*35);
                else samples[i]=noise*.25f*Mathf.Exp(-t*35);
            }
            var clip=AudioClip.Create(name,samples.Length,1,rate,false); clip.SetData(samples,0); owned.Add(clip); return clip;
        }

        void BuildCombatKnife(Camera camera)
        {
            if (combatKnife != null) Destroy(combatKnife.gameObject);
            var knifeObj = new GameObject("TacticalCombatKnife");
            combatKnife = knifeObj.transform;
            combatKnife.SetParent(camera.transform, false);

            EnsureFPSArmsMaterials();

            var steelMat = Mat(new Color(0.86f, 0.89f, 0.93f), 0.96f);
            steelMat.SetFloat("_Glossiness", 0.92f);
            var darkSteelMat = Mat(new Color(0.18f, 0.20f, 0.22f), 0.90f);
            darkSteelMat.SetFloat("_Glossiness", 0.75f);
            var handleMat = Mat(new Color(0.08f, 0.09f, 0.10f), 0.15f);
            handleMat.SetFloat("_Glossiness", 0.30f);

            // Container for knife + hand assembly
            var rig = new GameObject("KnifeHandRig").transform;
            rig.SetParent(combatKnife, false);

            // --- 1. TACTICAL COMBAT TANTO KNIFE (Blade pointing FORWARD +Z into attack) ---
            var knifeRoot = new GameObject("CombatKnife").transform;
            knifeRoot.SetParent(rig, false);
            knifeRoot.localPosition = new Vector3(0.015f, 0f, 0f);
            knifeRoot.localRotation = Quaternion.Euler(-8f, 4f, 0f);

            // Primary Tanto Blade (Lower sharp edge + Upper tactical carbon spine)
            var bladeEdge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bladeEdge.name = "TantoBladeEdge";
            bladeEdge.transform.SetParent(knifeRoot, false);
            bladeEdge.transform.localPosition = new Vector3(0, -0.008f, 0.11f);
            bladeEdge.transform.localScale = new Vector3(0.007f, 0.022f, 0.17f);
            bladeEdge.GetComponent<Renderer>().sharedMaterial = steelMat;
            var cE = bladeEdge.GetComponent<Collider>(); if (cE != null) Destroy(cE);

            var bladeSpine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bladeSpine.name = "TantoBladeSpine";
            bladeSpine.transform.SetParent(knifeRoot, false);
            bladeSpine.transform.localPosition = new Vector3(0, 0.007f, 0.10f);
            bladeSpine.transform.localScale = new Vector3(0.009f, 0.018f, 0.15f);
            bladeSpine.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var cS = bladeSpine.GetComponent<Collider>(); if (cS != null) Destroy(cS);

            // Angled Tanto Tip (sharp geometric point angled up at 40 degrees)
            var tantoTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tantoTip.name = "TantoPoint";
            tantoTip.transform.SetParent(knifeRoot, false);
            tantoTip.transform.localPosition = new Vector3(0, -0.003f, 0.20f);
            tantoTip.transform.localRotation = Quaternion.Euler(38f, 0, 0);
            tantoTip.transform.localScale = new Vector3(0.007f, 0.024f, 0.035f);
            tantoTip.GetComponent<Renderer>().sharedMaterial = steelMat;
            var cT = tantoTip.GetComponent<Collider>(); if (cT != null) Destroy(cT);

            // Blood groove / Fuller
            var fuller = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fuller.name = "BladeFuller";
            fuller.transform.SetParent(knifeRoot, false);
            fuller.transform.localPosition = new Vector3(0, 0.002f, 0.10f);
            fuller.transform.localScale = new Vector3(0.010f, 0.005f, 0.11f);
            fuller.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var cF = fuller.GetComponent<Collider>(); if (cF != null) Destroy(cF);

            // Spine jimping / thumb ramp
            var jimping = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jimping.name = "ThumbJimping";
            jimping.transform.SetParent(knifeRoot, false);
            jimping.transform.localPosition = new Vector3(0, 0.018f, 0.026f);
            jimping.transform.localRotation = Quaternion.Euler(20f, 0, 0);
            jimping.transform.localScale = new Vector3(0.012f, 0.008f, 0.025f);
            jimping.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var cJ = jimping.GetComponent<Collider>(); if (cJ != null) Destroy(cJ);

            // Crossguard (Tactical steel finger guard)
            var guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guard.name = "KnifeGuard";
            guard.transform.SetParent(knifeRoot, false);
            guard.transform.localPosition = new Vector3(0, -0.008f, 0.020f);
            guard.transform.localScale = new Vector3(0.020f, 0.052f, 0.012f);
            guard.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var c2 = guard.GetComponent<Collider>(); if (c2 != null) Destroy(c2);

            // Handle (Ergonomic combat ribbed grip)
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "KnifeHandle";
            handle.transform.SetParent(knifeRoot, false);
            handle.transform.localPosition = new Vector3(0, -0.004f, -0.055f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            handle.transform.localScale = new Vector3(0.022f, 0.065f, 0.018f);
            handle.GetComponent<Renderer>().sharedMaterial = handleMat;
            var c3 = handle.GetComponent<Collider>(); if (c3 != null) Destroy(c3);

            // Pommel (Heavy steel skull-crusher)
            var pommel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pommel.name = "KnifePommel";
            pommel.transform.SetParent(knifeRoot, false);
            pommel.transform.localPosition = new Vector3(0, -0.004f, -0.125f);
            pommel.transform.localRotation = Quaternion.Euler(0, 45f, 45f);
            pommel.transform.localScale = new Vector3(0.020f, 0.020f, 0.018f);
            pommel.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var cP = pommel.GetComponent<Collider>(); if (cP != null) Destroy(cP);

            // --- 2. TACTICAL GLOVED HAND HOLDING THE KNIFE ---
            var handRoot = new GameObject("GlovedHand").transform;
            handRoot.SetParent(rig, false);
            handRoot.localPosition = new Vector3(0.015f, 0f, 0f);

            // Palm & fist body
            var fist = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fist.name = "GloveFist";
            fist.transform.SetParent(handRoot, false);
            fist.transform.localPosition = new Vector3(0.008f, -0.002f, -0.055f);
            fist.transform.localScale = new Vector3(0.046f, 0.062f, 0.082f);
            fist.GetComponent<Renderer>().sharedMaterial = armsGloveMat;
            var cFist = fist.GetComponent<Collider>(); if (cFist != null) Destroy(cFist);

            // Carbon knuckle armor plate
            var knuckles = GameObject.CreatePrimitive(PrimitiveType.Cube);
            knuckles.name = "CarbonKnuckles";
            knuckles.transform.SetParent(handRoot, false);
            knuckles.transform.localPosition = new Vector3(0.028f, 0.005f, -0.055f);
            knuckles.transform.localRotation = Quaternion.Euler(0, 10f, 0);
            knuckles.transform.localScale = new Vector3(0.012f, 0.050f, 0.072f);
            knuckles.GetComponent<Renderer>().sharedMaterial = darkSteelMat;
            var cKnuck = knuckles.GetComponent<Collider>(); if (cKnuck != null) Destroy(cKnuck);

            // 4 Curled gloved fingers wrapped firmly around the hilt
            for (int fIdx = 0; fIdx < 4; fIdx++)
            {
                float zOffset = -0.022f - fIdx * 0.020f;
                var finger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                finger.name = $"GloveFinger_{fIdx}";
                finger.transform.SetParent(handRoot, false);
                finger.transform.localPosition = new Vector3(-0.012f, -0.005f, zOffset);
                finger.transform.localRotation = Quaternion.Euler(90f, 0, 15f);
                finger.transform.localScale = new Vector3(0.014f, 0.022f, 0.014f);
                finger.GetComponent<Renderer>().sharedMaterial = armsGloveMat;
                var cFin = finger.GetComponent<Collider>(); if (cFin != null) Destroy(cFin);
            }

            // Gloved thumb locked across thumb ramp
            var thumb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            thumb.name = "GloveThumb";
            thumb.transform.SetParent(handRoot, false);
            thumb.transform.localPosition = new Vector3(0.002f, 0.025f, -0.015f);
            thumb.transform.localRotation = Quaternion.Euler(30f, 35f, 65f);
            thumb.transform.localScale = new Vector3(0.015f, 0.026f, 0.015f);
            thumb.GetComponent<Renderer>().sharedMaterial = armsGloveMat;
            var cThm = thumb.GetComponent<Collider>(); if (cThm != null) Destroy(cThm);

            // Tactical forearm sleeve entering from bottom-right off-screen
            var forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            forearm.name = "TacticalSleeve";
            forearm.transform.SetParent(handRoot, false);
            forearm.transform.localPosition = new Vector3(0.065f, -0.095f, -0.165f);
            forearm.transform.localRotation = Quaternion.Euler(60f, -25f, 15f);
            forearm.transform.localScale = new Vector3(0.062f, 0.125f, 0.062f);
            forearm.GetComponent<Renderer>().sharedMaterial = armsSleeveMat;
            var cFore = forearm.GetComponent<Collider>(); if (cFore != null) Destroy(cFore);

            // Forearm extension to guarantee smooth off-screen taper
            var sleeveExt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sleeveExt.name = "SleeveExtension";
            sleeveExt.transform.SetParent(handRoot, false);
            sleeveExt.transform.localPosition = new Vector3(0.120f, -0.175f, -0.260f);
            sleeveExt.transform.localRotation = Quaternion.Euler(60f, -25f, 15f);
            sleeveExt.transform.localScale = new Vector3(0.072f, 0.135f, 0.072f);
            sleeveExt.GetComponent<Renderer>().sharedMaterial = armsSleeveMat;
            var cExt = sleeveExt.GetComponent<Collider>(); if (cExt != null) Destroy(cExt);

            // Turn off shadow casting on FPS knife rig to prevent self-shadow glitches
            foreach (var r in combatKnife.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            combatKnife.gameObject.SetActive(false);
        }

        public void TriggerKnifeSlash(bool isLethal = false)
        {
            knifeSlashTimer = 0.52f;
            knifeIsLethal = isLethal;
            knifeHitEffectPlayed = false;
            if (isLethal) knifeBloodTimer = 1.3f;
            if (combatKnife != null) combatKnife.gameObject.SetActive(true);
            if (rifle != null) rifle.gameObject.SetActive(false);
        }


        void BuildMuzzleFlash(Camera camera)
        {
            muzzleFlash=new GameObject("Muzzle flash");
            muzzleFlash.transform.SetParent(camera.transform,false);
            muzzleFlash.transform.localPosition=new Vector3(.20f,-.22f,1.5f);
            muzzleLight=muzzleFlash.AddComponent<Light>();
            muzzleLight.type=LightType.Point; muzzleLight.range=35; muzzleLight.intensity=0;
            muzzleLight.color=new Color(1f,.72f,.22f);
            
            suppressedShot = ProceduralAudio.CreateSuppressedShot();

            var flashShader=Shader.Find("Unlit/Color") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if(flashShader!=null)
            {
                var coreMat=new Material(flashShader);
                coreMat.color=new Color(1f,.96f,.75f);
                owned.Add(coreMat);

                var flameMat=new Material(flashShader);
                flameMat.color=new Color(1f,.52f,.08f);
                owned.Add(flameMat);
                
                var mesh = GetSharedQuadMesh();
                for(int i=0; i<4; i++) 
                {
                    var flash=new GameObject("Flash quad "+i);
                    flash.transform.SetParent(muzzleFlash.transform,false);
                    flash.transform.localScale=new Vector3(0.7f, 0.7f, 0.7f);
                    flash.transform.localRotation = Quaternion.Euler(0, 0, i * 45);
                    var mf = flash.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;
                    var mr = flash.AddComponent<MeshRenderer>();
                    mr.sharedMaterial=i%2==0?coreMat:flameMat;
                    mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                var cone = new GameObject("Flash cone");
                cone.transform.SetParent(muzzleFlash.transform, false);
                cone.transform.localScale = new Vector3(0.5f, 1.4f, 1f);
                cone.transform.localPosition = new Vector3(0, 0, 0.45f);
                cone.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var cmf = cone.AddComponent<MeshFilter>();
                cmf.sharedMesh = mesh;
                var cmr = cone.AddComponent<MeshRenderer>();
                cmr.sharedMaterial = flameMat;
                cmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            muzzleFlash.SetActive(false);
        }
        void BuildTracerLine()
        {
            var tracerObj=new GameObject("Bullet tracer");
            tracerObj.transform.SetParent(transform,false);
            tracerLine=tracerObj.AddComponent<LineRenderer>();
            tracerLine.positionCount=2;
            tracerLine.startWidth=.025f; tracerLine.endWidth=.008f;
            tracerLine.useWorldSpace=true;
            var shader=Shader.Find("Unlit/Color") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if(shader!=null)
            {
                var mat=new Material(shader);
                mat.color=new Color(1f,.92f,.5f);
                tracerLine.sharedMaterial=mat;
                owned.Add(mat);
            }
            tracerLine.enabled=false;
        }
        public void Shot() 
        { 
            OnShotFired();
            AudioClip activeReport = CurrentWeaponIndex == 2 ? (suppressedShot ?? shot) : shot;
            speaker.PlayOneShot(activeReport, CurrentWeaponIndex == 2 ? 0.7f : 1.0f); 
            CampaignAcoustics.RifleEcho(speaker,CurrentWeaponIndex==2);
            ShowMuzzleFlash();
            if(!WeaponHandlingMotion.BoltAction(CurrentWeaponIndex))EjectCasing();
        }
        public void ShotWithTracer(Vector3 from,Vector3 to,bool hitEnemy)
        {
            OnShotFired();
            AudioClip activeReport = CurrentWeaponIndex == 2 ? (suppressedShot ?? shot) : shot;
            speaker.PlayOneShot(activeReport, CurrentWeaponIndex == 2 ? 0.7f : 1.0f); 
            CampaignAcoustics.RifleEcho(speaker,CurrentWeaponIndex==2);
            ShowMuzzleFlash();
            if(!WeaponHandlingMotion.BoltAction(CurrentWeaponIndex))EjectCasing();
            ResolveProjectileVisual(from,to,hitEnemy);
        }

        void EjectCasing()
        {
            if (cam == null) return;
            var casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            casing.name = "SpentBrassCasing";
            casing.transform.localScale = new Vector3(0.016f, 0.040f, 0.016f);
            
            Vector3 spawnPos = cam.transform.position + cam.transform.right * 0.22f - cam.transform.up * 0.12f + cam.transform.forward * 0.45f;
            casing.transform.position = spawnPos;
            casing.transform.rotation = cam.transform.rotation * Quaternion.Euler(UnityEngine.Random.Range(-20f, 20f), 90f, UnityEngine.Random.Range(-30f, 30f));

            var rend = casing.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = casingMaterial;
            }

            var rb = casing.AddComponent<Rigidbody>();
            rb.mass = 0.04f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Vector3 ejectDir = (cam.transform.right * UnityEngine.Random.Range(1.8f, 2.6f) + cam.transform.up * UnityEngine.Random.Range(1.2f, 2.0f) - cam.transform.forward * UnityEngine.Random.Range(0.4f, 0.9f)).normalized;
            rb.linearVelocity = ejectDir * UnityEngine.Random.Range(3.2f, 4.8f);
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 40f;

            StartCoroutine(PlayCasingClinkDelayed(0.35f));
            Destroy(casing, 2.5f);
        }

        System.Collections.IEnumerator PlayCasingClinkDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (speaker != null && casingClink!=null) speaker.PlayOneShot(casingClink, 0.35f);
        }
        public void ResolveProjectileVisual(Vector3 from,Vector3 to,bool hitEnemy)
        {
            if (!BallisticsSystem.isBulletCamActive) ShowTracer(from, to);
        }
        public void HideTracer()
        {
            if (tracerLine != null) tracerLine.enabled = false;
            tracerTimer = 0;
        }
        public void ReloadSound(float duration=1.4f)
        {
            reloadDuration=Mathf.Max(.1f,duration);reloadSoundPhase=0;reloadAudioActive=true;
            boltCycleTime=boltCycleDuration=0;boltSoundPhase=2;
        }
        public void HitSound() { speaker.PlayOneShot(hit); }
        public void EmptySound() { speaker.PlayOneShot(empty); }
        public void ToggleMute() { Muted=!Muted; speaker.mute=Muted;CampaignAcoustics.SetMuted(Muted); }
        void ShowMuzzleFlash()
        {
            if(muzzleFlash==null || BallisticsSystem.isBulletCamActive) return;
            flashTimer=.035f;
            muzzleFlash.SetActive(true);
            muzzleLight.intensity=MobileGraphics.Selected==MobileGraphicsPreset.Performance?0f:2.2f;

            // Position at active weapon barrel tip
            Vector3 muzzleOffset = rifle!=null ? cam.transform.InverseTransformPoint(rifle.TransformPoint(muzzleLocal)) : new Vector3(.19f,-.25f,1.35f);
            muzzleFlash.transform.localPosition = muzzleOffset;
            muzzleFlash.transform.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));
            muzzleFlash.transform.localScale = Vector3.one * UnityEngine.Random.Range(.12f,.2f);

            if (cam != null)
            {
                Vector3 worldMuzzle = cam.transform.TransformPoint(muzzleOffset);
                SpawnMuzzleSparks(worldMuzzle, cam.transform.forward);
            }
        }

        public static void SpawnMuzzleSparks(Vector3 origin, Vector3 forward)
        {
            var fx = new GameObject("MuzzleSparksFX");
            fx.transform.position = origin;
            fx.transform.rotation = Quaternion.LookRotation(forward);

            var ps = fx.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.15f;
            main.loop = false;
            main.startLifetime = 0.08f;
            main.startSpeed = 38f;
            main.startSize = 0.04f;
            main.startColor = new Color(1f, 0.88f, 0.35f, 0.95f);

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 18) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.02f;

            var psr = fx.GetComponent<ParticleSystemRenderer>();
            var pMat = GetParticleMaterial();
            if (pMat != null) psr.sharedMaterial = pMat;

            ps.Play();
            Destroy(fx, 0.3f);
        }
        void ShowTracer(Vector3 from,Vector3 to)
        {
            if(tracerLine==null) return;
            tracerLine.SetPosition(0,from);
            tracerLine.SetPosition(1,to);
            tracerLine.enabled=true;
            tracerTimer=.05f;
        }

        static Material cachedParticleMat;
        static Material GetParticleMaterial()
        {
            if (cachedParticleMat == null)
            {
                var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Sprites/Default");
                if (shader != null) cachedParticleMat = new Material(shader);
            }
            return cachedParticleMat;
        }

        public static void SpawnImpactSparks(Vector3 position)
        {
            EmitSurfaceImpact(position,true,false);
        }

        static readonly List<ParticleSystem> impactPool=new List<ParticleSystem>(12);
        static Material dustMaterial;
        public static void SpawnSurfaceImpact(Vector3 position,Collider surface)
        {
            if(surface==null || surface.GetComponentInParent<CombatActor>()!=null)return;
            string name=surface.name.ToLowerInvariant();
            bool metal=name.Contains("metal") || name.Contains("steel") || name.Contains("rail")
                || name.Contains("vehicle") || name.Contains("car") || name.Contains("tank");
            EmitSurfaceImpact(position,metal,name.Contains("water"));
            GeoSniper.Duel.SniperDuelManager.ActiveInstance?.NotifySurfaceImpact(position, metal ? (byte)1 : name.Contains("water") ? (byte)2 : (byte)0);
        }

        public static void PlayRemoteSurfaceImpact(Vector3 position, byte kind) => EmitSurfaceImpact(position, kind == 1, kind == 2);

        static void EmitSurfaceImpact(Vector3 position,bool metal,bool water)
        {
            byte kind=metal ? (byte)1 : water ? (byte)2 : (byte)0;
            if(!CampaignAcoustics.Impact(position,kind))GeoSniper.Duel.DuelAudioFeedback.PlaySurfaceImpact(position,kind);
            ParticleSystem ps=null;
            for(int i=impactPool.Count-1;i>=0;i--)
            {
                if(impactPool[i]==null){impactPool.RemoveAt(i);continue;}
                if(!impactPool[i].IsAlive())ps=impactPool[i];
            }
            int cap=MobileGraphics.Selected==MobileGraphicsPreset.Performance?6:12;
            if(ps==null)
            {
                if(impactPool.Count>=cap)return;
                ps=new GameObject("Pooled surface impact").AddComponent<ParticleSystem>();
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var settings=ps.main;settings.loop=false;settings.playOnAwake=false;settings.duration=.8f;
                settings.maxParticles=12;settings.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=ps.emission;emission.enabled=false;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Hemisphere;shape.radius=.035f;
                var fade=ps.colorOverLifetime;fade.enabled=true;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});
                fade.color=gradient;
                var renderer=ps.GetComponent<ParticleSystemRenderer>();
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                impactPool.Add(ps);
            }
            if(dustMaterial==null)
            {
                var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);
                var pixels=new Color[1024];
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                {
                    float a=Mathf.Clamp01(1f-new Vector2((x-15.5f)/16f,(y-15.5f)/16f).sqrMagnitude);
                    pixels[y*32+x]=new Color(1,1,1,a*a);
                }
                texture.SetPixels(pixels);texture.Apply(false,true);
                dustMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=texture};
            }
            ps.transform.position=position;ps.transform.rotation=Quaternion.Euler(-90,0,0);
            var main=ps.main;
            main.startLifetime=metal?.18f:.65f;main.startSpeed=metal?5f:water?2.4f:1.1f;
            main.startSize=metal?.035f:water?.08f:.18f;main.gravityModifier=metal?1f:water?.8f:-.03f;
            main.startColor=metal?new Color(1f,.7f,.25f):water?new Color(.6f,.8f,.85f,.65f):new Color(.57f,.53f,.46f,.6f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=dustMaterial;
            ps.Play();
            ps.Emit(MobileGraphics.Selected==MobileGraphicsPreset.Performance?4:8);
        }

        public static void SpawnMissDustPlume(Vector3 position)
        {
            var fx = new GameObject("MissDustPlumeFX");
            fx.transform.position = position;

            var ps = fx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 2.8f;
            main.loop = false;
            main.startLifetime = 2.2f;
            main.startSpeed = 2.5f;
            main.startSize = 0.40f;
            main.gravityModifier = -0.06f;
            main.startColor = new Color(0.72f, 0.68f, 0.60f, 0.55f);

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { 
                new ParticleSystem.Burst(0f, 16),
                new ParticleSystem.Burst(0.08f, 10)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.1f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.4f);
            curve.AddKey(1f, 2.2f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.78f, 0.74f, 0.68f), 0f), new GradientColorKey(new Color(0.55f, 0.52f, 0.48f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.35f, 0.4f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var psr = fx.GetComponent<ParticleSystemRenderer>();
            var pMat = GetParticleMaterial();
            if (pMat != null) psr.sharedMaterial = pMat;

            ps.Play();
            Destroy(fx, 3.0f);
        }
        void UpdateEffects()
        {
            if (BallisticsSystem.isBulletCamActive)
            {
                if (tracerLine != null) { tracerLine.enabled = false; tracerTimer = 0; }
                if (muzzleFlash != null && muzzleFlash.activeSelf)
                {
                    muzzleFlash.SetActive(false);
                    flashTimer = 0f;
                    if (muzzleLight != null) muzzleLight.intensity = 0f;
                }
            }
            if(flashTimer>0)
            {
                flashTimer-=Time.deltaTime;
                muzzleLight.intensity=MobileGraphics.Selected==MobileGraphicsPreset.Performance?0f:2.2f*Mathf.Clamp01(flashTimer/.035f);
                if(flashTimer<=0) muzzleFlash.SetActive(false);
            }
            if(tracerTimer>0)
            {
                tracerTimer-=Time.deltaTime;
                if(tracerTimer<=0 && tracerLine!=null) tracerLine.enabled=false;
            }
            if(cam!=null && shakeIntensity>0)
            {
                shakeIntensity=Mathf.MoveTowards(shakeIntensity,0,Time.deltaTime*8);
                cam.transform.localPosition=cam.transform.localPosition-shakeOffset;
                shakeOffset=UnityEngine.Random.insideUnitSphere*shakeIntensity*.012f;
                cam.transform.localPosition=cam.transform.localPosition+shakeOffset;
            }
            else if(shakeOffset.sqrMagnitude>.0001f)
            {
                cam.transform.localPosition=cam.transform.localPosition-shakeOffset;
                shakeOffset=Vector3.zero;
            }
            if(hitMarkerTimer>0) hitMarkerTimer-=Time.deltaTime;
            if(killTimer>0) killTimer-=Time.deltaTime;
            if (knifeBloodTimer > 0f) knifeBloodTimer -= Time.deltaTime;
            if (knifeSlashTimer > 0f)
            {
                knifeSlashTimer -= Time.deltaTime;
                const float totalDur = 0.52f;
                float progress = 1f - Mathf.Clamp01(knifeSlashTimer / totalDur);
                if (combatKnife != null)
                {
                    Vector3 pos;
                    Quaternion rot;

                    if (progress < 0.16f)
                    {
                        float p = progress / 0.16f;
                        Vector3 p0 = new Vector3(0.18f, -0.24f, 0.32f);
                        Vector3 p1 = new Vector3(0.22f, -0.13f, 0.29f);
                        pos = Vector3.Lerp(p0, p1, p);
                        rot = Quaternion.Euler(Mathf.Lerp(18f, 28f, p), Mathf.Lerp(-15f, -24f, p), Mathf.Lerp(12f, 22f, p));
                    }
                    else if (progress < 0.30f)
                    {
                        float p = (progress - 0.16f) / 0.14f;
                        float ease = p * p;
                        Vector3 p1 = new Vector3(0.22f, -0.13f, 0.29f);
                        Vector3 p2 = new Vector3(-0.08f, -0.04f, 0.36f);
                        pos = Vector3.Lerp(p1, p2, ease);
                        rot = Quaternion.Euler(Mathf.Lerp(28f, -18f, ease), Mathf.Lerp(-24f, 32f, ease), Mathf.Lerp(22f, -40f, ease));
                    }
                    else if (progress < 0.38f)
                    {
                        float p = (progress - 0.30f) / 0.08f;
                        Vector3 p2 = new Vector3(-0.08f, -0.04f, 0.36f);
                        Vector3 p3 = new Vector3(-0.24f, -0.16f, 0.33f);
                        pos = Vector3.Lerp(p2, p3, p);
                        rot = Quaternion.Euler(Mathf.Lerp(-18f, -25f, p), Mathf.Lerp(32f, 40f, p), Mathf.Lerp(-40f, -50f, p));
                    }
                    else
                    {
                        float p = (progress - 0.38f) / 0.14f;
                        Vector3 p3 = new Vector3(-0.24f, -0.16f, 0.33f);
                        Vector3 pEnd = new Vector3(0.20f, -0.34f, 0.28f);
                        pos = Vector3.Lerp(p3, pEnd, p * p);
                        rot = Quaternion.Euler(Mathf.Lerp(-25f, 22f, p), Mathf.Lerp(40f, -12f, p), Mathf.Lerp(-50f, 15f, p));
                    }

                    combatKnife.localPosition = pos;
                    combatKnife.localRotation = rot;

                    // Trigger visceral impact effects at strike apex
                    if (progress >= 0.28f && !knifeHitEffectPlayed)
                    {
                        knifeHitEffectPlayed = true;
                        if (cam != null)
                        {
                            cam.transform.localRotation *= Quaternion.Euler(2.8f, -1.8f, 0.6f);
                        }
                        inertiaPos += new Vector3(-0.025f, 0.02f, -0.05f);
                    }
                }
                if (knifeSlashTimer <= 0f)
                {
                    if (combatKnife != null) combatKnife.gameObject.SetActive(false);
                    if (rifle != null) rifle.gameObject.SetActive((!UsesOptic || scopeBlend < 0.60f) && !BallisticsSystem.isBulletCamActive);
                }
            }

            // Lens Condensation & Breath Wipe (condenses in rain/night, wiped clean when holding breath)
            bool isColdOrRain = RainEffect || SectorStreetLighting.IsNight;
            bool isHoldingBreath = (sway != null && sway.breathStamina < 0.98f);
            float targetCond = (isColdOrRain && scopeBlend > 0.4f) ? 0.20f : 0f;
            if (isHoldingBreath) targetCond = 0f;
            CondensationAlpha = Mathf.MoveTowards(CondensationAlpha, targetCond, Time.deltaTime * (isHoldingBreath ? 3.0f : 0.45f));

            // FLIR Thermal toggle hotkey (V / T)
            if (Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.T))
            {
                ThermalMode = !ThermalMode;
            }
        }
        
        public void CycleZoom()
        {
            if (Config == null || Config.zoomLevels == null || Config.zoomLevels.Length == 0) return;
            currentZoomIndex = (currentZoomIndex + 1) % Config.zoomLevels.Length;
            MobileCombatInput.ZoomSliderValue=currentZoomIndex/(float)Mathf.Max(1,Config.zoomLevels.Length-1);
        }

        public void SetSway(WeaponSway newSway) { sway = newSway; }

        public void SetVisible(bool visible)
        {
            if (rifle != null) rifle.gameObject.SetActive(visible);
            if (!visible && muzzleFlash != null)
            {
                muzzleFlash.SetActive(false);
                flashTimer = 0f;
                if (muzzleLight != null) muzzleLight.intensity = 0f;
            }
        }

        public void ResetAimPresentation()
        {
            scopeBlend=0;
            if(MobilePostProcess.Instance!=null)MobilePostProcess.Instance.scopeBlend=0;
            if(thermalOverlay!=null)thermalOverlay.SetScopeState(false,0,null);
        }

        public void Animate(bool zoom, float reload, bool isMoving, bool isSprinting = false, bool isCrouching = false, float speed = 0f)
        {
            if(rifle==null) return;
            if (BallisticsSystem.isBulletCamActive)
            {
                rifle.gameObject.SetActive(false);
                if(thermalOverlay!=null)thermalOverlay.SetScopeState(false, 0, null);
                return;
            }
            if(reload>0)zoom=false;
            scopeBlend=Mathf.Lerp(scopeBlend,zoom?1f:0f,1-Mathf.Exp(-Time.deltaTime*16f));
            if (thermalOverlay == null && cam != null) thermalOverlay=ThermalVisionOverlay.Ensure(cam);
            if (MobilePostProcess.Instance != null) MobilePostProcess.Instance.scopeBlend = UsesOptic ? scopeBlend : 0f;
            bool showRifle = (!UsesOptic || scopeBlend < .60f) && knifeSlashTimer <= 0f && (UrbanPlayer.Instance == null || !UrbanPlayer.Instance.BinocularsMode);
            rifle.gameObject.SetActive(showRifle);
            WeaponHandlingMotion.Recover(ref kick,ref kickVelocity,Time.deltaTime);
            boltCycleTime=Mathf.Max(0,boltCycleTime-Time.deltaTime);
            shotKick=Mathf.MoveTowards(shotKick,0,Time.deltaTime*5f);
            float reloadProgress=reload>0?Mathf.Clamp01(1-reload/Mathf.Max(.1f,reloadDuration)):1;
            float tilt=WeaponHandlingMotion.ReloadEnvelope(reloadProgress);
            float boltProgress=boltCycleDuration>0?1-boltCycleTime/boltCycleDuration:1;
            float boltPose=WeaponHandlingMotion.BoltHand(boltProgress);
            if(boltSoundPhase==0 && boltCycleDuration>0 && boltProgress>=.25f)
            {HandlingClick(0);EjectCasing();boltSoundPhase=1;}
            if(boltSoundPhase==1 && boltProgress>=.72f){HandlingClick(2);boltSoundPhase=2;}
            if(reloadAudioActive)
            {
                if(reloadSoundPhase==0 && reloadProgress>=.28f){HandlingClick(0);reloadSoundPhase=1;}
                if(reloadSoundPhase==1 && reloadProgress>=.60f){HandlingClick(1);reloadSoundPhase=2;}
                if(reloadSoundPhase==2 && reloadProgress>=.84f){HandlingClick(2);reloadSoundPhase=3;}
                if(reloadProgress>=1)reloadAudioActive=false;
            }
            if(handlingPose!=null)handlingPose.SetHandling(reloadProgress,boltPose,WeaponHandlingMotion.BoltPull(boltProgress));
            
            Vector2 swayOffset = Vector2.zero;
            if (sway != null) swayOffset = sway.GetSwayOffset(isMoving, zoom);
            
            // Call of Duty Tactical Sprint & Crouch Stance Transitions
            bool tacticalSprint = isSprinting && !zoom && isMoving && reload <= 0;
            tacticalSprintBlend = Mathf.MoveTowards(tacticalSprintBlend, tacticalSprint ? 1f : 0f, Time.deltaTime * (tacticalSprint ? 6.5f : 8.5f));
            crouchBlend = Mathf.MoveTowards(crouchBlend, (isCrouching && !zoom) ? 1f : 0f, Time.deltaTime * 6f);
            
            // Call of Duty Stride Cadence & Dual-Harmonic Figure-8 Weapon Bob
            movementBlend=Mathf.Lerp(movementBlend,isMoving?1:0,1-Mathf.Exp(-Time.deltaTime*12f));
            float cadence=Mathf.Lerp(2.1f,Mathf.Lerp(Mathf.Lerp(6.4f,4.8f,crouchBlend),9.8f,tacticalSprintBlend),movementBlend);
            stridePhase += Time.deltaTime * cadence;
            
            float bobAmpX=Mathf.Lerp(Mathf.Lerp(.016f,.009f,crouchBlend),.034f,tacticalSprintBlend);
            float bobAmpY=Mathf.Lerp(Mathf.Lerp(.013f,.007f,crouchBlend),.028f,tacticalSprintBlend);
            float bobAmpRoll=Mathf.Lerp(Mathf.Lerp(1.9f,1f,crouchBlend),4.8f,tacticalSprintBlend);
            bobAmpX=Mathf.Lerp(.0016f,bobAmpX,movementBlend);
            bobAmpY=Mathf.Lerp(.0014f,bobAmpY,movementBlend);
            bobAmpRoll=Mathf.Lerp(.35f,bobAmpRoll,movementBlend);
            
            if (zoom)
            {
                bobAmpX *= 0.12f;
                bobAmpY *= 0.12f;
                bobAmpRoll *= 0.08f;
            }
            
            float bobX = Mathf.Cos(stridePhase) * bobAmpX;
            float bobY = (Mathf.Cos(stridePhase * 2f) - 1f) * 0.5f * bobAmpY;
            float bobRoll = Mathf.Sin(stridePhase) * bobAmpRoll;
            
            // Call of Duty Look Inertia & Weapon Drag / Rotational Lag
            if (cam != null)
            {
                if (!hasLastCamRot)
                {
                    lastCamRot = cam.transform.rotation;
                    hasLastCamRot = true;
                }
                Quaternion camRot = cam.transform.rotation;
                Vector3 deltaEuler = (Quaternion.Inverse(lastCamRot) * camRot).eulerAngles;
                float deltaYaw = Mathf.DeltaAngle(0, deltaEuler.y);
                float deltaPitch = Mathf.DeltaAngle(0, deltaEuler.x);
                float rateScale=1f/Mathf.Max(.001f,Time.deltaTime*60f);
                deltaYaw=Mathf.Clamp(deltaYaw*rateScale,-8,8);deltaPitch=Mathf.Clamp(deltaPitch*rateScale,-8,8);
                lastCamRot = camRot;
                
                float zoomLagMult = zoom ? 0.22f : 1.0f;
                Vector3 targetLagPos = new Vector3(-deltaYaw * 0.0026f, deltaPitch * 0.0022f, 0f) * zoomLagMult;
                targetLagPos = Vector3.ClampMagnitude(targetLagPos, 0.032f);
                inertiaPos = Vector3.Lerp(inertiaPos, targetLagPos, 1-Mathf.Exp(-Time.deltaTime*18f));
                
                Vector3 targetLagRot = new Vector3(deltaPitch * 0.85f, -deltaYaw * 0.75f, deltaYaw * 0.65f) * zoomLagMult;
                inertiaRot = Vector3.Lerp(inertiaRot, targetLagRot, 1-Mathf.Exp(-Time.deltaTime*18f));
            }
            inertiaPos *= Mathf.Exp(-Time.deltaTime*8.5f);
            inertiaRot *= Mathf.Exp(-Time.deltaTime*8.5f);
            
            // Signature Call of Duty Tactical Sprint High-Ready Carry Pose
            Vector3 sprintPosOffset = new Vector3(-0.038f, -0.055f, -0.045f) * tacticalSprintBlend;
            Quaternion sprintRotOffset = Quaternion.Euler(-26f * tacticalSprintBlend, 20f * tacticalSprintBlend, -14f * tacticalSprintBlend);
            
            // Crouch tuck offset
            Vector3 crouchPosOffset = new Vector3(-0.015f, 0.012f, -0.025f) * crouchBlend;
            Quaternion crouchRotOffset = Quaternion.Euler(2.2f * crouchBlend, -2.5f * crouchBlend, 1.2f * crouchBlend);
            
            Vector3 targetPos = weaponViewPosition + sprintPosOffset + crouchPosOffset + new Vector3(
                bobX + swayOffset.x * 0.02f + inertiaPos.x-tilt*.055f,
                -tilt * 0.045f + bobY + swayOffset.y * 0.02f + inertiaPos.y,
                -kick * 0.08f - tilt*.035f + inertiaPos.z
            );
            rifle.localPosition = targetPos;
            if(!UsesOptic)
                rifle.localPosition=Vector3.Lerp(targetPos,ironSightPosition+new Vector3(0,-tilt*.12f,-kick*.08f),scopeBlend);
            
            Quaternion targetRot = weaponViewRotation * sprintRotOffset * crouchRotOffset * Quaternion.Euler(
                -kick * (CurrentWeaponIndex == 0 ? 11f : 8f) - tilt * 8f - swayOffset.y * 1.5f + inertiaRot.x,
                swayOffset.x * 1.5f + bobRoll * 0.45f + inertiaRot.y,
                tilt * -24f + bobRoll + inertiaRot.z+boltPose*3f
            );
            rifle.localRotation = targetRot;
            if(!UsesOptic) rifle.localRotation=Quaternion.Slerp(targetRot,Quaternion.Euler(-kick*8f,0,0),scopeBlend);
            
            float targetFOV = 65f;
            if (UrbanPlayer.Instance != null && UrbanPlayer.Instance.BinocularsMode)
            {
                targetFOV = 65f / UrbanPlayer.Instance.BinocularZoom;
                currentFOV = ScopeOptics.SmoothFov(currentFOV, targetFOV,14,Time.deltaTime);
            }
            else if (Config != null) 
            {
                if (zoom && !UsesOptic)
                    targetFOV=Config.zoomLevels!=null && Config.zoomLevels.Length>0?Config.zoomLevels[0]:WeaponCatalog.Get(CurrentWeaponIndex).Fov;
                else if (zoom)
                {
                    targetFOV = ScopeOptics.ZoomFov(Config,MobileCombatInput.ZoomSliderValue);
                }
                else
                {
                    targetFOV = Config.hipFOV;
                }
                currentFOV = ScopeOptics.SmoothFov(currentFOV, targetFOV,Config.scopeTransitionSpeed,Time.deltaTime);
            }
            else 
            {
                targetFOV = zoom ? 12f : 65f;
                currentFOV = ScopeOptics.SmoothFov(currentFOV, targetFOV,10,Time.deltaTime);
            }
            
            if (cam != null)
            {
                cam.fieldOfView = currentFOV;
                if(zoom)
                {
                    float settle=Mathf.Sin(breathPhase*2f)*(.045f+(.025f*(1f-scopeBlend)));
                    cam.transform.localRotation*=Quaternion.Euler(settle,-settle*.65f,0);
                }

                // Optical Target Acquisition & Ballistic Telemetry Raycast
                if (scopeBlend > 0.05f)
                {
                    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                    if (Physics.Raycast(ray, out RaycastHit hitInfo, 1200f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        targetDistance = hitInfo.distance;
                        targetElevation = hitInfo.point.y - cam.transform.position.y;
                        currentTargetRival = hitInfo.collider.GetComponentInParent<GeoSniper.Duel.DuelOpponentAvatar>();
                        var actor = hitInfo.collider.GetComponentInParent<CombatActor>();
                        if (currentTargetRival != null && !currentTargetRival.IsDead)
                        {
                            currentTargetActor = null;
                            currentTargetClassification = "REAL PLAYER";
                        }
                        else if (actor != null && !actor.IsDead)
                        {
                            currentTargetRival = null;
                            currentTargetActor = actor;
                            var enemy = hitInfo.collider.GetComponentInParent<EnemyBot>();
                            var civ = hitInfo.collider.GetComponentInParent<CivilianBot>();
                            if (enemy != null) currentTargetClassification = "HOSTILE / TARGET";
                            else if (civ != null) currentTargetClassification = "CIVILIAN / NO-FIRE";
                            else currentTargetClassification = "OCCUPANT / UNKNOWN";
                        }
                        else
                        {
                            currentTargetActor = null;
                            currentTargetRival = null;
                            currentTargetClassification = "";
                        }
                    }
                    else
                    {
                        targetDistance = -1f;
                        targetElevation = 0f;
                        currentTargetActor = null;
                        currentTargetRival = null;
                        currentTargetClassification = "";
                    }
                }
                else
                {
                    targetDistance = -1f;
                    targetElevation = 0f;
                    currentTargetActor = null;
                    currentTargetRival = null;
                    currentTargetClassification = "";
                }
            }
            
            UpdateEffects();
            if(thermalOverlay!=null)
            {
                var viewer=UrbanPlayer.Instance;
                bool canView=viewer==null || (viewer.Scoped && !viewer.InputBlocked && viewer.Health!=null && !viewer.Health.IsDead);
                thermalOverlay.SetScopeState(UsesOptic && ThermalMode && zoom && canView,scopeBlend,GeoSniper.Duel.SniperDuelManager.ActiveOpponent);
            }
        }
        public bool ThermalMode;
        public bool RainEffect;
        public float CondensationAlpha = 0f;
        public int CurrentWeaponIndex = WeaponCatalog.StarterPrimary;
        public bool UsesOptic => CurrentWeaponIndex < 5;
        Vector3 ironSightPosition;
        public string WeaponName => WeaponCatalog.Get(CurrentWeaponIndex).Name;

        public bool UseDuelStats;

        public int MaxAmmo
        {
            get
            {
                int baseAmmo = WeaponCatalog.Get(CurrentWeaponIndex).Magazine;
                if (UseDuelStats) return baseAmmo;
                return WeaponProgression.Magazine(CurrentWeaponIndex,WeaponProgression.Tier(CurrentWeaponIndex,WeaponUpgrade.Magazine));
            }
        }

        public void EquipWeaponModel(int index)
        {
            kick=kickVelocity=boltCycleTime=boltCycleDuration=0;boltSoundPhase=2;reloadAudioActive=false;handlingPose=null;hasLastCamRot=false;
            CurrentWeaponIndex=Mathf.Clamp(index,0,WeaponCatalog.Count-1);
            if(cam==null) return;
            if(rifle!=null) { DestroyPresentationObject(rifle.gameObject); rifle=null; }
            string[] primaryPaths = { "Models/Weapons/Barrett50", "Models/Weapons/M24Tactical", "Models/Weapons/MK12SPR" };
            string[] fallbackPaths = { "Models/GeoSniperRifleProvided", "Models/Weapons/M24Tactical", "Models/SniperRifle" };
            var imported = Resources.Load<GameObject>("Models/Weapons/"+WeaponCatalog.Get(CurrentWeaponIndex).Model) ?? Resources.Load<GameObject>(fallbackPaths[Mathf.Min(CurrentWeaponIndex,2)]);
            
            rifle = new GameObject("First person " + WeaponName).transform;
            rifle.SetParent(cam.transform, false);
            
            if (imported != null)
            {
                var model = Instantiate(imported, rifle, false);
                ImportedVisual.SanitizeWeaponModel(model.transform);
                if(!WeaponGeometry.Configure(model.transform, rifle, CurrentWeaponIndex)) return;
                var calibrated=ImportedVisual.LocalBounds(rifle);
                ironSightPosition=new Vector3(-calibrated.center.x,-calibrated.max.y,.37f);
                muzzleLocal=new Vector3(calibrated.center.x,calibrated.center.y,calibrated.max.z);
                cam.nearClipPlane = .05f;
                weaponViewPosition = CurrentWeaponIndex == 0 ? new Vector3(.16f, -.225f, .38f) : new Vector3(.15f, -.225f, .37f);
                weaponViewRotation = Quaternion.Euler(0, 1, -2);
                // Start outside the camera even before the first gameplay update.
                rifle.localPosition = weaponViewPosition;
                rifle.localRotation = weaponViewRotation;
                
                ApplyRifleMaterials(model.transform);
                AttachFPSArms(rifle, CurrentWeaponIndex);
            }
            if (speaker != null && bolt != null) speaker.PlayOneShot(bolt, 0.75f);
        }

        static GameObject fpsArmsRigPrefab;
        static Material armsSleeveMat;
        static Material armsGloveMat;

        static void EnsureFPSArmsMaterials()
        {
            if (armsSleeveMat != null && armsGloveMat != null) return;
            var shader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse");
            armsSleeveMat = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = "FPSArmsSleeveMat" };
            armsGloveMat = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = "FPSArmsGloveMat" };
            
            var sleeveTex = Resources.Load<Texture2D>("Models/FPSArms/Textures/arms_baseColor") ?? Resources.Load<Texture2D>("Models/FPSArms/Textures/Arms_diffuse");
            var gloveTex = Resources.Load<Texture2D>("Models/FPSArms/Textures/arms_baseColor") ?? Resources.Load<Texture2D>("Models/FPSArms/Textures/Gloves_diffuse");
            var sleeveNorm = Resources.Load<Texture2D>("Models/FPSArms/Textures/arms_normal") ?? Resources.Load<Texture2D>("Models/FPSArms/Textures/Arms_normal");
            var gloveNorm = Resources.Load<Texture2D>("Models/FPSArms/Textures/arms_normal") ?? Resources.Load<Texture2D>("Models/FPSArms/Textures/Gloves_normal");

            if (sleeveTex != null) armsSleeveMat.mainTexture = sleeveTex;
            else armsSleeveMat.color = new Color(0.24f, 0.28f, 0.22f);
            if (sleeveNorm != null && armsSleeveMat.HasProperty("_BumpMap")) { armsSleeveMat.EnableKeyword("_NORMALMAP"); armsSleeveMat.SetTexture("_BumpMap", sleeveNorm); }
            if (armsSleeveMat.HasProperty("_Glossiness")) armsSleeveMat.SetFloat("_Glossiness", 0.25f);

            if (gloveTex != null) armsGloveMat.mainTexture = gloveTex;
            else armsGloveMat.color = new Color(0.12f, 0.12f, 0.13f);
            if (gloveNorm != null && armsGloveMat.HasProperty("_BumpMap")) { armsGloveMat.EnableKeyword("_NORMALMAP"); armsGloveMat.SetTexture("_BumpMap", gloveNorm); }
            if (armsGloveMat.HasProperty("_Glossiness")) armsGloveMat.SetFloat("_Glossiness", 0.45f);
            if (armsGloveMat.HasProperty("_Metallic")) armsGloveMat.SetFloat("_Metallic", 0.35f);
        }

        void AttachFPSArms(Transform weaponRoot, int weaponIndex)
        {
            if (weaponRoot != null && weaponRoot.GetComponentInChildren<SkinnedMeshRenderer>() != null)
            {
                // Weapon already has rigged arms integrated (e.g. M24 Tactical)
                CalibrateTacticalHands(weaponRoot, weaponIndex);
                return;
            }
            if (fpsArmsRigPrefab == null)
            {
                fpsArmsRigPrefab = Resources.Load<GameObject>("Models/FPSArms/FPSSniperRifle");
            }
            if (fpsArmsRigPrefab == null) return;

            // Instantiate rigged FPS arms on a temporary holder to calibrate exact transform
            var dummyHolder = new GameObject("DummyArmsHolder").transform;
            var armsInstance = Instantiate(fpsArmsRigPrefab, dummyHolder, false);
            WeaponGeometry.Configure(armsInstance.transform, dummyHolder, 1);

            // Re-parent to weaponRoot
            armsInstance.transform.SetParent(weaponRoot, false);
            DestroyPresentationObject(dummyHolder.gameObject);
            armsInstance.name = "FirstPersonTacticalArmsRig";

            // Immediately disable and strip all M24 rifle meshes so ONLY the authentic rigged arms and hands remain
            foreach (var mr in armsInstance.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.enabled = false;
                mr.gameObject.SetActive(false);
                DestroyPresentationObject(mr.gameObject);
            }
            foreach (var mf in armsInstance.GetComponentsInChildren<MeshFilter>(true))
            {
                DestroyPresentationObject(mf);
            }

            // Remove any colliders
            foreach (var col in armsInstance.GetComponentsInChildren<Collider>(true))
            {
                DestroyPresentationObject(col);
            }

            // Ensure proper arm materials and shadow settings
            EnsureFPSArmsMaterials();
            foreach (var smr in armsInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smr.receiveShadows = false;
                if (smr.sharedMaterial == null || smr.sharedMaterial.mainTexture == null)
                {
                    smr.sharedMaterial = armsSleeveMat;
                }
            }

            if (weaponIndex == 0) // Barrett .50 Cal
            {
                armsInstance.transform.localPosition += new Vector3(-0.005f, 0.008f, 0.015f);
            }
            else if (weaponIndex == 2) // MK12 SPR DMR
            {
                armsInstance.transform.localPosition += new Vector3(0.002f, 0.005f, -0.010f);
            }

            CalibrateTacticalHands(armsInstance.transform, weaponIndex);
        }

        void CalibrateTacticalHands(Transform weaponOrArmsRoot, int weaponIndex)
        {
            if (weaponOrArmsRoot == null) return;
            leftArmBone = null;
            Transform lElbow = null, lWrist = null;
            foreach (var t in weaponOrArmsRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "L_arm_01") leftArmBone = t;
                else if (t.name == "L_elbow_02") lElbow = t;
                else if (t.name == "L_wrist_03") lWrist = t;
            }

            if (leftArmBone != null) leftArmBone.localScale = Vector3.one;
            if (lElbow != null) lElbow.localScale = Vector3.one;
            if (lWrist != null) lWrist.localScale = Vector3.one;
            var pose=weaponOrArmsRoot.GetComponent<FirstPersonGripPose>() ?? weaponOrArmsRoot.gameObject.AddComponent<FirstPersonGripPose>();
            pose.Initialize(rifle,weaponOrArmsRoot);
            handlingPose=pose;

        }

        public void DrawScope(float width, float height, int currentAmmo = -1, bool isReloading = false)
        {
            if(!UsesOptic) return;
            EnsureScopeStyles();
            float d = ScopeOptics.Diameter(width,height), x = (width - d) / 2, y = (height - d) / 2;
            
            // Scope Mask Outer Blackout
            GUI.color = new Color(.02f, .03f, .04f, Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(0, 0, x, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + d, 0, width - x - d, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, 0, d, y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + d, d, height - y - d), Texture2D.whiteTexture);
            
            // Thermal Mode Lens Overlay
            if (ThermalMode)
            {
                GUI.color = new Color(0f, 0.15f, 0.32f, 0.65f * Mathf.Clamp01(scopeBlend)); // Deep FLIR Thermal Blue
                GUI.DrawTexture(new Rect(x, y, d, d), Texture2D.whiteTexture);
            }

            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(x, y, d, d), scope);

            // Procedural Lens Condensation (subtle outer ring)
            if (CondensationAlpha > 0.01f)
            {
                GUI.color = new Color(0.82f, 0.90f, 0.98f, CondensationAlpha * 0.12f * Mathf.Clamp01(scopeBlend));
                GUI.DrawTexture(new Rect(x + 12, y + 12, d - 24, d - 24), scope);
            }

            // ThermalVisionOverlay draws the actual animated human meshes into
            // the camera depth buffer, including the real rival. Heat therefore
            // follows the visible silhouette and cannot reveal a player through cover.

            // Authentic Mil-Dot Crosshair (Clean, razor-sharp optic etched reticle)
            float milStep = height*.5f*Mathf.Tan(.001f)/Mathf.Tan(Mathf.Clamp(currentFOV,1,120)*Mathf.Deg2Rad*.5f);
            GUI.color = (ThermalMode ? new Color(0f, 1f, 0.8f, 0.9f) : new Color(0, 0, 0, .85f)) * Mathf.Clamp01(scopeBlend);
            Color reticleColor=GUI.color;
            GUI.color=new Color(.9f,.94f,.93f,.32f*Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(width / 2 - d * .435f,height / 2-.5f,d*.87f,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width / 2-.5f,height / 2-d*.435f,2,d*.87f),Texture2D.whiteTexture);
            GUI.color=reticleColor;
            GUI.DrawTexture(new Rect(width / 2 - d * 0.435f, height / 2, d * 0.87f, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width / 2, height / 2 - d * 0.435f, 1, d * 0.87f), Texture2D.whiteTexture);

            for (int i = -4; i <= 4; i++)
            {
                if (i == 0) continue;
                bool isMajor = (i % 2 == 0);
                float tickLen = isMajor ? 6f : 3f;
                float offset = i * milStep*2;
                if(Mathf.Abs(offset)>d*.38f)continue;

                GUI.DrawTexture(new Rect(width / 2 - tickLen * 0.5f, height / 2 + offset, tickLen, 1), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(width / 2 + offset, height / 2 - tickLen * 0.5f, 1, tickLen), Texture2D.whiteTexture);

                // Subtle BDC holdover dots (no numbers cluttering the optic)
                if (i > 0 && isMajor)
                {
                    float dotMil = milStep * 2f;
                    int dotCount = Mathf.Min(i / 2, 3);
                    for (int wDot = 1; wDot <= dotCount; wDot++)
                    {
                        GUI.DrawTexture(new Rect(width / 2 - wDot * dotMil - 1f, height / 2 + offset - 1f, 2, 2), Texture2D.whiteTexture);
                        GUI.DrawTexture(new Rect(width / 2 + wDot * dotMil - 1f, height / 2 + offset - 1f, 2, 2), Texture2D.whiteTexture);
                    }
                }
            }

            // Windage Ballistics Calculation
            float crosswind = cam != null ? Vector3.Dot(cam.transform.right, BallisticsSystem.Wind) : BallisticsSystem.Wind.x;
            bool directDuelShot = GeoSniper.Duel.SniperDuelManager.ActiveInstance != null && GeoSniper.Duel.SniperDuelManager.ActiveInstance.HasActiveDuel;
            float windHoldMils = !directDuelShot && targetDistance > 30f ? (crosswind * (targetDistance / 800f) * 1.5f) : 0f;
            float windPipPx = windHoldMils * milStep;

            // Subtle Dynamic Windage Holdover Diamond (clean aim point without text clutter)
            if (Mathf.Abs(windPipPx) > 1.5f && targetDistance > 30f)
            {
                float pipX = width / 2 - windPipPx;
                float pipY = height / 2;
                Matrix4x4 savedM = GUI.matrix;
                GUI.color = new Color(1.0f, 0.65f, 0.15f, 0.9f * Mathf.Clamp01(scopeBlend));
                GUIUtility.RotateAroundPivot(45f, new Vector2(pipX, pipY));
                GUI.DrawTexture(new Rect(pipX - 3f, pipY - 3f, 6, 6), Texture2D.whiteTexture);
                GUI.matrix = savedM;
            }

            // Subtle Miss Impact Indicator (small 0.5s pip, no large obstructive HUD cards)
            if (lastMissImpactTimer > 0f && cam != null)
            {
                if (TryProjectScopePoint(cam,lastMissImpactPos,width,height,out var p))
                {
                    float ix = p.x;
                    float iy = p.y;
                    if (Vector2.Distance(new Vector2(ix, iy), new Vector2(width / 2, height / 2)) < d * 0.43f)
                    {
                        float alpha = Mathf.Clamp01(lastMissImpactTimer / 1.0f);
                        GUI.color = new Color(1f, 0.35f, 0.15f, 0.85f * alpha);
                        GUI.DrawTexture(new Rect(ix - 3, iy - 3, 6, 6), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                }
            }

            // Center crosshair dot
            GUI.color = ThermalMode ? new Color(1f, 0.9f, 0.1f, 0.95f) : new Color(.9f, .15f, .1f, .9f);
            float cx = width / 2f, cy = height / 2f;
            float bSize = 5f * (d / 600f), bDist = 22f * (d / 600f);
            GUI.color = (ThermalMode ? new Color(0f, 1f, 0.8f, 0.85f) : new Color(0.05f, 0.08f, 0.1f, 0.85f)) * Mathf.Clamp01(scopeBlend);
            GUI.DrawTexture(new Rect(cx - bDist, cy - bDist, bSize, 1.2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - bDist, cy - bDist, 1.2f, bSize), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + bDist - bSize, cy - bDist, bSize, 1.2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + bDist, cy - bDist, 1.2f, bSize), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - bDist, cy + bDist, bSize, 1.2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - bDist, cy + bDist - bSize, 1.2f, bSize), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + bDist - bSize, cy + bDist, bSize, 1.2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + bDist, cy + bDist - bSize, 1.2f, bSize), Texture2D.whiteTexture);
            Color illColor = ThermalMode ? new Color(1f, 0.95f, 0.2f, 0.95f) : new Color(1f, 0.2f, 0.12f, 0.95f);
            GUI.color = new Color(illColor.r, illColor.g, illColor.b, 0.35f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = illColor * Mathf.Clamp01(scopeBlend);
            GUI.DrawTexture(new Rect(cx - .8f, cy - .8f, 1.6f, 1.6f), Texture2D.whiteTexture);

            // Digital In-Reticle Rangefinder & Target Classification HUD
            if (CombatHudPreferences.TacticalDetails && targetDistance > 5f)
            {
                bool hasLock = (currentTargetRival != null && !currentTargetRival.IsDead)
                    || (currentTargetActor != null && !currentTargetActor.IsDead);
                Color hudColor = hasLock 
                    ? (ThermalMode ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(1f, 0.28f, 0.22f, 0.95f))
                    : (ThermalMode ? new Color(0.2f, 1f, 0.85f, 0.85f) : new Color(0.15f, 0.88f, 1f, 0.85f));
                hudColor.a *= Mathf.Clamp01(scopeBlend);

                scopeRangeStyle.normal.textColor = hudColor;

                // Compact military readout placed just right-below center reticle
                string rangeStr = hasLock ? $"[{targetDistance:F0}m {currentTargetClassification}]" : $"{targetDistance:F0}m";
                GUI.Label(new Rect(cx + bDist + 6f, cy + 2f, 180f, 18f), rangeStr, scopeRangeStyle);

                // When locked on hostile target, draw subtle corner reticle brackets around center
                if (hasLock)
                {
                    float brkSize = bDist + 6f;
                    GUI.color = hudColor;
                    GUI.DrawTexture(new Rect(cx - brkSize, cy - brkSize, 6f, 1.5f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - brkSize, cy - brkSize, 1.5f, 6f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + brkSize - 6f, cy - brkSize, 6f, 1.5f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + brkSize - 1.5f, cy - brkSize, 1.5f, 6f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - brkSize, cy + brkSize - 1.5f, 6f, 1.5f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - brkSize, cy + brkSize - 6f, 1.5f, 6f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + brkSize - 6f, cy + brkSize - 1.5f, 6f, 1.5f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + brkSize - 1.5f, cy + brkSize - 6f, 1.5f, 6f), Texture2D.whiteTexture);
                }
            }

            // Subtle Chromatic Aberration Rim
            GUI.color = new Color(0f, 0.85f, 1f, 0.05f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(x - 2, y - 2, d + 4, d + 4), scope);
            GUI.color = new Color(1f, 0.25f, 0f, 0.05f * Mathf.Clamp01(scopeBlend));
            GUI.DrawTexture(new Rect(x + 2, y + 2, d - 4, d - 4), scope);

            DrawHitFeedback(width,height);

            // Ballistics Telemetry
            float muzzleVel = WeaponCatalog.Get(CurrentWeaponIndex).Speed;
            float validDist = targetDistance > 5f ? targetDistance : 100f;
            float tof = validDist / muzzleVel;
            float gravity = 9.81f;
            float dropM = 0.5f * gravity * (tof * tof);
            float milDrop = (dropM / validDist) * 1000f;

            float windSpeed = BallisticsSystem.Wind.magnitude;
            string windArrow = crosswind > 0.4f ? "▶" : crosswind < -0.4f ? "◀" : "●";
            string zoomStr = ScopeOptics.Magnification(Config!=null?Config.hipFOV:65,currentFOV).ToString("F1")+"×";

            // Tactical Ballistics HUD Readouts (Optic Cyan & Ballistic Amber)
            var tagStyle = scopeTagStyle;
            var valStyle = scopeValueStyle;
            tagStyle.normal.textColor = ThermalMode ? new Color(0.00f, 0.94f, 1.00f, 0.85f) : new Color(0.00f, 0.94f, 1.00f, 0.90f);
            valStyle.normal.textColor = ThermalMode ? new Color(0.85f, 0.98f, 1.00f, 0.95f) : new Color(0.92f, 0.97f, 1.00f, 0.95f);

            // Top Compass Azimuth Heading Ribbon
            if (CombatHudPreferences.TacticalDetails && cam != null)
            {
                float yawDeg = cam.transform.eulerAngles.y;
                string dirStr = yawDeg < 22.5f || yawDeg >= 337.5f ? "N" : yawDeg < 67.5f ? "NE" : yawDeg < 112.5f ? "E" : yawDeg < 157.5f ? "SE" : yawDeg < 202.5f ? "S" : yawDeg < 247.5f ? "SW" : yawDeg < 292.5f ? "W" : "NW";
                string compassHeading = $"AZIMUTH: {yawDeg:000}° {dirStr}";
                var compStyle = scopeCompassStyle;
                compStyle.normal.textColor = new Color(0.00f, 0.94f, 1.00f, 0.85f * Mathf.Clamp01(scopeBlend));
                Rect compRect = new Rect(width / 2 - 90, height / 2 - d * 0.44f, 180, 16);
                GUI.color = new Color(0.067f, 0.078f, 0.090f, 0.75f * Mathf.Clamp01(scopeBlend));
                GUI.DrawTexture(compRect, Texture2D.whiteTexture);
                GUI.color = new Color(0.00f, 0.94f, 1.00f, 0.45f * Mathf.Clamp01(scopeBlend));
                GUI.DrawTexture(new Rect(compRect.x, compRect.y, compRect.width, 1), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(compRect.x, compRect.yMax - 1, compRect.width, 1), Texture2D.whiteTexture);
                GUI.Label(compRect, compassHeading, compStyle);
            }

            // Essential readings sit in the black optic wings when landscape space permits.
            // Narrow screens retain compact corner readings, leaving the aiming point clear.
            var instruments = CombatHudPreferences.ScopeInstruments(width, height, CombatHudPreferences.SafeCanvas(width, height));
            bool details = CombatHudPreferences.TacticalDetails;
            DrawScopeInstrument(instruments[0], "RANGE", targetDistance > 0 ? $"{targetDistance:F0} m" : "— m", new Color(0f, .94f, 1f));
            DrawScopeInstrument(instruments[1], directDuelShot ? "WIND HOLD" : $"WIND {windSpeed:F1} m/s", directDuelShot ? "0.0 mil" : $"{windArrow} {windHoldMils:+0.0;-0.0;0.0} mil", new Color(1f, .65f, .2f));
            DrawScopeInstrument(instruments[2], details && !directDuelShot ? $"DROP {dropM:F2} m" : "BULLET DROP", directDuelShot ? "0.0 mil" : $"+{milDrop:F1} mil", new Color(0f, .94f, 1f));
            string ammoDisplay = isReloading ? "RELOADING" : currentAmmo >= 0 ? $"{currentAmmo} / {MaxAmmo}" : MaxAmmo.ToString();
            DrawScopeInstrument(instruments[3], "AMMO · " + zoomStr, ammoDisplay,
                isReloading || currentAmmo == 0 ? new Color(1f, .65f, .2f) : new Color(.3f, .95f, .5f));

            // Thermal Mode minimal badge
            if (ThermalMode)
            {
                scopeFlirStyle.normal.textColor = new Color(0f, 1f, 0.8f, .85f);
                GUI.Label(new Rect(width / 2 - 60, height / 2 - d * 0.38f, 120, 22), "FLIR THERMAL", scopeFlirStyle);
            }

            // Kill confirmation
            if (killTimer > 0)
            {
                scopeKillStyle.normal.textColor = new Color(1f, 0.2f, 0.1f, Mathf.Min(1f, killTimer));
                GUI.Label(new Rect(width / 2 - 100, height / 2 - d * 0.2f, 200, 50), "ELIMINATED", scopeKillStyle);
            }

            // Breath stamina meter (only visible when holding breath or recovering stamina)
            if (sway != null && sway.breathStamina < 0.99f)
            {
                float barWidth = d * 0.26f;
                float barHeight = 4f;
                Rect barRect = new Rect(width / 2 - barWidth / 2, height / 2 + d * 0.28f, barWidth, barHeight);

                GUI.color = new Color(0, 0, 0, 0.6f);
                GUI.DrawTexture(barRect, Texture2D.whiteTexture);

                GUI.color = sway.breathStamina > 0.3f ? new Color(0.2f, 0.85f, 0.95f, 0.85f) : new Color(0.95f, 0.25f, 0.2f, 0.9f);
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barWidth * sway.breathStamina, barHeight), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        // All camera projections use the same logical coordinate space as the
        // scaled mission GUI, including cameras with a partial-screen viewport.
        public static bool TryProjectScopePoint(Camera camera,Vector3 world,float width,float height,out Vector2 guiPoint)
        {
            guiPoint=Vector2.zero;
            if(camera==null || !float.IsFinite(width) || !float.IsFinite(height) || width<=0 || height<=0)return false;
            var viewport=camera.WorldToViewportPoint(world);
            if(viewport.z<=.1f || !float.IsFinite(viewport.x) || !float.IsFinite(viewport.y))return false;
            var rect=camera.rect;
            guiPoint=new Vector2((rect.x+viewport.x*rect.width)*width,(1-rect.y-viewport.y*rect.height)*height);
            return true;
        }

        void DrawScopeInstrument(Rect rect, string label, string value, Color accent)
        {
            float alpha = Mathf.Clamp01(scopeBlend);
            GUI.color = new Color(.02f, .035f, .05f, .56f * alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(accent.r, accent.g, accent.b, .7f * alpha);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, alpha);
            var title = GUIStyleCache.Get(CombatHudPreferences.ScopeLabelFont, FontStyle.Bold, TextAnchor.MiddleLeft, accent);
            var reading = GUIStyleCache.Get(value.Length > 16 ? 16 : CombatHudPreferences.ScopeValueFont, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            GUI.Label(new Rect(rect.x + 10, rect.y + 4, rect.width - 18, 18), label, title);
            GUI.Label(new Rect(rect.x + 10, rect.y + 22, rect.width - 18, 26), value, reading);
            GUI.color = Color.white;
        }

        void EnsureScopeStyles()
        {
            if(scopeTagStyle!=null)return;
            scopeTagStyle=ScopeStyle(12,TextAnchor.UpperLeft);
            scopeValueStyle=ScopeStyle(18,TextAnchor.UpperLeft);
            scopeCompassStyle=ScopeStyle(13,TextAnchor.MiddleCenter);
            scopeRangeStyle=ScopeStyle(14,TextAnchor.MiddleLeft);
            scopeFlirStyle=ScopeStyle(13,TextAnchor.UpperLeft);
            scopeKillStyle=ScopeStyle(18,TextAnchor.MiddleCenter);
        }

        static GUIStyle ScopeStyle(int size,TextAnchor alignment) => new GUIStyle(GUI.skin.label)
        {fontSize=size,fontStyle=FontStyle.Bold,alignment=alignment};

        void OnGUI()
        {
            if (knifeBloodTimer > 0f)
            {
                DrawScreenBloodSplatter(Screen.width, Screen.height, Mathf.Clamp01(knifeBloodTimer / 1.0f));
            }
        }

        void DrawScreenBloodSplatter(float width, float height, float alpha)
        {
            if (alpha <= 0.01f) return;
            Matrix4x4 savedM = GUI.matrix;

            // 1. Red vignette edge pulses
            Color vignetteCol = new Color(0.45f, 0.02f, 0.02f, 0.35f * alpha);
            GUI.color = vignetteCol;
            GUI.DrawTexture(new Rect(0, 0, width * 0.25f, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.75f, 0, width * 0.25f, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, width, height * 0.18f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, height * 0.82f, width, height * 0.18f), Texture2D.whiteTexture);

            // 2. Arterial blood slash streak cutting diagonally across
            Color arterialCol = new Color(0.55f, 0.04f, 0.04f, 0.88f * alpha);
            GUI.color = arterialCol;
            GUIUtility.RotateAroundPivot(-28f, new Vector2(width * 0.58f, height * 0.38f));
            GUI.DrawTexture(new Rect(width * 0.35f, height * 0.36f, width * 0.45f, 7f), Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.01f, 0.01f, 0.95f * alpha);
            GUI.DrawTexture(new Rect(width * 0.42f, height * 0.37f, width * 0.32f, 3f), Texture2D.whiteTexture);
            GUI.matrix = savedM;

            // 3. Blood drops and splatter droplets in corners & center
            Color darkDropCol = new Color(0.40f, 0.02f, 0.02f, 0.90f * alpha);
            Color brightDropCol = new Color(0.68f, 0.05f, 0.05f, 0.82f * alpha);

            // Top-right splatter cluster
            GUI.color = darkDropCol;
            GUI.DrawTexture(new Rect(width * 0.72f, height * 0.15f, 16, 12), Texture2D.whiteTexture);
            GUI.color = brightDropCol;
            GUI.DrawTexture(new Rect(width * 0.74f, height * 0.19f, 22, 14), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.79f, height * 0.12f, 12, 18), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.83f, height * 0.22f, 8, 8), Texture2D.whiteTexture);

            // Bottom-left splatter cluster
            GUI.color = darkDropCol;
            GUI.DrawTexture(new Rect(width * 0.12f, height * 0.68f, 20, 14), Texture2D.whiteTexture);
            GUI.color = brightDropCol;
            GUI.DrawTexture(new Rect(width * 0.16f, height * 0.73f, 14, 22), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.09f, height * 0.78f, 10, 10), Texture2D.whiteTexture);

            // Center-left impact spray dots
            GUI.color = brightDropCol;
            GUI.DrawTexture(new Rect(width * 0.42f, height * 0.44f, 6, 6), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.38f, height * 0.48f, 9, 7), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width * 0.45f, height * 0.52f, 5, 8), Texture2D.whiteTexture);

            GUI.matrix = savedM;
        }

        void OnDestroy()
        {
            if(thermalOverlay!=null)thermalOverlay.SetScopeState(false,0,null);
            if(rifle!=null) DestroyPresentationObject(rifle.gameObject);
            if(combatKnife!=null) DestroyPresentationObject(combatKnife.gameObject);
            if(muzzleFlash!=null) DestroyPresentationObject(muzzleFlash);
            if(tracerLine!=null) DestroyPresentationObject(tracerLine.gameObject);
            foreach(var item in owned) DestroyPresentationObject(item); TimeScaleController.ClearHitCam();
        }

        static void DestroyPresentationObject(UnityEngine.Object item)
        {
            if(item==null) return;
            if(Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }
    }
}
// 
