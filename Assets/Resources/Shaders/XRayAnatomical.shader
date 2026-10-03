Shader "GeoSniper/XRayAnatomical"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _XRayColor ("XRay Contours", Color) = (0.1, 0.65, 0.95, 0.65)
        _SkeletonColor ("Bone Structure", Color) = (0.95, 0.95, 0.90, 0.90)
        _HitColor ("Impact Highlight", Color) = (1.0, 0.25, 0.05, 0.95)
        _HitPoint ("Hit Position (World)", Vector) = (0, 0, 0, 0)
        _HitRadius ("Hit Radius", Float) = 0.45
        _RimPower ("Rim Power", Float) = 2.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200

        Blend SrcAlpha One
        ZWrite Off
        Cull Back

        CGPROGRAM
        #pragma surface surf Unlit alpha:fade

        sampler2D _MainTex;
        fixed4 _XRayColor;
        fixed4 _SkeletonColor;
        fixed4 _HitColor;
        float4 _HitPoint;
        float _HitRadius;
        float _RimPower;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldPos;
        };

        half4 LightingUnlit(SurfaceOutput s, half3 lightDir, half atten)
        {
            return half4(s.Albedo, s.Alpha);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            
            // Fresnel Rim Glow
            float rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            float rimIntensity = pow(rim, _RimPower);

            // Distance to impact point
            float distToHit = distance(IN.worldPos, _HitPoint.xyz);
            float hitFactor = saturate(1.0 - (distToHit / max(0.01, _HitRadius)));
            hitFactor = pow(hitFactor, 1.5);

            // Tactical X-Ray Body Rim & Impact Blending
            fixed3 bodyColor = _XRayColor.rgb * (rimIntensity * 1.6f + 0.15f);
            fixed3 finalColor = lerp(bodyColor, _HitColor.rgb * 2.5f, hitFactor);

            o.Albedo = finalColor;
            o.Alpha = saturate(rimIntensity * _XRayColor.a + hitFactor * 0.9f + 0.12f);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
