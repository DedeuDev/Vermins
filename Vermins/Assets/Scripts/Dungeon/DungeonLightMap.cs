using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>
/// Arquivo do Ian. Fotografo a dungeon de cima uma vez, na carga: a cor
/// do material em RGB e a altura do mundo em A. A dungeon tem um andar
/// so e nao tem teto, entao essa foto descreve a cena inteira, e e nela
/// que o calculo da luz indireta vai tracar os raios.
///
/// Nao uso camera: desenho cada malha com o DungeonMapCapture numa
/// projecao ortografica que olha pra baixo. Assim escolho o que entra
/// sem esconder nada na cena, e a URP nao participa.
///
/// Entra o que e cenario fixo: o que mora num DungeonModule, os pilares
/// do DungeonSeams e os props do DungeonDressing. Fica de fora quem tem
/// Health (inimigo e boss nascem dentro do gerador) e a haste da tocha,
/// que faria sombra na propria luz.
///
/// Espero quatro frames depois do NavMesh, como o DungeonReflections: os
/// props chegam no terceiro e o tema ja trocou o material. Nao tem Update.
///
/// Depois da foto vem a luz indireta, no DungeonLightMap.compute: uma
/// sonda a cada meio metro, em camadas de altura, solta alguns raios por
/// frame na foto e guarda a media em SH L1, em tres texturas 3D (uma por
/// canal de cor). Espalhei a conta por varios frames pra carga nao travar,
/// e ela para sozinha: a dungeon e as luzes fixas nao mudam depois.
///
/// Luz fixa e toda Light pontual ou spot que mora no gerador e nao e de
/// ator: tochas, a do guarda, a da porta do boss e a do bau. A luz que
/// anda com o Player fica de fora, ela e filha da camera.
///
/// Quem usa o resultado e o shader Vermins/DungeonLit, por variaveis
/// globais: as tres texturas, a foto e as medidas. Quando a conta termina
/// eu subo o _DungeonGIBlend de 0 a 1 em meio segundo, pra luz nao trocar
/// de uma vez, e devolvo pra 0 ao sair da dungeon.
///
/// Player, inimigos e boss usam o Lit comum e se mexem, entao recebem a
/// mesma luz por objeto: leio as sondas de volta pra CPU uma vez e, quando
/// o ator anda mais de 10 cm, interpolo as oito sondas em volta dele (1 m
/// acima do pe) e mando como SH pelo MaterialPropertyBlock. Pego o bloco
/// antes de escrever pra nao apagar a cor do DamageFlash, que faz o mesmo.
/// Ator que nasce depois da carga fica com o ambiente chapado.
/// </summary>
[RequireComponent(typeof(RuntimeDungeonNavMesh))]
public class DungeonLightMap : MonoBehaviour
{
    [Tooltip("O Hidden/Vermins/DungeonMapCapture. Vazio, procuro pelo nome, " +
             "o que so funciona no editor.")]
    [SerializeField] private Shader captureShader;

    [Tooltip("Metros por texel da foto. A parede mais fina precisa de pelo " +
             "menos dois texels pra luz nao vazar.")]
    [SerializeField, Min(0.05f)] private float texelSize = 0.125f;

    [Tooltip("Lado maximo da foto. Dungeon maior que isso aumenta o texel.")]
    [SerializeField, Min(64)] private int maxResolution = 2048;

    [Tooltip("Folga em volta da dungeon, em metros, pro raio ter pra onde sair.")]
    [SerializeField, Min(0f)] private float border = 1f;

    [Tooltip("Maximo de frames esperando o NavMesh, igual aos spawners.")]
    [SerializeField, Min(1)] private int maxFramesWaitingForNavMesh = 300;

    [Header("Luz indireta")]
    [Tooltip("O DungeonLightMap.compute. Vazio, fica so a foto.")]
    [SerializeField] private ComputeShader bakeShader;

    [Tooltip("Metros entre uma sonda e a proxima, no chao.")]
    [SerializeField, Min(0.25f)] private float probeSpacing = 0.5f;

