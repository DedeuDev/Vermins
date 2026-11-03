using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Arquivo do Ian. Espalha props nas salas e corredores da dungeon:
/// caixa, barril e entulho na parede e nos cantos, cano na parede e poca
/// no chao. As salas so tinham piso e parede, e a dungeon ficava
/// repetitiva; com os props sorteados, o mesmo modulo aparece diferente
/// a cada vez.
///
/// O que espalhar vem do DungeonPropSet (um asset por dungeon). A
/// DungeonComPlayer2 sobrescreve o asset, como sobrescreve o material no
/// DungeonTheme. Os props sao primitivas com material de blockout, sem
/// collider. O NavMesh sai so das caixas do chao, entao prop nenhum fecha
/// caminho, e o clique passa por ele ate o chao.
///
/// Prop de parede e de canto vai na faixa entre a borda do NavMesh e a
/// parede, que tem de 0,15 m a 0,8 m. O que nao cabe pode entrar ate
/// maxIntrusion no NavMesh; mais que isso, sorteio outro lugar. No canto
/// deixo entrar mais (maxCornerIntrusion): ninguem anda ali, e com o
/// limite da parede a pilha de caixas nao coube em nenhum. A parede
/// sai do mesmo truque do DungeonGuide: MeshCollider nas malhas so durante
/// a montagem, e um raio da borda do NavMesh ate ela.
///
/// Fica longe das portas abertas (nao tampa a passagem nem o pilar), das
/// luzes do DungeonGuide (nao cobre a luz que guia), do que se clica
/// (escada, guarda morto) e do ponto do boss. Sala especial, como a do
/// tesouro, fica sem props.
///
/// Roda uma vez, tres frames depois do NavMesh: o DungeonGuide poe as
/// tochas no segundo. Nao tem Update.
/// </summary>
[RequireComponent(typeof(RuntimeDungeonNavMesh))]
public class DungeonDressing : MonoBehaviour
{
    [Tooltip("O que espalhar nesta dungeon. Vazio, nada.")]
    [SerializeField] private DungeonPropSet propSet;

    [Tooltip("Quanto um prop de parede pode entrar no NavMesh, em metros. " +
             "O agente tem 0,5 m de raio, entao o corpo do jogador ja passa " +
             "da borda.")]
    [SerializeField, Min(0f)] private float maxIntrusion = 0.15f;

    [Tooltip("O mesmo, pra prop de canto. Medi 66 props com 0,15 e nenhuma " +
             "pilha de caixas coube.")]
    [SerializeField, Min(0f)] private float maxCornerIntrusion = 0.35f;

    [Tooltip("Folga entre o prop e a parede.")]
    [SerializeField, Min(0f)] private float wallGap = 0.03f;

    [Tooltip("Raio livre em volta de cada porta aberta. A abertura tem 4 m e " +
             "o pilar fica a 1,95 m do centro.")]
    [SerializeField, Min(0f)] private float doorClearance = 2.6f;

    [Tooltip("Raio livre em volta de cada luz do DungeonGuide e do que se clica.")]
    [SerializeField, Min(0f)] private float markerClearance = 1.5f;

    [Tooltip("Lugares sorteados por prop antes de desistir dele.")]
    [SerializeField, Min(1)] private int attemptsPerProp = 12;

    [Tooltip("Maximo de frames esperando o NavMesh, igual aos spawners.")]
    [SerializeField, Min(1)] private int maxFramesWaitingForNavMesh = 300;

    // Nomes das raizes que o DungeonSeams e o DungeonGuide criam.
    private const string SeamsRootName = "Generated Seams";
    private const string GuideRootName = "Generated Guide";

    private const float SampleRadius = 3f;

    // Altura do raio que procura a parede: na altura dos props.
    private const float ProbeHeight = 0.5f;

    // Ate onde procuro parede: da borda do NavMesh ate ela, e ao longo
    // dela ate o canto.
    private const float WallProbe = 2f;

    // Folga entre dois props, alem dos raios.
    private const float PropSpacing = 0.25f;

    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    private readonly HashSet<MeshCollider> wallProbes = new HashSet<MeshCollider>();

    // Centro e raio (no w) do que ja foi posto e do que deve ficar livre.
    private readonly List<Vector4> placed = new List<Vector4>();
    private readonly List<Vector4> keepOut = new List<Vector4>();

