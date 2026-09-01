using System.Collections.Generic;
using UnityEngine;

public class ModularDungeonGenerator : MonoBehaviour
{
    [Header("Important Rooms")]
    [SerializeField] private DungeonModule startRoomPrefab;
    [SerializeField] private DungeonModule finalRoomPrefab;

    [Header("Normal Modules")]
    [SerializeField] private List<DungeonModule> roomPrefabs = new();
    [SerializeField] private List<DungeonModule> corridorPrefabs = new();

    [Header("Socket Closing")]
    [SerializeField] private GameObject socketBlockerPrefab;

    [Header("Generation")]
    [Min(1)]
    [SerializeField] private int targetModuleCount = 20;

    [Min(1)]
    [SerializeField] private int attemptsPerSocket = 30;

    [Header("Runtime")]
    [SerializeField] private bool generateOnStart = true;

    [Header("Seed")]
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int seed = 12345;

    [Header("Collision")]
    [SerializeField] private float overlapTolerance = 0.01f;

    [Header("Hierarchy")]
    [SerializeField] private Transform generatedRoot;

    private readonly List<DungeonModule> generatedModules = new();
    private readonly List<DungeonSocket> openSockets = new();

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateDungeon();
        }
    }

    [ContextMenu("Generate Dungeon")]
    public void GenerateDungeon()
    {
        ClearDungeon();

        if (startRoomPrefab == null)
        {
            Debug.LogError("Start Room Prefab não foi definido.");
            return;
        }

        if (roomPrefabs.Count == 0)
        {
            Debug.LogError("Nenhuma Room foi adicionada.");
            return;
        }

        if (corridorPrefabs.Count == 0)
        {
            Debug.LogError("Nenhum Corridor foi adicionado.");
            return;
        }

        CreateGeneratedRoot();

        if (randomSeed)
        {
            seed = unchecked((int)System.DateTime.Now.Ticks);
        }

        Random.InitState(seed);

        // ========================================
        // SALA INICIAL
        // ========================================

        DungeonModule startRoom = Instantiate(
            startRoomPrefab,
            transform.position,
            transform.rotation,
            generatedRoot
        );

        startRoom.Initialize();
        startRoom.GenerationDepth = 0;

        generatedModules.Add(startRoom);

        foreach (DungeonSocket socket in startRoom.Sockets)
        {
            openSockets.Add(socket);
        }

        /*
         * Reservamos aproximadamente um módulo
         * para a sala final.
         */
        int normalTarget = finalRoomPrefab != null
            ? Mathf.Max(1, targetModuleCount - 1)
            : targetModuleCount;

        // ========================================
        // GERAÇÃO NORMAL
        // ========================================

        int safety = 10000;

        while (
            generatedModules.Count < normalTarget &&
            openSockets.Count > 0 &&
            safety > 0
        )
        {
            safety--;

            int socketIndex =
                Random.Range(0, openSockets.Count);

            DungeonSocket targetSocket =
                openSockets[socketIndex];

            openSockets.RemoveAt(socketIndex);

            if (
                targetSocket == null ||
                targetSocket.IsConnected
            )
            {
                continue;
            }

            DungeonModule newModule;
            DungeonSocket newSocket;

            bool success = TryPlaceNormalModule(
                targetSocket,
                out newModule,
                out newSocket
            );

            if (!success)
                continue;

            targetSocket.Connect(newSocket);

            newModule.GenerationDepth =
                targetSocket.Owner.GenerationDepth + 1;

            generatedModules.Add(newModule);

            foreach (DungeonSocket socket in newModule.Sockets)
            {
                if (socket == newSocket)
                    continue;

                if (socket.IsConnected)
                    continue;

                openSockets.Add(socket);
            }
        }

        // ========================================
        // SALA FINAL
        // ========================================

        bool finalPlaced = PlaceFinalRoom();

        if (!finalPlaced && finalRoomPrefab != null)
        {
            Debug.LogWarning(
                "Não foi possível posicionar a sala final."
            );
        }

        // ========================================
        // FECHA TODAS AS PORTAS RESTANTES
        // ========================================

        SealUnusedSockets();

        Debug.Log(
            $"Dungeon gerada | Seed: {seed} | " +
            $"Módulos: {generatedModules.Count}"
        );
    }

    // ==================================================
    // ESCOLHE ROOM OU CORRIDOR
    // ==================================================

    private bool TryPlaceNormalModule(
        DungeonSocket targetSocket,
        out DungeonModule placedModule,
        out DungeonSocket placedSocket
    )
    {
        /*
         * Se o socket pertence a uma ROOM:
         *
         * Room -> Corridor
         *
         * Se pertence a um CORRIDOR:
         *
         * Corridor -> Room
         */

        if (targetSocket.Owner.ModuleType ==
            DungeonModuleType.Room)
        {
            return TryPlaceFromPool(
                targetSocket,
                corridorPrefabs,
                out placedModule,
                out placedSocket
            );
        }

        return TryPlaceFromPool(
            targetSocket,
            roomPrefabs,
            out placedModule,
            out placedSocket
        );
    }

    // ==================================================
    // TENTA UM DOS PREFABS DE UMA LISTA
    // ==================================================

    private bool TryPlaceFromPool(
        DungeonSocket targetSocket,
        IReadOnlyList<DungeonModule> pool,
        out DungeonModule placedModule,
        out DungeonSocket placedSocket
    )
    {
        placedModule = null;
        placedSocket = null;

        if (pool == null || pool.Count == 0)
            return false;

        for (
            int attempt = 0;
            attempt < attemptsPerSocket;
            attempt++
        )
        {
            DungeonModule prefab =
                pool[Random.Range(0, pool.Count)];

            if (prefab == null)
                continue;

            if (
                TryPlacePrefabOnce(
                    targetSocket,
                    prefab,
                    out placedModule,
                    out placedSocket
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    // ==================================================
    // TENTA UM PREFAB ESPECÍFICO
    // ==================================================

    private bool TryPlaceSpecificPrefab(
        DungeonSocket targetSocket,
        DungeonModule prefab,
        out DungeonModule placedModule,
        out DungeonSocket placedSocket
    )
    {
        placedModule = null;
        placedSocket = null;

        if (prefab == null)
            return false;

        for (
            int attempt = 0;
            attempt < attemptsPerSocket;
            attempt++
        )
        {
            if (
                TryPlacePrefabOnce(
                    targetSocket,
                    prefab,
                    out placedModule,
                    out placedSocket
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    // ==================================================
    // CRIA E TESTA UM CANDIDATO
    // ==================================================

    private bool TryPlacePrefabOnce(
        DungeonSocket targetSocket,
        DungeonModule prefab,
        out DungeonModule placedModule,
        out DungeonSocket placedSocket
    )
    {
        placedModule = null;
        placedSocket = null;

        DungeonModule candidate = Instantiate(
            prefab,
            Vector3.zero,
            Quaternion.identity,
            generatedRoot
        );

        candidate.Initialize();

        List<DungeonSocket> compatibleSockets =
            new List<DungeonSocket>();

        foreach (DungeonSocket socket in candidate.Sockets)
        {
            if (socket.IsCompatibleWith(targetSocket))
            {
                compatibleSockets.Add(socket);
            }
        }

        if (compatibleSockets.Count == 0)
        {
            DestroyObject(candidate.gameObject);
            return false;
        }

        DungeonSocket candidateSocket =
            compatibleSockets[
                Random.Range(
                    0,
                    compatibleSockets.Count
                )
            ];

        AlignModule(
            candidate,
            candidateSocket,
            targetSocket
        );

        if (!IsPlacementValid(candidate))
        {
            DestroyObject(candidate.gameObject);
            return false;
        }

        placedModule = candidate;
        placedSocket = candidateSocket;

        return true;
    }

    // ==================================================
    // SALA FINAL
    // ==================================================

    private bool PlaceFinalRoom()
    {
        if (finalRoomPrefab == null)
            return true;

        /*
         * Primeiro procuramos sockets livres
         * pertencentes a corredores.
         *
         * Corridor -> Final Room
         */

        List<DungeonSocket> candidates =
            GetUnusedSockets(
                DungeonModuleType.Corridor
            );

        /*
         * Os módulos mais distantes da sala inicial
         * vêm primeiro.
         */

        candidates.Sort(
            (a, b) =>
                b.Owner.GenerationDepth.CompareTo(
                    a.Owner.GenerationDepth
                )
        );

        foreach (DungeonSocket socket in candidates)
        {
            DungeonModule finalRoom;
            DungeonSocket finalSocket;

            if (
                TryPlaceSpecificPrefab(
                    socket,
                    finalRoomPrefab,
                    out finalRoom,
                    out finalSocket
                )
            )
            {
                socket.Connect(finalSocket);

                finalRoom.GenerationDepth =
                    socket.Owner.GenerationDepth + 1;

                generatedModules.Add(finalRoom);

                return true;
            }
        }

        /*
         * FALLBACK:
         *
         * Se só existem sockets livres em salas,
         * tentamos criar:
         *
         * Room -> Corridor -> Final Room
         */

        List<DungeonSocket> roomSockets =
            GetUnusedSockets(
                DungeonModuleType.Room
            );

        roomSockets.Sort(
            (a, b) =>
                b.Owner.GenerationDepth.CompareTo(
                    a.Owner.GenerationDepth
                )
        );

        foreach (DungeonSocket roomSocket in roomSockets)
        {
            DungeonModule bridgeCorridor;
            DungeonSocket bridgeEntrance;

            if (
                !TryPlaceFromPool(
                    roomSocket,
                    corridorPrefabs,
                    out bridgeCorridor,
                    out bridgeEntrance
                )
            )
            {
                continue;
            }

            bridgeCorridor.GenerationDepth =
                roomSocket.Owner.GenerationDepth + 1;

            /*
             * Adicionamos temporariamente para que
             * a checagem de colisão da sala final
             * também considere este corredor.
             */

            generatedModules.Add(bridgeCorridor);

            foreach (
                DungeonSocket corridorSocket
                in bridgeCorridor.Sockets
            )
            {
                if (corridorSocket == bridgeEntrance)
                    continue;

                DungeonModule finalRoom;
                DungeonSocket finalSocket;

                bool success =
                    TryPlaceSpecificPrefab(
                        corridorSocket,
                        finalRoomPrefab,
                        out finalRoom,
                        out finalSocket
                    );

                if (!success)
                    continue;

                roomSocket.Connect(bridgeEntrance);

                corridorSocket.Connect(finalSocket);

                finalRoom.GenerationDepth =
                    bridgeCorridor.GenerationDepth + 1;

                generatedModules.Add(finalRoom);

                return true;
            }

            /*
             * Nenhuma saída desse corredor serviu.
             * Então descartamos ele.
             */

            generatedModules.Remove(bridgeCorridor);

            DestroyObject(
                bridgeCorridor.gameObject
            );
        }

        return false;
    }

    private List<DungeonSocket> GetUnusedSockets(
        DungeonModuleType ownerType
    )
    {
        List<DungeonSocket> result =
            new List<DungeonSocket>();

        foreach (DungeonModule module in generatedModules)
        {
            if (module.ModuleType != ownerType)
                continue;

            foreach (DungeonSocket socket in module.Sockets)
            {
                if (!socket.IsConnected)
                {
                    result.Add(socket);
                }
            }
        }

        return result;
    }

    // ==================================================
    // PAREDES DE FECHAMENTO
    // ==================================================

    private void SealUnusedSockets()
    {
        if (socketBlockerPrefab == null)
        {
            Debug.LogWarning(
                "Socket Blocker Prefab não definido."
            );

            return;
        }

        foreach (DungeonModule module in generatedModules)
        {
            foreach (DungeonSocket socket in module.Sockets)
            {
                if (socket.IsConnected)
                    continue;

                Instantiate(
                    socketBlockerPrefab,
                    socket.transform.position,
                    socket.transform.rotation,
                    socket.transform
                );

                socket.Seal();
            }
        }
    }

    // ==================================================
    // ALINHAMENTO
    // ==================================================

    private void AlignModule(
        DungeonModule module,
        DungeonSocket moduleSocket,
        DungeonSocket targetSocket
    )
    {
        Quaternion desiredSocketRotation =
            targetSocket.transform.rotation *
            Quaternion.Euler(0f, 180f, 0f);

        Quaternion rotationDifference =
            desiredSocketRotation *
            Quaternion.Inverse(
                moduleSocket.transform.rotation
            );

        module.transform.rotation =
            rotationDifference *
            module.transform.rotation;

        Vector3 positionDifference =
            targetSocket.transform.position -
            moduleSocket.transform.position;

        module.transform.position +=
            positionDifference;
    }

    // ==================================================
    // COLISÃO
    // ==================================================

    private bool IsPlacementValid(
        DungeonModule candidate
    )
    {
        BoxCollider candidateBounds =
            candidate.PlacementBounds;

        if (candidateBounds == null)
        {
            Debug.LogWarning(
                $"O módulo {candidate.name} " +
                $"não possui PlacementBounds."
            );

            return false;
        }

        Physics.SyncTransforms();

        foreach (DungeonModule existing in generatedModules)
        {
            if (existing == null)
                continue;

            BoxCollider existingBounds =
                existing.PlacementBounds;

            if (existingBounds == null)
                continue;

            bool overlapping =
                Physics.ComputePenetration(
                    candidateBounds,
                    candidateBounds.transform.position,
                    candidateBounds.transform.rotation,

                    existingBounds,
                    existingBounds.transform.position,
                    existingBounds.transform.rotation,

                    out Vector3 direction,
                    out float distance
                );

            if (
                overlapping &&
                distance > overlapTolerance
            )
            {
                return false;
            }
        }

        return true;
    }

    // ==================================================
    // CLEANUP
    // ==================================================

    [ContextMenu("Clear Dungeon")]
    public void ClearDungeon()
    {
        generatedModules.Clear();
        openSockets.Clear();

        if (generatedRoot == null)
            return;

        for (
            int i = generatedRoot.childCount - 1;
            i >= 0;
            i--
        )
        {
            DestroyObject(
                generatedRoot
                    .GetChild(i)
                    .gameObject
            );
        }
    }

    private void CreateGeneratedRoot()
    {
        if (generatedRoot != null)
            return;

        Transform existing =
            transform.Find("Generated Dungeon");

        if (existing != null)
        {
            generatedRoot = existing;
            return;
        }

        GameObject root =
            new GameObject(
                "Generated Dungeon"
            );

        generatedRoot = root.transform;

        generatedRoot.SetParent(transform);

        generatedRoot.localPosition =
            Vector3.zero;

        generatedRoot.localRotation =
            Quaternion.identity;
    }

    private void DestroyObject(
        GameObject obj
    )
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
            DestroyImmediate(obj);
        }
    }
}