    [Tooltip("Camadas de sondas entre o piso e o topo da parede.")]
    [SerializeField, Range(1, 8)] private int probeLayers = 4;

    [Tooltip("Raios por sonda em cada frame do calculo.")]
    [SerializeField, Range(1, 64)] private int raysPerFrame = 16;

    [Tooltip("Frames de calculo. Raios por sonda = este vezes o de cima.")]
    [SerializeField, Min(1)] private int bakeFrames = 64;

    [Tooltip("Ate onde o raio anda, em metros.")]
    [SerializeField, Min(1f)] private float maxRayDistance = 16f;

    [Tooltip("Desligado, a luz rebate uma vez so.")]
    [SerializeField] private bool multiBounce = true;

    [Tooltip("Multiplica o ambiente do tema que entra por cima das paredes. " +
             "Com 1, medi o chao recebendo 0,56 a 0,61 do ambiente chapado: " +
             "a parede de 3 m tapa quase metade. Com 1,7 o chao volta ao " +
             "brilho de antes e a parede fica mais escura que ele.")]
    [SerializeField, Min(0f)] private float skyStrength = 1.7f;

    [Tooltip("Frames pra luz indireta entrar depois de calculada.")]
    [SerializeField, Min(1)] private int fadeFrames = 30;

    /// <summary>Altura gravada onde nao ha malha: fora das paredes.</summary>
    public const float VoidHeight = -1000f;

    private const string SeamsRootName = "Generated Seams";
    private const string PropsRootName = "Generated Props";

    private static readonly int CaptureColorId = Shader.PropertyToID("_CaptureColor");
    private static readonly int CaptureMapId = Shader.PropertyToID("_CaptureMap");
    private static readonly int CaptureMapStId = Shader.PropertyToID("_CaptureMap_ST");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    private static readonly int MapId = Shader.PropertyToID("_Map");
    private static readonly int LightsId = Shader.PropertyToID("_Lights");
    private static readonly int LightCountId = Shader.PropertyToID("_LightCount");
    private static readonly int MapMinId = Shader.PropertyToID("_MapMin");
    private static readonly int MapSizeId = Shader.PropertyToID("_MapSize");
    private static readonly int MapTexelsId = Shader.PropertyToID("_MapTexels");
    private static readonly int VolumeDimsId = Shader.PropertyToID("_VolumeDims");
    private static readonly int SkyColorId = Shader.PropertyToID("_SkyColor");
    private static readonly int MaxDistanceId = Shader.PropertyToID("_MaxDistance");
    private static readonly int RaysPerFrameId = Shader.PropertyToID("_RaysPerFrame");
    private static readonly int IterationId = Shader.PropertyToID("_Iteration");
    private static readonly int UseBounceId = Shader.PropertyToID("_UseBounce");
    private static readonly int GlobalMapId = Shader.PropertyToID("_DungeonMap");
    private static readonly int GlobalMinId = Shader.PropertyToID("_DungeonGIMin");
    private static readonly int GlobalSizeId = Shader.PropertyToID("_DungeonGISize");
    private static readonly int GlobalDimsId = Shader.PropertyToID("_DungeonGIDims");
    private static readonly int GlobalMapTexelsId = Shader.PropertyToID("_DungeonMapTexels");
    private static readonly int GlobalBlendId = Shader.PropertyToID("_DungeonGIBlend");
    private static readonly int GlobalSkyId = Shader.PropertyToID("_DungeonGISky");
    private static readonly int[] GlobalIrradianceIds =
        { Shader.PropertyToID("_DungeonGI_R"), Shader.PropertyToID("_DungeonGI_G"), Shader.PropertyToID("_DungeonGI_B") };
    private static readonly int[] SourceIds =
        { Shader.PropertyToID("_SrcR"), Shader.PropertyToID("_SrcG"), Shader.PropertyToID("_SrcB") };
    private static readonly int[] DestinationIds =
        { Shader.PropertyToID("_DstR"), Shader.PropertyToID("_DstG"), Shader.PropertyToID("_DstB") };

    // Igual ao LightData do compute: 48 bytes.
    private struct LightData
    {
        public Vector3 position;
        public float range;
        public Vector3 color;
        public float spotCosOuter;
        public Vector3 direction;
        public float spotInvRange;
    }

