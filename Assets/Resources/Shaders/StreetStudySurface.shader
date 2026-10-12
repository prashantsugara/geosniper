Shader "GeoSniper/StreetStudySurface"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Aggregate", 2D) = "white" {}
        _BumpMap ("Aggregate normal", 2D) = "bump" {}
        _BumpScale ("Relief", Range(0,1)) = .12
        _Glossiness ("Dry smoothness", Range(0,1)) = .16
        _Metallic ("Metal", Range(0,1)) = 0
        _WetResponse ("Rain response", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        sampler2D _BumpMap;
        half _BumpScale;
        fixed4 _Color;
        half _Glossiness, _Metallic, _WetResponse, _StreetStudyWetness;
        half4 _CityNightEmission;
        float4 _StudyLampPositions[4];
        float4 _StudyLampColors[4];
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; float3 worldPos; float3 worldNormal; float3 viewDir; INTERNAL_DATA };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            half3 surface=tex2D(_MainTex,IN.uv_MainTex).rgb*_Color.rgb;
            float2 p=IN.worldPos.xz;
            // Large irregular patches, independent of the repeating aggregate UVs.
            float noise=sin(p.x*.37+sin(p.y*.23)*2)*sin(p.y*.41+sin(p.x*.19)*2);
            float puddle=smoothstep(.25,.7,noise)*_StreetStudyWetness*_WetResponse;
            float wet=_StreetStudyWetness*_WetResponse;
            o.Albedo=surface*lerp(1,.58,wet);
            o.Metallic=_Metallic;
            o.Smoothness=lerp(_Glossiness,.62,puddle);
            o.Normal=normalize(lerp(half3(0,0,1),UnpackNormal(tex2D(_BumpMap,IN.uv_BumpMap)),_BumpScale));
            float3 normal=WorldNormalVector(IN,o.Normal);
            o.Occlusion=lerp(.94,1,puddle);
            half3 spill=0;
            // Low-cost fixture spill and broken wet highlights for four nearby lamps.
            // This is an artistic approximation, not a planar/reflection-camera pass.
            [unroll] for(int i=0;i<4;i++)
            {
                float2 delta=p-_StudyLampPositions[i].xz;
                float dist2=dot(delta,delta);
                float pool=exp(-dist2*.035)*saturate(normal.y);
                float eyeHeight=max(.2,_WorldSpaceCameraPos.y-IN.worldPos.y);
                float lampHeight=max(.2,_StudyLampPositions[i].y-IN.worldPos.y);
                float2 reflected=lerp(_WorldSpaceCameraPos.xz,_StudyLampPositions[i].xz,eyeHeight/(eyeHeight+lampHeight));
                float2 axis=normalize(_WorldSpaceCameraPos.xz-_StudyLampPositions[i].xz+float2(.001,.001));
                float2 reflectionDelta=p-reflected;
                float along=dot(reflectionDelta,axis),across=dot(reflectionDelta,float2(-axis.y,axis.x));
                float streak=exp(-across*across*.65-along*along*.025);
                spill+=_StudyLampColors[i].rgb*(surface*pool*.9 + streak*puddle*.10)*saturate(normal.y)*step(IN.worldPos.y,_StudyLampPositions[i].y);
            }
            o.Emission=spill;
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Standard"
}
