Shader "GeoSniper/TerrainBlend" {
 Properties {_GrassTex("Grass",2D)="white" {} _SoilTex("Soil",2D)="white" {} _ConcreteTex("Concrete",2D)="white" {} }
 SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" }
 Pass { Tags { "LightMode"="ForwardBase" }
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #pragma multi_compile_fwdbase
 #include "UnityCG.cginc"
 #include "Lighting.cginc"
 #include "AutoLight.cginc"
 sampler2D _GrassTex,_SoilTex,_ConcreteTex;
 struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;float3 normal:NORMAL; };
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed2 blend:TEXCOORD1;float3 normal:TEXCOORD2;UNITY_FOG_COORDS(3) SHADOW_COORDS(4)};
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.blend=v.color.rg;o.normal=UnityObjectToWorldNormal(v.normal);UNITY_TRANSFER_FOG(o,o.pos);TRANSFER_SHADOW(o);return o;}
 fixed4 frag(v2f i):SV_Target {
 fixed3 grass=tex2D(_GrassTex,i.uv).rgb;
 fixed3 soil=tex2D(_SoilTex,i.uv).rgb;
 fixed3 concrete=tex2D(_ConcreteTex,i.uv).rgb;
 fixed3 albedo=lerp(lerp(grass,soil,saturate(i.blend.r)),concrete,saturate(i.blend.g));
 fixed light=saturate(dot(normalize(i.normal),normalize(_WorldSpaceLightPos0.xyz)));
 fixed3 ambient=max(UNITY_LIGHTMODEL_AMBIENT.rgb,fixed3(.22,.23,.22));
 fixed shadow=SHADOW_ATTENUATION(i);
 fixed4 color=fixed4(albedo*(ambient+_LightColor0.rgb*shadow*(.18+light*.72)),1);
 UNITY_APPLY_FOG(i.fogCoord,color);return color;
 }
 ENDCG
 }
 }
 Fallback "Diffuse"
}