    private RuntimeDungeonNavMesh runtimeNavMesh;
    private Transform generatedRoot;

    /// <summary>Quantos props foram postos. O teste le daqui.</summary>
    public int PropCount { get; private set; }

    private void Awake()
    {
        runtimeNavMesh = GetComponent<RuntimeDungeonNavMesh>();
    }

    private void Start()
    {
        StartCoroutine(DressWhenReady());
    }

    private IEnumerator DressWhenReady()
    {
        int esperou = 0;

        while (!runtimeNavMesh.HasBuiltNavMesh && esperou < maxFramesWaitingForNavMesh)
        {
            esperou++;
            yield return null;
        }

        if (!runtimeNavMesh.HasBuiltNavMesh)
        {
            Debug.LogError("DungeonDressing: o NavMesh nao ficou pronto.", this);
            yield break;
        }

        // O DungeonGuide poe as tochas dois frames depois do NavMesh. Um a
        // mais garante que elas ja existem pra eu desviar.
        yield return null;
        yield return null;
        yield return null;

        Build();
    }

    [ContextMenu("Rebuild Props")]
    private void Build()
    {
        Clear();

        if (propSet == null || propSet.Entries == null)
            return;

        generatedRoot = new GameObject("Generated Props").transform;
        generatedRoot.SetParent(transform, false);

        CollectKeepOut();
        AddWallProbes();

        foreach (DungeonModule modulo in GetComponentsInChildren<DungeonModule>())
        {
            if (modulo.PlacementBounds == null || modulo.RoomCategory != DungeonRoomCategory.Normal)
                continue;

            bool sala = modulo.ModuleType == DungeonModuleType.Room;
            Bounds caixa = modulo.PlacementBounds.bounds;

            foreach (DungeonPropSet.Entry item in propSet.Entries)
            {
                if (item == null || item.Prefab == null)
                    continue;

                Vector2Int faixa = sala ? item.PerRoom : item.PerCorridor;
                int quantos = Random.Range(faixa.x, faixa.y + 1);

                for (int i = 0; i < quantos; i++)
                    TryPlace(item, caixa);
            }
        }

        RemoveWallProbes();
    }

    private void Clear()
    {
        PropCount = 0;
        placed.Clear();
        keepOut.Clear();

        if (generatedRoot != null)
        {
            // Solto antes do Destroy, que so vale no fim do frame.
            generatedRoot.SetParent(null);
            Destroy(generatedRoot.gameObject);
        }
    }

    /// <summary>
    /// Portas abertas, luzes do DungeonGuide (tochas, guarda e porta do
    /// boss), o que se clica e o ponto do boss.
    /// </summary>
    private void CollectKeepOut()
    {
        foreach (DungeonSocket socket in GetComponentsInChildren<DungeonSocket>())
        {
            if (socket.IsConnected)
                AddKeepOut(socket.transform.position, doorClearance);
        }

        Transform guia = transform.Find(GuideRootName);

        if (guia != null)
        {
            foreach (Light luz in guia.GetComponentsInChildren<Light>())
                AddKeepOut(luz.transform.position, markerClearance);
        }

        foreach (MonoBehaviour coisa in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (coisa is IInteractable)
                AddKeepOut(coisa.transform.position, markerClearance);
        }

        foreach (BossSpawnData boss in GetComponentsInChildren<BossSpawnData>())
            AddKeepOut(boss.transform.position, markerClearance * 2f);
    }

    private void AddKeepOut(Vector3 centro, float raio)
    {
        keepOut.Add(new Vector4(centro.x, centro.y, centro.z, raio));
    }

