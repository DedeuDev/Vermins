using System.Collections.Generic;
using UnityEngine;

public class ModularDungeonGenerator : MonoBehaviour
{
    private enum GenerationContext
    {
        MainPath,
        SideBranch
    }

    private const int RetrySeedStep = 7919;

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

    [Header("Validation")]
    [SerializeField] private bool validateAfterGeneration = true;
    [SerializeField] private bool verboseValidationLog = true;

    [Header("Generation Retry")]
    [Tooltip(
        "Se a dungeon for inválida, tenta gerar novamente " +
        "usando outra seed."
    )]
    [SerializeField] private bool retryInvalidDungeon = true;

    [Min(1)]
    [SerializeField] private int maxGenerationAttempts = 10;

    [SerializeField] private bool logFailedAttempts = true;

    [Header("Runtime NavMesh")]
    [SerializeField] private RuntimeDungeonNavMesh runtimeNavMesh;

    [SerializeField] private bool buildNavMeshAfterGeneration = true;

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
    // ESTADO
    // ==================================================

    private readonly List<DungeonModule> generatedModules =
        new List<DungeonModule>();

    private readonly List<DungeonSocket> openSockets =
        new List<DungeonSocket>();

    private readonly List<DungeonSocket> pendingBranchSockets =
        new List<DungeonSocket>();

    private readonly Dictionary<DungeonModule, int> prefabUsageCounts =
        new Dictionary<DungeonModule, int>();

    private DungeonModule generatedStartRoom;
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
        if (!ValidateGeneratorConfiguration())
        {
            return;
        }

        int effectiveMainPathRoomCount =
            GetEffectiveMainPathRoomCount();

        int effectiveTargetRoomCount =
            GetEffectiveTargetRoomCount();

        int baseSeed;

        if (randomSeed)
        {
            baseSeed =
                unchecked(
                    (int)System.DateTime.Now.Ticks
                );
        }
        else
        {
            baseSeed = seed;
        }

        int generationAttempts =
            retryInvalidDungeon
                ? Mathf.Max(
                    1,
                    maxGenerationAttempts
                )
                : 1;

        bool dungeonAccepted = false;

        int lastAttemptSeed =
            baseSeed;

        for (
            int attemptIndex = 0;
            attemptIndex < generationAttempts;
            attemptIndex++
        )
        {
            int attemptNumber =
                attemptIndex + 1;

            int attemptSeed =
                unchecked(
                    baseSeed +
                    attemptIndex *
                    RetrySeedStep
                );

            lastAttemptSeed =
                attemptSeed;

            bool generationCompleted =
                GenerateSingleAttempt(
                    attemptSeed,
                    effectiveMainPathRoomCount,
                    effectiveTargetRoomCount
                );

            bool shouldValidate =
                validateAfterGeneration ||
                retryInvalidDungeon;

            bool validationPassed =
                true;

            List<string> validationErrors =
                new List<string>();

            if (shouldValidate)
            {
                validationPassed =
                    RunDungeonValidation(
                        false,
                        out validationErrors
                    );
            }

            bool attemptAccepted =
                generationCompleted &&
                validationPassed;

            // ====================================
            // TENTATIVA ACEITA
            // ====================================

            if (attemptAccepted)
            {
                dungeonAccepted = true;

                seed = attemptSeed;

                LogGenerationStatistics(
                    effectiveMainPathRoomCount,
                    effectiveTargetRoomCount
                );

                if (
                    retryInvalidDungeon &&
                    attemptNumber > 1
                )
                {
                    Debug.Log(
                        $"Dungeon válida encontrada na " +
                        $"tentativa {attemptNumber}/" +
                        $"{generationAttempts}. " +
                        $"Seed aceita: {attemptSeed}."
                    );
                }

                if (validateAfterGeneration)
                {
                    RunDungeonValidation(
                        true,
                        out _
                    );
                }

                // ================================
                // BUILD DO NAVMESH
                // ================================

                if (
                    buildNavMeshAfterGeneration &&
                    runtimeNavMesh != null
                )
                {
                    bool navMeshBuilt =
                        runtimeNavMesh.BuildNavMesh();

                    if (!navMeshBuilt)
                    {
                        Debug.LogError(
                            "A dungeon foi gerada e validada, " +
                            "mas o Runtime NavMesh não pôde " +
                            "ser construído."
                        );
                    }
                }

                break;
            }

            // ====================================
            // TENTATIVA INVÁLIDA
            // ====================================

            if (logFailedAttempts)
            {
                string reason;

                if (!generationCompleted)
                {
                    reason =
                        "Não foi possível concluir " +
                        "o Main Path.";
                }
                else if (
                    validationErrors.Count > 0
                )
                {
                    reason =
                        string.Join(
                            " | ",
                            validationErrors
                        );
                }
                else
                {
                    reason =
                        "A dungeon não passou " +
                        "pela validação.";
                }

                Debug.LogWarning(
                    $"Tentativa {attemptNumber}/" +
                    $"{generationAttempts} inválida | " +
                    $"Seed: {attemptSeed} | " +
                    $"{reason}"
                );
            }
        }

