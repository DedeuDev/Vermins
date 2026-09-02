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
    [Tooltip("Casado com a velocidade do clipe de sprint (5,31 m/s). " +
             "Mudar isso sem mexer no blend tree faz o pe deslizar no " +
             "talo, que e onde o personagem mais fica.")]
    [SerializeField] private float moveSpeed = 5.31f;

    [Tooltip("Medi tres valores com a animacao rodando: 40 chega na " +
             "velocidade em 0,12 s e a animacao nao acompanha (pico de " +
             "3,2 m/s de diferenca). 20 chega em 0,25 s, continua seco " +
             "no clique e o pico cai pra 1,2.")]
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float angularSpeed = 720f;

    [Header("NavMesh")]
    [Tooltip("Altura do pivo do personagem acima dos pes dele. " +
             "Nao e 1 de proposito. O NavMesh assado nao fica colado no " +
             "chao: ele nasce meio voxel acima, e o voxel padrao e o raio " +
             "do agente dividido por 3. Com raio 0,5 da 0,083 m. Se eu " +
             "deixasse 1 aqui, o personagem andaria flutuando 8 cm - da " +
             "pra ver, ainda mais com sombra. Medi na cena: chao em 0, " +
             "NavMesh em 0,0833.")]
    [SerializeField] private float baseOffset = 0.9167f;

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
        agent.baseOffset = baseOffset;
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