    private void TryPlace(DungeonPropSet.Entry item, Bounds caixa)
    {
        bool chao = item.Placement == DungeonPropSet.Placement.Floor;

        for (int tentativa = 0; tentativa < attemptsPerProp; tentativa++)
        {
            Vector3 sorteio = new Vector3(
                Random.Range(caixa.min.x, caixa.max.x),
                caixa.center.y,
                Random.Range(caixa.min.z, caixa.max.z));

            if (!NavMesh.SamplePosition(sorteio, out NavMeshHit ponto, SampleRadius, NavMesh.AllAreas))
                continue;

            // O SamplePosition pode pular pro modulo vizinho.
            if (ponto.position.x < caixa.min.x || ponto.position.x > caixa.max.x ||
                ponto.position.z < caixa.min.z || ponto.position.z > caixa.max.z)
                continue;

            // Poca varia bastante de tamanho; caixa e barril, pouco.
            float escala = chao ? Random.Range(0.7f, 1.3f) : Random.Range(0.9f, 1.1f);
            Vector2 tamanho = item.Size * escala;

            Vector3 lugar;
            Quaternion giro;

            bool achou = chao
                ? FloorSpot(ponto.position, tamanho, out lugar, out giro)
                : WallSpot(ponto.position, tamanho, item.Placement == DungeonPropSet.Placement.Corner, out lugar, out giro);

            float raio = Mathf.Max(tamanho.x, tamanho.y) * 0.5f;

            if (!achou || Crowded(lugar, raio))
                continue;

            Transform prop = Instantiate(item.Prefab, lugar, giro, generatedRoot).transform;
            prop.localScale = Vector3.one * escala;
            placed.Add(new Vector4(lugar.x, lugar.y, lugar.z, raio));
            PropCount++;
            return;
        }
    }

    /// <summary>
    /// Encostado na parede mais perto do ponto, ou no canto. A borda do
    /// NavMesh diz pra que lado fica a parede, e o raio diz onde ela esta.
    /// </summary>
    private bool WallSpot(Vector3 ponto, Vector2 tamanho, bool canto, out Vector3 lugar, out Quaternion giro)
    {
        lugar = default;
        giro = Quaternion.identity;

        if (!NavMesh.FindClosestEdge(ponto, out NavMeshHit borda, NavMesh.AllAreas))
            return false;

        Vector3 paraParede = Flat(-borda.normal);

        if (paraParede == Vector3.zero)
            return false;

        Vector3 altura = Vector3.up * ProbeHeight;

        if (!FindWall(borda.position + altura, paraParede, out RaycastHit parede))
            return false;

        Vector3 paraDentro = Flat(parede.normal);

        // Parede torta em relacao a borda e pilar ou quina de porta.
        if (Vector3.Dot(paraDentro, -paraParede) < 0.9f)
            return false;

        float largura = tamanho.x;
        float fundo = tamanho.y;
        Vector3 lado = Vector3.Cross(Vector3.up, paraDentro);
        Vector3 centro = parede.point + paraDentro * (fundo * 0.5f + wallGap);

        if (canto)
        {
            // A outra parede, de um lado ou do outro, na altura do prop.
            float perto = float.MaxValue;
            Vector3 sentido = Vector3.zero;

            for (int s = -1; s <= 1; s += 2)
            {
                if (FindWall(centro, lado * s, out RaycastHit outra) &&
                    Mathf.Abs(Vector3.Dot(Flat(outra.normal), lado)) > 0.9f &&
                    outra.distance < perto)
                {
                    perto = outra.distance;
                    sentido = lado * s;
                }
            }

            if (sentido == Vector3.zero)
                return false;

            centro += sentido * (perto - largura * 0.5f - wallGap);
        }
        else
        {
            // A parede tem que continuar dos dois lados do prop. Sem isso
            // ele sobra no vao da porta ou atravessa a quina.
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 ponta = parede.point + paraDentro * 0.2f + lado * (s * largura * 0.5f);

                if (!FindWall(ponta, -paraDentro, out RaycastHit atras) || Mathf.Abs(atras.distance - 0.2f) > 0.08f)
                    return false;
            }
        }

        // O fundo do prop pode ficar mais largo que a faixa fora do
        // NavMesh. Testo a frente: o meio e as duas pontas.
        Vector3 frente = centro + paraDentro * (fundo * 0.5f);
        frente.y = borda.position.y;
        Vector3 meiaLargura = lado * (largura * 0.5f);
        float limite = canto ? maxCornerIntrusion : maxIntrusion;

        if (Intrudes(frente, limite) || Intrudes(frente + meiaLargura, limite) || Intrudes(frente - meiaLargura, limite))
            return false;

        lugar = centro;
        lugar.y = FloorHeight(centro, borda.position.y);

