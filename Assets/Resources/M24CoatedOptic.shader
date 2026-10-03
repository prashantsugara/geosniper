Shader "GeoSniper/CoatedOptic"
{
    Properties
    {
        _Color ("Coating Tint", Color) = (0.035, 0.14, 0.22, 1)
        _ReflectionColor ("Fresnel Reflection", Color) = (0.2, 0.65, 0.95, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.98
        _Metallic ("Metallic", Range(0, 1)) = 0.75
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 250

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        fixed4 _Color;
        fixed4 _ReflectionColor;
        half _Glossiness;
        half _Metallic;

        struct Input
        {
            float3 viewDir;
            float3 worldNormal;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float facing = saturate(abs(dot(normalize(IN.viewDir), normalize(IN.worldNormal))));
            float fresnel = pow(1.0 - facing, 3.5);
            
            // Optical lens keeps a high-clarity dark center with emerald-cyan grazing reflections
            fixed3 baseAlbedo = lerp(_Color.rgb * 0.15, _ReflectionColor.rgb * 0.4, fresnel);
            o.Albedo = baseAlbedo;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
            o.Emission = lerp(_Color.rgb * 0.1, _ReflectionColor.rgb * 0.6, fresnel);
        }
        ENDCG
    }
    FallBack "Standard"
}
