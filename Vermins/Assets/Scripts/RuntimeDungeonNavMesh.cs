using System.Collections;
using UnityEngine;
using Unity.AI.Navigation;

public class RuntimeDungeonNavMesh : MonoBehaviour
{
    [Header("NavMesh")]
    [SerializeField] private NavMeshSurface navMeshSurface;

    [Header("NavMesh Floor")]
    [Tooltip(
        "Layer usada pelos BoxColliders NavMeshFloor."
    )]
    [SerializeField] private string navMeshFloorLayerName =
        "DungeonNavMesh";

    [Header("Debug")]
    [SerializeField] private bool logBuildResult = true;

    public bool HasBuiltNavMesh { get; private set; }

    private Coroutine pendingBuildCoroutine;

    // ==================================================
    // AWAKE
    // ==================================================

    private void Awake()
    {
        FindSurfaceIfNeeded();
    }

    // ==================================================
    // PROCURA O NAVMESH SURFACE
    // ==================================================

    private bool FindSurfaceIfNeeded()
    {
        if (navMeshSurface != null)
        {
            return true;
        }

        navMeshSurface =
            GetComponent<NavMeshSurface>();

        return navMeshSurface != null;
    }

    // ==================================================
    // SOLICITA BUILD
    // ==================================================

    [ContextMenu("Build Runtime NavMesh")]
    public bool BuildNavMesh()
    {
        if (!FindSurfaceIfNeeded())
        {
            Debug.LogError(
                "RuntimeDungeonNavMesh: " +
                "nenhum NavMeshSurface foi encontrado.",
                this
            );

            HasBuiltNavMesh = false;

            return false;
        }

        /*
         * Se já havia um build agendado,
         * cancelamos antes de iniciar outro.
         */
        CancelPendingBuild();

        /*
         * Remove qualquer NavMesh anterior.
         */
        RemoveCurrentNavMesh();

        HasBuiltNavMesh = false;

        /*
         * Em Play Mode, esperamos um frame.
         *
         * Isso é MUITO importante:
         *
         * módulos candidatos rejeitados durante
         * a geração foram chamados com Destroy(),
         * mas a Unity só os remove completamente
         * depois do loop atual.
         *
         * Se fizermos BuildNavMesh imediatamente,
         * esses módulos temporários ainda podem
         * entrar na coleta de geometria.
         */
        if (Application.isPlaying)
        {
            pendingBuildCoroutine =
                StartCoroutine(
                    BuildNavMeshNextFrame()
                );

            return true;
        }

        /*
         * Fora do Play Mode não precisamos
         * esperar um frame.
         */
        return BuildNavMeshNow();
    }

    // ==================================================
    // ESPERA A LIMPEZA DOS OBJETOS REJEITADOS
    // ==================================================

    private IEnumerator BuildNavMeshNextFrame()
    {
        /*
         * Espera até o próximo frame.
         *
         * Quando retomarmos, os objetos chamados
         * com Destroy() durante a geração anterior
         * já terão sido removidos pela Unity.
         */
        yield return null;

        pendingBuildCoroutine = null;

        BuildNavMeshNow();
    }

    // ==================================================
    // BUILD REAL
    // ==================================================

    private bool BuildNavMeshNow()
    {
        if (!FindSurfaceIfNeeded())
        {
            HasBuiltNavMesh = false;

            return false;
        }

        // ========================================
        // CONTAGEM DE FONTES PARA DEBUG
        // ========================================

        int floorColliderCount =
            CountActiveNavMeshFloorColliders();

        if (floorColliderCount <= 0)
        {
            Debug.LogError(
                "RuntimeDungeonNavMesh: " +
                $"nenhum BoxCollider ativo na Layer " +
                $"'{navMeshFloorLayerName}' foi encontrado.",
                this
            );

            HasBuiltNavMesh = false;

            return false;
        }

        // ========================================
        // REMOVE NAVMESH ANTIGO
        // ========================================

        RemoveCurrentNavMesh();

        // ========================================
        // BUILD NORMAL DO NAVMESH SURFACE
        // ========================================

        navMeshSurface.BuildNavMesh();

        HasBuiltNavMesh =
            navMeshSurface.navMeshData != null;

        // ========================================
        // LOG
        // ========================================

        if (logBuildResult)
        {
            if (HasBuiltNavMesh)
            {
                Debug.Log(
                    "Runtime NavMesh construído com sucesso | " +
                    $"NavMeshFloor Colliders ativos: " +
                    $"{floorColliderCount}.",
                    this
                );
            }
            else
            {
                Debug.LogError(
                    "RuntimeDungeonNavMesh: " +
                    "o NavMeshSurface não conseguiu " +
                    "construir o NavMesh.",
                    this
                );
            }
        }

        return HasBuiltNavMesh;
    }

    // ==================================================
    // CONTA SOMENTE OS NAVMESHFLOOR
    // QUE REALMENTE EXISTEM NA DUNGEON FINAL
    // ==================================================

    private int CountActiveNavMeshFloorColliders()
    {
        int targetLayer =
            LayerMask.NameToLayer(
                navMeshFloorLayerName
            );

        if (targetLayer < 0)
        {
            Debug.LogError(
                "RuntimeDungeonNavMesh: " +
                $"a Layer '{navMeshFloorLayerName}' não existe.",
                this
            );

            return 0;
        }

        BoxCollider[] colliders =
            GetComponentsInChildren<BoxCollider>(
                false
            );

        int count = 0;

        foreach (BoxCollider box in colliders)
        {
            if (box == null)
                continue;

            if (!box.enabled)
                continue;

            if (!box.gameObject.activeInHierarchy)
                continue;

            if (
                box.gameObject.layer !=
                targetLayer
            )
            {
                continue;
            }

            count++;
        }

        return count;
    }

    // ==================================================
    // REMOVE NAVMESH ATUAL
    // ==================================================

    private void RemoveCurrentNavMesh()
    {
        if (navMeshSurface == null)
            return;

        navMeshSurface.RemoveData();

        navMeshSurface.navMeshData =
            null;

        HasBuiltNavMesh = false;
    }

    // ==================================================
    // CANCELA BUILD PENDENTE
    // ==================================================

    private void CancelPendingBuild()
    {
        if (pendingBuildCoroutine == null)
            return;

        StopCoroutine(
            pendingBuildCoroutine
        );

        pendingBuildCoroutine =
            null;
    }

    // ==================================================
    // CLEAR
    // ==================================================

    [ContextMenu("Clear Runtime NavMesh")]
    public void ClearNavMesh()
    {
        CancelPendingBuild();

        RemoveCurrentNavMesh();
    }

    // ==================================================
    // ON DISABLE
    // ==================================================

    private void OnDisable()
    {
        ClearNavMesh();
    }
}