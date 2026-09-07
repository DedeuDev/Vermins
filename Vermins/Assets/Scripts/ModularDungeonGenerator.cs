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
    [SerializeField] private int targetRoomCount = 8;

    [Min(1)]
    [SerializeField] private int attemptsPerSocket = 30;

    [Header("Branching")]
    [Min(1)]
    [SerializeField] private int maxActiveBranches = 3;

    [Header("Final Room Rules")]
    [Min(1)]
    [SerializeField] private int minFinalRoomDepth = 8;

    [Header("Runtime")]
    [SerializeField] private bool generateOnStart = true;

    [Header("Seed")]
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int seed = 12345;

    [Header("Collision")]
    [SerializeField] private float overlapTolerance = 0.01f;

    [Header("Hierarchy")]
    [SerializeField] private Transform generatedRoot;

    private readonly List<DungeonModule> generatedModules =
        new List<DungeonModule>();

    private readonly List<DungeonSocket> openSockets =
        new List<DungeonSocket>();

    private int lastFinalRoomDepth = -1;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateDungeon();
        }
    }

    // ==================================================
    // GERAÇÃO PRINCIPAL
    // ==================================================

    [ContextMenu("Generate Dungeon")]
    public void GenerateDungeon()
    {
        ClearDungeon();

        // ========================================
        // VALIDAÇÃO
        // ========================================

        if (startRoomPrefab == null)
        {
            Debug.LogError(
                "Start Room Prefab não foi definido."
            );

            return;
        }

        if (
            roomPrefabs == null ||
            roomPrefabs.Count == 0
        )
        {
            Debug.LogError(
                "Nenhuma Room foi adicionada."
            );

            return;
        }

        if (
            corridorPrefabs == null ||
            corridorPrefabs.Count == 0
        )
        {
            Debug.LogError(
                "Nenhum Corridor foi adicionado."
            );

            return;
        }

        if (maxActiveBranches < 1)
        {
            maxActiveBranches = 1;
        }

        if (minFinalRoomDepth < 1)
        {
            minFinalRoomDepth = 1;
        }

        if (targetRoomCount < 1)
        {
            targetRoomCount = 1;
        }

        /*
         * Se existe Final Room, precisamos no mínimo:
         *
         * Start Room + Final Room
         *
         * Portanto, o mínimo real passa a ser 2.
         */
        int effectiveTargetRoomCount =
            finalRoomPrefab != null
                ? Mathf.Max(2, targetRoomCount)
                : Mathf.Max(1, targetRoomCount);

        CreateGeneratedRoot();

        // ========================================
        // SEED
        // ========================================

        if (randomSeed)
        {
            seed = unchecked(
                (int)System.DateTime.Now.Ticks
            );
        }

        Random.InitState(seed);

        lastFinalRoomDepth = -1;

        // ========================================
        // SALA INICIAL
        // ========================================

        DungeonModule startRoom =
            Instantiate(
                startRoomPrefab,
                transform.position,
                transform.rotation,
                generatedRoot
            );

        startRoom.Initialize();
        startRoom.GenerationDepth = 0;

        generatedModules.Add(startRoom);

        AddModuleOpenSockets(
            startRoom,
            null
        );

        // ========================================
        // QUANTAS ROOMS DEVEM EXISTIR
        // ANTES DA FINAL ROOM
        // ========================================

        int normalRoomTarget;

        if (finalRoomPrefab != null)
        {
            /*
             * Exemplo:
             *
             * Target Room Count = 8
             *
             * Antes da Final:
             * 7 Rooms
             *
             * Depois:
             * + Final Room
             *
             * Total = 8
             */
            normalRoomTarget =
                effectiveTargetRoomCount - 1;
        }
        else
        {
            normalRoomTarget =
                effectiveTargetRoomCount;
        }

        // ========================================
        // GERAÇÃO NORMAL
        // ========================================

        int safety = 10000;

        /*
         * Continua gerando enquanto:
         *
         * 1. Ainda faltam Rooms
         *
         * OU
         *
         * 2. A quantidade de Rooms já foi atingida,
         *    mas ainda não existe uma posição
         *    suficientemente profunda para
         *    a Final Room.
         */
        while (
            ShouldContinueNormalGeneration(
                normalRoomTarget
            ) &&
            openSockets.Count > 0 &&
            safety > 0
        )
        {
            safety--;

            int socketIndex =
                Random.Range(
                    0,
                    openSockets.Count
                );

            DungeonSocket targetSocket =
                openSockets[socketIndex];

            openSockets.RemoveAt(
                socketIndex
            );

            if (targetSocket == null)
                continue;

            if (targetSocket.IsConnected)
                continue;

            DungeonModule newModule;
            DungeonSocket newSocket;

            bool success =
                TryPlaceNormalModule(
                    targetSocket,
                    out newModule,
                    out newSocket
                );

            if (!success)
            {
                /*
                 * Esse socket permanece sem conexão.
                 *
                 * Depois poderá:
                 *
                 * - receber a Final Room;
                 * - receber um corredor para a Final;
                 * - ou ser fechado pelo SocketBlocker.
                 */
                continue;
            }

            targetSocket.Connect(
                newSocket
            );

            newModule.GenerationDepth =
                targetSocket.Owner.GenerationDepth
                + 1;

            generatedModules.Add(
                newModule
            );

            AddModuleOpenSockets(
                newModule,
                newSocket
            );
        }

        // ========================================
        // SALA FINAL
        // ========================================

        bool finalPlaced =
            PlaceFinalRoom();

        if (
            !finalPlaced &&
            finalRoomPrefab != null
        )
        {
            Debug.LogWarning(
                "Não foi possível posicionar a Final Room " +
                $"respeitando Min Final Room Depth = " +
                $"{minFinalRoomDepth}."
            );
        }

        // ========================================
        // FECHA SOCKETS RESTANTES
        // ========================================

        SealUnusedSockets();

        // ========================================
        // ESTATÍSTICAS
        // ========================================

        int roomCount =
            CountGeneratedRooms();

        int corridorCount =
            CountGeneratedCorridors();

        string finalDepthText =
            lastFinalRoomDepth >= 0
                ? lastFinalRoomDepth.ToString()
                : "Não posicionada";

        Debug.Log(
            $"Dungeon gerada | " +
            $"Seed: {seed} | " +
            $"Rooms: {roomCount} | " +
            $"Corridors: {corridorCount} | " +
            $"Total de módulos: {generatedModules.Count} | " +
            $"Target Rooms: {effectiveTargetRoomCount} | " +
            $"Máx. ramificações: {maxActiveBranches} | " +
            $"Final Room Depth: {finalDepthText}"
        );
    }

    // ==================================================
    // DECIDE SE A GERAÇÃO NORMAL DEVE CONTINUAR
    // ==================================================

    private bool ShouldContinueNormalGeneration(
        int normalRoomTarget
    )
    {
        int currentRoomCount =
            CountGeneratedRooms();

        /*
         * Ainda não atingimos a quantidade
         * desejada de salas.
         */
        if (
            currentRoomCount <
            normalRoomTarget
        )
        {
            return true;
        }

        /*
         * Se não existe Final Room configurada,
         * terminamos assim que atingirmos
         * Target Room Count.
         */
        if (finalRoomPrefab == null)
        {
            return false;
        }

        /*
         * A quantidade de Rooms já foi atingida,
         * mas ainda precisamos garantir que exista
         * uma posição profunda o suficiente
         * para a Final Room.
         *
         * Nesse caso, a dungeon continua crescendo.
         */
        return !HasDepthEligibleFinalConnection();
    }

    // ==================================================
    // CONTA ROOMS GERADAS
    // ==================================================

    private int CountGeneratedRooms()
    {
        int count = 0;

        foreach (
            DungeonModule module
            in generatedModules
        )
        {
            if (module == null)
                continue;

            if (
                module.ModuleType ==
                DungeonModuleType.Room
            )
            {
                count++;
            }
        }

        return count;
    }

    // ==================================================
    // CONTA CORREDORES GERADOS
    // ==================================================

    private int CountGeneratedCorridors()
    {
        int count = 0;

        foreach (
            DungeonModule module
            in generatedModules
        )
        {
            if (module == null)
                continue;

            if (
                module.ModuleType ==
                DungeonModuleType.Corridor
            )
            {
                count++;
            }
        }

        return count;
    }

    // ==================================================
    // VERIFICA SE JÁ EXISTE UM PONTO PROFUNDO
    // SUFICIENTE PARA A FINAL ROOM
    // ==================================================

    private bool HasDepthEligibleFinalConnection()
    {
        foreach (
            DungeonModule module
            in generatedModules
        )
        {
            if (module == null)
                continue;

            foreach (
                DungeonSocket socket
                in module.Sockets
            )
            {
                if (socket == null)
                    continue;

                if (socket.IsConnected)
                    continue;

                /*
                 * CORRIDOR -> FINAL ROOM
                 *
                 * A Final ficará em:
                 *
                 * corridorDepth + 1
                 */
                if (
                    module.ModuleType ==
                    DungeonModuleType.Corridor
                )
                {
                    int possibleFinalDepth =
                        module.GenerationDepth + 1;

                    if (
                        possibleFinalDepth >=
                        minFinalRoomDepth
                    )
                    {
                        return true;
                    }
                }

                /*
                 * ROOM -> CORRIDOR -> FINAL ROOM
                 *
                 * A Final ficará em:
                 *
                 * roomDepth + 2
                 */
                if (
                    module.ModuleType ==
                    DungeonModuleType.Room
                )
                {
                    int possibleFinalDepth =
                        module.GenerationDepth + 2;

                    if (
                        possibleFinalDepth >=
                        minFinalRoomDepth
                    )
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // ==================================================
    // CONTROLE DE RAMIFICAÇÕES
    // ==================================================

    private void AddModuleOpenSockets(
        DungeonModule module,
        DungeonSocket connectedSocket
    )
    {
        if (module == null)
            return;

        List<DungeonSocket> availableSockets =
            new List<DungeonSocket>();

        foreach (
            DungeonSocket socket
            in module.Sockets
        )
        {
            if (socket == null)
                continue;

            /*
             * Socket usado para conectar
             * com o módulo anterior.
             */
            if (
                socket ==
                connectedSocket
            )
            {
                continue;
            }

            if (socket.IsConnected)
                continue;

            availableSockets.Add(
                socket
            );
        }

        /*
         * Embaralha as possíveis saídas.
         *
         * Dessa forma, um Hall_T ou Hall_X
         * não favorece sempre a mesma direção.
         */
        ShuffleSockets(
            availableSockets
        );

        int availableBranchSlots =
            maxActiveBranches -
            openSockets.Count;

        if (
            availableBranchSlots <= 0
        )
        {
            return;
        }

        int socketsToAdd =
            Mathf.Min(
                availableBranchSlots,
                availableSockets.Count
            );

        for (
            int i = 0;
            i < socketsToAdd;
            i++
        )
        {
            openSockets.Add(
                availableSockets[i]
            );
        }
    }

    // ==================================================
    // EMBARALHA SOCKETS
    // ==================================================

    private void ShuffleSockets(
        List<DungeonSocket> sockets
    )
    {
        if (sockets == null)
            return;

        for (
            int i =
                sockets.Count - 1;

            i > 0;

            i--
        )
        {
            int randomIndex =
                Random.Range(
                    0,
                    i + 1
                );

            DungeonSocket temp =
                sockets[i];

            sockets[i] =
                sockets[randomIndex];

            sockets[randomIndex] =
                temp;
        }
    }

    // ==================================================
    // ESCOLHE ENTRE ROOM E CORRIDOR
    // ==================================================

    private bool TryPlaceNormalModule(
        DungeonSocket targetSocket,
        out DungeonModule placedModule,
        out DungeonSocket placedSocket
    )
    {
        placedModule = null;
        placedSocket = null;

        if (
            targetSocket == null ||
            targetSocket.Owner == null
        )
        {
            return false;
        }

        /*
         * ROOM -> CORRIDOR
         *
         * CORRIDOR -> ROOM
         */

        if (
            targetSocket.Owner.ModuleType ==
            DungeonModuleType.Room
        )
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
    // TENTA COLOCAR UM PREFAB DA LISTA
    // UTILIZANDO SPAWN WEIGHT
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

        if (
            pool == null ||
            pool.Count == 0
        )
        {
            return false;
        }

        for (
            int attempt = 0;
            attempt < attemptsPerSocket;
            attempt++
        )
        {
            DungeonModule prefab =
                GetWeightedRandomPrefab(
                    pool
                );

            if (prefab == null)
            {
                continue;
            }

            bool success =
                TryPlacePrefabOnce(
                    targetSocket,
                    prefab,
                    out placedModule,
                    out placedSocket
                );

            if (success)
            {
                return true;
            }
        }

        return false;
    }

    // ==================================================
    // ESCOLHA ALEATÓRIA POR PESO
    // ==================================================

    private DungeonModule GetWeightedRandomPrefab(
        IReadOnlyList<DungeonModule> pool
    )
    {
        if (
            pool == null ||
            pool.Count == 0
        )
        {
            return null;
        }

        float totalWeight = 0f;

        DungeonModule lastValidPrefab =
            null;

        foreach (
            DungeonModule prefab
            in pool
        )
        {
            if (prefab == null)
                continue;

            float weight =
                Mathf.Max(
                    0f,
                    prefab.SpawnWeight
                );

            if (weight <= 0f)
                continue;

            totalWeight += weight;

            lastValidPrefab =
                prefab;
        }

        if (
            totalWeight <= 0f ||
            lastValidPrefab == null
        )
        {
            return null;
        }

        float randomValue =
            Random.Range(
                0f,
                totalWeight
            );

        float accumulatedWeight =
            0f;

        foreach (
            DungeonModule prefab
            in pool
        )
        {
            if (prefab == null)
                continue;

            float weight =
                Mathf.Max(
                    0f,
                    prefab.SpawnWeight
                );

            if (weight <= 0f)
                continue;

            accumulatedWeight +=
                weight;

            if (
                randomValue <=
                accumulatedWeight
            )
            {
                return prefab;
            }
        }

        return lastValidPrefab;
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
            bool success =
                TryPlacePrefabOnce(
                    targetSocket,
                    prefab,
                    out placedModule,
                    out placedSocket
                );

            if (success)
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

        if (
            targetSocket == null ||
            prefab == null
        )
        {
            return false;
        }

        DungeonModule candidate =
            Instantiate(
                prefab,
                Vector3.zero,
                Quaternion.identity,
                generatedRoot
            );

        candidate.Initialize();

        List<DungeonSocket> compatibleSockets =
            new List<DungeonSocket>();

        foreach (
            DungeonSocket socket
            in candidate.Sockets
        )
        {
            if (socket == null)
                continue;

            if (
                socket.IsCompatibleWith(
                    targetSocket
                )
            )
            {
                compatibleSockets.Add(
                    socket
                );
            }
        }

        if (
            compatibleSockets.Count == 0
        )
        {
            DestroyObject(
                candidate.gameObject
            );

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

        Physics.SyncTransforms();

        if (
            !IsPlacementValid(
                candidate
            )
        )
        {
            DestroyObject(
                candidate.gameObject
            );

            return false;
        }

        placedModule =
            candidate;

        placedSocket =
            candidateSocket;

        return true;
    }

    // ==================================================
    // SALA FINAL
    // ==================================================

    private bool PlaceFinalRoom()
    {
        if (finalRoomPrefab == null)
        {
            return true;
        }

        // ========================================
        // PRIMEIRA OPÇÃO:
        // CORRIDOR -> FINAL ROOM
        // ========================================

        List<DungeonSocket> corridorCandidates =
            GetUnusedSockets(
                DungeonModuleType.Corridor
            );

        /*
         * Mantemos apenas sockets capazes
         * de colocar a Final na profundidade
         * mínima configurada.
         */
        corridorCandidates.RemoveAll(
            socket =>
                socket == null ||
                socket.Owner == null ||
                socket.Owner.GenerationDepth + 1 <
                minFinalRoomDepth
        );

        /*
         * Mais profundos primeiro.
         */
        corridorCandidates.Sort(
            (a, b) =>
                b.Owner.GenerationDepth.CompareTo(
                    a.Owner.GenerationDepth
                )
        );

        foreach (
            DungeonSocket socket
            in corridorCandidates
        )
        {
            DungeonModule finalRoom;
            DungeonSocket finalSocket;

            bool success =
                TryPlaceSpecificPrefab(
                    socket,
                    finalRoomPrefab,
                    out finalRoom,
                    out finalSocket
                );

            if (!success)
                continue;

            socket.Connect(
                finalSocket
            );

            finalRoom.GenerationDepth =
                socket.Owner.GenerationDepth
                + 1;

            lastFinalRoomDepth =
                finalRoom.GenerationDepth;

            generatedModules.Add(
                finalRoom
            );

            return true;
        }

        // ========================================
        // FALLBACK:
        //
        // ROOM -> CORRIDOR -> FINAL ROOM
        // ========================================

        List<DungeonSocket> roomCandidates =
            GetUnusedSockets(
                DungeonModuleType.Room
            );

        roomCandidates.RemoveAll(
            socket =>
                socket == null ||
                socket.Owner == null ||
                socket.Owner.GenerationDepth + 2 <
                minFinalRoomDepth
        );

        roomCandidates.Sort(
            (a, b) =>
                b.Owner.GenerationDepth.CompareTo(
                    a.Owner.GenerationDepth
                )
        );

        foreach (
            DungeonSocket roomSocket
            in roomCandidates
        )
        {
            DungeonModule bridgeCorridor;
            DungeonSocket bridgeEntrance;

            bool bridgePlaced =
                TryPlaceFromPool(
                    roomSocket,
                    corridorPrefabs,
                    out bridgeCorridor,
                    out bridgeEntrance
                );

            if (!bridgePlaced)
            {
                continue;
            }

            bridgeCorridor.GenerationDepth =
                roomSocket.Owner.GenerationDepth
                + 1;

            /*
             * O corredor entra temporariamente
             * na dungeon para que a Final Room
             * também teste colisão contra ele.
             */
            generatedModules.Add(
                bridgeCorridor
            );

            bool finalPlaced =
                false;

            foreach (
                DungeonSocket corridorSocket
                in bridgeCorridor.Sockets
            )
            {
                if (corridorSocket == null)
                    continue;

                if (
                    corridorSocket ==
                    bridgeEntrance
                )
                {
                    continue;
                }

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

                finalRoom.GenerationDepth =
                    bridgeCorridor.GenerationDepth
                    + 1;

                if (
                    finalRoom.GenerationDepth <
                    minFinalRoomDepth
                )
                {
                    DestroyObject(
                        finalRoom.gameObject
                    );

                    continue;
                }

                roomSocket.Connect(
                    bridgeEntrance
                );

                corridorSocket.Connect(
                    finalSocket
                );

                lastFinalRoomDepth =
                    finalRoom.GenerationDepth;

                generatedModules.Add(
                    finalRoom
                );

                finalPlaced = true;

                break;
            }

            if (finalPlaced)
            {
                return true;
            }

            generatedModules.Remove(
                bridgeCorridor
            );

            DestroyObject(
                bridgeCorridor.gameObject
            );
        }

        return false;
    }

    // ==================================================
    // PROCURA SOCKETS LIVRES
    // ==================================================

    private List<DungeonSocket> GetUnusedSockets(
        DungeonModuleType ownerType
    )
    {
        List<DungeonSocket> result =
            new List<DungeonSocket>();

        foreach (
            DungeonModule module
            in generatedModules
        )
        {
            if (module == null)
                continue;

            if (
                module.ModuleType !=
                ownerType
            )
            {
                continue;
            }

            foreach (
                DungeonSocket socket
                in module.Sockets
            )
            {
                if (socket == null)
                    continue;

                if (!socket.IsConnected)
                {
                    result.Add(
                        socket
                    );
                }
            }
        }

        return result;
    }

    // ==================================================
    // FECHA SOCKETS NÃO UTILIZADOS
    // ==================================================

    private void SealUnusedSockets()
    {
        if (
            socketBlockerPrefab == null
        )
        {
            Debug.LogWarning(
                "Socket Blocker Prefab não definido."
            );

            return;
        }

        foreach (
            DungeonModule module
            in generatedModules
        )
        {
            if (module == null)
                continue;

            foreach (
                DungeonSocket socket
                in module.Sockets
            )
            {
                if (socket == null)
                    continue;

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
            Quaternion.Euler(
                0f,
                180f,
                0f
            );

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
    // VERIFICAÇÃO DE COLISÃO
    // ==================================================

    private bool IsPlacementValid(
        DungeonModule candidate
    )
    {
        if (candidate == null)
            return false;

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

        foreach (
            DungeonModule existing
            in generatedModules
        )
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
                    candidateBounds
                        .transform.position,
                    candidateBounds
                        .transform.rotation,

                    existingBounds,
                    existingBounds
                        .transform.position,
                    existingBounds
                        .transform.rotation,

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
    // LIMPEZA
    // ==================================================

    [ContextMenu("Clear Dungeon")]
    public void ClearDungeon()
    {
        generatedModules.Clear();
        openSockets.Clear();

        lastFinalRoomDepth = -1;

        if (generatedRoot == null)
            return;

        for (
            int i =
                generatedRoot.childCount - 1;

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

    // ==================================================
    // ROOT DA DUNGEON
    // ==================================================

    private void CreateGeneratedRoot()
    {
        if (generatedRoot != null)
            return;

        Transform existing =
            transform.Find(
                "Generated Dungeon"
            );

        if (existing != null)
        {
            generatedRoot =
                existing;

            return;
        }

        GameObject root =
            new GameObject(
                "Generated Dungeon"
            );

        generatedRoot =
            root.transform;

        generatedRoot.SetParent(
            transform
        );

        generatedRoot.localPosition =
            Vector3.zero;

        generatedRoot.localRotation =
            Quaternion.identity;

        generatedRoot.localScale =
            Vector3.one;
    }

    // ==================================================
    // DESTROY
    // ==================================================

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