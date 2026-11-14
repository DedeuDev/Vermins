// A parte do DungeonLit que e minha: ler a luz indireta das sondas do
// DungeonLightMap no pixel.
//
// As sondas ficam a 0,5 m e a parede tem 0,25 m, entao a interpolacao
// comum misturaria a sonda deste lado com a do outro lado da parede. Por
// isso faco a interpolacao em X e Z na mao: das quatro colunas de sondas
// em volta do pixel, so entra a que o pixel enxerga, conferindo a altura
// na foto de cima em tres pontos do caminho ate ela. Na altura deixo a
// interpolacao da placa, lendo cada coluna bem no centro dela.
//
// Cada textura guarda um canal de cor: x e a media da radiancia e yzw a
// media da radiancia vezes a direcao. A luz difusa na convencao da Unity e
// m0 + 2 * dot(m1, normal).
#ifndef VERMINS_DUNGEON_LIT_GI_INCLUDED
#define VERMINS_DUNGEON_LIT_GI_INCLUDED

TEXTURE3D(_DungeonGI_R);
TEXTURE3D(_DungeonGI_G);
TEXTURE3D(_DungeonGI_B);
TEXTURE2D(_DungeonMap);

float4 _DungeonGIMin;      // xyz = canto de menor X, Y e Z; w = metros por texel da foto
float4 _DungeonGISize;     // xyz = metros em X, Y e Z
float4 _DungeonGIDims;     // sondas em X, sondas em Z, camadas
float4 _DungeonMapTexels;  // largura e altura da foto
float4 _DungeonGISky;      // o ambiente do tema, ja com a forca
float _DungeonGIBlend;     // 0 = ambiente do Lit, 1 = sondas

float DungeonHeight(float2 xz)
{
    int2 texel = int2(floor((xz - _DungeonGIMin.xz) / _DungeonGIMin.w));
    texel = clamp(texel, int2(0, 0), int2(_DungeonMapTexels.xy) - 1);
    return LOAD_TEXTURE2D(_DungeonMap, texel).a;
}

half3 SampleDungeonGI(float3 positionWS, half3 normalWS)
{
    // Saio um pouco da superficie: o pixel da parede cairia no texel da
    // propria parede e nao enxergaria sonda nenhuma.
    float3 position = positionWS + normalWS * 0.15;
    float2 cell = _DungeonGISize.xz / _DungeonGIDims.xy;
    float2 grid = (position.xz - _DungeonGIMin.xz) / cell - 0.5;
    float2 corner = floor(grid);
    float2 fraction = grid - corner;
    float layer = saturate((position.y - _DungeonGIMin.y) / _DungeonGISize.y);

    // Mais alto que isto entre o pixel e a sonda e parede ou caixa.
    float limit = position.y + 0.3;

    float4 r = 0.0, g = 0.0, b = 0.0;
    float4 rAll = 0.0, gAll = 0.0, bAll = 0.0;
    float total = 0.0;

    for (int i = 0; i < 4; i++)
    {
        float2 offset = float2(i & 1, i >> 1);
        float2 column = corner + offset;
        float2 weights = lerp(1.0 - fraction, fraction, offset);
        float weight = weights.x * weights.y;
        float2 probe = _DungeonGIMin.xz + (column + 0.5) * cell;
        float2 toProbe = probe - position.xz;

        // O caminho tem ate 0,7 m; em tercos, nenhum vao passa de 0,24 m,
        // menos que a parede mais fina.
        float atProbe = DungeonHeight(probe);
        float highest = max(atProbe, max(DungeonHeight(position.xz + toProbe * 0.333),
                                         DungeonHeight(position.xz + toProbe * 0.667)));
        float visible = (highest <= limit && atProbe > -500.0) ? 1.0 : 0.0;

        float3 uvw = float3((column + 0.5) / _DungeonGIDims.xy, layer);
        float4 sampleR = SAMPLE_TEXTURE3D_LOD(_DungeonGI_R, sampler_LinearClamp, uvw, 0);
        float4 sampleG = SAMPLE_TEXTURE3D_LOD(_DungeonGI_G, sampler_LinearClamp, uvw, 0);
        float4 sampleB = SAMPLE_TEXTURE3D_LOD(_DungeonGI_B, sampler_LinearClamp, uvw, 0);

        rAll += sampleR * weight;
        gAll += sampleG * weight;
        bAll += sampleB * weight;
        r += sampleR * (weight * visible);
        g += sampleG * (weight * visible);
        b += sampleB * (weight * visible);
        total += weight * visible;
    }

    // Sem nenhuma coluna a vista, sao dois casos. Se o proprio lugar do
    // pixel e vazio na foto, ele esta do lado de fora: a face da parede
    // que da pro nada, e ali so chega o ambiente, por cima. Senao ele
    // esta embaixo de alguma coisa (o chao sob o cano, que fica suspenso)
    // e fico com a mistura comum das sondas em volta.
    // Errei os dois antes: com a mistura comum em tudo, o rebote da tocha
    // de dentro manchava a parede por fora; com o ambiente em tudo, o
    // chao embaixo do cano ficava com uma faixa clara.
    if (total > 0.001)
    {
        float scale = 1.0 / total;
        r *= scale;
        g *= scale;
        b *= scale;
    }
    else if (DungeonHeight(position.xz) < -500.0)
    {
        r = float4(0.5, 0.0, 0.25, 0.0) * _DungeonGISky.r;
        g = float4(0.5, 0.0, 0.25, 0.0) * _DungeonGISky.g;
        b = float4(0.5, 0.0, 0.25, 0.0) * _DungeonGISky.b;
    }
    else
    {
        r = rAll;
        g = gAll;
        b = bAll;
    }

    float3 normal = normalWS;
    return max(0.0, half3(r.x + 2.0 * dot(r.yzw, normal),
                          g.x + 2.0 * dot(g.yzw, normal),
                          b.x + 2.0 * dot(b.yzw, normal)));
}

// O LitPassFragment da URP 17.3 com uma linha a mais, a do bakedGI.
void DungeonLitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    if (_DungeonGIBlend > 0.0)
        inputData.bakedGI = lerp(inputData.bakedGI, SampleDungeonGI(inputData.positionWS, inputData.normalWS), _DungeonGIBlend);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
