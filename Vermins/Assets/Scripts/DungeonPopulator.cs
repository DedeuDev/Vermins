using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonPopulator : MonoBehaviour
{
    [Header("Enemies")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Generation")]
    [SerializeField] private bool populateAutomatically = true;

    [Tooltip(
        "Quantos frames esperar antes de popular. " +
        "Isso garante que a geração da dungeon " +
        "e a limpeza dos módulos rejeitados terminem."
    )]
    [Min(1)]
    [SerializeField] private int framesToWait = 2;

    [Header("Hierarchy")]
    [SerializeField] private string enemyRootName =
        "Generated Enemies";

    [Header("Debug")]
    [SerializeField] private bool logResult = true;

    private Transform generatedEnemiesRoot;

    private readonly List<GameObject> spawnedEnemies =
        new List<GameObject>();

    // ==================================================
    // START
    // ==================================================

    private void Start()
    {
        if (!populateAutomatically)
            return;

        StartCoroutine(
            PopulateAfterGeneration()
        );
    }

    // ==================================================
    // ESPERA A DUNGEON TERMINAR
    // ==================================================

    private IEnumerator PopulateAfterGeneration()
    {
        for (int i = 0; i < framesToWait; i++)
        {
            yield return null;
        }

        PopulateDungeon();
    }

    // ==================================================
    // POPULA A DUNGEON
    // ==================================================

    [ContextMenu("Populate Dungeon")]
    public void PopulateDungeon()
    {
        // ========================================
        // PREFAB
        // ========================================

        if (enemyPrefab == null)
        {
            Debug.LogError(
                "DungeonPopulator: nenhum Enemy Prefab foi definido.",
                this
            );

            return;
        }

        // ========================================
        // REMOVE POPULAÇÃO ANTERIOR
        // ========================================

        ClearPopulation();

        CreateEnemyRoot();

        // ========================================
        // PROCURA SALAS COM SPAWN DATA
        // ========================================

        DungeonRoomSpawnData[] rooms =
            GetComponentsInChildren<DungeonRoomSpawnData>(
                false
            );

        if (rooms.Length == 0)
        {
            Debug.LogWarning(
                "DungeonPopulator: nenhuma sala com " +
                "DungeonRoomSpawnData foi encontrada.",
                this
            );

            return;
        }

        int populatedRooms = 0;
        int totalEnemies = 0;

        // ========================================
        // PROCESSA CADA SALA
        // ========================================

        foreach (DungeonRoomSpawnData room in rooms)
        {
            if (room == null)
                continue;

            if (!room.gameObject.activeInHierarchy)
                continue;

            if (!room.AllowEnemySpawns)
                continue;

            List<EnemySpawnPoint> validPoints =
                room.GetValidSpawnPoints();

            if (validPoints.Count == 0)
                continue;

            // ====================================
            // QUANTIDADE DE INIMIGOS
            // ====================================

            int minEnemies =
                Mathf.Max(
                    0,
                    room.MinEnemies
                );

            int maxEnemies =
                Mathf.Max(
                    minEnemies,
                    room.MaxEnemies
                );

            /*
             * Nunca podemos gerar mais inimigos
             * do que existem SpawnPoints.
             */
            maxEnemies =
                Mathf.Min(
                    maxEnemies,
                    validPoints.Count
                );

            minEnemies =
                Mathf.Min(
                    minEnemies,
                    maxEnemies
                );

            if (maxEnemies <= 0)
                continue;

            int enemyCount =
                Random.Range(
                    minEnemies,
                    maxEnemies + 1
                );

            if (enemyCount <= 0)
                continue;

            // ====================================
            // EMBARALHA OS SPAWN POINTS
            // ====================================

            ShuffleSpawnPoints(
                validPoints
            );

            // ====================================
            // SPAWN
            // ====================================

            for (int i = 0; i < enemyCount; i++)
            {
                EnemySpawnPoint spawnPoint =
                    validPoints[i];

                if (spawnPoint == null)
                    continue;

                GameObject enemy =
                    Instantiate(
                        enemyPrefab,
                        spawnPoint.transform.position,
                        spawnPoint.transform.rotation,
                        generatedEnemiesRoot
                    );

                spawnedEnemies.Add(
                    enemy
                );

                totalEnemies++;
            }

            populatedRooms++;
        }

        // ========================================
        // LOG
        // ========================================

        if (logResult)
        {
            Debug.Log(
                "Dungeon populada | " +
                $"Salas processadas: {populatedRooms} | " +
                $"Inimigos gerados: {totalEnemies}.",
                this
            );
        }
    }

    // ==================================================
    // EMBARALHA SPAWN POINTS
    // ==================================================

    private void ShuffleSpawnPoints(
        List<EnemySpawnPoint> points
    )
    {
        for (int i = points.Count - 1; i > 0; i--)
        {
            int randomIndex =
                Random.Range(
                    0,
                    i + 1
                );

            EnemySpawnPoint temp =
                points[i];

            points[i] =
                points[randomIndex];

            points[randomIndex] =
                temp;
        }
    }

    // ==================================================
    // ROOT DOS INIMIGOS
    // ==================================================

    private void CreateEnemyRoot()
    {
        if (generatedEnemiesRoot != null)
            return;

        Transform existing =
            transform.Find(
                enemyRootName
            );

        if (existing != null)
        {
            generatedEnemiesRoot =
                existing;

            return;
        }

        GameObject rootObject =
            new GameObject(
                enemyRootName
            );

        rootObject.transform.SetParent(
            transform
        );

        rootObject.transform.localPosition =
            Vector3.zero;

        rootObject.transform.localRotation =
            Quaternion.identity;

        generatedEnemiesRoot =
            rootObject.transform;
    }

    // ==================================================
    // CLEAR
    // ==================================================

    [ContextMenu("Clear Population")]
    public void ClearPopulation()
    {
        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy == null)
                continue;

            if (Application.isPlaying)
            {
                enemy.SetActive(false);
                Destroy(enemy);
            }
            else
            {
                DestroyImmediate(enemy);
            }
        }

        spawnedEnemies.Clear();

        /*
         * Caso a lista tenha sido perdida,
         * também limpamos os filhos existentes
         * dentro de Generated Enemies.
         */
        if (generatedEnemiesRoot != null)
        {
            for (
                int i =
                    generatedEnemiesRoot.childCount - 1;
                i >= 0;
                i--
            )
            {
                Transform child =
                    generatedEnemiesRoot.GetChild(i);

                if (child == null)
                    continue;

                if (Application.isPlaying)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
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
}