using UnityEngine;

public class DungeonModule : MonoBehaviour
{
    public DoorSocket[] doorSockets;
    public ModuleBounds moduleBounds;

    private void Awake()
    {
        doorSockets = GetComponentsInChildren<DoorSocket>();
        moduleBounds = GetComponentInChildren<ModuleBounds>();
    }
}