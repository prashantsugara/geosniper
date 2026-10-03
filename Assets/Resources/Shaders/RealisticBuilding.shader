Shader "GeoSniper/RealisticBuilding"
{
    Properties
    {
        _Color ("Facade Tint", Color) = (0.82, 0.81, 0.78, 1.0)
        _MainTex ("Albedo (RGB), Window Mask (A)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _EmissionColor ("Night Emission Color", Color) = (0, 0, 0, 0)
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
        half _Glossiness;
        half _Metallic;
        half _GlassGlossiness;
        half _GlassMetallic;

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
            o.Normal = normalLocal;

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
            float baseContactAO = smoothstep(0.0, 0.22, wallV) * 0.28 + 0.72;

            if (isGlass)
            {
                // PBR Glass Surface parameters
                o.Albedo = _GlassTint.rgb * tex.rgb;
                o.Metallic = _GlassMetallic;
                o.Smoothness = _GlassGlossiness;

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
                float dayReflection = max(daylight, 0.40);

                // Glass exterior optical reflection
                half3 glassEmission = (envColor * dayReflection + sunHighlight) * fresnel;

                // Night window interior illumination
                if (daylight < 0.25)
                {
                    half3 nightLight = max(_EmissionColor.rgb, _CityNightEmission.rgb);
                    if (max(max(nightLight.r, nightLight.g), nightLight.b) > 0.01)
                    {
                        float nightFactor = saturate((0.25 - daylight) / 0.25);
                        float interiorLum = saturate(dot(tex.rgb, half3(0.3, 0.59, 0.11)) * 2.0 + 0.25);
                        glassEmission += nightLight * interiorLum * nightFactor;
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
