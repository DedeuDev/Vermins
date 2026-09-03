using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Persegue o alvo, chega no alcance, vira de frente e bate no ritmo
/// do cooldown.
///
/// Nao le input de proposito - quem escolhe o alvo e o PlayerController,
/// igual quem escolhe pra onde andar. Assim, quando uma quest ou um
/// script de cutscene precisar mandar o jogador atacar alguem, e so
/// chamar SetTarget e nao precisa fingir um clique de mouse.
///
/// Quem realmente tira a vida e o MeleeAttack, o mesmo componente que
/// o inimigo usa. Aqui so mora a decisao de QUANDO bater.
/// </summary>
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(MeleeAttack))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Ataque")]
    [Tooltip("Distancia de centro a centro. Era 2, de corpo a corpo, e " +
             "subiu pra 9 porque o jogador lanca magia e nao bate de " +
             "perto. O inimigo alcanca 1,5 m, entao o jogador tem uns " +
             "tres golpes antes do bicho encostar - e isso que faz valer " +
             "a pena recuar em vez de ficar parado trocando dano. " +
             "Duas coisas que ainda nao existem e ficam mais visiveis " +
             "com o alcance grande: nao tem projetil (o dano acontece na " +
             "hora, a 9 m de distancia, sem nada sair da mao) e nao tem " +
             "linha de visao (da pra atacar atraves de parede).")]
    [SerializeField] private float attackRange = 9f;

    [Tooltip("Segundos entre um golpe e o proximo. Era 0,8 e subiu pra " +
             "1,2 quando entrou a animacao de magia: o clipe leva 2,2 s e " +
             "solta a magia perto do fim, entao com 0,8 o golpe seguinte " +
             "reiniciava a animacao antes dela chegar a soltar - o " +
             "personagem ficava carregando pra sempre. " +
             "Quem mexer aqui nao precisa mexer na animacao: o menu " +
             "Vermins/Player/Montar Animator le este numero e acerta a " +
             "velocidade dos clipes de ataque pra caberem. So rode ele " +
             "depois.")]
    [SerializeField] private float attackCooldown = 1.2f;

    [Tooltip("Graus por segundo pra virar de frente pro alvo. O " +
             "NavMeshAgent so gira quem esta andando, entao parado " +
             "quem gira e este script.")]
    [SerializeField] private float turnSpeed = 720f;

    [Header("Perseguicao")]
    [Tooltip("So refaz o caminho quando o alvo anda mais que isso. " +
             "Sem isso a gente calcularia rota nova todo frame, e com " +
             "uma horda na tela isso pesa.")]
    [SerializeField] private float repathThreshold = 0.25f;

    private PlayerMotor motor;
    private MeleeAttack weapon;
    private Health ownHealth;

    private Health target;
    private NavMeshAgent targetAgent;
    private Vector3 lastChasePoint;
    private float nextAttackTime;

    public Health Target => target;
    public bool HasTarget => target != null && !target.IsDead;

    /// <summary>
    /// Quanto tempo entre um golpe e o proximo. Quem le isto e o montador
    /// do Animator, pra acertar a velocidade do clipe de ataque.
    /// </summary>
    public float AttackCooldown => attackCooldown;

    /// <summary>
    /// Disparado no golpe que acertou. Serve pra animacao, som e
    /// tremida de camera - sem ninguem precisar mexer aqui dentro.
    /// </summary>
    public event System.Action<Health> OnAttack;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        weapon = GetComponent<MeleeAttack>();
        ownHealth = GetComponent<Health>();
    }

    /// <summary>Manda perseguir e bater. Ignora alvo morto ou nulo.</summary>
    public void SetTarget(Health novoAlvo)
    {
        if (novoAlvo == null || novoAlvo.IsDead)
            return;

        target = novoAlvo;

        // Guardo o agente do alvo porque preciso da altura dele pra
        // achar o chao. Alvo sem agente (um barril, por exemplo) ja
        // esta no chao e nao precisa de desconto.
        targetAgent = novoAlvo.GetComponent<NavMeshAgent>();

        // Forca o primeiro calculo de rota, senao o repathThreshold
        // podia engolir ele.
        lastChasePoint = Vector3.positiveInfinity;
    }

    /// <summary>Desiste do alvo. E isso que o clique de andar chama.</summary>
    public void ClearTarget()
    {
        target = null;
        targetAgent = null;
    }

    private void Update()
    {
        if (ownHealth != null && ownHealth.IsDead)
        {
            ClearTarget();
            return;
        }

        if (target == null)
            return;

        if (target.IsDead)
        {
            // Matou. Para de perseguir e fica onde esta - nao faz
            // sentido continuar andando pra cima de um cadaver.
            ClearTarget();
            StopChasing();
            return;
        }

        Vector3 alvo = target.transform.position;
        float distancia = Vector3.Distance(transform.position, alvo);

        if (distancia > attackRange)
        {
            Chase(alvo);
            return;
        }

        StopChasing();
        FaceTarget(alvo);

        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        if (weapon.TryHit(target))
            OnAttack?.Invoke(target);
    }

    private void Chase(Vector3 alvo)
    {
        Vector3 ponto = ChasePoint(alvo);

        if ((ponto - lastChasePoint).sqrMagnitude <= repathThreshold * repathThreshold)
            return;

        // So marco como pedido se o pedido deu certo. Se eu marcasse
        // antes, uma falha do NavMesh travava a perseguicao pra sempre,
        // porque o proximo frame acharia que ja tinha pedido esse ponto.
        if (motor.MoveTo(ponto))
            lastChasePoint = ponto;
    }

    /// <summary>
    /// Pra onde andar pra encostar no alvo.
    ///
    /// Duas correcoes moram aqui. A primeira: o transform de um
    /// personagem fica na altura do corpo e o NavMesh fica no chao,
    /// entao desconto a altura do agente do alvo - sem isso o
    /// SamplePosition nao acha nada e o jogador nao sai do lugar.
    ///
    /// A segunda: paro na beirada do alcance em vez de andar pra dentro
    /// do inimigo. Fica melhor de ver e evita os dois agentes ficarem
    /// se empurrando.
    /// </summary>
    private Vector3 ChasePoint(Vector3 alvo)
    {
        if (targetAgent != null)
            alvo.y -= targetAgent.baseOffset;

        Vector3 direcao = alvo - transform.position;
        direcao.y = 0f;

        if (direcao.sqrMagnitude < 0.0001f)
            return alvo;

        return alvo - direcao.normalized * (attackRange * 0.8f);
    }

    private void StopChasing()
    {
        if (motor.IsMoving)
            motor.Stop();
    }

    private void FaceTarget(Vector3 alvo)
    {
        Vector3 direcao = alvo - transform.position;
        direcao.y = 0f;

        if (direcao.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(direcao),
            turnSpeed * Time.deltaTime
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.transform.position);
        }
    }
}
