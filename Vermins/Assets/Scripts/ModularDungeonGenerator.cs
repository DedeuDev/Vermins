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
     * Sockets que estão sendo processados
     * atualmente como Side Branches.
     */
    private readonly List<DungeonSocket> openSockets =
        new List<DungeonSocket>();

    /*
     * Sockets disponíveis para futuras ramificações,
     * mas que ainda não estão ativos.
     */
    private readonly List<DungeonSocket> pendingBranchSockets =
        new List<DungeonSocket>();

    /*
     * Quantas vezes cada prefab normal
     * já foi utilizado.
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
                "Nenhuma Room normal foi adicionada."
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
        // CALCULA MAIN PATH NECESSÁRIO
        // ========================================

        /*
         * Estrutura do Main Path:
         *
         * Room
         * Corridor
         * Room
         * Corridor
         * Room
         * ...
         *
         * Portanto:
         *
         * 2 Rooms no Main Path
         * -> Final Depth 2
         *
         * 3 Rooms
         * -> Final Depth 4
         *
         * 4 Rooms
         * -> Final Depth 6
         *
         * Fórmula:
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

        /*
         * A dungeon nunca pode ter menos salas
         * que seu próprio Main Path.
         */
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

        /*
         * Start Room não conta para limites
         * de prefabs normais.
         */
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
                "Não foi possível construir o Main Path completo."
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

        Debug.Log(
            $"Dungeon gerada | " +
            $"Seed: {seed} | " +
            $"Rooms: {roomCount} | " +
            $"Corridors: {corridorCount} | " +
            $"Total Modules: {generatedModules.Count} | " +
            $"Target Rooms: {effectiveTargetRoomCount} | " +
            $"Main Path Rooms: {effectiveMainPathRoomCount} | " +
            $"Max Active Branches: {maxActiveBranches} | " +
            $"Final Room Depth: {lastFinalRoomDepth}"
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
         * Start e Final já ocupam duas posições.
         *
         * Exemplo:
         *
         * MainPathRoomCount = 5
         *
         * Start
         * Room
         * Room
         * Room
         * Final
         *
         * Portanto:
         *
         * 5 - 2 = 3 Rooms normais.
         */
        int normalRoomsToCreate =
            Mathf.Max(
                0,
                desiredMainPathRoomCount - 2
            );

        // ========================================
        // CRIA OS PARES:
        //
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
        // ÚLTIMA ETAPA:
        //
        // ROOM -> CORRIDOR -> FINAL ROOM
        // ========================================

        return TryPlaceFinalPathSegment(
            currentRoom
        );
    }

    // ==================================================
    // CRIA UM TRECHO:
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
                        corridorPrefabs
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

                /*
                 * Um corredor do Main Path precisa
                 * ter pelo menos uma saída além
                 * da entrada.
                 */
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
                 * Adicionamos temporariamente para
                 * que a Room teste colisão contra
                 * o corredor.
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
                                roomPrefabs
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
                         * Como essa não é a Final Room,
                         * ela precisa ter ao menos
                         * outra saída para o Main Path
                         * continuar depois.
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
                        // COMMIT DO TRECHO
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

                /*
                 * Nenhuma Room conseguiu ser
                 * posicionada depois deste corredor.
                 *
                 * Como ainda não conectamos nada,
                 * podemos simplesmente removê-lo.
                 */
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
    // ÚLTIMO TRECHO DO MAIN PATH:
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
                        corridorPrefabs
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

                /*
                 * Adiciona temporariamente para
                 * a Final Room considerar o corredor
                 * durante o teste de colisão.
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
                        // COMMIT FINAL
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
         * Todo socket livre do Main Path pode
         * potencialmente iniciar uma Side Branch.
         *
         * A Final Room é excluída propositalmente:
         * não queremos caminhos continuando depois
         * dela.
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
    // GERA SIDE BRANCHES
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

            /*
             * A Final Room nunca deve gerar
             * uma Side Branch.
             */
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
                    out newModule,
                    out newSocket
                );

            if (!success)
            {
                /*
                 * O socket simplesmente permanece
                 * sem conexão e será fechado
                 * pelo SocketBlocker no final.
                 */
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
    // DE SIDE BRANCHES
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
    // MANTÉM O LIMITE DE BRANCHES ATIVAS
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
    // ESCOLHE ROOM OU CORRIDOR
    // PARA SIDE BRANCH
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
    // TENTA UM PREFAB DO POOL
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
    // ESCOLHA ALEATÓRIA POR PESO
    // + LIMITE DE INSTÂNCIAS
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

            if (!CanUsePrefab(prefab))
                continue;

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

            if (!CanUsePrefab(prefab))
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
    // LIMITE DE PREFAB
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
    // CRIA E TESTA CANDIDATO
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
    // RETORNA SOCKETS LIVRES DE UM MÓDULO
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
    // CONTA ROOMS
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
    // CONTA CORREDORES
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
    // LIMPEZA
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
    // ROOT
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