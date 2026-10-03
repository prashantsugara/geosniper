Shader "GeoSniper/TacticalThermalNVG"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _Mode ("Mode (0: Normal, 1: NVG Green, 2: Thermal Heatmap)", Float) = 0
        _ThermalColor ("Thermal Hot Spot Color", Color) = (1, 0.25, 0.05, 1)
        _NVGColor ("NVG Phosphor Color", Color) = (0.1, 0.95, 0.2, 1)
        _Scanline ("Scanline Intensity", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        float _Mode;
        fixed4 _ThermalColor;
        fixed4 _NVGColor;
        float _Scanline;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
            
            if (_Mode > 1.5) // Thermal Heatmap
            {
                float heat = saturate(lum * 1.5);
                fixed3 thermal = lerp(fixed3(0.02, 0.04, 0.12), _ThermalColor.rgb, heat);
                o.Albedo = thermal;
                o.Emission = thermal * heat * 0.4;
            }
            else if (_Mode > 0.5) // NVG Night Vision
            {
                float scan = sin(IN.worldPos.y * 120.0 + _Time.y * 10.0) * 0.5 + 0.5;
                fixed3 nvg = _NVGColor.rgb * (lum + scan * _Scanline * 0.15);
                o.Albedo = nvg;
                o.Emission = nvg * 0.3;
            }
            else // Standard PBR
            {
                o.Albedo = c.rgb;
                o.Metallic = 0.2;
                o.Smoothness = 0.5;
            }
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Standard"
}
