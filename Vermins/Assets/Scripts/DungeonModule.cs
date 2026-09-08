using System.Collections.Generic;
using UnityEngine;

public enum DungeonModuleType
{
    Room,
    Corridor
}

public class DungeonModule : MonoBehaviour
{
    [Header("Module")]
    [SerializeField] private DungeonModuleType moduleType;

    [Min(0f)]
    [SerializeField] private float spawnWeight = 1f;

    [Tooltip("0 = sem limite.")]
    [Min(0)]
    [SerializeField] private int maxInstancesPerDungeon = 0;

    [Header("Collision")]
    [SerializeField] private BoxCollider placementBounds;

    private DungeonSocket[] sockets;

    public DungeonModuleType ModuleType => moduleType;

    public float SpawnWeight => spawnWeight;

    public int MaxInstancesPerDungeon => maxInstancesPerDungeon;

    public IReadOnlyList<DungeonSocket> Sockets => sockets;

    public BoxCollider PlacementBounds => placementBounds;

    // Distância em conexões a partir da sala inicial.
    public int GenerationDepth { get; set; }

    /*
     * Referência ao prefab que originou esta instância.
     *
     * É usada pelo gerador para controlar
     * quantas vezes cada prefab apareceu.
     */
    public DungeonModule SourcePrefab { get; set; }

    public void Initialize()
    {
        sockets = GetComponentsInChildren<DungeonSocket>(true);

        foreach (DungeonSocket socket in sockets)
        {
            socket.Initialize(this);
        }
    }

    private void Awake()
    {
        Initialize();
    }
}