    private readonly List<Material> materialBuffer = new List<Material>();
    private Material captureMaterial;
    private RenderTexture[] irradiance = new RenderTexture[3];
    private RenderTexture[] scratch = new RenderTexture[3];
    private ComputeBuffer lightBuffer;
    private Coroutine bakeRoutine;
    private bool giEnabled = true;

    // Quem tem Health na cena, com os renderers e o uso de sonda original.
    private struct Actor
    {
        public Transform root;
        public Renderer[] renderers;
        public LightProbeUsage[] originalUsage;
        public Vector3 lastPosition;
    }

    private readonly List<Actor> actors = new List<Actor>();
    private readonly float[][] probes = new float[3][];
    private readonly SphericalHarmonicsL2[] actorProbe = new SphericalHarmonicsL2[1];
    private MaterialPropertyBlock actorBlock;
    private Coroutine actorRoutine;
    private float actorBlend;
    private bool actorsDirty;

    public bool Ready { get; private set; }

    /// <summary>RGB = cor linear, A = altura do mundo em metros.</summary>
    public RenderTexture Map { get; private set; }

    /// <summary>Canto de menor X e Z da foto; o Y e o piso mais baixo.</summary>
    public Vector3 WorldMin { get; private set; }

    /// <summary>Tamanho da foto em metros: X, altura do cenario, Z.</summary>
    public Vector3 WorldSize { get; private set; }

    /// <summary>Metros por texel de verdade, depois do teto de resolucao.</summary>
    public float TexelSize { get; private set; }

    public int RendererCount { get; private set; }
    public double CaptureMilliseconds { get; private set; }

    /// <summary>A luz indireta terminou e as tres texturas valem.</summary>
    public bool BakeComplete { get; private set; }

    /// <summary>
    /// Liga e desliga a luz indireta no cenario sem refazer a conta.
    /// Desligada, o DungeonLit volta a usar o ambiente chapado.
    /// </summary>
    public bool GIEnabled
    {
        get => giEnabled;
        set
        {
            giEnabled = value;
            actorsDirty = true;
            if (BakeComplete) Shader.SetGlobalFloat(GlobalBlendId, value ? 1f : 0f);
        }
    }

    /// <summary>Sondas em X, em Z e camadas de altura.</summary>
    public Vector3Int VolumeSize { get; private set; }

    public int LightCount { get; private set; }

    /// <summary>Atores que recebem a luz das sondas por objeto.</summary>
    public int ActorCount => actors.Count;
    public int BakedFrames { get; private set; }
    public double BakeElapsedMilliseconds { get; private set; }

    /// <summary>O frame mais lento durante o calculo, em ms.</summary>
    public double BakeWorstFrameMilliseconds { get; private set; }

    /// <summary>
    /// Canal 0, 1 ou 2 (R, G, B). Em cada textura, x e a media da radiancia
    /// e yzw e a media da radiancia vezes a direcao. X e Z do mundo ficam no
    /// plano e a altura nas fatias.
    /// </summary>
    public RenderTexture GetIrradiance(int channel) => irradiance[channel];

    private IEnumerator Start()
    {
        var navMesh = GetComponent<RuntimeDungeonNavMesh>();
        int waitedFrames = 0;
        while (!navMesh.HasBuiltNavMesh && waitedFrames++ < maxFramesWaitingForNavMesh)
            yield return null;

        if (!navMesh.HasBuiltNavMesh)
        {
            Debug.LogError("DungeonLightMap: o NavMesh nao ficou pronto.", this);
            yield break;
        }

        for (int i = 0; i < 4; i++)
            yield return null;

        Capture();
        Rebake();
    }

    /// <summary>Refaz a luz indireta em cima da foto que ja existe.</summary>
    public void Rebake()
    {
        if (bakeRoutine != null) StopCoroutine(bakeRoutine);
        if (actorRoutine != null) StopCoroutine(actorRoutine);
        bakeRoutine = null;
        actorRoutine = null;
        actorBlend = 0f;
        ApplyActors(true);
        BakeComplete = false;
        Shader.SetGlobalFloat(GlobalBlendId, 0f);

        if (Ready && bakeShader != null && isActiveAndEnabled)
            bakeRoutine = StartCoroutine(Bake());
    }

