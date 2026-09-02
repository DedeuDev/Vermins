using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Liga a animacao na velocidade real do NavMeshAgent.
///
/// Nao le input e nao decide nada: se o agente esta andando a 3 m/s, ele
/// avisa o Animator que esta a 3 m/s, e o blend tree escolhe entre andar,
/// correr e esprintar. Do mesmo jeito que o PlayerMotor nao sabe do mouse,
/// aqui nao se sabe se quem mandou andar foi o clique, o PlayerCombat ou
/// uma cutscene - a animacao sai certa nos tres casos de graca.
///
/// O detalhe que mais importa e usar agent.velocity e nao
/// agent.desiredVelocity. desiredVelocity e o que o agente QUERIA fazer;
/// se ele estiver preso numa quina ou empurrado por outro agente, ele
/// continua querendo correr enquanto o corpo esta parado, e o personagem
/// pedala no lugar. velocity e o que aconteceu de verdade.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class PlayerAnimator : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Deixe vazio pra pegar o Animator do modelo filho sozinho.")]
    [SerializeField] private Animator animator;

    [Header("Suavizacao")]
    [Tooltip("Segundos pra velocidade da animacao alcancar a do agente. " +
             "Medi o descompasso somado num trajeto de 38 m: com 0,12 da " +
             "1,28 m de escorregao, com 0,06 da 0,64 m. Abaixo disso o " +
             "blend comeca a trancar.")]
    [SerializeField] private float suavizacao = 0.06f;

    [Tooltip("Abaixo disso eu trato como parado. O NavMeshAgent nunca zera " +
             "a velocidade de verdade e sem esse corte o personagem fica " +
             "tremendo entre parado e andando.")]
    [SerializeField] private float velocidadeMinima = 0.1f;

    private static readonly int SpeedId = Animator.StringToHash("Speed");

    private NavMeshAgent agent;
    private Health health;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError($"[{name}] Nao achei Animator nenhum. O modelo do " +
                           "personagem precisa estar como filho do Player.", this);
            enabled = false;
            return;
        }

        // Quem move o personagem e o NavMeshAgent. Se o root motion ficar
        // ligado, os dois empurram ao mesmo tempo e o corpo descola da
        // posicao que o agente acha que ele tem.
        animator.applyRootMotion = false;

        if (!TemParametroDeVelocidade())
        {
            Debug.LogError($"[{name}] O Animator Controller nao tem o float " +
                           "'Speed'. Sem ele o personagem fica congelado.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        animator.SetFloat(SpeedId, VelocidadeNoChao(), suavizacao, Time.deltaTime);
    }

    private float VelocidadeNoChao()
    {
        if (health != null && health.IsDead)
            return 0f;

        Vector3 velocidade = agent.velocity;

        // So o plano interessa. Rampa e degrau do NavMesh metem Y na
        // conta e isso viraria "corrida" numa descida.
        velocidade.y = 0f;

        float modulo = velocidade.magnitude;

        return modulo < velocidadeMinima ? 0f : modulo;
    }

    private bool TemParametroDeVelocidade()
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == SpeedId && p.type == AnimatorControllerParameterType.Float)
                return true;
        }

        return false;
    }
}
