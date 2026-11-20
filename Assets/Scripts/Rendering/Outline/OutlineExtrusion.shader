Shader "Hidden/Unite/OutlineExtrusion"
{
    Properties
    {
        _SourceTex ("Source Texture", 2D) = "white" {}
        _SourceColor ("Source Color", Color) = (1,1,1,1)
        _OutlineSize ("Outline Size", Float) = 0.04
        _OutlineConsistency ("Outline Consistency", Range(0, 1)) = 1
        _OutlineSaturation ("Outline Saturation", Range(0, 5)) = 1
        _OutlineValue ("Outline Value", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+1" }

        Pass
        {
            Name "OutlineExtrusion"
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SourceTex);
            SAMPLER(sampler_SourceTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _SourceColor;
                float4 _SourceTex_ST;
                float _OutlineSize;
                float _OutlineConsistency;
                float _OutlineSaturation;
                float _OutlineValue;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 worldPosition = TransformObjectToWorld(input.positionOS.xyz);
                float3 worldNormal = normalize(TransformObjectToWorldNormal(input.normalOS));
                float3 scaleX = float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20);
                float3 scaleY = float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21);
                float3 scaleZ = float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22);
                float averageScale = max((length(scaleX) + length(scaleY) + length(scaleZ)) / 3.0, 0.0001);
                float extrusion = lerp(_OutlineSize * averageScale, _OutlineSize, _OutlineConsistency);

                worldPosition += worldNormal * extrusion;

                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = TRANSFORM_TEX(input.uv, _SourceTex);
                output.normalWS = worldNormal;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(worldPosition);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 color = SAMPLE_TEXTURE2D(_SourceTex, sampler_SourceTex, input.uv).rgb * _SourceColor.rgb;
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = lerp(luminance.xxx, color, _OutlineSaturation) * _OutlineValue;

                float normalDotView = abs(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                float silhouette = saturate(1.0 - normalDotView);
                return float4(color, silhouette * silhouette);
            }
            ENDHLSL
        }
    }
}