        // Um giro pequeno pra caixa nao ficar no esquadro.
        giro = Quaternion.LookRotation(paraDentro) * Quaternion.Euler(0f, Random.Range(-6f, 6f), 0f);
        return true;
    }

    /// <summary>
    /// Solto no chao. Pode passar da borda do NavMesh, mas nao da parede,
    /// que fica pelo menos 0,15 m alem dela.
    /// </summary>
    private bool FloorSpot(Vector3 ponto, Vector2 tamanho, out Vector3 lugar, out Quaternion giro)
    {
        lugar = default;
        giro = Quaternion.identity;

        float raio = Mathf.Max(tamanho.x, tamanho.y) * 0.5f;

        if (!NavMesh.FindClosestEdge(ponto, out NavMeshHit borda, NavMesh.AllAreas) ||
            borda.distance < raio - 0.15f)
            return false;

        lugar = ponto;
        lugar.y = FloorHeight(ponto + Vector3.up * ProbeHeight, ponto.y);
        giro = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        return true;
    }

    /// <summary>
    /// Se o ponto esta dentro do NavMesh mais fundo que o limite. O
    /// SamplePosition de um ponto fora acha a borda, a alguns centimetros
    /// dele; de um ponto dentro, acha o proprio ponto.
    /// </summary>
    private bool Intrudes(Vector3 ponto, float limite)
    {
        if (!NavMesh.SamplePosition(ponto, out NavMeshHit perto, 0.3f, NavMesh.AllAreas))
            return false;

        if (Flat(perto.position - ponto, false).sqrMagnitude > 0.0004f)
            return false;

        return NavMesh.FindClosestEdge(perto.position, out NavMeshHit borda, NavMesh.AllAreas) &&
               borda.distance > limite;
    }

    private bool Crowded(Vector3 centro, float raio)
    {
        foreach (Vector4 outro in placed)
        {
            if (FlatDistance(centro, outro) < raio + outro.w + PropSpacing)
                return true;
        }

        foreach (Vector4 livre in keepOut)
        {
            if (FlatDistance(centro, livre) < raio + livre.w)
                return true;
        }

        return false;
    }

    private static float FlatDistance(Vector3 a, Vector4 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static Vector3 Flat(Vector3 v, bool normalizar = true)
    {
        v.y = 0f;

        if (!normalizar)
            return v;

        return v.sqrMagnitude < 0.01f ? Vector3.zero : v.normalized;
    }

    /// <summary>
    /// A altura do piso de verdade embaixo do ponto. O NavMesh fica um
    /// pouco acima dele, no topo das caixas do chao.
    /// </summary>
    private float FloorHeight(Vector3 ponto, float reserva)
    {
        int quantos = Physics.RaycastNonAlloc(ponto + Vector3.up, Vector3.down, Hits, 3f, ~0, QueryTriggerInteraction.Ignore);
        float melhor = float.MaxValue;
        float altura = reserva;

        for (int i = 0; i < quantos; i++)
        {
            if (Hits[i].distance < melhor && Hits[i].collider is MeshCollider mc && wallProbes.Contains(mc))
            {
                melhor = Hits[i].distance;
                altura = Hits[i].point.y;
            }
        }

        return altura;
    }

    /// <summary>
    /// O acerto mais perto que seja malha de sala ou pilar. Igual ao do
    /// DungeonGuide.
    /// </summary>
    private bool FindWall(Vector3 origem, Vector3 direcao, out RaycastHit parede)
    {
        int quantos = Physics.RaycastNonAlloc(origem, direcao, Hits, WallProbe, ~0, QueryTriggerInteraction.Ignore);
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
    /// MeshCollider nas salas e nos pilares so durante a montagem, como no
    /// DungeonGuide. Uma vez por dungeon, entao a alocacao aqui nao pesa.
    /// </summary>
    private void AddWallProbes()
    {
        foreach (MeshFilter malha in GetComponentsInChildren<MeshFilter>())
        {
            if (malha.sharedMesh == null || !malha.sharedMesh.isReadable)
                continue;

            bool sala = malha.GetComponentInParent<DungeonModule>() != null;
            bool pilar = malha.transform.parent != null && malha.transform.parent.parent != null &&
                         malha.transform.parent.parent.name == SeamsRootName;

            if (!sala && !pilar)
                continue;

            MeshCollider sonda = malha.gameObject.AddComponent<MeshCollider>();
            sonda.sharedMesh = malha.sharedMesh;
            wallProbes.Add(sonda);
        }

        Physics.SyncTransforms();
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
}
