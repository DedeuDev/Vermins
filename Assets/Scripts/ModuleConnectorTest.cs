using UnityEngine;

public class ModuleConnectorTest : MonoBehaviour
{
    public DungeonModule moduleA;
    public DungeonModule moduleB;
    public DungeonModule moduleC;

    private void Start()
    {
        ConnectModules(moduleA, 0, moduleB, 0);
        ConnectModules(moduleB, 1, moduleC, 0);
    }

    public void ConnectModules(
        DungeonModule fixedModule,
        int fixedSocketIndex,
        DungeonModule movingModule,
        int movingSocketIndex)
    {
        DoorSocket fixedSocket =
            fixedModule.doorSockets[fixedSocketIndex];

        DoorSocket movingSocket =
            movingModule.doorSockets[movingSocketIndex];

        float angle = Vector3.SignedAngle(
            movingSocket.transform.forward,
            -fixedSocket.transform.forward,
            Vector3.up
        );

        movingModule.transform.Rotate(Vector3.up, angle);

        Vector3 offset =
            fixedSocket.transform.position -
            movingSocket.transform.position;

        movingModule.transform.position += offset;
    }
}