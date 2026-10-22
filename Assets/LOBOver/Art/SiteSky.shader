Shader "LOBOver/SiteSky"
{
    Properties
    {
        _Cube ("Imported sky", Cube) = "" {}
        _UseCube ("Use imported sky", Float) = 0
        _Exposure ("Exposure", Float) = 1
        _Rotation ("Cubemap rotation", Float) = 0
        [HideInInspector] _SunDirection ("Cubemap sun direction", Vector) = (0,1,0,0)
        _Zenith ("Zenith", Color) = (0.16,0.32,0.53,1)
        _Horizon ("Horizon haze", Color) = (0.68,0.73,0.75,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Cull Front ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            TEXTURECUBE(_Cube); SAMPLER(sampler_Cube);
            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon;
                float _UseCube, _Exposure, _Rotation;
            CBUFFER_END
            // Weather is shared by the dome and all site surfaces, including future materialization.
            float _SiteRain, _SiteCloudBank;
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            float Fbm(float2 p) { return Noise(p)*0.57+Noise(p*2.03)*0.28+Noise(p*4.07)*0.15; }
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.positionCS.z=UNITY_RAW_FAR_CLIP_VALUE*output.positionCS.w;
                output.direction=TransformObjectToWorldDir(input.positionOS.xyz); return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 d=normalize(input.direction);
                half3 col=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),0.45));
                if (_UseCube>0.5)
                {
                    float a=radians(_Rotation); float s=sin(a), c=cos(a);
                    float3 q=float3(c*d.x-s*d.z,d.y,s*d.x+c*d.z);
                    col=SAMPLE_TEXTURECUBE(_Cube,sampler_Cube,q).rgb*_Exposure;
                }
                else
                {
                    // WO-L4 Amendment 2: clouds vanish below 8 degrees, fully return at 16.
                    float2 uv=d.xz/max(d.y,0.139);
                    float cloud=Fbm(uv*1.2+_Time.y*float2(0.002,0.0007));
                    cloud=lerp(cloud,Fbm(uv*2.7+_Time.y*float2(-0.001,0.0015)),0.3);
                    col=lerp(col,half3(0.91,0.91,0.88),smoothstep(0.5,0.72,cloud)*smoothstep(0.139,0.276,d.y));
                    // URP's direction points toward the sun: minus the directional light's forward.
                    Light sun=GetMainLight(); float alignment=saturate(dot(d,sun.direction));
                    col+=sun.color*(pow(alignment,1100)*0.8+pow(alignment,48)*0.12)*(1-_SiteRain);
                }
                float bank=smoothstep(0.25,0.8,-d.z)*(1-smoothstep(0.12,0.42,d.y));
                col=lerp(col,half3(0.36,0.40,0.44),saturate(_SiteCloudBank*bank*0.7+_SiteRain*0.45));
                col=lerp(col,_Horizon.rgb,pow(1-saturate(abs(d.y)*3),4)*0.7);
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
