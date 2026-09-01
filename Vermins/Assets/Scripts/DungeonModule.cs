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

    [Header("Collision")]
    [SerializeField] private BoxCollider placementBounds;

    private DungeonSocket[] sockets;

    public DungeonModuleType ModuleType => moduleType;

    public IReadOnlyList<DungeonSocket> Sockets => sockets;

    public BoxCollider PlacementBounds => placementBounds;

    // Distância em conexões a partir da sala inicial.
    public int GenerationDepth { get; set; }

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