using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    public DungeonModule startRoomPrefab;

    public DungeonModule[] corridorPrefabs;
    public DungeonModule[] roomPrefabs;

    public int roomsToGenerate = 10;
    public int maxRoomAttempts = 5;

    public LayerMask dungeonBoundsLayer;

    private void Start()
    {
        DungeonModule currentRoom = Instantiate(
            startRoomPrefab,
            Vector3.zero,
            Quaternion.identity
        );

        for (int i = 0; i < roomsToGenerate; i++)
        {
            int currentRoomSocket =
                GetRandomFreeSocketIndex(currentRoom);

            if (currentRoomSocket == -1)
            {
                Debug.Log(
                    "A sala atual não possui saídas livres. Geração encerrada."
                );

                break;
            }

            DungeonModule selectedCorridor =
                corridorPrefabs[
                    Random.Range(0, corridorPrefabs.Length)
                ];

            DungeonModule corridor = Instantiate(
                selectedCorridor,
                Vector3.zero,
                Quaternion.identity
            );

            int corridorEntrySocket =
                GetRandomFreeSocketIndex(corridor);

            ConnectModules(
                currentRoom,
                currentRoomSocket,
                corridor,
                corridorEntrySocket
            );

            if (HasOverlap(corridor))
            {
                Debug.Log(
                    "O corredor se sobrepôs a outro módulo. Geração interrompida."
                );

                corridor.gameObject.SetActive(false);
                Destroy(corridor.gameObject);

                break;
            }

            OccupyConnection(
                currentRoom,
                currentRoomSocket,
                corridor,
                corridorEntrySocket
            );

            int corridorExitSocket =
                GetRandomFreeSocketIndex(corridor);

            if (corridorExitSocket == -1)
            {
                Debug.Log(
                    "O corredor não possui uma saída livre."
                );

                break;
            }

            bool roomPlaced = false;

            for (int attempt = 0; attempt < maxRoomAttempts; attempt++)
            {
                DungeonModule selectedRoom =
                    roomPrefabs[
                        Random.Range(0, roomPrefabs.Length)
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
                    corridor,
                    corridorExitSocket,
                    newRoom,
                    newRoomEntrySocket
                );

                currentRoom = newRoom;
                roomPlaced = true;

                break;
            }

            if (!roomPlaced)
            {
                Debug.Log(
                    "Nenhuma sala conseguiu encaixar. Geração interrompida."
                );

                break;
            }
        }
    }

    private int GetRandomFreeSocketIndex(DungeonModule module)
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

    private void OccupyConnection(
        DungeonModule moduleA,
        int socketAIndex,
        DungeonModule moduleB,
        int socketBIndex)
    {
        moduleA.doorSockets[socketAIndex].isOccupied = true;
        moduleB.doorSockets[socketBIndex].isOccupied = true;
    }

    private void ConnectModules(
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

        foreach (BoxCollider box in module.moduleBounds.boxColliders)
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