Shader "GeoSniper/Vegetation"
{
    Properties
    {
        _MainTex ("Leaf and bark atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Leaf edge", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert alphatest:_Cutoff addshadow fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN,inout SurfaceOutput o)
        {
            fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;
            o.Albedo=c.rgb;o.Alpha=c.a;
        }
        ENDCG
    }
    Fallback "Transparent/Cutout/Diffuse"
}
