using System.Collections.Generic;
using UnityEngine;

public class ModularDungeonGenerator : MonoBehaviour
{
    [Header("Modules")]
    [SerializeField] private DungeonModule startModulePrefab;
    [SerializeField] private List<DungeonModule> modulePrefabs = new();

    [Header("Generation")]
    [Min(1)]
    [SerializeField] private int targetModuleCount = 20;

    [Min(1)]
    [SerializeField] private int attemptsPerSocket = 30;

    [Header("Seed")]
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int seed = 12345;

    [Header("Collision")]
    [Tooltip("Pequenas interpenetrações abaixo desse valor serão ignoradas.")]
    [SerializeField] private float overlapTolerance = 0.01f;

    [Header("Hierarchy")]
    [SerializeField] private Transform generatedRoot;

    private readonly List<DungeonModule> generatedModules = new();
    private readonly List<DungeonSocket> openSockets = new();

    [ContextMenu("Generate Dungeon")]
    public void GenerateDungeon()
    {
        ClearDungeon();

        if (startModulePrefab == null)
        {
            Debug.LogError("Nenhum Start Module foi definido.");
            return;
        }

        if (modulePrefabs == null || modulePrefabs.Count == 0)
        {
            Debug.LogError("Nenhum módulo foi adicionado à lista.");
            return;
        }

        CreateGeneratedRoot();

        if (randomSeed)
        {
            seed = unchecked((int)System.DateTime.Now.Ticks);
        }

        Random.InitState(seed);

        // ------------------------------------------------
        // CRIA A PRIMEIRA SALA
        // ------------------------------------------------

        DungeonModule startModule = Instantiate(
            startModulePrefab,
            transform.position,
            transform.rotation,
            generatedRoot
        );

        startModule.Initialize();

        generatedModules.Add(startModule);

        foreach (DungeonSocket socket in startModule.Sockets)
        {
            openSockets.Add(socket);
        }

        // ------------------------------------------------
        // GERA AS OUTRAS SALAS
        // ------------------------------------------------

        int safety = 10000;

        while (
            generatedModules.Count < targetModuleCount &&
            openSockets.Count > 0 &&
            safety > 0
        )
        {
            safety--;

            // Escolhe aleatoriamente uma porta disponível.
            int socketIndex = Random.Range(0, openSockets.Count);

            DungeonSocket targetSocket = openSockets[socketIndex];

            // Ela será processada apenas uma vez.
            openSockets.RemoveAt(socketIndex);

            if (targetSocket == null || targetSocket.IsConnected)
                continue;

            DungeonModule newModule;
            DungeonSocket newModuleSocket;

            bool success = TryPlaceModule(
                targetSocket,
                out newModule,
                out newModuleSocket
            );

            if (!success)
                continue;

            // Conecta os dois sockets.
            targetSocket.Connect(newModuleSocket);

            generatedModules.Add(newModule);

            // Todos os outros sockets da nova sala
            // agora podem receber novas salas.
            foreach (DungeonSocket socket in newModule.Sockets)
            {
                if (socket == newModuleSocket)
                    continue;

                if (socket.IsConnected)
                    continue;

                openSockets.Add(socket);
            }
        }

        Debug.Log(
            $"Dungeon gerada. " +
            $"Seed: {seed} | " +
            $"Módulos: {generatedModules.Count}"
        );
    }

    private bool TryPlaceModule(
        DungeonSocket targetSocket,
        out DungeonModule placedModule,
        out DungeonSocket placedSocket
    )
    {
        placedModule = null;
        placedSocket = null;

        for (int attempt = 0; attempt < attemptsPerSocket; attempt++)
        {
            // --------------------------------------------
            // ESCOLHE UM PREFAB ALEATÓRIO
            // --------------------------------------------

            DungeonModule prefab =
                modulePrefabs[Random.Range(0, modulePrefabs.Count)];

            if (prefab == null)
                continue;

            DungeonModule candidate = Instantiate(
                prefab,
                Vector3.zero,
                Quaternion.identity,
                generatedRoot
            );

            candidate.Initialize();

            // --------------------------------------------
            // PROCURA SOCKETS COMPATÍVEIS
            // --------------------------------------------

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
                continue;
            }

            DungeonSocket candidateSocket =
                compatibleSockets[
                    Random.Range(0, compatibleSockets.Count)
                ];

            // --------------------------------------------
            // ALINHA AS DUAS PORTAS
            // --------------------------------------------

            AlignModule(
                candidate,
                candidateSocket,
                targetSocket
            );

            // --------------------------------------------
            // TESTA COLISÃO
            // --------------------------------------------

            if (!IsPlacementValid(candidate))
            {
                DestroyObject(candidate.gameObject);
                continue;
            }

            // Funcionou.
            placedModule = candidate;
            placedSocket = candidateSocket;

            return true;
        }

        return false;
    }

    private void AlignModule(
        DungeonModule module,
        DungeonSocket moduleSocket,
        DungeonSocket targetSocket
    )
    {
        /*
         * Queremos:
         *
         * targetSocket
         *        →
         *
         *        ←
         * moduleSocket
         *
         * Então o socket novo precisa ficar 180 graus
         * oposto ao socket existente.
         */

        Quaternion desiredSocketRotation =
            targetSocket.transform.rotation *
            Quaternion.Euler(0f, 180f, 0f);

        Quaternion rotationDifference =
            desiredSocketRotation *
            Quaternion.Inverse(moduleSocket.transform.rotation);

        module.transform.rotation =
            rotationDifference *
            module.transform.rotation;

        // Depois da rotação, move o módulo
        // para colocar os sockets exatamente no mesmo ponto.

        Vector3 positionDifference =
            targetSocket.transform.position -
            moduleSocket.transform.position;

        module.transform.position += positionDifference;
    }

    private bool IsPlacementValid(DungeonModule candidate)
    {
        BoxCollider candidateBounds =
            candidate.PlacementBounds;

        if (candidateBounds == null)
        {
            Debug.LogWarning(
                $"O módulo {candidate.name} não possui PlacementBounds."
            );

            return false;
        }

        foreach (DungeonModule existing in generatedModules)
        {
            if (existing == null)
                continue;

            BoxCollider existingBounds =
                existing.PlacementBounds;

            if (existingBounds == null)
                continue;

            bool overlapping = Physics.ComputePenetration(
                candidateBounds,
                candidateBounds.transform.position,
                candidateBounds.transform.rotation,

                existingBounds,
                existingBounds.transform.position,
                existingBounds.transform.rotation,

                out Vector3 direction,
                out float distance
            );

            if (overlapping && distance > overlapTolerance)
            {
                return false;
            }
        }

        return true;
    }

    [ContextMenu("Clear Dungeon")]
    public void ClearDungeon()
    {
        generatedModules.Clear();
        openSockets.Clear();

        if (generatedRoot == null)
            return;

        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
        {
            DestroyObject(
                generatedRoot.GetChild(i).gameObject
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
            new GameObject("Generated Dungeon");

        generatedRoot = root.transform;

        generatedRoot.SetParent(transform);

        generatedRoot.localPosition = Vector3.zero;
        generatedRoot.localRotation = Quaternion.identity;
    }

    private void DestroyObject(GameObject obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}