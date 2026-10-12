Shader "GeoSniper/ThermalSignature"
{
    Properties
    {
        _ScopeBlend ("Scope Blend", Float) = 1
        _Aspect ("Camera Aspect", Float) = 1.77778
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            ZTest LEqual ZWrite Off Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            float _ScopeBlend, _Aspect;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float4 screen : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.position);
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                float2 lens = i.screen.xy / i.screen.w - .5;
                lens.x *= max(.1, _Aspect);
                // Matches the transparent inner glass of the 92%-height scope.
                clip(.404 * .404 - dot(lens, lens));
                // Warm human silhouette without the old oversized white block.
                return half4(1.0, .44, .08, .84 * saturate(_ScopeBlend));
            }
            ENDCG
        }
    }
    FallBack Off
}
