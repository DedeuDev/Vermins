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
                selectedPoint.Position,
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