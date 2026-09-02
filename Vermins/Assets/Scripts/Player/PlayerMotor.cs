using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Cuida so de mover o jogador pelo NavMesh. Nao le input e nao
/// decide pra onde ir - quem manda e o PlayerController.
/// Separei assim porque depois o ataque, a IA e o sistema de quest
/// tambem vao precisar mandar o jogador pra algum lugar, e nenhum
/// deles deveria depender do mouse.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 40f;
    [SerializeField] private float angularSpeed = 720f;

    [Header("NavMesh")]
    [Tooltip("Distancia maxima entre o ponto pedido e o NavMesh pra " +
             "ele ainda valer como destino.")]
    [SerializeField] private float sampleDistance = 1f;

    private NavMeshAgent agent;

    /// <summary>Verdadeiro enquanto o jogador esta indo pra algum lugar.</summary>
    public bool IsMoving =>
        agent.hasPath && agent.remainingDistance > agent.stoppingDistance;

    public Vector3 Destination => agent.destination;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.updateRotation = true;
    }

    /// <summary>
    /// Manda o jogador andar ate um ponto do mundo.
    /// Devolve false quando nao tem NavMesh perto o suficiente,
    /// que e o caso de clique no vazio ou fora do cenario.
    /// </summary>
    public bool MoveTo(Vector3 worldPoint)
    {
        if (!NavMesh.SamplePosition(
                worldPoint,
                out NavMeshHit hit,
                sampleDistance,
                NavMesh.AllAreas))
        {
            return false;
        }

        agent.isStopped = false;
        agent.SetDestination(hit.position);

        return true;
    }

    /// <summary>Para na hora e esquece o caminho atual.</summary>
    public void Stop()
    {
        if (!agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    private void OnDrawGizmosSelected()
    {
        if (agent == null || !agent.hasPath)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(agent.destination, 0.2f);

        Vector3[] corners = agent.path.corners;

        for (int i = 0; i < corners.Length - 1; i++)
        {
            Gizmos.DrawLine(corners[i], corners[i + 1]);
        }
    }
}
