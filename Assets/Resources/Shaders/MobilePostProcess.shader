Shader "GeoSniper/MobilePostProcess"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _BloomParams ("Bloom Params", Vector) = (0.8, 0.5, 1.2, 1.5)
        _ColorGrading ("Color Grading", Vector) = (1.06, 1.12, 1.05, 0.42)
        _ScopeParams ("Scope Params", Vector) = (0, 0.44, 1.77, 0.06)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _BloomTex;
        float4 _BloomTex_TexelSize;
        float4 _BloomParams; // x: threshold, y: softKnee, z: intensity, w: blur spread
        float4 _ColorGrading; // x: contrast, y: saturation, z: exposure, w: vignette
        float4 _ScopeParams; // x: scopeBlend, y: scopeRadius, z: aspect, w: barrel distortion

        struct appdata
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f vert(appdata v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            return o;
        }

        // Fast ACES Filmic Tone Mapping (Narkowicz fit)
        float3 ACESFilm(float3 x)
        {
            float a = 2.51;
            float b = 0.03;
            float c = 2.43;
            float d = 0.59;
            float e = 0.14;
            return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
        }
        ENDCG

        // Pass 0: Bloom Bright-Pass Extract
        Pass
        {
            Name "BloomThreshold"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            half4 frag(v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv);
                half lum = dot(col.rgb, half3(0.2126, 0.7152, 0.0722));
                half threshold = _BloomParams.x;
                half soft = _BloomParams.y;
                half knee = max(0.001, threshold * soft);
                half softLuma = lum - threshold + knee;
                softLuma = clamp(softLuma, 0.0, 2.0 * knee);
                softLuma = (softLuma * softLuma) / (4.0 * knee + 0.0001);
                half weight = max(softLuma, lum - threshold) / max(lum, 0.0001);
                return half4(col.rgb * saturate(weight), 1.0);
            }
            ENDCG
        }

        // Pass 1: Kawase Blur
        Pass
        {
            Name "BloomBlur"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            half4 frag(v2f i) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * _BloomParams.w;
                half4 col = tex2D(_MainTex, i.uv + float2(-offset.x, -offset.y));
                col += tex2D(_MainTex, i.uv + float2(offset.x, -offset.y));
                col += tex2D(_MainTex, i.uv + float2(-offset.x, offset.y));
                col += tex2D(_MainTex, i.uv + float2(offset.x, offset.y));
                return col * 0.25;
            }
            ENDCG
        }

        // Pass 2: Final Composite (ACES + Bloom + Grading + Vignette + Scope)
        Pass
        {
            Name "CompositeFinal"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float scopeBlend = saturate(_ScopeParams.x);
                float scopeRadius = _ScopeParams.y;
                float aspect = max(0.1, _ScopeParams.z);

                // Scope optical coordinates
                float2 centeredUV = uv - 0.5;
                centeredUV.x *= aspect;
                float distFromCenter = length(centeredUV);

                // Optical barrel distortion inside scope glass
                if (scopeBlend > 0.01 && distFromCenter < scopeRadius)
                {
                    float r = distFromCenter / scopeRadius;
                    float distortion = 1.0 + _ScopeParams.w * (r * r);
                    float2 distorted = (uv - 0.5) * distortion + 0.5;
                    uv = lerp(uv, distorted, scopeBlend);
                }

                // Chromatic Aberration
                float caBase = 0.0015;
                float caScope = (scopeBlend > 0.01 && distFromCenter < scopeRadius) ? (0.004 * pow(distFromCenter / scopeRadius, 2.0)) : 0.0;
                float2 caOffset = (uv - 0.5) * (caBase + caScope);

                half3 col;
                col.r = tex2D(_MainTex, uv + caOffset).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - caOffset).b;

                // Add bloom
                half3 bloom = tex2D(_BloomTex, uv).rgb * _BloomParams.z;
                col += bloom;

                // Exposure
                col *= _ColorGrading.z;

                // ACES Tone Mapping
                col = ACESFilm(col);

                // Contrast
                float contrast = _ColorGrading.x;
                col = (col - 0.5) * contrast + 0.5;

                // Saturation
                float saturation = _ColorGrading.y;
                half lum = dot(col, half3(0.2126, 0.7152, 0.0722));
                col = lerp(half3(lum, lum, lum), col, saturation);

                // Cinematic color grade: Cool tactical shadows, neutral/warm highlights
                half3 shadowGrade = half3(0.94, 0.97, 1.04);
                half3 highlightGrade = half3(1.02, 1.01, 0.98);
                col *= lerp(shadowGrade, highlightGrade, saturate(lum * 1.3));

                // Natural Vignette
                float vigDist = length((i.uv - 0.5) * float2(aspect, 1.0));
                float vignette = smoothstep(1.15, 0.35, vigDist * _ColorGrading.w);
                col *= vignette;

                // Scope Optical Mask & Eye-Relief Bezel
                if (scopeBlend > 0.01)
                {
                    float innerRadius = scopeRadius * 0.94;
                    float outerRadius = scopeRadius;

                    // Inner optic transmission mask
                    float lensMask = smoothstep(outerRadius, innerRadius, distFromCenter);

                    // Tunnel vision outside optic
                    float outsideDim = lerp(1.0, 0.03, scopeBlend);
                    col = lerp(col * outsideDim, col, lensMask);

                    // Anodized scope metal bezel
                    float bezel = smoothstep(innerRadius - 0.008, innerRadius, distFromCenter) * 
                                  smoothstep(outerRadius + 0.008, outerRadius, distFromCenter);
                    col = lerp(col, half3(0.018, 0.022, 0.026), bezel * scopeBlend * 0.96);

                    // Multi-coated emerald-cyan reflection glint on lens edge
                    if (distFromCenter < innerRadius)
                    {
                        float edgeGlint = pow(distFromCenter / innerRadius, 4.5);
                        half3 glintColor = half3(0.03, 0.16, 0.22) * edgeGlint * scopeBlend * 0.5;
                        col += glintColor;
                    }
                }

                return half4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