    /// <summary>Do mundo para a UV da foto. O Y nao entra.</summary>
    public Vector2 WorldToUV(Vector3 world)
    {
        return new Vector2((world.x - WorldMin.x) / WorldSize.x, (world.z - WorldMin.z) / WorldSize.z);
    }

    [ContextMenu("Recapture")]
    public void Capture()
    {
        long started = Stopwatch.GetTimestamp();
        Ready = false;

        if (captureShader == null)
            captureShader = Shader.Find("Hidden/Vermins/DungeonMapCapture");

        if (captureShader == null)
        {
            Debug.LogError("DungeonLightMap: sem o shader de captura.", this);
            return;
        }

        if (captureMaterial == null)
            captureMaterial = new Material(captureShader) { hideFlags = HideFlags.HideAndDontSave };

        var renderers = new List<MeshRenderer>();
        var meshes = new List<Mesh>();
        Bounds bounds = default;
        Transform seams = transform.Find(SeamsRootName);
        Transform props = transform.Find(PropsRootName);

        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
        {
            if (!renderer.enabled || renderer.GetComponentInParent<Health>() != null)
                continue;

            bool scenery = renderer.GetComponentInParent<DungeonModule>() != null ||
                           (seams != null && renderer.transform.IsChildOf(seams)) ||
                           (props != null && renderer.transform.IsChildOf(props));
            MeshFilter filter = renderer.GetComponent<MeshFilter>();

            if (!scenery || filter == null || filter.sharedMesh == null)
                continue;

            if (renderers.Count == 0) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            renderers.Add(renderer);
            meshes.Add(filter.sharedMesh);
        }

        RendererCount = renderers.Count;

        if (renderers.Count == 0)
        {
            Debug.LogError("DungeonLightMap: nao achei cenario pra fotografar.", this);
            return;
        }

        bounds.Expand(new Vector3(border * 2f, 0f, border * 2f));

        // Se a dungeon nao cabe no teto, aumento o texel em vez de cortar.
        TexelSize = Mathf.Max(texelSize, Mathf.Max(bounds.size.x, bounds.size.z) / maxResolution);
        int width = Mathf.CeilToInt(bounds.size.x / TexelSize);
        int height = Mathf.CeilToInt(bounds.size.z / TexelSize);

        // Arredondei pra cima: a foto cobre um pouco mais que a caixa, e o
        // tamanho em metros tem que ser o da foto pra UV bater com o texel.
        WorldMin = bounds.min;
        WorldSize = new Vector3(width * TexelSize, bounds.size.y, height * TexelSize);

        if (Map == null || Map.width != width || Map.height != height)
        {
            ReleaseMap();
            Map = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = "Dungeon Light Map Capture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
            };
            Map.Create();
        }

        // Camera de mentira em cima da dungeon, olhando pra baixo, com o
        // +Z do mundo pra cima da foto: assim U cresce com X e V com Z.
        float top = bounds.max.y + 1f;
        float depth = bounds.size.y + 2f;
        Vector3 eye = new Vector3(WorldMin.x + WorldSize.x * 0.5f, top, WorldMin.z + WorldSize.z * 0.5f);
        Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
                         Matrix4x4.LookAt(eye, eye + Vector3.down, Vector3.forward).inverse;
        Matrix4x4 projection = Matrix4x4.Ortho(-WorldSize.x * 0.5f, WorldSize.x * 0.5f,
                                               -WorldSize.z * 0.5f, WorldSize.z * 0.5f, 0f, depth);

        var block = new MaterialPropertyBlock();
        var command = new CommandBuffer { name = "Dungeon Light Map Capture" };
        command.SetRenderTarget(Map);
        command.ClearRenderTarget(true, true, new Color(0f, 0f, 0f, VoidHeight));
        command.SetViewProjectionMatrices(view, projection);

