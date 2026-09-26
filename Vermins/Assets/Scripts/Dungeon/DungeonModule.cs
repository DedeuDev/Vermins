using System.Collections.Generic;
using UnityEngine;

public enum DungeonModuleType
{
    Room,
    Corridor
}

public enum DungeonRoomCategory
{
    Normal,
    Treasure,
    Elite,
    Shop,
    Event
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

    [Header("Room Rules")]
    [Tooltip("Usado apenas quando Module Type = Room.")]
    [SerializeField] private DungeonRoomCategory roomCategory =
        DungeonRoomCategory.Normal;

    [Tooltip("Usado apenas quando Module Type = Room.")]
    [SerializeField] private bool allowedOnMainPath = true;

    [Tooltip("Usado apenas quando Module Type = Room.")]
    [SerializeField] private bool allowedOnSideBranch = true;

    [Header("Collision")]
    [SerializeField] private BoxCollider placementBounds;

    private DungeonSocket[] sockets;

    public DungeonModuleType ModuleType => moduleType;

    public float SpawnWeight => spawnWeight;

    public int MaxInstancesPerDungeon =>
        maxInstancesPerDungeon;

    public DungeonRoomCategory RoomCategory =>
        roomCategory;

    public bool AllowedOnMainPath =>
        allowedOnMainPath;

    public bool AllowedOnSideBranch =>
        allowedOnSideBranch;

    public IReadOnlyList<DungeonSocket> Sockets =>
        sockets;

    public BoxCollider PlacementBounds =>
        placementBounds;

    // Distância em conexões a partir da Start Room.
    public int GenerationDepth { get; set; }

    /*
     * Prefab que originou esta instância.
     *
     * O gerador usa isso para controlar
     * Max Instances Per Dungeon.
     */
    public DungeonModule SourcePrefab { get; set; }

    public void Initialize()
    {
        sockets =
            GetComponentsInChildren<DungeonSocket>(
                true
            );

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