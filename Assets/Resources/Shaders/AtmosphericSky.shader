Shader "GeoSniper/AtmosphericSky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Sky" }
        Cull Off ZWrite Off ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 direction : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.direction = v.vertex.xyz;
                return o;
            }

            float _Storm, _Night, _SkyDetail;
            float3 _SunDirection;
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d=normalize(i.direction);
                float2 p=d.xz/max(.18,d.y+.25)*2.3+float2(_Time.y*.003,0);
                float n=noise(p)*.57+noise(p*2.1)*.28+noise(p*4.3)*.15;
                float cloud=smoothstep(lerp(.56,.35,_Storm),lerp(.78,.64,_Storm),n)*smoothstep(-.015,.12,d.y);
                cloud*=_SkyDetail;
                float sun=pow(saturate(dot(d,normalize(_SunDirection))),32);
                float3 cloudColor=lerp(float3(.73,.76,.80),float3(.33,.39,.47),_Storm);
                cloudColor=lerp(cloudColor,float3(.085,.12,.20),_Night);
                cloudColor+=sun*float3(.3,.16,.045)*(1-_Night)*(1-_Storm*.7);
                float3 result=lerp(i.color.rgb,cloudColor,cloud*.86);
                result+=sun*float3(.24,.12,.025)*(1-_Night)*(1-cloud);
                float stars=step(.9985,hash(floor(d.xz/max(.2,d.y)*700)))*smoothstep(.12,.4,d.y)*_Night*(1-cloud);
                // A fixed world direction keeps the moon stable as the player looks around.
                float3 moonDirection=normalize(float3(.18,.42,.89));
                float3 moonRight=normalize(cross(float3(0,1,0),moonDirection));
                float3 moonUp=cross(moonDirection,moonRight);
                float2 moonUV=float2(dot(d,moonRight),dot(d,moonUp))/.012;
                float moonRadius=length(moonUV);
                float edge=max(fwidth(moonRadius),.015);
                float disc=1-smoothstep(1-edge,1+edge,moonRadius);
                float craters=noise(moonUV*7+float2(13,29))*.18+noise(moonUV*19)*.08;
                float moonVisible=_Night*(1-cloud*.98)*(1-_Storm*.65)*step(0,dot(d,moonDirection));
                float halo=exp(-moonRadius*moonRadius*.22)*.035;
                result+=(disc*(.86-craters)+halo)*float3(.82,.87,1)*moonVisible;
                return float4(result+stars*.45*_SkyDetail,1);
            }
            ENDCG
        }
    }
}
