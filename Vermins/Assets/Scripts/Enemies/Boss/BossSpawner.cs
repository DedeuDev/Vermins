using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private RuntimeDungeonNavMesh runtimeNavMesh;

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
    private string bossRootName =
        "Generated Bosses";

    [Header("Debug")]
    [SerializeField]
    private bool logResult = true;

    private Transform generatedBossesRoot;

    private readonly List<GameObject> spawnedBosses =
        new List<GameObject>();

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
                SpawnBossesWhenReady()
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
    // ESPERA NAVMESH
    // ==================================================

    private IEnumerator SpawnBossesWhenReady()
    {
        if (!FindRuntimeNavMeshIfNeeded())
        {
            Debug.LogError(
                "BossSpawner: RuntimeDungeonNavMesh " +
                "não foi encontrado.",
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
                "BossSpawner: o Runtime NavMesh " +
                "não ficou pronto dentro do tempo esperado.",
                this
            );

            yield break;
        }

        SpawnBosses();
    }

    // ==================================================
    // SPAWN
    // ==================================================

    [ContextMenu("Spawn Bosses")]
    public void SpawnBosses()
    {
        ClearBosses();

        BossSpawnData[] bossRooms =
            GetComponentsInChildren<BossSpawnData>(
                false
            );

        if (bossRooms.Length == 0)
        {
            Debug.LogWarning(
                "BossSpawner: nenhuma sala com " +
                "BossSpawnData foi encontrada.",
                this
            );

            return;
        }

        CreateBossRoot();

        int bossesSpawned = 0;

        foreach (BossSpawnData bossData in bossRooms)
        {
            if (bossData == null)
                continue;

            if (!bossData.gameObject.activeInHierarchy)
                continue;

            if (!bossData.AllowBossSpawn)
                continue;

            // ====================================
            // PREFAB
            // ====================================

            if (bossData.BossPrefab == null)
            {
                Debug.LogWarning(
                    "BossSpawner: uma Final Room possui " +
                    "BossSpawnData, mas nenhum Boss Prefab.",
                    bossData
                );

                continue;
            }

            // ====================================
            // SPAWN POINT
            // ====================================

            BossSpawnPoint spawnPoint =
                bossData.SpawnPoint;

            if (spawnPoint == null)
            {
                Debug.LogWarning(
                    "BossSpawner: BossSpawnPoint " +
                    "não encontrado na Final Room.",
                    bossData
                );

                continue;
            }

            // ====================================
            // INSTANCIA BOSS
            // ====================================

            GameObject boss =
                Instantiate(
                    bossData.BossPrefab,
                    spawnPoint.Position,
                    spawnPoint.Rotation,
                    generatedBossesRoot
                );

            spawnedBosses.Add(
                boss
            );

            bossesSpawned++;
        }

        // ========================================
        // LOG
        // ========================================

        if (logResult)
        {
            Debug.Log(
                "BossSpawner concluído | " +
                $"Bosses gerados: {bossesSpawned}.",
                this
            );
        }
    }

    // ==================================================
    // ROOT
    // ==================================================

    private void CreateBossRoot()
    {
        if (generatedBossesRoot != null)
            return;

        Transform existing =
            transform.Find(
                bossRootName
            );

        if (existing != null)
        {
            generatedBossesRoot =
                existing;

            return;
        }

        GameObject root =
            new GameObject(
                bossRootName
            );

        root.transform.SetParent(
            transform
        );

        root.transform.localPosition =
            Vector3.zero;

        root.transform.localRotation =
            Quaternion.identity;

        generatedBossesRoot =
            root.transform;
    }

    // ==================================================
    // CLEAR
    // ==================================================

    [ContextMenu("Clear Bosses")]
    public void ClearBosses()
    {
        foreach (GameObject boss in spawnedBosses)
        {
            if (boss == null)
                continue;

            if (Application.isPlaying)
            {
                boss.SetActive(false);
                Destroy(boss);
            }
            else
            {
                DestroyImmediate(boss);
            }
        }

        spawnedBosses.Clear();

        if (generatedBossesRoot == null)
            return;

        for (
            int i = generatedBossesRoot.childCount - 1;
            i >= 0;
            i--
        )
        {
            Transform child =
                generatedBossesRoot.GetChild(i);

            if (child == null)
                continue;

            if (Application.isPlaying)
            {
                child.gameObject.SetActive(false);

                Destroy(
                    child.gameObject
                );
            }
            else
            {
                DestroyImmediate(
                    child.gameObject
                );
            }
        }
    }
}