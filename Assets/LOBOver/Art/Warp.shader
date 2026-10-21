Shader "LOBOver/Warp"
{
    Properties
    {
        _Progress ("Dissolve", Range(0,1)) = 0
        _EdgeWidth ("Rim width", Float) = 0.015
        _EdgeColor ("Rim", Color) = (1,0.85,0.55,1)
        _NoiseScale ("Noise scale", Float) = 9
        _Bottom ("Sphere bottom", Float) = -0.3
        _Span ("Sphere height", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay+100" "RenderType"="Transparent" }
        Pass
        {
            Cull Front ZWrite Off ZTest Always Blend Off ColorMask RGBA
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
                float _Progress, _EdgeWidth, _NoiseScale, _Bottom, _Span;
                half4 _EdgeColor;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p = input.positionWS * _NoiseScale;
                float noise = sin(p.x + sin(p.z)) * sin(p.y * 0.73 + p.z);
                float height = (input.positionWS.y - _Bottom) / _Span + noise * 0.035;
                float edge = height - lerp(-0.1, 1.1, _Progress);
                clip(edge);
                if (_Progress > 0.001 && _Progress < 0.999 && edge < _EdgeWidth) return half4(_EdgeColor.rgb, 1);
                // Replace the eye-buffer alpha (not alpha blending): the real room is the underlay.
                return half4(0,0,0,0);
            }
            ENDHLSL
        }
    }
}
