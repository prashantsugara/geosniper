Shader "GeoSniper/WaterFoam" {
 Properties { _Color("Foam tint",Color)=(.72,.86,.87,.28) }
 SubShader { Tags { "RenderType"="Transparent" "Queue"="Transparent" }
 ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #include "UnityCG.cginc"
 fixed4 _Color;
 struct appdata {float4 vertex:POSITION;fixed4 color:COLOR;};
 struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;fixed4 color:COLOR;UNITY_FOG_COORDS(1)};
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;}
 fixed4 frag(v2f i):SV_Target {
  float2 p=i.world.xz;
  float ripples=sin(p.x*1.7+p.y*.9+_Time.y*1.8)*sin(p.y*2.2-p.x*.4-_Time.y*1.1);
  float flecks=saturate((ripples-.18)*2.2);
  fixed alpha=i.color.a*_Color.a*(.35+flecks*.65);
  fixed4 result=fixed4(_Color.rgb,alpha);UNITY_APPLY_FOG(i.fogCoord,result);return result;
 }
 ENDCG }
 }
}
