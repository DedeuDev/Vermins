using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadGuardSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private RuntimeDungeonNavMesh runtimeNavMesh;

    [SerializeField]
    private GameObject deadGuardPrefab;

    [Header("Generation")]
    [SerializeField]
    private bool spawnAutomatically = true;

    [Tooltip(
        "Máximo de frames que o sistema espera " +
        "o Runtime NavMesh ficar pronto."
    )]
    [Min(1)]
    [SerializeField]
    private int maxFramesWaitingForNavMesh = 300;

    [Header("Hierarchy")]
    [SerializeField]
    private string generatedRootName =
        "Generated Quest Objects";

    [Header("Debug")]
    [SerializeField]
    private bool logResult = true;

    private Transform generatedRoot;

    private GameObject spawnedDeadGuard;

    private Coroutine spawnCoroutine;

    // ==================================================
    // AWAKE
    // ==================================================

    private void Awake()
    {
        FindRuntimeNavMeshIfNeeded();
    }

    // ==================================================
    // START
    // ==================================================

    private void Start()
    {
        if (!spawnAutomatically)
            return;

        spawnCoroutine =
            StartCoroutine(
                SpawnWhenReady()
            );
    }

    // ==================================================
    // PROCURA RUNTIME NAVMESH
    // ==================================================

    private bool FindRuntimeNavMeshIfNeeded()
    {
        if (runtimeNavMesh != null)
            return true;

        runtimeNavMesh =
            GetComponent<RuntimeDungeonNavMesh>();

        return runtimeNavMesh != null;
    }

    // ==================================================
    // ESPERA A DUNGEON FICAR PRONTA
    // ==================================================

    private IEnumerator SpawnWhenReady()
    {
        if (!FindRuntimeNavMeshIfNeeded())
        {
            Debug.LogError(
                "DeadGuardSpawner: " +
                "RuntimeDungeonNavMesh não encontrado.",
                this
            );

            spawnCoroutine = null;

            yield break;
        }

        int waitedFrames = 0;

        while (
            !runtimeNavMesh.HasBuiltNavMesh &&
            waitedFrames < maxFramesWaitingForNavMesh
        )
        {
            waitedFrames++;

            yield return null;
        }

        spawnCoroutine = null;

        if (!runtimeNavMesh.HasBuiltNavMesh)
        {
            Debug.LogError(
                "DeadGuardSpawner: o Runtime NavMesh " +
                "não ficou pronto dentro do tempo esperado.",
                this
            );

            yield break;
        }

        SpawnDeadGuard();
    }

    // ==================================================
    // SPAWN
    // ==================================================

    [ContextMenu("Spawn Dead Guard")]
    public void SpawnDeadGuard()
    {
        ClearDeadGuard();

        // ========================================
        // QUEST MANAGER
        // ========================================

        if (QuestManager.Instance == null)
        {
            Debug.LogWarning(
                "DeadGuardSpawner: QuestManager não encontrado.",
                this
            );

            return;
        }

        // ========================================
        // SÓ PRECISA EXISTIR ENQUANTO O OBJETIVO
        // AINDA NÃO FOI CONCLUÍDO
        // ========================================

        QuestState state =
            QuestManager.Instance.SewerQuestState;

        bool questAllowsSpawn =
            state == QuestState.Active ||
            state == QuestState.ReadyToTurnIn;

        if (!questAllowsSpawn)
        {
            if (logResult)
            {
                Debug.Log(
                    "DeadGuardSpawner: a quest não está ativa. " +
                    "Nenhum guarda morto foi gerado.",
                    this
                );
            }

            return;
        }

        if (QuestManager.Instance.FoundDeadGuard)
        {
            if (logResult)
            {
                Debug.Log(
                    "DeadGuardSpawner: o guarda já foi encontrado. " +
                    "Nenhum novo guarda será gerado.",
                    this
                );
            }

            return;
        }

        // ========================================
        // PREFAB
        // ========================================

        if (deadGuardPrefab == null)
        {
            Debug.LogError(
                "DeadGuardSpawner: Dead Guard Prefab " +
                "não foi definido.",
                this
            );

            return;
        }

        // ========================================
        // PROCURA TODOS OS SPAWN POINTS
        // DA DUNGEON FINAL
        // ========================================

        DeadGuardSpawnPoint[] allPoints =
            GetComponentsInChildren<DeadGuardSpawnPoint>(
                false
            );

        List<DeadGuardSpawnPoint> validPoints =
            new List<DeadGuardSpawnPoint>();

        foreach (
            DeadGuardSpawnPoint point
            in allPoints
        )
        {
            if (point == null)
                continue;

            if (!point.gameObject.activeInHierarchy)
                continue;

            if (!point.CanSpawn)
                continue;

            validPoints.Add(
                point
            );
        }

        // ========================================
        // NENHUM PONTO
        // ========================================

        if (validPoints.Count == 0)
        {
            // Ian: so o Room_SmallI1 tem DeadGuardSpawnPoint. Quando a
            // seed nao sorteava essa sala, o guarda nao nascia e a quest
            // 1 nao fechava naquela dungeon: medi 2 de 4 dungeons assim.
            // Agora, sem ponto, ele nasce numa sala comum.
            if (TrySpawnInAnyRoom())
                return;

            Debug.LogError(
                "DeadGuardSpawner: nenhum " +
                "DeadGuardSpawnPoint válido foi encontrado " +
                "na dungeon gerada.",
                this
            );

            return;
        }

        // ========================================
        // ESCOLHE EXATAMENTE UM
        // ========================================

        int randomIndex =
            Random.Range(
                0,
                validPoints.Count
            );

        DeadGuardSpawnPoint selectedPoint =
            validPoints[randomIndex];

        // ========================================
        // ROOT
        // ========================================

        CreateGeneratedRoot();

        // ========================================
        // INSTANCIA
        // ========================================

        spawnedDeadGuard =
            Instantiate(
                deadGuardPrefab,
                // Ian: o ponto fica colado na parede; ver PullAwayFromWall.
                PullAwayFromWall(selectedPoint.Position),
                selectedPoint.Rotation,
                generatedRoot
            );

        // ========================================
        // LOG
        // ========================================

        if (logResult)
        {
            Debug.Log(
                "DeadGuardSpawner concluído | " +
                $"Spawn Points disponíveis: {validPoints.Count} | " +
                $"Ponto escolhido: {selectedPoint.name}.",
                this
            );
        }
    }

    // ==================================================
    // Ian: SEM PONTO, NASCE NUMA SALA COMUM
    // ==================================================

    // Ian: quantos pontos eu sorteio por sala antes de tentar a proxima.
    private const int FallbackTriesPerRoom = 12;

    // Ian: o guarda tem 0,5 m de raio. Com pelo menos 1 m ate a borda do
    // NavMesh ele nao entra na parede nem num prop, e com no maximo 2,5 m
    // fica encostado num canto em vez de no meio do caminho.
    private const float FallbackMinEdge = 1f;
    private const float FallbackMaxEdge = 2.5f;

    // Ian: os pontos do Room_SmallI1 ficam em cima da borda do NavMesh
    // (medi de 0,01 a 0,06 m), e o guarda e uma capsula deitada de 2 m:
    // em 6 dungeons, 52 de 120 guardas entravam 0,3 m ou mais na parede.
    // Procuro em volta do ponto o lugar mais perto com a mesma folga de
    // 1 m do caminho da sala comum. A altura do ponto eu nao mexo.
    private static Vector3 PullAwayFromWall(Vector3 position)
    {
        if (!UnityEngine.AI.NavMesh.SamplePosition(
                position,
                out UnityEngine.AI.NavMeshHit start,
                1f,
                UnityEngine.AI.NavMesh.AllAreas) ||
            HasWallMargin(start.position))
        {
            return position;
        }

        for (float radius = 0.5f; radius <= 2f; radius += 0.5f)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;

                Vector3 candidate =
                    start.position +
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                if (UnityEngine.AI.NavMesh.SamplePosition(
                        candidate,
                        out UnityEngine.AI.NavMeshHit hit,
                        0.3f,
                        UnityEngine.AI.NavMesh.AllAreas) &&
                    HasWallMargin(hit.position))
                {
                    return new Vector3(hit.position.x, position.y, hit.position.z);
                }
            }
        }

        return position;
    }

    private static bool HasWallMargin(Vector3 navMeshPoint)
    {
        return UnityEngine.AI.NavMesh.FindClosestEdge(
                   navMeshPoint,
                   out UnityEngine.AI.NavMeshHit edge,
                   UnityEngine.AI.NavMesh.AllAreas) &&
               edge.distance >= FallbackMinEdge;
    }

    private bool TrySpawnInAnyRoom()
    {
        DungeonModule[] modules =
            GetComponentsInChildren<DungeonModule>(false);

        List<DungeonModule> rooms =
            new List<DungeonModule>();

        foreach (DungeonModule module in modules)
        {
            // Ian: fica de fora o corredor, a sala do tesouro, a sala
            // onde o Player nasce (profundidade 0) e a do boss.
            if (module.ModuleType != DungeonModuleType.Room ||
                module.RoomCategory != DungeonRoomCategory.Normal ||
                module.GenerationDepth == 0 ||
                module.PlacementBounds == null ||
                module.GetComponentInChildren<BossSpawnData>() != null)
            {
                continue;
            }

            rooms.Add(module);
        }

        // Ian: comeco numa sala sorteada e dou a volta na lista, para o
        // guarda nao cair sempre na mesma.
        int first =
            rooms.Count > 0 ? Random.Range(0, rooms.Count) : 0;

        for (int i = 0; i < rooms.Count; i++)
        {
            DungeonModule room =
                rooms[(first + i) % rooms.Count];

            if (!TryFindSpotInRoom(room, out Vector3 spot))
                continue;

            CreateGeneratedRoot();

            spawnedDeadGuard =
                Instantiate(
                    deadGuardPrefab,
                    spot,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    generatedRoot
                );

            if (logResult)
            {
                Debug.Log(
                    "DeadGuardSpawner concluído | " +
                    "sem Spawn Point na dungeon; " +
                    $"guarda posto na sala {room.name}.",
                    this
                );
            }

            return true;
        }

        return false;
    }

    private bool TryFindSpotInRoom(
        DungeonModule room,
        out Vector3 spot
    )
    {
        Bounds bounds = room.PlacementBounds.bounds;
        spot = Vector3.zero;

        for (int i = 0; i < FallbackTriesPerRoom; i++)
        {
            Vector3 point =
                new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    bounds.min.y + 0.2f,
                    Random.Range(bounds.min.z, bounds.max.z)
                );

            if (!UnityEngine.AI.NavMesh.SamplePosition(
                    point,
                    out UnityEngine.AI.NavMeshHit hit,
                    1f,
                    UnityEngine.AI.NavMesh.AllAreas))
            {
                continue;
            }

            if (!UnityEngine.AI.NavMesh.FindClosestEdge(
                    hit.position,
                    out UnityEngine.AI.NavMeshHit edge,
                    UnityEngine.AI.NavMesh.AllAreas))
            {
                continue;
            }

            if (edge.distance < FallbackMinEdge ||
                edge.distance > FallbackMaxEdge)
            {
                continue;
            }

            spot = hit.position;

            // Ian: o NavMesh fica um pouco acima do piso (medi 0,16 m
            // contra 0,13 m do chao). Desco ate o chao de verdade para
            // o guarda nao flutuar.
            if (Physics.Raycast(
                    hit.position + Vector3.up * 0.5f,
                    Vector3.down,
                    out RaycastHit floor,
                    2f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                spot = floor.point;
            }

            return true;
        }

        return false;
    }

    // ==================================================
    // CRIA ROOT
    // ==================================================

    private void CreateGeneratedRoot()
    {
        if (generatedRoot != null)
            return;

        Transform existing =
            transform.Find(
                generatedRootName
            );

        if (existing != null)
        {
            generatedRoot =
                existing;

            return;
        }

        GameObject rootObject =
            new GameObject(
                generatedRootName
            );

        rootObject.transform.SetParent(
            transform
        );

        rootObject.transform.localPosition =
            Vector3.zero;

        rootObject.transform.localRotation =
            Quaternion.identity;

        generatedRoot =
            rootObject.transform;
    }

    // ==================================================
    // CLEAR
    // ==================================================

    [ContextMenu("Clear Dead Guard")]
    public void ClearDeadGuard()
    {
        if (spawnedDeadGuard == null)
            return;

        if (Application.isPlaying)
        {
            spawnedDeadGuard.SetActive(
                false
            );

            Destroy(
                spawnedDeadGuard
            );
        }
        else
        {
            DestroyImmediate(
                spawnedDeadGuard
            );
        }

        spawnedDeadGuard = null;
    }
}