using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    public DungeonModule startRoomPrefab;

    public DungeonModule[] corridorPrefabs;
    public DungeonModule[] roomPrefabs;

    public int roomsToGenerate = 10;

    public int maxCorridorAttempts = 5;
    public int maxRoomAttempts = 5;

    public LayerMask dungeonBoundsLayer;

    private List<DoorSocket> availableSockets =
        new List<DoorSocket>();

    private void Start()
    {
        DungeonModule startRoom = Instantiate(
            startRoomPrefab,
            Vector3.zero,
            Quaternion.identity
        );

        AddFreeSockets(startRoom);

        int generatedRooms = 0;

        while (
            generatedRooms < roomsToGenerate &&
            availableSockets.Count > 0
        )
        {
            int randomSocketIndex =
                Random.Range(0, availableSockets.Count);

            DoorSocket targetSocket =
                availableSockets[randomSocketIndex];

            DungeonModule targetModule =
                targetSocket.GetComponentInParent<DungeonModule>();

            int targetSocketIndex =
                GetSocketIndex(
                    targetModule,
                    targetSocket
                );

            bool branchCreated = false;

            for (
                int corridorAttempt = 0;
                corridorAttempt < maxCorridorAttempts;
                corridorAttempt++
            )
            {
                DungeonModule selectedCorridor =
                    corridorPrefabs[
                        Random.Range(
                            0,
                            corridorPrefabs.Length
                        )
                    ];

                DungeonModule corridor = Instantiate(
                    selectedCorridor,
                    Vector3.zero,
                    Quaternion.identity
                );

                int corridorEntrySocket =
                    GetRandomFreeSocketIndex(corridor);

                int corridorExitSocket =
                    GetRandomFreeSocketIndexExcept(
                        corridor,
                        corridorEntrySocket
                    );

                if (corridorExitSocket == -1)
                {
                    Destroy(corridor.gameObject);
                    continue;
                }

                ConnectModules(
                    targetModule,
                    targetSocketIndex,
                    corridor,
                    corridorEntrySocket
                );

                if (HasOverlap(corridor))
                {
                    Debug.Log(
                        "Corredor sobreposto. Tentando outro corredor..."
                    );

                    corridor.gameObject.SetActive(false);
                    Destroy(corridor.gameObject);

                    continue;
                }

                for (
                    int roomAttempt = 0;
                    roomAttempt < maxRoomAttempts;
                    roomAttempt++
                )
                {
                    DungeonModule selectedRoom =
                        roomPrefabs[
                            Random.Range(
                                0,
                                roomPrefabs.Length
                            )
                        ];

                    DungeonModule newRoom = Instantiate(
                        selectedRoom,
                        Vector3.zero,
                        Quaternion.identity
                    );

                    int newRoomEntrySocket =
                        GetRandomFreeSocketIndex(newRoom);

                    if (newRoomEntrySocket == -1)
                    {
                        Destroy(newRoom.gameObject);
                        continue;
                    }

                    ConnectModules(
                        corridor,
                        corridorExitSocket,
                        newRoom,
                        newRoomEntrySocket
                    );

                    if (HasOverlap(newRoom))
                    {
                        Debug.Log(
                            "Sala sobreposta. Tentando outra sala..."
                        );

                        newRoom.gameObject.SetActive(false);
                        Destroy(newRoom.gameObject);

                        continue;
                    }

                    OccupyConnection(
                        targetModule,
                        targetSocketIndex,
                        corridor,
                        corridorEntrySocket
                    );

                    OccupyConnection(
                        corridor,
                        corridorExitSocket,
                        newRoom,
                        newRoomEntrySocket
                    );

                    availableSockets.Remove(targetSocket);

                    AddFreeSockets(newRoom);

                    generatedRooms++;
                    branchCreated = true;

                    break;
                }

                if (branchCreated)
                {
                    break;
                }

                corridor.gameObject.SetActive(false);
                Destroy(corridor.gameObject);
            }

            if (!branchCreated)
            {
                Debug.Log(
                    "Não foi possível gerar nada neste socket. " +
                    "Tentando outro ponto da dungeon."
                );

                availableSockets.Remove(targetSocket);
            }
        }

        Debug.Log(
            "Salas geradas: " + generatedRooms
        );

        Debug.Log(
            "Sockets livres restantes: " +
            availableSockets.Count
        );
    }

    private void AddFreeSockets(DungeonModule module)
    {
        foreach (DoorSocket socket in module.doorSockets)
        {
            if (!socket.isOccupied)
            {
                availableSockets.Add(socket);
            }
        }
    }

    private int GetRandomFreeSocketIndex(
        DungeonModule module
    )
    {
        List<int> freeSockets = new List<int>();

        for (int i = 0; i < module.doorSockets.Length; i++)
        {
            if (!module.doorSockets[i].isOccupied)
            {
                freeSockets.Add(i);
            }
        }

        if (freeSockets.Count == 0)
        {
            return -1;
        }

        return freeSockets[
            Random.Range(0, freeSockets.Count)
        ];
    }

    private int GetRandomFreeSocketIndexExcept(
        DungeonModule module,
        int excludedIndex
    )
    {
        List<int> freeSockets = new List<int>();

        for (int i = 0; i < module.doorSockets.Length; i++)
        {
            if (
                !module.doorSockets[i].isOccupied &&
                i != excludedIndex
            )
            {
                freeSockets.Add(i);
            }
        }

        if (freeSockets.Count == 0)
        {
            return -1;
        }

        return freeSockets[
            Random.Range(0, freeSockets.Count)
        ];
    }

    private int GetSocketIndex(
        DungeonModule module,
        DoorSocket socket
    )
    {
        for (int i = 0; i < module.doorSockets.Length; i++)
        {
            if (module.doorSockets[i] == socket)
            {
                return i;
            }
        }

        return -1;
    }

    private void OccupyConnection(
        DungeonModule moduleA,
        int socketAIndex,
        DungeonModule moduleB,
        int socketBIndex
    )
    {
        moduleA.doorSockets[socketAIndex].isOccupied = true;
        moduleB.doorSockets[socketBIndex].isOccupied = true;
    }

    private void ConnectModules(
        DungeonModule fixedModule,
        int fixedSocketIndex,
        DungeonModule movingModule,
        int movingSocketIndex
    )
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

        movingModule.transform.Rotate(
            Vector3.up,
            angle
        );

        Vector3 offset =
            fixedSocket.transform.position -
            movingSocket.transform.position;

        movingModule.transform.position += offset;
    }

    private bool HasOverlap(DungeonModule module)
    {
        Physics.SyncTransforms();

        foreach (
            BoxCollider box
            in module.moduleBounds.boxColliders
        )
        {
            Vector3 center =
                box.transform.TransformPoint(box.center);

            Vector3 halfExtents = Vector3.Scale(
                box.size * 0.5f,
                new Vector3(
                    Mathf.Abs(box.transform.lossyScale.x),
                    Mathf.Abs(box.transform.lossyScale.y),
                    Mathf.Abs(box.transform.lossyScale.z)
                )
            );

            Collider[] hits = Physics.OverlapBox(
                center,
                halfExtents * 0.95f,
                box.transform.rotation,
                dungeonBoundsLayer,
                QueryTriggerInteraction.Collide
            );

            foreach (Collider hit in hits)
            {
                if (!hit.transform.IsChildOf(module.transform))
                {
                    return true;
                }
            }
        }

        return false;
    }
}