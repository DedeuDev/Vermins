using System.Collections.Generic;
using UnityEngine;

public class ModularDungeonGenerator : MonoBehaviour
{
    private enum GenerationContext
    {
        MainPath,
        SideBranch
    }

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

    [Header("Main Path")]
    [Tooltip(
        "Quantidade de Rooms no caminho principal, " +
        "incluindo Start Room e Final Room."
    )]
    [Min(2)]
    [SerializeField] private int mainPathRoomCount = 5;

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

    // ==================================================
    // ESTADO DA DUNGEON
    // ==================================================

    private readonly List<DungeonModule> generatedModules =
        new List<DungeonModule>();

    /*
     * Side Branches atualmente ativas.
     */
    private readonly List<DungeonSocket> openSockets =
        new List<DungeonSocket>();

    /*
     * Possíveis Side Branches que ainda estão
     * esperando para serem ativadas.
     */
    private readonly List<DungeonSocket> pendingBranchSockets =
        new List<DungeonSocket>();

    /*
     * Quantas vezes cada prefab normal
     * foi utilizado nesta dungeon.
     */
    private readonly Dictionary<DungeonModule, int> prefabUsageCounts =
        new Dictionary<DungeonModule, int>();

    private DungeonModule generatedFinalRoom;

    private int lastFinalRoomDepth = -1;

    // ==================================================
    // START
    // ==================================================

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

