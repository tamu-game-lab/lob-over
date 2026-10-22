Shader "LOBOver/SiteLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = 1
        _MetallicGlossMap ("Metallic (R), smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.3
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _OcclusionStrength ("Occlusion strength", Range(0,1)) = 1
        _OcclusionFromMask ("Occlusion packed in mask G", Float) = 0
        [Toggle(_WORLD)] _World ("World metre tiling", Float) = 0
        _TileMetres ("Metres per tile", Float) = 2
        [Toggle(_Splat)] _Splat ("Vertex RGB terrain layers", Float) = 0
        _GravelMap ("Gravel albedo", 2D) = "white" {}
        [Normal] _GravelNormal ("Gravel normal", 2D) = "bump" {}
        _GravelMask ("Gravel mask", 2D) = "white" {}
        _GrassMap ("Grass albedo", 2D) = "white" {}
        [Normal] _GrassNormal ("Grass normal", 2D) = "bump" {}
        _GrassMask ("Grass mask", 2D) = "white" {}
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Cutout foliage / lettering", Float) = 0
        _Cutoff ("Cutout threshold", Range(0,1)) = 0.5
        _AlphaMap ("Separate foliage opacity", 2D) = "white" {}
        _UseAlphaMap ("Separate opacity present", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        // TMP needs these properties to retain its atlas UVs and glyph padding.
        [HideInInspector] _MainTex ("Font Atlas", 2D) = "white" {}
        [HideInInspector] _FaceColor ("Face", Color) = (1,1,1,1)
        [HideInInspector] _FaceDilate ("Dilate", Float) = 0
        [HideInInspector] _OutlineWidth ("Outline", Float) = 0
        [HideInInspector] _OutlineSoftness ("Softness", Float) = 0
        [HideInInspector] _GradientScale ("Gradient", Float) = 5
        [HideInInspector] _WeightNormal ("Weight", Float) = 0
        [HideInInspector] _WeightBold ("Bold", Float) = 0.5
        [HideInInspector] _ScaleRatioA ("Ratio", Float) = 1
        [HideInInspector] _ScaleX ("X", Float) = 1
        [HideInInspector] _ScaleY ("Y", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);
        TEXTURE2D(_GravelMap); TEXTURE2D(_GravelNormal); TEXTURE2D(_GravelMask);
        TEXTURE2D(_GrassMap); TEXTURE2D(_GrassNormal); TEXTURE2D(_GrassMask);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BumpMap_ST, _MetallicGlossMap_ST, _OcclusionMap_ST; half4 _BaseColor, _FaceColor;
            float _BumpScale, _Metallic, _Smoothness, _OcclusionStrength, _OcclusionFromMask, _TileMetres;
            float _World, _AlphaClip, _Cutoff, _UseAlphaMap, _Cull;
            float _FaceDilate, _OutlineWidth, _OutlineSoftness, _GradientScale, _WeightNormal, _WeightBold, _ScaleRatioA, _ScaleX, _ScaleY;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1; half4 tangentWS : TEXCOORD2;
            float2 uv : TEXCOORD3; half4 color : COLOR; half fog : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
        };
        void Cutout(float2 uv, half alpha)
        {
            #if defined(_ALPHATEST_ON)
            clip(alpha-_Cutoff);
            #endif
        }
        Varyings Vert(Attributes input)
        {
            Varyings output=(Varyings)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs n=GetVertexNormalInputs(input.normalOS,input.tangentOS);
            output.positionCS=p.positionCS; output.positionWS=p.positionWS; output.normalWS=n.normalWS;
            output.tangentWS=half4(n.tangentWS,input.tangentOS.w*GetOddNegativeScale());
            output.uv=input.uv; output.color=input.color;
            output.fog=ComputeFogFactor(p.positionCS.z); return output;
        }
        // Stochastic triplanar: one projection is selected with the triplanar weights.
        // This keeps all three PBR maps within XR_RENDERING's three reads per layer;
        // stable world-space selection avoids nine reads per layer on Quest.
        void Projection(float3 p, half3 n, out float2 uv, out half3 t, out half3 b)
        {
            half3 w=pow(abs(n),4); w/=max(dot(w,1),0.0001);
            float h=frac(sin(dot(floor(p*64),float3(12.9898,78.233,39.425)))*43758.5453);
            if(h<w.x) { uv=p.zy; t=half3(0,0,sign(n.x)); b=half3(0,1,0); }
            else if(h<w.x+w.y) { uv=p.xz; t=half3(1,0,0); b=half3(0,0,sign(n.y)); }
            else { uv=p.xy; t=half3(sign(n.z),0,0); b=half3(0,1,0); }
            uv/=max(_TileMetres,0.01);
        }
        float GroundHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float2 GrassUV(float2 uv, float3 positionWS)
        {
            // WO-L4 rejection 1: stochastic triangular tiling. Barycentric selection
            // blends independently offset tiles without a square grid. One selected
            // tile supplies all three PBR maps, retaining the Quest three-read limit.
            float2 grid=float2(uv.x-uv.y*0.57735027,uv.y*1.15470054)/8;
            float2 cell=floor(grid), f=frac(grid);
            float2 a=cell, b=cell+float2(1,0), c=cell+float2(0,1);
            float3 weights=float3(1-f.x-f.y,f.x,f.y);
            if(f.x+f.y>1)
            {
                a=cell+1; weights=float3(f.x+f.y-1,1-f.y,1-f.x);
            }
            float pick=GroundHash(floor(positionWS.xz*128));
            float2 tile=pick<weights.x?a:(pick<weights.x+weights.y?b:c);
            return uv+float2(GroundHash(tile),GroundHash(tile+float2(19,47)))*57;
        }
        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half3 n=normalize(input.normalWS), t=normalize(input.tangentWS.xyz), b=cross(n,t)*input.tangentWS.w;
            float2 uv=TRANSFORM_TEX(input.uv,_BaseMap);
            float2 normalUV=TRANSFORM_TEX(input.uv,_BumpMap), maskUV=TRANSFORM_TEX(input.uv,_MetallicGlossMap), occlusionUV=TRANSFORM_TEX(input.uv,_OcclusionMap);
            #if defined(_WORLD)
                Projection(input.positionWS,n,uv,t,b);
                normalUV=maskUV=occlusionUV=uv;
            #endif
            half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
            Cutout(uv,albedo.a*_BaseColor.a);
            half3 bump=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,normalUV),_BumpScale);
            half4 mask=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,maskUV);
            half occlusion=lerp(1,mask.g,_OcclusionFromMask);
            #if defined(_Splat)
                half3 weights=max(input.color.rgb,0); weights/=max(dot(weights,1),0.001);
                half4 gravel=SAMPLE_TEXTURE2D(_GravelMap,sampler_BaseMap,uv);
                float2 grassUV=GrassUV(uv,input.positionWS);
                // Derivatives of the unjittered coordinates keep mip selection stable
                // across the randomized tile selection, including the horizon mesh.
                float2 grassDx=ddx(uv), grassDy=ddy(uv);
                half4 grass=SAMPLE_TEXTURE2D_GRAD(_GrassMap,sampler_BaseMap,grassUV,grassDx,grassDy);
                albedo=albedo*weights.r+gravel*weights.g+grass*weights.b;
                bump=bump*weights.r+UnpackNormalScale(SAMPLE_TEXTURE2D(_GravelNormal,sampler_BumpMap,uv),_BumpScale)*weights.g
                    +UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_GrassNormal,sampler_BumpMap,grassUV,grassDx,grassDy),_BumpScale)*weights.b;
                mask=mask*weights.r+SAMPLE_TEXTURE2D(_GravelMask,sampler_MetallicGlossMap,uv)*weights.g
                    +SAMPLE_TEXTURE2D_GRAD(_GrassMask,sampler_MetallicGlossMap,grassUV,grassDx,grassDy)*weights.b;
                occlusion=mask.g;
            #endif
            SurfaceData surface=(SurfaceData)0;
            surface.albedo=albedo.rgb*_BaseColor.rgb;
            surface.metallic=_Metallic; surface.smoothness=_Smoothness;
            #if defined(_METALLICSPECGLOSSMAP) || defined(_Splat)
                surface.metallic=mask.r; surface.smoothness=mask.a*_Smoothness;
            #endif
            surface.occlusion=lerp(1,occlusion,_OcclusionStrength); surface.alpha=1;
            surface.normalTS=normalize(bump);
            InputData data=(InputData)0;
            data.positionWS=input.positionWS;
            data.normalWS=normalize(bump.x*t+bump.y*b+bump.z*n);
            data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
            data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
            // UniversalFragmentPBR calls GetMainLight(shadowCoord) and uses SH as baked GI.
            data.bakedGI=SampleSH(data.normalWS); data.shadowMask=half4(1,1,1,1);
            data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
            half4 color=UniversalFragmentPBR(data,surface);
            color.rgb=MixFog(color.rgb,input.fog); color.a=1; return color;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit" Tags { "LightMode"="UniversalForward" }
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma shader_feature_local _WORLD
            #pragma shader_feature_local _Splat
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _METALLICSPECGLOSSMAP
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull] ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            float3 _LightDirection;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output=Vert(input);
                output.positionCS=TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,_LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z=min(output.positionCS.z,UNITY_NEAR_CLIP_VALUE*output.positionCS.w);
                #else
                    output.positionCS.z=max(output.positionCS.z,UNITY_NEAR_CLIP_VALUE*output.positionCS.w);
                #endif
                return output;
            }
            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=TRANSFORM_TEX(input.uv,_BaseMap);
                Cutout(uv,SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).a*_BaseColor.a); return 0;
            }
            ENDHLSL
        }
    }
}