        if (!dungeonAccepted)
        {
            seed =
                lastAttemptSeed;

            Debug.LogError(
                $"Não foi possível gerar uma dungeon válida " +
                $"após {generationAttempts} tentativa(s). " +
                $"Última seed testada: {lastAttemptSeed}."
            );

            if (
                validateAfterGeneration ||
                retryInvalidDungeon
            )
            {
                RunDungeonValidation(
                    true,
                    out _
                );
            }
        }
    }

    // ==================================================
    // TAMANHOS EFETIVOS
    // ==================================================

    private int GetEffectiveMainPathRoomCount()
    {
        int roomsRequiredByDepth =
            Mathf.CeilToInt(
                minFinalRoomDepth / 2f
            ) + 1;

        return Mathf.Max(
            mainPathRoomCount,
            roomsRequiredByDepth
        );
    }

    private int GetEffectiveTargetRoomCount()
    {
        int effectiveMainPathRoomCount =
            GetEffectiveMainPathRoomCount();

        return Mathf.Max(
            targetRoomCount,
            effectiveMainPathRoomCount
        );
    }

    // ==================================================
    // UMA TENTATIVA
    // ==================================================

    private bool GenerateSingleAttempt(
        int attemptSeed,
        int effectiveMainPathRoomCount,
        int effectiveTargetRoomCount
    )
    {
        ClearDungeon();

        CreateGeneratedRoot();

        Random.InitState(
            attemptSeed
        );

        generatedStartRoom = null;
        generatedFinalRoom = null;

        lastFinalRoomDepth = -1;

        // ========================================
        // START
        // ========================================

        generatedStartRoom =
            Instantiate(
                startRoomPrefab,
                transform.position,
                transform.rotation,
                generatedRoot
            );

        generatedStartRoom.Initialize();

        generatedStartRoom.GenerationDepth =
            0;

        generatedStartRoom.SourcePrefab =
            null;

        generatedModules.Add(
            generatedStartRoom
        );

        // ========================================
        // MAIN PATH
        // ========================================

        bool mainPathSuccess =
            GenerateMainPath(
                generatedStartRoom,
                effectiveMainPathRoomCount
            );

        if (!mainPathSuccess)
        {
            SealUnusedSockets();

            return false;
        }

        // ========================================
        // SIDE BRANCHES
        // ========================================

        PrepareSideBranches();

        GenerateSideBranches(
            effectiveTargetRoomCount
        );

        SealUnusedSockets();

        return true;
    }

    // ==================================================
    // CONFIGURAÇÃO
    // ==================================================

    private bool ValidateGeneratorConfiguration()
    {
        if (startRoomPrefab == null)
        {
            Debug.LogError(
                "Start Room Prefab não foi definido."
            );

            return false;
        }

        if (finalRoomPrefab == null)
        {
            Debug.LogError(
                "Final Room Prefab não foi definido."
            );

            return false;
        }

        if (
            roomPrefabs == null ||
            roomPrefabs.Count == 0
        )
        {
            Debug.LogError(
                "Nenhuma Room foi adicionada."
            );

            return false;
        }

        if (
            corridorPrefabs == null ||
            corridorPrefabs.Count == 0
        )
        {
            Debug.LogError(
                "Nenhum Corridor foi adicionado."
            );

            return false;
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

            return false;
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

        if (attemptsPerSocket < 1)
        {
            attemptsPerSocket = 1;
        }

        if (maxGenerationAttempts < 1)
        {
            maxGenerationAttempts = 1;
        }

        // ========================================
        // NAVMESH
        // ========================================

        if (
            buildNavMeshAfterGeneration &&
            runtimeNavMesh == null
        )
        {
            runtimeNavMesh =
                GetComponent<RuntimeDungeonNavMesh>();
        }

        if (
            buildNavMeshAfterGeneration &&
            runtimeNavMesh == null
        )
        {
            Debug.LogError(
                "Build NavMesh After Generation está ativo, " +
                "mas nenhum RuntimeDungeonNavMesh foi encontrado."
            );

            return false;
        }

        return true;
    }

    // ==================================================
    // ESTATÍSTICAS
    // ==================================================

    private void LogGenerationStatistics(
        int effectiveMainPathRoomCount,
        int effectiveTargetRoomCount
    )
    {
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
    // VALIDAÇÃO PÚBLICA
    // ==================================================

    public bool ValidateDungeon()
    {
        return RunDungeonValidation(
            true,
            out _
        );
    }

    [ContextMenu("Validate Dungeon")]
    private void ValidateDungeonFromContextMenu()
    {
        RunDungeonValidation(
            true,
            out _
        );
    }

    // ==================================================
    // VALIDAÇÃO INTERNA
    // ==================================================

    private bool RunDungeonValidation(
        bool logResult,
        out List<string> errors
    )
    {
        errors =
            new List<string>();

        List<string> successMessages =
            new List<string>();

        // ========================================
        // START
        // ========================================

        if (generatedStartRoom == null)
        {
            errors.Add(
                "Start Room não existe."
            );
        }
        else
        {
            successMessages.Add(
                "Start Room: OK"
            );
        }

        // ========================================
        // FINAL
        // ========================================

        if (generatedFinalRoom == null)
        {
            errors.Add(
                "Final Room não existe."
            );
        }
        else
        {
            successMessages.Add(
                "Final Room: OK"
            );
        }

        // ========================================
        // ROOM COUNT
        // ========================================

        int generatedRoomCount =
            CountGeneratedRooms();

        int requiredRoomCount =
            GetEffectiveTargetRoomCount();

        if (
            generatedRoomCount <
            requiredRoomCount
        )
        {
            errors.Add(
                $"Quantidade de Rooms insuficiente. " +
                $"Atual: {generatedRoomCount} | " +
                $"Mínimo: {requiredRoomCount}."
            );
        }
        else
        {
            successMessages.Add(
                $"Room Count: OK " +
                $"({generatedRoomCount} >= " +
                $"{requiredRoomCount})"
            );
        }

        // ========================================
        // FINAL DEPTH
        // ========================================

        if (generatedFinalRoom != null)
        {
            if (
                generatedFinalRoom.GenerationDepth <
                minFinalRoomDepth
            )
            {
                errors.Add(
                    $"Final Room Depth inválido. " +
                    $"Atual: " +
                    $"{generatedFinalRoom.GenerationDepth} | " +
                    $"Mínimo: {minFinalRoomDepth}."
                );
            }
            else
            {
                successMessages.Add(
                    $"Final Room Depth: OK " +
                    $"({generatedFinalRoom.GenerationDepth} >= " +
                    $"{minFinalRoomDepth})"
                );
            }
        }

        // ========================================
        // START -> FINAL
        // ========================================

        if (
            generatedStartRoom != null &&
            generatedFinalRoom != null
        )
        {
            bool finalReachable =
                CanReachModule(
                    generatedStartRoom,
                    generatedFinalRoom
                );

            if (!finalReachable)
            {
                errors.Add(
                    "Não existe um caminho conectado " +
                    "entre Start Room e Final Room."
                );
            }
            else
            {
                successMessages.Add(
                    "Caminho Start -> Final: OK"
                );
            }
        }

        // ========================================
        // SOCKETS
        // ========================================

        int unresolvedSocketCount =
            0;

        int invalidConnectionCount =
            0;

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
                {
                    errors.Add(
                        $"O módulo {module.name} possui " +
                        "uma referência de Socket nula."
                    );

                    continue;
                }

                if (!socket.IsResolved)
                {
                    unresolvedSocketCount++;

                    continue;
                }

                if (socket.IsConnected)
                {
                    if (
                        socket.ConnectedSocket == null
                    )
                    {
                        invalidConnectionCount++;

                        continue;
                    }

                    if (
                        socket.ConnectedSocket
                            .ConnectedSocket != socket
                    )
                    {
                        invalidConnectionCount++;

                        continue;
                    }

                    DungeonModule otherOwner =
                        socket.ConnectedSocket.Owner;

                    if (otherOwner == null)
                    {
                        invalidConnectionCount++;

                        continue;
                    }

                    if (
                        !generatedModules.Contains(
                            otherOwner
                        )
                    )
                    {
                        invalidConnectionCount++;
                    }
                }
            }
        }

        if (unresolvedSocketCount > 0)
        {
            errors.Add(
                $"{unresolvedSocketCount} socket(s) " +
                "ficaram sem conexão e sem SocketBlocker."
            );
        }
        else
        {
            successMessages.Add(
                "Sockets abertos: 0"
            );
        }

        if (invalidConnectionCount > 0)
        {
            errors.Add(
                $"{invalidConnectionCount} socket(s) " +
                "possuem conexões inválidas."
            );
        }
        else
        {
            successMessages.Add(
                "Integridade das conexões: OK"
            );
        }

        // ========================================
        // RESULTADO
        // ========================================

        bool isValid =
            errors.Count == 0;

        if (!logResult)
        {
            return isValid;
        }

        if (isValid)
        {
            Debug.Log(
                "====================================\n" +
                "DUNGEON VALIDATION: VALID\n" +
                "====================================\n" +
                string.Join(
                    "\n",
                    successMessages
                ) +
                "\n===================================="
            );
        }
        else
        {
            Debug.LogError(
                "====================================\n" +
                "DUNGEON VALIDATION: INVALID\n" +
                "====================================\n" +
                string.Join(
                    "\n",
                    errors
                ) +
                "\n===================================="
            );

            if (
                verboseValidationLog &&
                successMessages.Count > 0
            )
            {
                Debug.Log(
                    "Validações aprovadas:\n" +
                    string.Join(
                        "\n",
                        successMessages
                    )
                );
            }
        }

        return isValid;
    }

    // ==================================================
    // CONECTIVIDADE
    // ==================================================

    private bool CanReachModule(
        DungeonModule start,
        DungeonModule target
    )
    {
        if (
            start == null ||
            target == null
        )
        {
            return false;
        }

        if (start == target)
        {
            return true;
        }

        Queue<DungeonModule> queue =
            new Queue<DungeonModule>();

        HashSet<DungeonModule> visited =
            new HashSet<DungeonModule>();

        queue.Enqueue(
            start
        );

        visited.Add(
            start
        );

        while (
            queue.Count > 0
        )
        {
            DungeonModule current =
                queue.Dequeue();

            foreach (
                DungeonSocket socket
                in current.Sockets
            )
            {
                if (socket == null)
                    continue;

                if (!socket.IsConnected)
                    continue;

                DungeonSocket otherSocket =
                    socket.ConnectedSocket;

                if (otherSocket == null)
                    continue;

                DungeonModule neighbour =
                    otherSocket.Owner;

                if (neighbour == null)
                    continue;

                if (
                    neighbour ==
                    target
                )
                {
                    return true;
                }

                if (
                    visited.Contains(
                        neighbour
                    )
                )
                {
                    continue;
                }

                visited.Add(
                    neighbour
                );

                queue.Enqueue(
                    neighbour
                );
            }
        }

        return false;
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

        int normalRoomsToCreate =
            Mathf.Max(
                0,
                desiredMainPathRoomCount - 2
            );

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

        return TryPlaceFinalPathSegment(
            currentRoom
        );
    }

    // ==================================================
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
    // SIDE BRANCHES
    // ==================================================

    private void PrepareSideBranches()
    {
        openSockets.Clear();

        pendingBranchSockets.Clear();

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

                if (socket.IsResolved)
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
                continue;

            if (targetSocket.IsResolved)
                continue;

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
                targetSocket.Owner.GenerationDepth + 1;

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
                $"Essa geração será considerada inválida."
            );
        }
    }

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

            if (socket.IsResolved)
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
    // SORTEIO DE PREFABS
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

    private bool CanUsePrefabInContext(
        DungeonModule prefab,
        GenerationContext context
    )
    {
        if (prefab == null)
            return false;

        if (!CanUsePrefab(prefab))
            return false;

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
    // LIMITES DE PREFAB
    // ==================================================

    private bool CanUsePrefab(
        DungeonModule prefab
    )
    {
        if (prefab == null)
            return false;

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
    // INSTANCIAÇÃO
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
    // SOCKETS DISPONÍVEIS
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

            if (socket.IsResolved)
                continue;

            result.Add(
                socket
            );
        }

        return result;
    }

    // ==================================================
    // SHUFFLE
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
        if (socketBlockerPrefab == null)
        {
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

                if (socket.IsResolved)
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
        /*
         * A geometria será apagada.
         * O NavMesh correspondente também precisa sair.
         */
        if (runtimeNavMesh != null)
        {
            runtimeNavMesh.ClearNavMesh();
        }

        generatedModules.Clear();

        openSockets.Clear();

        pendingBranchSockets.Clear();

        prefabUsageCounts.Clear();

        generatedStartRoom = null;

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
            GameObject child =
                generatedRoot
                    .GetChild(i)
                    .gameObject;

            if (child == null)
                continue;

            /*
             * Destroy() durante Play Mode só termina
             * no fim do frame.
             *
             * Desativamos imediatamente para que
             * tentativas anteriores não participem
             * da geração nem do NavMesh.
             */
            child.SetActive(false);

            DestroyObject(
                child
            );
        }

        Physics.SyncTransforms();
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