        if (finalRoomPrefab == null)
        {
            Debug.LogError(
                "Final Room Prefab não foi definido."
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

        if (
            !HasRoomAvailableForContext(
                GenerationContext.MainPath
            )
        )
        {
            Debug.LogError(
                "Não existe nenhuma Room habilitada " +
                "para aparecer no Main Path."
            );

            return;
        }

        if (targetRoomCount < 2)
        {
            targetRoomCount = 2;
        }

        if (mainPathRoomCount < 2)
        {
            mainPathRoomCount = 2;
        }

        if (maxActiveBranches < 1)
        {
            maxActiveBranches = 1;
        }

        if (minFinalRoomDepth < 1)
        {
            minFinalRoomDepth = 1;
        }

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

        generatedFinalRoom = null;
        lastFinalRoomDepth = -1;

        // ========================================
        // TAMANHO NECESSÁRIO DO MAIN PATH
        // ========================================

        /*
         * Estrutura:
         *
         * Room
         * Corridor
         * Room
         * Corridor
         * Room
         *
         * FinalDepth =
         * (MainPathRooms - 1) * 2
         */

        int roomsRequiredByDepth =
            Mathf.CeilToInt(
                minFinalRoomDepth / 2f
            ) + 1;

        int effectiveMainPathRoomCount =
            Mathf.Max(
                mainPathRoomCount,
                roomsRequiredByDepth
            );

        int effectiveTargetRoomCount =
            Mathf.Max(
                targetRoomCount,
                effectiveMainPathRoomCount
            );

        // ========================================
        // START ROOM
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
        startRoom.SourcePrefab = null;

        generatedModules.Add(
            startRoom
        );

        // ========================================
        // MAIN PATH
        // ========================================

        bool mainPathSuccess =
            GenerateMainPath(
                startRoom,
                effectiveMainPathRoomCount
            );

        if (!mainPathSuccess)
        {
            Debug.LogError(
                "Não foi possível construir " +
                "o Main Path completo."
            );

            SealUnusedSockets();

            return;
        }

        // ========================================
        // SIDE BRANCHES
        // ========================================

        PrepareSideBranches();

        GenerateSideBranches(
            effectiveTargetRoomCount
        );

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

        int treasureCount =
            CountRoomsByCategory(
                DungeonRoomCategory.Treasure
            );

        int eliteCount =
            CountRoomsByCategory(
                DungeonRoomCategory.Elite
            );

        int shopCount =
            CountRoomsByCategory(
                DungeonRoomCategory.Shop
            );

        int eventCount =
            CountRoomsByCategory(
                DungeonRoomCategory.Event
            );

        Debug.Log(
            $"Dungeon gerada | " +
            $"Seed: {seed} | " +
            $"Rooms: {roomCount} | " +
            $"Corridors: {corridorCount} | " +
            $"Total Modules: {generatedModules.Count} | " +
            $"Target Rooms: {effectiveTargetRoomCount} | " +
            $"Main Path Rooms: {effectiveMainPathRoomCount} | " +
            $"Max Active Branches: {maxActiveBranches} | " +
            $"Final Room Depth: {lastFinalRoomDepth} | " +
            $"Treasure: {treasureCount} | " +
            $"Elite: {eliteCount} | " +
            $"Shop: {shopCount} | " +
            $"Event: {eventCount}"
        );
    }

    // ==================================================
    // MAIN PATH
    // ==================================================

    private bool GenerateMainPath(
        DungeonModule startRoom,
        int desiredMainPathRoomCount
    )
    {
        if (startRoom == null)
            return false;

        DungeonModule currentRoom =
            startRoom;

        /*
         * Start e Final ocupam duas posições.
         */
        int normalRoomsToCreate =
            Mathf.Max(
                0,
                desiredMainPathRoomCount - 2
            );

        // ========================================
        // ROOM -> CORRIDOR -> ROOM
        // ========================================

        for (
            int i = 0;
            i < normalRoomsToCreate;
            i++
        )
        {
            DungeonModule nextRoom;

            bool success =
                TryPlaceMainPathPair(
                    currentRoom,
                    out nextRoom
                );

            if (!success)
            {
                return false;
            }

            currentRoom =
                nextRoom;
        }

        // ========================================
        // ROOM -> CORRIDOR -> FINAL
        // ========================================

        return TryPlaceFinalPathSegment(
            currentRoom
        );
    }

    // ==================================================
    // MAIN PATH:
    //
    // ROOM -> CORRIDOR -> ROOM
    // ==================================================

    private bool TryPlaceMainPathPair(
        DungeonModule currentRoom,
        out DungeonModule nextRoom
    )
    {
        nextRoom = null;

        if (currentRoom == null)
            return false;

        List<DungeonSocket> roomExitSockets =
            GetAvailableSockets(
                currentRoom,
                null
            );

        ShuffleSockets(
            roomExitSockets
        );

        foreach (
            DungeonSocket roomExit
            in roomExitSockets
        )
        {
            if (roomExit == null)
                continue;

            for (
                int corridorAttempt = 0;
                corridorAttempt < attemptsPerSocket;
                corridorAttempt++
            )
            {
                DungeonModule corridorPrefab =
                    GetWeightedRandomPrefab(
                        corridorPrefabs,
                        GenerationContext.MainPath
                    );

                if (corridorPrefab == null)
                {
                    return false;
                }

                DungeonModule corridor;
                DungeonSocket corridorEntrance;

                bool corridorPlaced =
                    TryPlacePrefabOnce(
                        roomExit,
                        corridorPrefab,
                        out corridor,
                        out corridorEntrance
                    );

                if (!corridorPlaced)
                {
                    continue;
                }

                List<DungeonSocket> corridorExits =
                    GetAvailableSockets(
                        corridor,
                        corridorEntrance
                    );

                if (corridorExits.Count == 0)
                {
                    DestroyObject(
                        corridor.gameObject
                    );

                    continue;
                }

                corridor.GenerationDepth =
                    currentRoom.GenerationDepth + 1;

                /*
                 * Entra temporariamente na lista
                 * para a próxima Room considerar
                 * sua colisão.
                 */
                generatedModules.Add(
                    corridor
                );

                ShuffleSockets(
                    corridorExits
                );

                foreach (
                    DungeonSocket corridorExit
                    in corridorExits
                )
                {
                    for (
                        int roomAttempt = 0;
                        roomAttempt < attemptsPerSocket;
                        roomAttempt++
                    )
                    {
                        DungeonModule roomPrefab =
                            GetWeightedRandomPrefab(
                                roomPrefabs,
                                GenerationContext.MainPath
                            );

                        if (roomPrefab == null)
                        {
                            break;
                        }

                        DungeonModule room;
                        DungeonSocket roomEntrance;

                        bool roomPlaced =
                            TryPlacePrefabOnce(
                                corridorExit,
                                roomPrefab,
                                out room,
                                out roomEntrance
                            );

                        if (!roomPlaced)
                        {
                            continue;
                        }

                        /*
                         * Não é Final Room.
                         *
                         * Precisa de pelo menos
                         * uma saída para o Main Path
                         * continuar.
                         */
                        List<DungeonSocket> roomFutureExits =
                            GetAvailableSockets(
                                room,
                                roomEntrance
                            );

                        if (
                            roomFutureExits.Count == 0
                        )
                        {
                            DestroyObject(
                                room.gameObject
                            );

                            continue;
                        }

                        // ============================
                        // COMMIT
                        // ============================

                        roomExit.Connect(
                            corridorEntrance
                        );

                        corridorExit.Connect(
                            roomEntrance
                        );

                        corridor.SourcePrefab =
                            corridorPrefab;

                        room.SourcePrefab =
                            roomPrefab;

                        IncrementPrefabUsage(
                            corridorPrefab
                        );

                        IncrementPrefabUsage(
                            roomPrefab
                        );

                        room.GenerationDepth =
                            corridor.GenerationDepth + 1;

                        generatedModules.Add(
                            room
                        );

                        nextRoom =
                            room;

                        return true;
                    }
                }

                generatedModules.Remove(
                    corridor
                );

                DestroyObject(
                    corridor.gameObject
                );
            }
        }

        return false;
    }

    // ==================================================
    // FINAL DO MAIN PATH:
    //
    // ROOM -> CORRIDOR -> FINAL
    // ==================================================

    private bool TryPlaceFinalPathSegment(
        DungeonModule currentRoom
    )
    {
        if (
            currentRoom == null ||
            finalRoomPrefab == null
        )
        {
            return false;
        }

        List<DungeonSocket> roomExitSockets =
            GetAvailableSockets(
                currentRoom,
                null
            );

        ShuffleSockets(
            roomExitSockets
        );

        foreach (
            DungeonSocket roomExit
            in roomExitSockets
        )
        {
            for (
                int corridorAttempt = 0;
                corridorAttempt < attemptsPerSocket;
                corridorAttempt++
            )
            {
                DungeonModule corridorPrefab =
                    GetWeightedRandomPrefab(
                        corridorPrefabs,
                        GenerationContext.MainPath
                    );

                if (corridorPrefab == null)
                {
                    return false;
                }

                DungeonModule corridor;
                DungeonSocket corridorEntrance;

                bool corridorPlaced =
                    TryPlacePrefabOnce(
                        roomExit,
                        corridorPrefab,
                        out corridor,
                        out corridorEntrance
                    );

                if (!corridorPlaced)
                {
                    continue;
                }

                List<DungeonSocket> corridorExits =
                    GetAvailableSockets(
                        corridor,
                        corridorEntrance
                    );

                if (
                    corridorExits.Count == 0
                )
                {
                    DestroyObject(
                        corridor.gameObject
                    );

                    continue;
                }

                corridor.GenerationDepth =
                    currentRoom.GenerationDepth + 1;

                generatedModules.Add(
                    corridor
                );

                ShuffleSockets(
                    corridorExits
                );

                foreach (
                    DungeonSocket corridorExit
                    in corridorExits
                )
                {
                    for (
                        int finalAttempt = 0;
                        finalAttempt < attemptsPerSocket;
                        finalAttempt++
                    )
                    {
                        DungeonModule finalRoom;
                        DungeonSocket finalEntrance;

                        bool finalPlaced =
                            TryPlacePrefabOnce(
                                corridorExit,
                                finalRoomPrefab,
                                out finalRoom,
                                out finalEntrance
                            );

                        if (!finalPlaced)
                        {
                            continue;
                        }

                        int finalDepth =
                            corridor.GenerationDepth + 1;

                        if (
                            finalDepth <
                            minFinalRoomDepth
                        )
                        {
                            DestroyObject(
                                finalRoom.gameObject
                            );

                            continue;
                        }

                        // ============================
                        // COMMIT
                        // ============================

                        roomExit.Connect(
                            corridorEntrance
                        );

                        corridorExit.Connect(
                            finalEntrance
                        );

                        corridor.SourcePrefab =
                            corridorPrefab;

                        IncrementPrefabUsage(
                            corridorPrefab
                        );

                        finalRoom.SourcePrefab =
                            null;

                        finalRoom.GenerationDepth =
                            finalDepth;

                        generatedModules.Add(
                            finalRoom
                        );

                        generatedFinalRoom =
                            finalRoom;

                        lastFinalRoomDepth =
                            finalDepth;

                        return true;
                    }
                }

                generatedModules.Remove(
                    corridor
                );

                DestroyObject(
                    corridor.gameObject
                );
            }
        }

        return false;
    }

    // ==================================================
    // PREPARA SIDE BRANCHES
    // ==================================================

    private void PrepareSideBranches()
    {
        openSockets.Clear();

        pendingBranchSockets.Clear();

        /*
         * Qualquer socket livre do Main Path
         * pode iniciar uma Side Branch.
         *
         * Final Room fica de fora.
         */
        foreach (
            DungeonModule module
            in generatedModules
        )
        {
            if (module == null)
                continue;

            if (
                module ==
                generatedFinalRoom
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

                if (socket.IsConnected)
                    continue;

                pendingBranchSockets.Add(
                    socket
                );
            }
        }

        ShuffleSockets(
            pendingBranchSockets
        );

        FillActiveBranches();
    }

    // ==================================================
    // SIDE BRANCHES
    // ==================================================

    private void GenerateSideBranches(
        int desiredRoomCount
    )
    {
        int safety = 10000;

        while (
            CountGeneratedRooms() <
            desiredRoomCount &&
            (
                openSockets.Count > 0 ||
                pendingBranchSockets.Count > 0
            ) &&
            safety > 0
        )
        {
            safety--;

            FillActiveBranches();

            if (
                openSockets.Count == 0
            )
            {
                break;
            }

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
            {
                continue;
            }

            if (targetSocket.IsConnected)
            {
                continue;
            }

            if (
                targetSocket.Owner ==
                generatedFinalRoom
            )
            {
                continue;
            }

            DungeonModule newModule;
            DungeonSocket newSocket;

            bool success =
                TryPlaceNormalModule(
                    targetSocket,
                    GenerationContext.SideBranch,
                    out newModule,
                    out newSocket
                );

            if (!success)
            {
                FillActiveBranches();

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

            QueueModuleBranchSockets(
                newModule,
                newSocket
            );

            FillActiveBranches();
        }

        if (
            CountGeneratedRooms() <
            desiredRoomCount
        )
        {
            Debug.LogWarning(
                $"A dungeon terminou com " +
                $"{CountGeneratedRooms()} Rooms, " +
                $"mas o Target era {desiredRoomCount}. " +
                $"Pode ter faltado espaço ou prefabs elegíveis."
            );
        }
    }

    // ==================================================
    // COLOCA NOVOS SOCKETS NA FILA
    // ==================================================

    private void QueueModuleBranchSockets(
        DungeonModule module,
        DungeonSocket connectedSocket
    )
    {
        if (module == null)
            return;

        List<DungeonSocket> sockets =
            GetAvailableSockets(
                module,
                connectedSocket
            );

        ShuffleSockets(
            sockets
        );

        foreach (
            DungeonSocket socket
            in sockets
        )
        {
            pendingBranchSockets.Add(
                socket
            );
        }
    }

    // ==================================================
    // PREENCHE BRANCHES ATIVAS
    // ==================================================

    private void FillActiveBranches()
    {
        while (
            openSockets.Count <
            maxActiveBranches &&
            pendingBranchSockets.Count > 0
        )
        {
            int index =
                Random.Range(
                    0,
                    pendingBranchSockets.Count
                );

            DungeonSocket socket =
                pendingBranchSockets[index];

            pendingBranchSockets.RemoveAt(
                index
            );

            if (socket == null)
                continue;

            if (socket.IsConnected)
                continue;

            if (
                socket.Owner ==
                generatedFinalRoom
            )
            {
                continue;
            }

            openSockets.Add(
                socket
            );
        }
    }

    // ==================================================
    // ROOM / CORRIDOR
    // ==================================================

    private bool TryPlaceNormalModule(
        DungeonSocket targetSocket,
        GenerationContext context,
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
                context,
                out placedModule,
                out placedSocket
            );
        }

        return TryPlaceFromPool(
            targetSocket,
            roomPrefabs,
            context,
            out placedModule,
            out placedSocket
        );
    }