        for (int i = 0; i < renderers.Count; i++)
        {
            renderers[i].GetSharedMaterials(materialBuffer);
            int subMeshes = Mathf.Min(meshes[i].subMeshCount, materialBuffer.Count);

            for (int sub = 0; sub < subMeshes; sub++)
            {
                Material source = materialBuffer[sub];
                Color color = Color.white;
                Texture texture = Texture2D.whiteTexture;
                Vector4 scaleOffset = new Vector4(1f, 1f, 0f, 0f);

                if (source != null && source.HasProperty(BaseColorId))
                    color = source.GetColor(BaseColorId);

                if (source != null && source.HasProperty(BaseMapId))
                {
                    Texture baseMap = source.GetTexture(BaseMapId);
                    Vector2 scale = source.GetTextureScale(BaseMapId);
                    Vector2 offset = source.GetTextureOffset(BaseMapId);
                    if (baseMap != null) texture = baseMap;
                    scaleOffset = new Vector4(scale.x, scale.y, offset.x, offset.y);
                }

                // O Inspector guarda a cor em sRGB e a foto e linear. Mando
                // como vetor pra conversao ser so esta, e nao duas.
                block.SetVector(CaptureColorId, color.linear);
                block.SetTexture(CaptureMapId, texture);
                block.SetVector(CaptureMapStId, scaleOffset);
                command.DrawMesh(meshes[i], renderers[i].localToWorldMatrix, captureMaterial, sub, 0, block);
            }
        }

        Graphics.ExecuteCommandBuffer(command);
        command.Release();
        materialBuffer.Clear();

