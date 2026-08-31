using System.Collections.Generic;
using UnityEngine;

public class DungeonModule : MonoBehaviour
{
    [Header("Collision")]
    [SerializeField] private BoxCollider placementBounds;

    private DungeonSocket[] sockets;

    public IReadOnlyList<DungeonSocket> Sockets => sockets;

    public BoxCollider PlacementBounds => placementBounds;

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