    // ==================================================
    // TENTA UM PREFAB DO POOL
    // ==================================================

    private bool TryPlaceFromPool(
        DungeonSocket targetSocket,
        IReadOnlyList<DungeonModule> pool,
        GenerationContext context,
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
                    pool,
                    context
                );

            if (prefab == null)
            {
                return false;
            }

            bool success =
                TryPlacePrefabOnce(
                    targetSocket,
                    prefab,
                    out placedModule,
                    out placedSocket
                );

            if (!success)
            {
                continue;
            }

            placedModule.SourcePrefab =
                prefab;

            IncrementPrefabUsage(
                prefab
            );

            return true;
        }

        return false;
    }

    // ==================================================
    // SORTEIO POR PESO
    // + LIMITE
    // + MAIN PATH / SIDE BRANCH
    // ==================================================

    private DungeonModule GetWeightedRandomPrefab(
        IReadOnlyList<DungeonModule> pool,
        GenerationContext context
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

            if (
                !CanUsePrefabInContext(
                    prefab,
                    context
                )
            )
            {
                continue;
            }

            float weight =
                Mathf.Max(
                    0f,
                    prefab.SpawnWeight
                );

            if (weight <= 0f)
                continue;

            totalWeight +=
                weight;

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

            if (
                !CanUsePrefabInContext(
                    prefab,
                    context
                )
            )
            {
                continue;
            }

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
    // VERIFICA SE PREFAB PODE APARECER
    // NESTE CONTEXTO
    // ==================================================

    private bool CanUsePrefabInContext(
        DungeonModule prefab,
        GenerationContext context
    )
    {
        if (prefab == null)
            return false;

        if (!CanUsePrefab(prefab))
            return false;

        /*
         * Corredores não possuem restrição
         * Main Path / Side Branch nesta versão.
         */
        if (
            prefab.ModuleType !=
            DungeonModuleType.Room
        )
        {
            return true;
        }

        if (
            context ==
            GenerationContext.MainPath
        )
        {
            return prefab.AllowedOnMainPath;
        }

        return prefab.AllowedOnSideBranch;
    }

    // ==================================================
    // EXISTE ROOM DISPONÍVEL PARA O CONTEXTO?
    // ==================================================

    private bool HasRoomAvailableForContext(
        GenerationContext context
    )
    {
        if (
            roomPrefabs == null ||
            roomPrefabs.Count == 0
        )
        {
            return false;
        }

        foreach (
            DungeonModule prefab
            in roomPrefabs
        )
        {
            if (prefab == null)
                continue;

            if (
                prefab.ModuleType !=
                DungeonModuleType.Room
            )
            {
                continue;
            }

            if (
                prefab.SpawnWeight <= 0f
            )
            {
                continue;
            }

            if (
                context ==
                GenerationContext.MainPath &&
                prefab.AllowedOnMainPath
            )
            {
                return true;
            }

            if (
                context ==
                GenerationContext.SideBranch &&
                prefab.AllowedOnSideBranch
            )
            {
                return true;
            }
        }

        return false;
    }

    // ==================================================
    // LIMITE DE INSTÂNCIAS
    // ==================================================

    private bool CanUsePrefab(
        DungeonModule prefab
    )
    {
        if (prefab == null)
            return false;

        /*
         * 0 = ilimitado.
         */
        if (
            prefab.MaxInstancesPerDungeon <= 0
        )
        {
            return true;
        }

        int currentUsage =
            GetPrefabUsage(
                prefab
            );

        return
            currentUsage <
            prefab.MaxInstancesPerDungeon;
    }

    private int GetPrefabUsage(
        DungeonModule prefab
    )
    {
        if (prefab == null)
            return 0;

        if (
            prefabUsageCounts.TryGetValue(
                prefab,
                out int count
            )
        )
        {
            return count;
        }

        return 0;
    }

    private void IncrementPrefabUsage(
        DungeonModule prefab
    )
    {
        if (prefab == null)
            return;

        int current =
            GetPrefabUsage(
                prefab
            );

        prefabUsageCounts[prefab] =
            current + 1;
    }

    // ==================================================
    // CRIA E TESTA UM PREFAB
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
    // SOCKETS LIVRES DE UM MÓDULO
    // ==================================================

    private List<DungeonSocket> GetAvailableSockets(
        DungeonModule module,
        DungeonSocket excludedSocket
    )
    {
        List<DungeonSocket> result =
            new List<DungeonSocket>();

        if (module == null)
            return result;

        foreach (
            DungeonSocket socket
            in module.Sockets
        )
        {
            if (socket == null)
                continue;

            if (
                socket ==
                excludedSocket
            )
            {
                continue;
            }

            if (socket.IsConnected)
                continue;

            result.Add(
                socket
            );
        }

        return result;
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
    // CONTADORES
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

    private int CountRoomsByCategory(
        DungeonRoomCategory category
    )
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
                module.ModuleType !=
                DungeonModuleType.Room
            )
            {
                continue;
            }

            if (
                module.RoomCategory ==
                category
            )
            {
                count++;
            }
        }

        return count;
    }

    // ==================================================
    // SOCKET BLOCKERS
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
    // COLISÃO
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
    // CLEAR
    // ==================================================

    [ContextMenu("Clear Dungeon")]
    public void ClearDungeon()
    {
        generatedModules.Clear();

        openSockets.Clear();

        pendingBranchSockets.Clear();

        prefabUsageCounts.Clear();

        generatedFinalRoom = null;

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
    // GENERATED ROOT
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