        Ready = true;
        CaptureMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
    }

    private IEnumerator Bake()
    {
        long started = Stopwatch.GetTimestamp();
        BakedFrames = 0;
        BakeWorstFrameMilliseconds = 0d;

        CollectLights();

        int width = Mathf.Max(1, Mathf.CeilToInt(WorldSize.x / probeSpacing));
        int depth = Mathf.Max(1, Mathf.CeilToInt(WorldSize.z / probeSpacing));
        VolumeSize = new Vector3Int(width, depth, probeLayers);

        for (int i = 0; i < 3; i++)
        {
            irradiance[i] = EnsureVolume(irradiance[i], "Dungeon Irradiance " + i);
            scratch[i] = EnsureVolume(scratch[i], "Dungeon Irradiance Scratch " + i);
        }

        // O ambiente do tema e a luz que vem de cima das paredes. Leio do
        // RenderSettings porque o DungeonTheme ja escreveu la no Start.
        var up = new[] { Vector3.up };
        var sky = new Color[1];
        RenderSettings.ambientProbe.Evaluate(up, sky);

        int bake = bakeShader.FindKernel("Bake");
        int dilate = bakeShader.FindKernel("Dilate");
        bakeShader.SetVector(MapMinId, new Vector4(WorldMin.x, WorldMin.y, WorldMin.z, TexelSize));
        bakeShader.SetVector(MapSizeId, new Vector4(WorldSize.x, WorldSize.y, WorldSize.z, WorldMin.y + WorldSize.y));
        bakeShader.SetInts(MapTexelsId, Map.width, Map.height, 0, 0);
        bakeShader.SetInts(VolumeDimsId, width, depth, probeLayers, 0);
        bakeShader.SetVector(SkyColorId, sky[0] * skyStrength);
        bakeShader.SetFloat(MaxDistanceId, maxRayDistance);
        bakeShader.SetInt(RaysPerFrameId, raysPerFrame);
        bakeShader.SetInt(LightCountId, LightCount);
        bakeShader.SetTexture(bake, MapId, Map);
        bakeShader.SetTexture(dilate, MapId, Map);
        bakeShader.SetBuffer(bake, LightsId, lightBuffer);

        int groupsX = (width + 7) / 8;
        int groupsY = (depth + 7) / 8;

        for (int frame = 0; frame < bakeFrames; frame++)
        {
            // Na primeira passada ainda nao existe rebote pra ler.
            bakeShader.SetInt(IterationId, frame);
            bakeShader.SetInt(UseBounceId, multiBounce && frame > 0 ? 1 : 0);
            Dispatch(bake, groupsX, groupsY);
            BakedFrames = frame + 1;
            yield return null;
            BakeWorstFrameMilliseconds = System.Math.Max(BakeWorstFrameMilliseconds, Time.unscaledDeltaTime * 1000d);
        }

        Dispatch(dilate, groupsX, groupsY);

        BakeComplete = true;
        BakeElapsedMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;

        for (int i = 0; i < 3; i++)
            Shader.SetGlobalTexture(GlobalIrradianceIds[i], irradiance[i]);
        Shader.SetGlobalTexture(GlobalMapId, Map);
        Shader.SetGlobalVector(GlobalMinId, new Vector4(WorldMin.x, WorldMin.y, WorldMin.z, TexelSize));
        Shader.SetGlobalVector(GlobalSizeId, WorldSize);
        Shader.SetGlobalVector(GlobalDimsId, new Vector4(width, depth, probeLayers, 0f));
        Shader.SetGlobalVector(GlobalMapTexelsId, new Vector4(Map.width, Map.height, 0f, 0f));
        Shader.SetGlobalVector(GlobalSkyId, sky[0] * skyStrength);

        // Copia das sondas na CPU, pros atores. Uma vez so; o pedido volta
        // em poucos frames e a luz do cenario espera junto pra entrar igual.
        bool readBack = SystemInfo.supportsAsyncGPUReadback;
        int sliceLength = width * depth * 4;

        for (int i = 0; i < 3 && readBack; i++)
        {
            var request = AsyncGPUReadback.Request(irradiance[i], 0, TextureFormat.RGBAFloat);
            while (!request.done)
                yield return null;

            if (request.hasError || request.layerCount != probeLayers)
            {
                readBack = false;
                break;
            }

            if (probes[i] == null || probes[i].Length != sliceLength * probeLayers)
                probes[i] = new float[sliceLength * probeLayers];

            for (int layer = 0; layer < probeLayers; layer++)
                Unity.Collections.NativeArray<float>.Copy(request.GetData<float>(layer), 0, probes[i], layer * sliceLength, sliceLength);
        }

        if (readBack) CollectActors();
        else Debug.LogWarning("DungeonLightMap: nao consegui ler as sondas; os atores ficam com o ambiente.", this);

        for (int frame = 1; frame <= fadeFrames && giEnabled; frame++)
        {
            actorBlend = frame / (float)fadeFrames;
            Shader.SetGlobalFloat(GlobalBlendId, actorBlend);
            ApplyActors(true);
            yield return null;
        }

        actorBlend = 1f;
        Shader.SetGlobalFloat(GlobalBlendId, giEnabled ? 1f : 0f);
        ApplyActors(true);
        bakeRoutine = null;
        actorRoutine = StartCoroutine(FollowActors());
    }

    private void CollectActors()
    {
        actors.Clear();
        if (actorBlock == null) actorBlock = new MaterialPropertyBlock();
        var found = new List<Renderer>();

        foreach (Health health in FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (health.gameObject.scene != gameObject.scene)
                continue;

            found.Clear();
            foreach (Renderer renderer in health.GetComponentsInChildren<Renderer>(true))
                if (!(renderer is ParticleSystemRenderer) && !(renderer is LineRenderer) && !(renderer is TrailRenderer))
                    found.Add(renderer);

            if (found.Count == 0)
                continue;

            var actor = new Actor
            {
                root = health.transform,
                renderers = found.ToArray(),
                originalUsage = new LightProbeUsage[found.Count],
                lastPosition = health.transform.position,
            };

            for (int i = 0; i < found.Count; i++)
                actor.originalUsage[i] = found[i].lightProbeUsage;

            actors.Add(actor);
        }
    }

    // Um laco por frame so pra ver quem andou. Ator parado nao custa nada
    // alem da comparacao, e nada aqui aloca.
    private IEnumerator FollowActors()
    {
        while (true)
        {
            ApplyActors(actorsDirty);
            actorsDirty = false;
            yield return null;
        }
    }

    private void ApplyActors(bool force)
    {
        if (actors.Count == 0 || probes[0] == null)
            return;

        bool lit = giEnabled && actorBlend > 0f && isActiveAndEnabled;

        for (int i = actors.Count - 1; i >= 0; i--)
        {
            Actor actor = actors[i];

            if (actor.root == null)
            {
                actors.RemoveAt(i);
                continue;
            }

            Vector3 position = actor.root.position;
            if (!force && (position - actor.lastPosition).sqrMagnitude < 0.01f)
                continue;

            actor.lastPosition = position;
            actors[i] = actor;

            if (!lit)
            {
                for (int k = 0; k < actor.renderers.Length; k++)
                    if (actor.renderers[k] != null)
                        actor.renderers[k].lightProbeUsage = actor.originalUsage[k];
                continue;
            }

            SphericalHarmonicsL2 light = SampleProbes(position + Vector3.up);
            if (actorBlend < 1f)
                light = RenderSettings.ambientProbe * (1f - actorBlend) + light * actorBlend;
            actorProbe[0] = light;

            for (int k = 0; k < actor.renderers.Length; k++)
            {
                Renderer renderer = actor.renderers[k];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(actorBlock);
                actorBlock.CopySHCoefficientArraysFrom(actorProbe);
                renderer.lightProbeUsage = LightProbeUsage.CustomProvided;
                renderer.SetPropertyBlock(actorBlock);
            }
        }
    }

    /// <summary>
    /// As oito sondas em volta do ponto, interpoladas na CPU, como o SH da
    /// Unity: o termo constante e a media, e os tres lineares sao o dobro
    /// da media vezes a direcao (o shader faz m0 + 2 * dot(m1, normal)).
    /// </summary>
    public SphericalHarmonicsL2 SampleProbes(Vector3 world)
    {
        var result = new SphericalHarmonicsL2();
        if (probes[0] == null) return result;

        int width = VolumeSize.x, depth = VolumeSize.y, layers = VolumeSize.z;
        float gx = Mathf.Clamp((world.x - WorldMin.x) / WorldSize.x * width - 0.5f, 0f, width - 1f);
        float gz = Mathf.Clamp((world.z - WorldMin.z) / WorldSize.z * depth - 0.5f, 0f, depth - 1f);
        float gy = Mathf.Clamp((world.y - WorldMin.y) / WorldSize.y * layers - 0.5f, 0f, layers - 1f);
        int x0 = (int)gx, z0 = (int)gz, y0 = (int)gy;
        int x1 = Mathf.Min(x0 + 1, width - 1), z1 = Mathf.Min(z0 + 1, depth - 1), y1 = Mathf.Min(y0 + 1, layers - 1);
        float fx = gx - x0, fz = gz - z0, fy = gy - y0;

        for (int channel = 0; channel < 3; channel++)
        {
            float[] data = probes[channel];
            float m0 = 0f, mx = 0f, my = 0f, mz = 0f;

            for (int corner = 0; corner < 8; corner++)
            {
                bool right = (corner & 1) != 0, far = (corner & 2) != 0, top = (corner & 4) != 0;
                float weight = (right ? fx : 1f - fx) * (far ? fz : 1f - fz) * (top ? fy : 1f - fy);
                int index = (((top ? y1 : y0) * depth + (far ? z1 : z0)) * width + (right ? x1 : x0)) * 4;
                m0 += data[index] * weight;
                mx += data[index + 1] * weight;
                my += data[index + 2] * weight;
                mz += data[index + 3] * weight;
            }

            // Ordem dos coeficientes da Unity: 0 constante, 1 Y, 2 Z, 3 X.
            result[channel, 0] = m0;
            result[channel, 1] = 2f * my;
            result[channel, 2] = 2f * mz;
            result[channel, 3] = 2f * mx;
        }

        return result;
    }

    // Le de irradiance, escreve em scratch e troca os dois: o resultado
    // mais novo fica sempre em irradiance.
    private void Dispatch(int kernel, int groupsX, int groupsY)
    {
        for (int i = 0; i < 3; i++)
        {
            bakeShader.SetTexture(kernel, SourceIds[i], irradiance[i]);
            bakeShader.SetTexture(kernel, DestinationIds[i], scratch[i]);
        }

        bakeShader.Dispatch(kernel, groupsX, groupsY, probeLayers);

        RenderTexture[] swap = irradiance;
        irradiance = scratch;
        scratch = swap;
    }

    private RenderTexture EnsureVolume(RenderTexture volume, string volumeName)
    {
        if (volume != null && volume.width == VolumeSize.x && volume.height == VolumeSize.y &&
            volume.volumeDepth == VolumeSize.z)
            return volume;

        ReleaseTexture(volume);
        volume = new RenderTexture(VolumeSize.x, VolumeSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
        {
            name = volumeName,
            dimension = TextureDimension.Tex3D,
            volumeDepth = VolumeSize.z,
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
        };
        volume.Create();
        return volume;
    }

    private void CollectLights()
    {
        var lights = new List<LightData>();

        // A forca do rebote e do tema, pra cada dungeon ter a sua.
        DungeonTheme theme = GetComponent<DungeonTheme>();
        float strength = theme != null ? theme.GIStrength : 1f;

        foreach (Light light in GetComponentsInChildren<Light>())
        {
            if (!light.enabled || light.intensity <= 0f || light.GetComponentInParent<Health>() != null ||
                (light.type != LightType.Point && light.type != LightType.Spot))
                continue;

            // A URP multiplica a cor linear pela intensidade, e pela
            // temperatura quando ela esta ligada na luz.
            Color color = light.color.linear * (light.intensity * strength);
            if (light.useColorTemperature)
                color *= Mathf.CorrelatedColorTemperatureToRGB(light.colorTemperature);

            var data = new LightData
            {
                position = light.transform.position,
                range = light.range,
                color = new Vector3(color.r, color.g, color.b),
                spotCosOuter = -2f,
                direction = light.transform.forward,
                spotInvRange = 0f,
            };

            if (light.type == LightType.Spot)
            {
                float cosOuter = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
                float cosInner = Mathf.Cos(light.innerSpotAngle * 0.5f * Mathf.Deg2Rad);
                data.spotCosOuter = cosOuter;
                data.spotInvRange = 1f / Mathf.Max(0.001f, cosInner - cosOuter);
            }

            lights.Add(data);
        }

        LightCount = lights.Count;

        // Buffer vazio nao existe: sem luz, deixo uma posicao que o
        // _LightCount nao deixa ler.
        int capacity = Mathf.Max(1, lights.Count);
        if (lightBuffer == null || lightBuffer.count != capacity)
        {
            lightBuffer?.Release();
            lightBuffer = new ComputeBuffer(capacity, 48);
        }

        if (lights.Count > 0)
            lightBuffer.SetData(lights);
    }

    private static void ReleaseTexture(RenderTexture texture)
    {
        if (texture == null) return;
        texture.Release();
        Destroy(texture);
    }

    private void OnEnable()
    {
        // No primeiro OnEnable a foto ainda nao existe e nada acontece.
        if (BakeComplete)
        {
            Shader.SetGlobalFloat(GlobalBlendId, giEnabled ? 1f : 0f);
            ApplyActors(true);
            if (actorRoutine == null) actorRoutine = StartCoroutine(FollowActors());
        }
        else if (Ready) Rebake();
    }

    private void OnDisable()
    {
        // A rotina morre com o componente desligado. O blend e global: se
        // ficasse em 1, o DungeonLit leria sondas de uma dungeon que saiu.
        bakeRoutine = null;
        actorRoutine = null;
        Shader.SetGlobalFloat(GlobalBlendId, 0f);
        ApplyActors(true);
    }

    private void ReleaseMap()
    {
        if (Map == null) return;
        Map.Release();
        Destroy(Map);
        Map = null;
    }

    private void OnDestroy()
    {
        ReleaseMap();
        if (captureMaterial != null) Destroy(captureMaterial);

        for (int i = 0; i < 3; i++)
        {
            ReleaseTexture(irradiance[i]);
            ReleaseTexture(scratch[i]);
        }

        lightBuffer?.Release();
        lightBuffer = null;
    }
}
