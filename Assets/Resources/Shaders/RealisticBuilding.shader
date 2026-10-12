Shader "GeoSniper/RealisticBuilding"
{
    Properties
    {
        _Color ("Facade Tint", Color) = (0.82, 0.81, 0.78, 1.0)
        _MainTex ("Albedo (RGB), Window Mask (A)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Surface Relief", Range(0,1)) = 1
        _StreetStudy ("Street Study", Range(0,1)) = 0
        _WindowLightScale ("Room Occupancy Brightness", Range(0,1)) = 1
        _EmissionColor ("Night Emission Color", Color) = (0, 0, 0, 0)
        _WindowGrid ("Window Bays Per Texture", Float) = 2
        _Glossiness ("Wall Smoothness", Range(0, 1)) = 0.16
        _Metallic ("Wall Metallic", Range(0, 1)) = 0.0
        _GlassGlossiness ("Glass Smoothness", Range(0, 1)) = 0.98
        _GlassMetallic ("Glass Metallic", Range(0, 1)) = 0.88
        _GlassTint ("Glass Base Tint", Color) = (0.05, 0.09, 0.14, 1.0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        fixed4 _EmissionColor;
        fixed4 _CityNightEmission;
        fixed4 _GlassTint;
        half _BumpScale;
        half _StreetStudy;
        half _WindowLightScale;
        half _Glossiness;
        half _Metallic;
        half _GlassGlossiness;
        half _GlassMetallic;
        float _WindowGrid;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            half3 normalLocal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            o.Normal = normalize(lerp(half3(0,0,1),normalLocal,_BumpScale));

            // Material zone classified by texture Alpha:
            // a >= 0.70: Optical Reflective Window Glass
            // 0.20 <= a < 0.70: Window Frame, Mullions, Trim, Ledges (Dark Metal / Stone)
            // a < 0.20: Matte Architectural Facade Wall (Concrete, Brick, Stucco, Stone)
            float mask = tex.a;
            bool isGlass = mask >= 0.70;
            bool isFrame = mask >= 0.20 && mask < 0.70;

            // Foundation contact ambient occlusion:
            // Wall V coordinate in [0..1] repeats every 2 storeys; darken near ground contact
            float wallV = frac(IN.uv_MainTex.y);
            float baseContactAO = lerp(smoothstep(0.0, 0.22, wallV) * 0.28 + 0.72,1,_StreetStudy);

            if (isGlass)
            {
                // PBR Glass Surface parameters
                o.Albedo = _GlassTint.rgb * tex.rgb;
                o.Metallic = _GlassMetallic;
                o.Smoothness = _GlassGlossiness;

                float2 roomCell=floor(IN.uv_MainTex*_WindowGrid);
                float2 roomLocal=frac(IN.uv_MainTex*_WindowGrid);
                float3 roomOrigin=unity_ObjectToWorld._m03_m13_m23;
                float roomSeed=dot(roomCell,float2(127.1,311.7))+dot(roomOrigin,float3(.37,.19,.53));
                float curtainChoice=frac(sin(roomSeed+41.7)*31217.381);
                half3 curtainTint=curtainChoice<.28 ? half3(.63,.57,.46) : curtainChoice<.55 ? half3(.73,.70,.62)
                    : curtainChoice<.78 ? half3(.36,.43,.49) : half3(.43,.48,.39);
                float opening=lerp(.12,.32,frac(sin(roomSeed+8.9)*17313.71));
                float curtainEdges=max(1-smoothstep(opening-.015,opening+.015,roomLocal.x),smoothstep(1-opening-.015,1-opening+.015,roomLocal.x));
                float curtains=curtainEdges*step(.18,curtainChoice);
                float folds=.84+.16*cos(roomLocal.x*100);
                o.Albedo=lerp(o.Albedo,curtainTint*folds*.42,curtains*.8);
                o.Metallic=lerp(o.Metallic,.12,curtains*.65);
                o.Smoothness=lerp(o.Smoothness,.35,curtains*.65);

                // World-space reflection vector for crisp procedural sky & environment reflections
                float3 worldNormal = WorldNormalVector(IN, o.Normal);
                if (dot(worldNormal, worldNormal) < 0.1) worldNormal = IN.worldNormal;
                worldNormal = normalize(worldNormal);

                float3 worldView = normalize(_WorldSpaceCameraPos - IN.worldPos);
                float3 worldRefl = reflect(-worldView, worldNormal);

                // Optical Fresnel reflectance (higher base F0 for architectural coated glass)
                float NdotV = saturate(dot(worldNormal, worldView));
                float fresnel = 0.18 + 0.82 * pow(1.0 - NdotV, 3.5);

                // Procedural Atmospheric Sky Gradient
                // Zenith: deep sapphire blue; Horizon: bright hazy atmospheric glow; Ground: slate asphalt
                half3 skyZenith = half3(0.18, 0.38, 0.72);
                half3 skyHorizon = half3(0.82, 0.88, 0.95);
                half3 groundRefl = half3(0.15, 0.16, 0.18);

                half3 envColor = (worldRefl.y > -0.05)
                    ? lerp(skyHorizon, skyZenith, pow(saturate(worldRefl.y + 0.05), 0.55))
                    : lerp(skyHorizon, groundRefl, saturate((-worldRefl.y - 0.05) * 2.0));

                // Cloud silhouette variation in reflection
                float cloudDetail = sin(worldRefl.x * 6.0 + worldRefl.z * 6.0) * 0.5 + 0.5;
                envColor *= (0.85 + cloudDetail * 0.25);

                // Directional Sun Specular Highlight on glass
                float sunDot = saturate(dot(worldRefl, _WorldSpaceLightPos0.xyz));
                half3 sunHighlight = _LightColor0.rgb * pow(sunDot, 36.0) * 4.0;

                // Evaluate daylight level from directional light
                float daylight = saturate(dot(_LightColor0.rgb, half3(0.3, 0.59, 0.11)) * 1.5);
                float dayReflection = max(daylight, lerp(.40,.055,_StreetStudy));

                // Glass exterior optical reflection
                half3 glassEmission = (envColor * dayReflection + sunHighlight) * fresnel;
                glassEmission*=1-curtains*.4;

                // Night window interior illumination
                if (daylight < 0.25)
                {
                    half3 nightLight = max(_EmissionColor.rgb, _CityNightEmission.rgb);
                    if (max(max(nightLight.r, nightLight.g), nightLight.b) > 0.01)
                    {
                        float nightFactor = saturate((0.25 - daylight) / 0.25);
                        float interiorLum = saturate(dot(tex.rgb, half3(0.3, 0.59, 0.11)) * 2.0 + 0.25);
                        // Stable room colours: mostly warm domestic lamps, with neutral/cool offices.
                        // Quantized texture bays keep each pane uniform instead of noisy per-pixel colour.
                        float2 room=floor(IN.uv_MainTex*_WindowGrid);
                        float3 buildingOrigin=unity_ObjectToWorld._m03_m13_m23;
                        float seed=dot(room,float2(127.1,311.7))+dot(buildingOrigin,float3(.37,.19,.53));
                        float choice=frac(sin(seed)*43758.5453);
                        float brightness=frac(sin(seed+17.3)*23421.631);
                        half3 roomTint=choice<.60 ? half3(1,.79,.55) : choice<.87 ? half3(1,.91,.78) : half3(.83,.91,1);
                        float groupChoice=frac(sin(dot(floor(room/float2(3,2)),float2(71.7,193.1))+dot(buildingOrigin,float3(.37,.19,.53)))*43758.5453);
                        float occupied=_StreetStudy>.5 ? (groupChoice<.58 || choice<.22 ? .018 : lerp(.35,.82,brightness)) : (choice<.14 ? .06 : lerp(.55,1.0,brightness));
                        float lightPower=max(max(nightLight.r,nightLight.g),nightLight.b);
                        // A glass tower must not become one luminous slab. Its closely
                        // spaced office grid transmits less light than individual room panes.
                        lightPower*=lerp(1,.18,_StreetStudy*step(7.9,_WindowGrid));
                        half3 transmitted=lerp(roomTint,roomTint*curtainTint*folds,curtains*.8);
                        glassEmission += transmitted * lightPower * occupied * interiorLum * nightFactor * _WindowLightScale;
                    }
                }

                o.Emission = glassEmission;
            }
            else if (isFrame)
            {
                // Anodized dark aluminum window frame, mullions, or architectural stone sills
                o.Albedo = tex.rgb * 0.55;
                o.Metallic = 0.65;
                o.Smoothness = 0.48;
                o.Emission = half3(0, 0, 0);
            }
            else
            {
                // Matte architectural wall with contact ambient occlusion
                o.Albedo = tex.rgb * _Color.rgb * baseContactAO;
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
                o.Emission = half3(0, 0, 0);
            }

            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Standard"
}
