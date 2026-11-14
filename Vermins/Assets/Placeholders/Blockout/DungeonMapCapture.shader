// Foto de cima da dungeon pro DungeonLightMap: a cor do material em RGB e
// a altura do mundo em A. O DungeonLightMap desenha cada malha com este
// shader numa camera ortografica que olha pra baixo, entao o que fica
// gravado em cada texel e a superficie mais alta daquele ponto.
//
// A cor e a textura vem por MaterialPropertyBlock, com nome proprio: a
// malha e desenhada com este material, nao com o dela, e eu so preciso
// do _BaseColor e do _BaseMap de cada uma.
//
// Cull Off de proposito: olhando de cima eu quero o topo da parede mesmo
// que a malha tenha a face virada.
Shader "Hidden/Vermins/DungeonMapCapture"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Capture"

            Cull Off
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CaptureMap);
            SAMPLER(sampler_CaptureMap);
            float4 _CaptureMap_ST;
            float4 _CaptureColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float height : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv * _CaptureMap_ST.xy + _CaptureMap_ST.zw;
                output.height = positionWS.y;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 albedo = SAMPLE_TEXTURE2D(_CaptureMap, sampler_CaptureMap, input.uv).rgb * _CaptureColor.rgb;
                return float4(albedo, input.height);
            }
            ENDHLSL
        }
    }
}
