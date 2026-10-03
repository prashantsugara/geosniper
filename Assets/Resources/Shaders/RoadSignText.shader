Shader "GeoSniper/RoadSignText"
{
    Properties { _MainTex ("Font Atlas", 2D) = "white" {} _Color ("Text Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        ZWrite On
        ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            fixed4 _Color;
            Output vert(Input v)
            {
                Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                fixed alpha=tex2D(_MainTex,i.uv).a*_Color.a;
                clip(alpha-.05);
                return fixed4(_Color.rgb,1);
            }
            ENDCG
        }
    }
}
