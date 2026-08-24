using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    public DungeonModule startRoomPrefab;
    public DungeonModule corridorPrefab;
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

        int currentExitSocket = 0;

        for (int i = 0; i < roomsToGenerate; i++)
        {
            DungeonModule corridor = Instantiate(
                corridorPrefab,
                Vector3.zero,
                Quaternion.identity
            );

            ConnectModules(
                currentRoom,
                currentExitSocket,
                corridor,
                0
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

                ConnectModules(
                    corridor,
                    1,
                    newRoom,
                    0
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

                currentRoom = newRoom;
                currentExitSocket = 1;

                roomPlaced = true;

                break;
            }

            if (!roomPlaced)
            {
                Debug.Log(
                    "Nenhuma sala conseguiu encaixar. Geração interrompida."
                );

                corridor.gameObject.SetActive(false);
                Destroy(corridor.gameObject);

                break;
            }
        }
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