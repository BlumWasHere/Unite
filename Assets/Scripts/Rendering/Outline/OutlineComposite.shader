Shader "Hidden/Unite/OutlineComposite"
{
    Properties
    {
        _OutlineTexture ("Outline Texture", 2D) = "black" {}
        _OutlineDepthTexture ("Outline Depth Texture", 2D) = "white" {}
        _OutlineScale ("Outline Scale", Float) = 1
        _OutlineSharpness ("Outline Sharpness", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OutlineComposite"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_OutlineTexture);
            TEXTURE2D_X_FLOAT(_OutlineDepthTexture);

            CBUFFER_START(UnityPerMaterial)
                float _OutlineScale;
                float _OutlineSharpness;
            CBUFFER_END

            float GetOutlineLinearDepth(float2 uv)
            {
                float raw = SAMPLE_TEXTURE2D_X(_OutlineDepthTexture, sampler_PointClamp, uv).r;
                if (raw >= 0.999999)
                    return 1.0;
                return Linear01Depth(raw, _ZBufferParams);
            }

            float GetOutlinePresence(float2 uv)
            {
                float alpha = SAMPLE_TEXTURE2D_X(_OutlineTexture, sampler_LinearClamp, uv).a;
                return step(0.001, alpha) * step(GetOutlineLinearDepth(uv), 0.99999);
            }

            float ComputeBlendFactor(float2 uv)
            {
                float baseDepth = Linear01Depth(SampleSceneDepth(uv), _ZBufferParams);
                float depthDifference = baseDepth - GetOutlineLinearDepth(uv); // positive where the shell is in front
                float fadeDistance = max(_OutlineScale * 0.5, 0.0001);
                return step(0.0, depthDifference) * saturate(depthDifference / fadeDistance);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float4 baseColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (GetOutlinePresence(uv) <= 0.0)
                    return baseColor;

                float3 outlineColor = SAMPLE_TEXTURE2D_X(_OutlineTexture, sampler_LinearClamp, uv).rgb;
                float mixFactor = ComputeBlendFactor(uv);
                if (_OutlineSharpness > 0.0001)
                    mixFactor = saturate((mixFactor - 0.5) * (_OutlineSharpness + 1.0) + 0.5);

                return float4(lerp(baseColor.rgb, outlineColor, mixFactor), baseColor.a);
            }
            ENDHLSL
        }
    }
}
