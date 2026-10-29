using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// Arquivo do Ian. O cenario guia o jogador na dungeon: tochas no caminho
/// principal, luz fria em cima do guarda morto e luz roxa na entrada da
/// sala do boss. O professor apontou que o jogador se perdia na dungeon e
/// na quest, e eu preferi resolver com luz no cenario em vez de seta na
/// tela.
///
/// Mora no DungeonGenerator, ao lado do DeadGuardSpawner e do BossSpawner,
/// e roda do mesmo jeito que eles: espera o RuntimeDungeonNavMesh ficar
/// pronto. Nao mexe no gerador; so le o que ele deixou nos filhos (a sala
/// de profundidade 0, a BossSpawnData e o guarda morto).
///
/// O caminho e a rota do NavMesh da sala inicial ate a sala do boss, que
/// e por onde o jogador anda de verdade. A cada torchSpacing metros dessa
/// rota vai uma tocha na parede mais proxima. Sala lateral fica sem tocha,
/// entao "siga a luz" leva ao boss.
///
/// A parede nao tem collider: o NavMesh sai so das caixas do chao, e a
/// borda dele ficava de 0,15 m a 0,8 m da parede, com tocha solta no ar.
/// Pra achar a parede de verdade eu ponho MeshCollider nas salas so
/// durante a montagem, jogo um raio do caminho ate a parede e tiro os
/// colliders no fim. Por isso os FBX dos modulos estao com Read/Write
/// ligado: sem isso o MeshCollider nao monta na build.
///
/// A dungeon nao tinha nenhuma luz, so o ambiente do ceu, e com isso a
/// tocha nao aparecia. Por isso escureco o ambiente em ambientScale
/// enquanto este componente existir, e devolvo o original no OnDestroy.
///
/// Tudo acontece uma vez, na carga da dungeon. Nao tem Update.
/// </summary>
[RequireComponent(typeof(RuntimeDungeonNavMesh))]
public class DungeonGuide : MonoBehaviour
{
    [Header("Caminho")]
    [Tooltip("Tocha com luz laranja, pivo no chao. O TochaGuia.prefab.")]
    [SerializeField] private GameObject torchPrefab;

    [Tooltip("Metros de rota entre uma tocha e a proxima. Com 8, uma " +
             "sala de corredor ganha uma ou duas.")]
    [SerializeField, Min(2f)] private float torchSpacing = 8f;

    [Tooltip("Folga entre a tocha e a parede, pra a haste nao entrar nela.")]
    [SerializeField, Min(0f)] private float wallGap = 0.06f;

    [Tooltip("Ate onde procuro parede a partir do caminho. Mais longe que " +
             "isso e sala aberta, e a tocha anda um pouco na rota.")]
    [SerializeField, Min(1f)] private float wallProbe = 4f;

    [Header("Marcos")]
    [Tooltip("Luz em cima do guarda morto. Vazio, sem marco.")]
    [SerializeField] private GameObject guardMarkerPrefab;

    [Tooltip("Luz na entrada da sala do boss. Vazio, sem marco.")]
    [SerializeField] private GameObject bossDoorPrefab;

    [Header("Ambiente")]
    [Tooltip("Quanto do ambiente do ceu fica na dungeon. 1 deixa como " +
             "esta; mais baixo deixa a tocha aparecer.")]
    [SerializeField, Range(0f, 1f)] private float ambientScale = 0.2f;

    [Tooltip("Maximo de frames esperando o NavMesh, igual aos spawners.")]
    [SerializeField, Min(1)] private int maxFramesWaitingForNavMesh = 300;

    // Buffer fixo pros cantos da rota; 256 sobra pra 20 salas.
    private static readonly Vector3[] Corners = new Vector3[256];

    private const float SampleRadius = 5f;
    private const float DoorStep = 0.5f;

    // Nome da raiz que o DungeonSeams cria pros pilares.
    private const string SeamsRootName = "Generated Seams";

