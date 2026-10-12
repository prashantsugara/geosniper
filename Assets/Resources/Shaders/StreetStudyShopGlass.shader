Shader "GeoSniper/StreetStudyShopGlass"
{
    Properties
    {
        _Color ("Glazing tint", Color) = (.08,.10,.11,.24)
        _Glossiness ("Smoothness", Range(0,1)) = .65
        _Metallic ("Metal", Range(0,1)) = .06
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0
        fixed4 _Color;
        half _Glossiness, _Metallic;
        struct Input { float3 viewDir; };
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            half fresnel=pow(1-saturate(normalize(IN.viewDir).z),4);
            o.Albedo=_Color.rgb;
            o.Metallic=_Metallic;
            o.Smoothness=_Glossiness;
            o.Emission=half3(.025,.032,.038)*fresnel;
            o.Alpha=_Color.a+fresnel*.28;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
