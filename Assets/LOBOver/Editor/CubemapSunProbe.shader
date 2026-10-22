Shader "Hidden/LOBOver/CubemapSunProbe"
{
    Properties { _Cube ("Sky", Cube) = "" {} }
    SubShader
    {
        Pass
        {
            // This Editor readback has no depth attachment.
            Cull Off ZWrite Off ZTest Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURECUBE(_Cube); SAMPLER(sampler_Cube);
            float3 _FaceForward, _FaceRight, _FaceUp;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv; return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.uv*2-1;
                return SAMPLE_TEXTURECUBE_LOD(_Cube,sampler_Cube,normalize(_FaceForward+p.x*_FaceRight+p.y*_FaceUp),0);
            }
            ENDHLSL
        }
    }
}