    // Altura do raio que procura a parede: acima de caixote e de inimigo.
    private const float ProbeHeight = 1.6f;

    // Sem parede perto do ponto, tento andar na rota nestes metros.
    private static readonly float[] RouteNudges = { 0f, 1.5f, -1.5f, 3f, -3f };

    private static readonly RaycastHit[] Hits = new RaycastHit[16];
    private static readonly Collider[] Overlaps = new Collider[16];

    // Altura e folga da chama da TochaGuia, pra nao encostar no pilar.
    private const float FlameHeight = 1.85f;
    private const float FlameClearance = 0.3f;
    private readonly List<MeshCollider> wallProbes = new List<MeshCollider>();

    private RuntimeDungeonNavMesh runtimeNavMesh;
    private Transform generatedRoot;
    private SphericalHarmonicsL2 originalAmbient;
    private bool ambientChanged;

    /// <summary>Quantas tochas foram postas. O teste le daqui.</summary>
    public int TorchCount { get; private set; }

    /// <summary>Metros da rota do inicio ate o boss.</summary>
    public float RouteLength { get; private set; }

    private void Awake()
    {
        runtimeNavMesh = GetComponent<RuntimeDungeonNavMesh>();
    }

    private void Start()
    {
        StartCoroutine(GuideWhenReady());
    }

    private void OnDestroy()
    {
        if (ambientChanged)
            RenderSettings.ambientProbe = originalAmbient;
    }

    private IEnumerator GuideWhenReady()
    {
        int esperou = 0;

        while (!runtimeNavMesh.HasBuiltNavMesh && esperou < maxFramesWaitingForNavMesh)
        {
            esperou++;
            yield return null;
        }

        if (!runtimeNavMesh.HasBuiltNavMesh)
        {
            Debug.LogError("DungeonGuide: o NavMesh nao ficou pronto.", this);
            yield break;
        }

        // Os spawners do guarda e do boss acordam no mesmo frame que eu.
        // Dois frames de folga garantem que os dois ja puseram o que
        // tinham que por.
        yield return null;
        yield return null;

        DarkenAmbient();
        Build();
    }

    private void DarkenAmbient()
    {
        if (ambientScale >= 1f)
            return;

        originalAmbient = RenderSettings.ambientProbe;
        RenderSettings.ambientProbe = originalAmbient * ambientScale;
        ambientChanged = true;
    }

    [ContextMenu("Rebuild Guide")]
    private void Build()
    {
        Clear();

        generatedRoot = new GameObject("Generated Guide").transform;
        generatedRoot.SetParent(transform, false);

        DeadGuardQuestInteractable guarda = GetComponentInChildren<DeadGuardQuestInteractable>();

        if (guarda != null && guardMarkerPrefab != null)
            Instantiate(guardMarkerPrefab, guarda.transform.position, Quaternion.identity, generatedRoot);

        if (!FindRoute(out int cantos, out Bounds salaDoBoss))
            return;

        AddWallProbes();
        PlaceTorches(cantos);
        RemoveWallProbes();

        if (bossDoorPrefab != null && FindDoor(cantos, salaDoBoss, out Vector3 porta))
            Instantiate(bossDoorPrefab, porta, Quaternion.identity, generatedRoot);
    }

    private void Clear()
    {
        TorchCount = 0;
        RouteLength = 0f;

        if (generatedRoot != null)
        {
            // Solto do gerador antes: o Destroy so vale no fim do frame, e
            // no teste o Find achou a raiz velha no lugar da nova.
            generatedRoot.SetParent(null);
            Destroy(generatedRoot.gameObject);
        }
    }

