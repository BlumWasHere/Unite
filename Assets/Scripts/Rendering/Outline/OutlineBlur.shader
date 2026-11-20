Shader "Hidden/Unite/OutlineBlur"
{
// Copied from Kirby RPG
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "OutlineBlur"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                // Unit step for this pass in UV space: (1/width, 0) for the horizontal
                // pass, (0, 1/height) for the vertical pass.
                float2 _BlurDirection;
                // Tap spacing in texels (<= ~2 to stay ghost-free).
                float _BlurSpread;
            CBUFFER_END

            void AccumulateTap(float2 uv, float weight, inout float3 rgbSum, inout float covSum, inout float aSum)
            {
                float4 sample = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                // Coverage-weighted colour: empty (alpha ~0) texels are black filler and must
                // not darken the average — weight the RGB by presence, track coverage.
                float coverage = step(0.001, sample.a) * weight;
                rgbSum += sample.rgb * coverage;
                covSum += coverage;
                aSum += sample.a * weight;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 stepUv = _BlurDirection * _BlurSpread;

                // Normalised 9-tap Gaussian (sigma ~1.8).
                const float w0 = 0.227027;
                const float w1 = 0.1945946;
                const float w2 = 0.1216216;
                const float w3 = 0.0540541;
                const float w4 = 0.0162162;

                float3 rgbSum = 0.0;
                float covSum = 0.0;
                float aSum = 0.0;

                AccumulateTap(uv, w0, rgbSum, covSum, aSum);
                AccumulateTap(uv + stepUv, w1, rgbSum, covSum, aSum);
                AccumulateTap(uv - stepUv, w1, rgbSum, covSum, aSum);
                AccumulateTap(uv + stepUv * 2.0, w2, rgbSum, covSum, aSum);
                AccumulateTap(uv - stepUv * 2.0, w2, rgbSum, covSum, aSum);
                AccumulateTap(uv + stepUv * 3.0, w3, rgbSum, covSum, aSum);
                AccumulateTap(uv - stepUv * 3.0, w3, rgbSum, covSum, aSum);
                AccumulateTap(uv + stepUv * 4.0, w4, rgbSum, covSum, aSum);
                AccumulateTap(uv - stepUv * 4.0, w4, rgbSum, covSum, aSum);

                float3 rgb = covSum > 0.0001 ? rgbSum / covSum : 0.0;
                return half4(rgb, aSum);
            }
            ENDHLSL
        }
    }
}
