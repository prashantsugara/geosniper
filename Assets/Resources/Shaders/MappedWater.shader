Shader "GeoSniper/MappedWater" {
 Properties { _ShoreDepth("Shore distance",2D)="white" {} _Color("Water tint",Color)=(.035,.22,.27,1) }
 SubShader {
 Tags { "RenderType"="Opaque" "Queue"="Geometry+5" }
 Cull Off
 Pass {
 Tags { "LightMode"="ForwardBase" }
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
 #include "UnityCG.cginc"
 #include "Lighting.cginc"
 #include "AutoLight.cginc"
 struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
 struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;UNITY_FOG_COORDS(1) float2 uv:TEXCOORD2;SHADOW_COORDS(3)};
 fixed4 _Color;sampler2D _ShoreDepth;
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.uv=v.uv;UNITY_TRANSFER_FOG(o,o.pos);TRANSFER_SHADOW(o);return o;}
 fixed4 frag(v2f i):SV_Target {
 UNITY_LIGHT_ATTENUATION(attenuation,i,i.world);
 float t=_Time.y;
 float2 p=i.world.xz;
 float a=sin(p.x*.45+p.y*.31+t)*.045+sin(p.x*1.63-p.y*.82+t*1.9)*.025+cos(p.y*2.7+p.x*.63-t*2.2)*.014;
 float b=cos(p.y*.57-p.x*.28+t*.8)*.04+sin(p.y*1.91+p.x*.94-t*1.5)*.022+cos(p.x*2.3-p.y*.71+t*2.4)*.012;
 float3 n=normalize(float3(a,1,b));
 float3 view=normalize(_WorldSpaceCameraPos-i.world);
 float fresnel=pow(1-saturate(dot(n,view)),4);
 float light=saturate(dot(n,normalize(_WorldSpaceLightPos0.xyz)));
 float spec=pow(saturate(dot(n,normalize(view+normalize(_WorldSpaceLightPos0.xyz)))),80)*.6;
 float3 ambient=max(UNITY_LIGHTMODEL_AMBIENT.rgb,float3(.035,.04,.05));
 float3 waterTint=lerp(float3(.16,.31,.27),_Color.rgb,tex2D(_ShoreDepth,i.uv).r);
 float3 color=lerp(waterTint,float3(.32,.48,.55),fresnel*.65)*(ambient+_LightColor0.rgb*(.15+(light*.5+.2)*attenuation));
 fixed4 result=fixed4(color+spec*_LightColor0.rgb*attenuation,1);UNITY_APPLY_FOG(i.fogCoord,result);return result;
 }
 ENDCG
 }
 }
}
