using UnityEngine;
using UnityEngine.AI;

// Seguro o Monge na arena para ele nao interromper os outros testes.
[RequireComponent(typeof(BossMonge), typeof(NavMeshAgent))]
public sealed class SandboxBossArena : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Vector3 arenaCenter = new Vector3(19f, 0f, 19f);
    [SerializeField] private float activationRadius = 8f;
    private BossMonge monk;
    private NavMeshAgent agent;
    private BossStats stats;

    private void Awake()
    {
        monk = GetComponent<BossMonge>();
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<BossStats>();
    }

    private void Update()
    {
        Vector3 offset = player.position - arenaCenter;
        offset.y = 0f;
        bool active = !stats.EstaDerrotado && offset.sqrMagnitude <= activationRadius * activationRadius;
        if (monk.enabled != active) monk.enabled = active;
        if (!active && agent.isOnNavMesh && !agent.isStopped)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }
}
