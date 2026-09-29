// Contorno de destaque (casco invertido): desenha a malha de novo, so as
// faces de tras e empurradas pra fora pela normal. O que sobra aparecendo
// em volta do corpo vira a borda.
//
// Entra como material extra no renderer (o DestaqueDeAlvo poe e tira), e
// a cor tem nome proprio de proposito: o FlashDeDano pinta a _BaseColor
// pelo MaterialPropertyBlock do renderer inteiro, e se o contorno usasse
// o mesmo nome ele piscaria junto e ficaria com a cor do corpo depois.
//
// Em malha com quina dura (normal separada por face, tipo um cubo) a
// borda abre nas quinas. Em capsula e personagem com normal suave fica
// fechada.
Shader "Vermins/Contorno"
{
    Properties
    {
        _CorDoContorno ("Cor", Color) = (0.9, 0.15, 0.1, 1)
        _Espessura ("Espessura (m)", Float) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
        }

        Pass
        {
            Name "Contorno"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CorDoContorno;
                float _Espessura;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes entrada)
            {
                Varyings saida;

                // Empurro no mundo, e nao no objeto, pra espessura ficar
                // em metros mesmo com o inimigo escalado.
                float3 posicao = TransformObjectToWorld(entrada.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(entrada.normalOS);
                posicao += normal * _Espessura;

                saida.positionCS = TransformWorldToHClip(posicao);
                return saida;
            }

            half4 frag(Varyings entrada) : SV_Target
            {
                return _CorDoContorno;
            }
            ENDHLSL
        }
    }
}
