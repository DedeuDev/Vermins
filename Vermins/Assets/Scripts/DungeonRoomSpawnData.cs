using System.Collections.Generic;
using UnityEngine;

public class DungeonRoomSpawnData : MonoBehaviour
{
    [Header("Enemy Spawning")]
    [SerializeField] private bool allowEnemySpawns = true;

    [Min(0)]
    [SerializeField] private int minEnemies = 1;

    [Min(0)]
    [SerializeField] private int maxEnemies = 3;

    public bool AllowEnemySpawns => allowEnemySpawns;
    public int MinEnemies => minEnemies;
    public int MaxEnemies => maxEnemies;

    public IReadOnlyList<EnemySpawnPoint> SpawnPoints
    {
        get
        {
            return GetComponentsInChildren<EnemySpawnPoint>(
                true
            );
        }
    }

    // ==================================================
    // QUANTOS PONTOS VÁLIDOS EXISTEM?
    // ==================================================

    public int GetValidSpawnPointCount()
    {
        EnemySpawnPoint[] points =
            GetComponentsInChildren<EnemySpawnPoint>(
                true
            );

        int count = 0;

        foreach (EnemySpawnPoint point in points)
        {
            if (point == null)
                continue;

            if (!point.CanSpawn)
                continue;

            if (!point.gameObject.activeInHierarchy)
                continue;

            count++;
        }

        return count;
    }

    // ==================================================
    // DEVOLVE OS PONTOS VÁLIDOS
    // ==================================================

    public List<EnemySpawnPoint> GetValidSpawnPoints()
    {
        EnemySpawnPoint[] points =
            GetComponentsInChildren<EnemySpawnPoint>(
                true
            );

        List<EnemySpawnPoint> validPoints =
            new List<EnemySpawnPoint>();

        foreach (EnemySpawnPoint point in points)
        {
            if (point == null)
                continue;

            if (!point.CanSpawn)
                continue;

            if (!point.gameObject.activeInHierarchy)
                continue;

            validPoints.Add(
                point
            );
        }

        return validPoints;
    }

    // ==================================================
    // VALIDATION
    // ==================================================

    private void OnValidate()
    {
        if (minEnemies < 0)
        {
            minEnemies = 0;
        }

        if (maxEnemies < minEnemies)
        {
            maxEnemies = minEnemies;
        }
    }
}