    /// <summary>
    /// A rota do NavMesh da sala de profundidade 0 ate a sala com a
    /// BossSpawnData. Devolve os cantos no buffer estatico.
    /// </summary>
    private bool FindRoute(out int cantos, out Bounds salaDoBoss)
    {
        cantos = 0;
        salaDoBoss = default;

        DungeonModule inicio = null;

        foreach (DungeonModule sala in GetComponentsInChildren<DungeonModule>())
        {
            if (sala.GenerationDepth == 0)
            {
                inicio = sala;
                break;
            }
        }

        BossSpawnData boss = GetComponentInChildren<BossSpawnData>();

        if (inicio == null || boss == null)
        {
            Debug.LogWarning("DungeonGuide: nao achei a sala inicial ou a do boss.", this);
            return false;
        }

        DungeonModule salaFinal = boss.GetComponentInParent<DungeonModule>();
        salaDoBoss = salaFinal != null && salaFinal.PlacementBounds != null
            ? salaFinal.PlacementBounds.bounds
            : new Bounds(boss.transform.position, Vector3.one * 6f);

        if (!NavMesh.SamplePosition(CenterOf(inicio), out NavMeshHit de, SampleRadius, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(boss.transform.position, out NavMeshHit ate, SampleRadius, NavMesh.AllAreas))
        {
            Debug.LogWarning("DungeonGuide: inicio ou boss fora do NavMesh.", this);
            return false;
        }

        NavMeshPath rota = new NavMeshPath();

        if (!NavMesh.CalculatePath(de.position, ate.position, NavMesh.AllAreas, rota) ||
            rota.status != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning("DungeonGuide: sem rota completa ate o boss.", this);
            return false;
        }

        cantos = rota.GetCornersNonAlloc(Corners);
        return cantos >= 2;
    }

    private static Vector3 CenterOf(DungeonModule sala)
    {
        return sala.PlacementBounds != null ? sala.PlacementBounds.bounds.center : sala.transform.position;
    }

    /// <summary>
    /// Anda pela rota e poe uma tocha a cada torchSpacing metros. A
    /// primeira sai na metade do espaco, pra nao nascer no pe do jogador.
    /// </summary>
    private void PlaceTorches(int cantos)
    {
        if (torchPrefab == null)
            return;

        float proxima = torchSpacing * 0.5f;
        float andado = 0f;

        for (int i = 1; i < cantos; i++)
        {
            Vector3 a = Corners[i - 1];
            Vector3 b = Corners[i];
            float trecho = Vector3.Distance(a, b);
            Vector3 direcao = trecho > 0.001f ? (b - a) / trecho : Vector3.forward;

            while (andado + trecho >= proxima)
            {
                Vector3 ponto = Vector3.Lerp(a, b, (proxima - andado) / trecho);
                PlaceTorchNear(ponto, direcao);
                proxima += torchSpacing;
            }

            andado += trecho;
        }

        RouteLength = andado;
    }

    private void PlaceTorchNear(Vector3 ponto, Vector3 direcao)
    {
        foreach (float passo in RouteNudges)
        {
            if (TryPlaceTorch(ponto + direcao * passo))
                return;
        }
    }

    /// <summary>
    /// A borda do NavMesh mais perto diz pra que lado fica a parede; o raio
    /// diz onde ela esta. Sem parede no alcance (porta, sala aberta), nao
    /// poe.
    /// </summary>
    private bool TryPlaceTorch(Vector3 ponto)
    {
        if (!NavMesh.FindClosestEdge(ponto, out NavMeshHit borda, NavMesh.AllAreas))
            return false;

        // A normal da borda aponta pra dentro do NavMesh; a parede fica do
        // lado oposto.
        Vector3 paraParede = -borda.normal;
        paraParede.y = 0f;

        if (paraParede.sqrMagnitude < 0.01f)
            return false;

        paraParede.Normalize();

        Vector3 origem = ponto + Vector3.up * ProbeHeight;

        if (!FindWall(origem, paraParede, out RaycastHit parede))
            return false;

        Vector3 paraDentro = parede.normal;
        paraDentro.y = 0f;

        if (paraDentro.sqrMagnitude < 0.01f)
            return false;

        paraDentro.Normalize();

        Vector3 lugar = parede.point + paraDentro * wallGap;
        lugar.y = ponto.y;

        // Tocha do lado de um pilar entrava nele pela metade. Se a chama
        // encosta em outra coisa que nao a propria parede, ando na rota.
        if (Blocked(lugar + Vector3.up * FlameHeight, parede.collider))
            return false;
        Instantiate(torchPrefab, lugar, Quaternion.LookRotation(paraDentro), generatedRoot);
        TorchCount++;
        return true;
    }

    /// <summary>
    /// O acerto mais perto que seja parede de sala. Inimigo, caixote e o
    /// proprio Player tambem tem collider, e a tocha nao pode ir neles.
    /// </summary>
    private bool FindWall(Vector3 origem, Vector3 direcao, out RaycastHit parede)
    {
        int quantos = Physics.RaycastNonAlloc(origem, direcao, Hits, wallProbe, ~0, QueryTriggerInteraction.Ignore);
        float melhor = float.MaxValue;
        parede = default;

        for (int i = 0; i < quantos; i++)
        {
            if (Hits[i].distance < melhor && Hits[i].collider is MeshCollider mc && wallProbes.Contains(mc))
            {
                melhor = Hits[i].distance;
                parede = Hits[i];
            }
        }

        return melhor < float.MaxValue;
    }

    /// <summary>
    /// MeshCollider em cada malha da dungeon, so pra montagem: as salas e
    /// os pilares do DungeonSeams, que ficam na frente da parede perto das
    /// portas. Tocha que cai ali vai pro pilar em vez de entrar nele. Uma
    /// vez por dungeon, entao a alocacao aqui nao pesa.
    /// </summary>
    private void AddWallProbes()
    {
        foreach (MeshFilter malha in GetComponentsInChildren<MeshFilter>())
        {
            if (malha.sharedMesh == null || !malha.sharedMesh.isReadable)
                continue;

            // So sala e pilar. Inimigo e guarda morto tambem sao filhos do
            // gerador, e tocha neles ficaria no ar quando eles saissem.
            bool sala = malha.GetComponentInParent<DungeonModule>() != null;
            bool pilar = malha.transform.parent != null && malha.transform.parent.parent != null &&
                         malha.transform.parent.parent.name == SeamsRootName;

            if (!sala && !pilar)
                continue;

            MeshCollider sonda = malha.gameObject.AddComponent<MeshCollider>();
            sonda.sharedMesh = malha.sharedMesh;
            wallProbes.Add(sonda);
        }

        // Garanto que os colliders novos ja valem pro raio neste frame.
        Physics.SyncTransforms();
    }

    private bool Blocked(Vector3 chama, Collider parede)
    {
        int quantos = Physics.OverlapSphereNonAlloc(chama, FlameClearance, Overlaps, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < quantos; i++)
        {
            if (Overlaps[i] != parede && Overlaps[i] is MeshCollider mc && wallProbes.Contains(mc))
                return true;
        }

        return false;
    }

    private void RemoveWallProbes()
    {
        foreach (MeshCollider sonda in wallProbes)
        {
            if (sonda != null)
                Destroy(sonda);
        }

        wallProbes.Clear();
    }

    /// <summary>
    /// O primeiro ponto da rota que entra na sala do boss: e a porta.
    /// </summary>
    private static bool FindDoor(int cantos, Bounds salaDoBoss, out Vector3 porta)
    {
        // A caixa da sala pode ser baixa; estico a altura pra nao perder a
        // rota por causa do y.
        salaDoBoss.Expand(new Vector3(0f, 10f, 0f));

        for (int i = 1; i < cantos; i++)
        {
            Vector3 a = Corners[i - 1];
            Vector3 b = Corners[i];
            int passos = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / DoorStep));

            for (int k = 0; k <= passos; k++)
            {
                Vector3 p = Vector3.Lerp(a, b, k / (float)passos);

                if (salaDoBoss.Contains(p))
                {
                    porta = p;
                    return true;
                }
            }
        }

        porta = default;
        return false;
    }
}
