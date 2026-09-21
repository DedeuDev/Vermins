using UnityEngine;

public class BossSpawnData : MonoBehaviour
{
    [Header("Boss")]
    [SerializeField] private bool allowBossSpawn = true;

    [SerializeField] private GameObject bossPrefab;

    [Header("Spawn Point")]
    [SerializeField] private BossSpawnPoint spawnPoint;

    public bool AllowBossSpawn => allowBossSpawn;
    public GameObject BossPrefab => bossPrefab;

    public BossSpawnPoint SpawnPoint
    {
        get
        {
            FindSpawnPointIfNeeded();
            return spawnPoint;
        }
    }

    // ==================================================
    // AWAKE
    // ==================================================

    private void Awake()
    {
        FindSpawnPointIfNeeded();
    }

    // ==================================================
    // PROCURA SPAWN POINT
    // ==================================================

    private void FindSpawnPointIfNeeded()
    {
        if (spawnPoint != null)
            return;

        spawnPoint =
            GetComponentInChildren<BossSpawnPoint>(
                true
            );
    }

    // ==================================================
    // VALIDATION
    // ==================================================

    private void OnValidate()
    {
        FindSpawnPointIfNeeded();
    }
}