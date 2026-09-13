using UnityEngine;
using UnityEngine.AI;

public class EnemyNavMeshWander : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;

    [Header("Wander")]
    [Min(0.5f)]
    [SerializeField] private float wanderRadius = 6f;

    [Tooltip(
        "Distância máxima usada para procurar " +
        "um ponto válido próximo da posição sorteada."
    )]
    [Min(0.1f)]
    [SerializeField] private float sampleDistance = 2f;

    [Tooltip(
        "Quantas tentativas fazer para encontrar " +
        "um próximo destino válido."
    )]
    [Min(1)]
    [SerializeField] private int maxDestinationAttempts = 10;

    [Tooltip(
        "Considera que o inimigo chegou ao destino " +
        "quando estiver dentro desta margem."
    )]
    [Min(0.01f)]
    [SerializeField] private float arrivalThreshold = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool logProblems = false;

    // ==================================================
    // AWAKE
    // ==================================================

    private void Awake()
    {
        FindAgentIfNeeded();
    }

    // ==================================================
    // START
    // ==================================================

    private void Start()
    {
        TrySetRandomDestination();
    }

    // ==================================================
    // UPDATE
    // ==================================================

    private void Update()
    {
        if (!FindAgentIfNeeded())
            return;

        /*
         * Se o agente ainda não estiver em cima
         * de um NavMesh válido, apenas espera.
         */
        if (!agent.isOnNavMesh)
        {
            if (logProblems)
            {
                Debug.LogWarning(
                    $"{name}: NavMeshAgent ainda não está sobre o NavMesh.",
                    this
                );
            }

            return;
        }

        /*
         * Se ainda está calculando caminho,
         * não faz nada neste frame.
         */
        if (agent.pathPending)
            return;

        /*
         * Se por algum motivo ele ficou sem caminho,
         * tenta gerar um novo imediatamente.
         */
        if (!agent.hasPath)
        {
            TrySetRandomDestination();
            return;
        }

        /*
         * Se já chegou (ou quase chegou),
         * escolhe um novo destino na hora.
         */
        if (HasReachedDestination())
        {
            TrySetRandomDestination();
        }
    }

    // ==================================================
    // PROCURA NAVMESH AGENT
    // ==================================================

    private bool FindAgentIfNeeded()
    {
        if (agent != null)
            return true;

        agent = GetComponent<NavMeshAgent>();

        return agent != null;
    }

    // ==================================================
    // CHEGOU AO DESTINO?
    // ==================================================

    private bool HasReachedDestination()
    {
        if (agent.pathPending)
            return false;

        float remainingDistance =
            agent.remainingDistance;

        if (remainingDistance == Mathf.Infinity)
            return false;

        return
            remainingDistance <=
            agent.stoppingDistance + arrivalThreshold;
    }

    // ==================================================
    // DESTINO ALEATÓRIO
    // ==================================================

    private void TrySetRandomDestination()
    {
        if (!FindAgentIfNeeded())
            return;

        if (!agent.isOnNavMesh)
            return;

        for (
            int attempt = 0;
            attempt < maxDestinationAttempts;
            attempt++
        )
        {
            Vector2 randomCircle =
                Random.insideUnitCircle * wanderRadius;

            Vector3 desiredPosition =
                transform.position +
                new Vector3(
                    randomCircle.x,
                    0f,
                    randomCircle.y
                );

            NavMeshHit hit;

            bool foundPosition =
                NavMesh.SamplePosition(
                    desiredPosition,
                    out hit,
                    sampleDistance,
                    NavMesh.AllAreas
                );

            if (!foundPosition)
            {
                continue;
            }

            NavMeshPath path =
                new NavMeshPath();

            bool foundPath =
                agent.CalculatePath(
                    hit.position,
                    path
                );

            if (
                !foundPath ||
                path.status != NavMeshPathStatus.PathComplete
            )
            {
                continue;
            }

            agent.SetDestination(
                hit.position
            );

            return;
        }

        if (logProblems)
        {
            Debug.LogWarning(
                $"{name}: não conseguiu encontrar um novo destino de wander.",
                this
            );
        }
    }
}