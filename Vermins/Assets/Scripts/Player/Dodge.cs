using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// Rolamento no Espaco, como o evade do Diablo 4: o jogador avanca uns
/// metros na direcao do mouse, fica invulneravel durante o avanco e corta
/// o golpe que estiver fazendo.
///
/// Quem anda aqui continua sendo o NavMeshAgent, pelo agent.Move. O Move
/// respeita a borda do NavMesh, entao rolar contra a parede para na
/// parede em vez de atravessar.
///
/// Copiei o Path of Exile 2: distancia e tempo fixos, mais chao no comeco
/// que no fim, e a parede so corta o caminho. O corpo chega nela na
/// velocidade normal, para ali, e o clipe termina no lugar na mesma
/// velocidade de sempre. Antes eu encolhia a curva ate a parede e ele
/// rolava em camera lenta; depois encolhi o tempo junto e ele ficava
/// acelerado.
///
/// A animacao e o DiveForward do Mixamo, que rola no lugar (In Place). O
/// deslocamento sai todo daqui, e os tempos abaixo foram casados com o
/// clipe: ele mergulha aos 0,13 s e termina de rolar em 1,0 s. Tocado a
/// 2x (duracao de 0,8 s), isso cai de 0,06 s a 0,5 s na tela, e e nessa
/// janela que o corpo anda.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(Health))]
public class Dodge : MonoBehaviour
{
    [Tooltip("Quanto o rolamento anda, em metros, se nada estiver no " +
             "caminho.")]
    [FormerlySerializedAs("distancia")]
    [SerializeField] private float distance = 4f;

    [Tooltip("Espera entre apertar e o corpo comecar a andar. E o " +
             "agachar do clipe antes do mergulho.")]
    [FormerlySerializedAs("inicioDoAvanco")]
    [SerializeField] private float advanceDelay = 0.05f;

    [Tooltip("Quanto tempo o corpo leva pra andar a distancia toda. Casa " +
             "com o mergulho e o rolamento do clipe na velocidade da " +
             "animationDuration.")]
    [FormerlySerializedAs("duracaoDoAvanco")]
    [SerializeField] private float advanceDuration = 0.45f;

    [Tooltip("Quanto a animacao inteira dura na tela. O clipe tem 1,63 s " +
             "e o PlayerAnimator acelera ele pra caber aqui.")]
    [FormerlySerializedAs("duracaoDaAnimacao")]
    [SerializeField] private float animationDuration = 0.8f;

    [Tooltip("Tempo minimo entre uma esquiva e a proxima, contado do " +
             "aperto.")]
    [FormerlySerializedAs("recarga")]
    [SerializeField] private float cooldown = 1f;

    private NavMeshAgent agent;
    private PlayerMotor motor;
    private PlayerCombat combat;
    private PlayerInteractor interactor;
    private Health health;

    private float startTime = -100f;
    private Vector3 direction;
    private float freeDistance;
    private float traveled;

    // Velocidade com que o corpo entra e sai do rolamento, na direcao
    // dele. Correndo e a velocidade de corrida; parado ou rolando pra
    // tras e zero.
    private float entrySpeed;
    private float exitSpeed;

    // Distancia andada no agachar, antes da curva comecar.
    private float crouchDistance;

    // Se o avanco ainda nao entregou o corpo de volta pro agente.
    private bool advancing;

    /// <summary>
    /// Disparado no aperto, com quanto tempo a animacao tem que durar. O
    /// PlayerAnimator escuta pra tocar o rolamento na velocidade certa.
    /// </summary>
    public event System.Action<float> OnDodged;

    /// <summary>
    /// Verdadeiro enquanto o jogador nao manda no personagem. O
    /// PlayerController ignora clique nesse tempo.
    ///
    /// Acaba junto com o avanco. Antes voltava 0,05 s depois, e medi o
    /// corpo parado 4 frames esperando o clique segurado valer.
    /// </summary>
    public bool IsDodging => Time.time < startTime + advanceDelay + advanceDuration;

    public bool IsReady => Time.time >= startTime + cooldown;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        motor = GetComponent<PlayerMotor>();
        combat = GetComponent<PlayerCombat>();
        interactor = GetComponent<PlayerInteractor>();
        health = GetComponent<Health>();
    }

    private void OnDisable()
    {
        // Desligado no meio do rolamento, o jogador ficaria imortal pra
        // sempre.
        health.DesligarInvulnerabilidade(this);

        startTime = -100f;
        advancing = false;
    }

    /// <summary>
    /// Rola na direcao do ponto, que e onde o mouse esta no chao. Devolve
    /// false se ainda estiver na recarga ou nao tiver como rolar.
    /// </summary>
    public bool TryDodge(Vector3 ponto)
    {
        if (!IsReady || health.IsDead || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        Vector3 dir = ponto - transform.position;
        dir.y = 0f;

        // Mouse em cima do proprio personagem: rola pra frente.
        if (dir.sqrMagnitude < 0.09f)
            dir = transform.forward;

        direction = dir.normalized;

        // Corto o destino no NavMesh antes de sair, pra o rolamento parar
        // na parede e nao escorregar por ela. Meco so no plano: o pivo fica
        // 0,92 m acima do NavMesh, e em 3D essa altura entrava na conta.
        // Parede a 0,5 m: planejava 1,04 m e batia na borda a 3,5 m/s.
        Vector3 fim = transform.position + direction * distance;
        freeDistance = NavMesh.Raycast(transform.position, fim, out NavMeshHit hit, NavMesh.AllAreas)
            ? Vector3.ProjectOnPlane(hit.position - transform.position, Vector3.up).magnitude
            : distance;

        // Dodge corta tudo: o golpe no meio, o alvo, a ida ate o NPC e o
        // caminho que o agente estava fazendo.
        if (combat != null)
        {
            combat.InterruptAttack();
            combat.ClearTarget();
        }

        if (interactor != null)
            interactor.ClearTarget();

        // O embalo que o corpo traz, so na direcao do rolamento. Leio antes
        // do Stop. Rolar pra tras de uma corrida da zero: ai ele sai do
        // agachado como se estivesse parado.
        entrySpeed = Mathf.Clamp(Vector3.Dot(agent.velocity, direction), 0f, agent.speed);

        // Sem parede, ele sai com a mesma velocidade com que entrou, e quem
        // rolou correndo continua correndo. Contra a parede ele ja esta
        // parado nela.
        exitSpeed = freeDistance < distance ? 0f : entrySpeed;

        crouchDistance = Mathf.Min(entrySpeed * advanceDelay, distance);

        motor.Stop();

        // O Stop so freia: o agente continuava andando sozinho por 4
        // frames (3,2, 2,1 e 1,0 m/s) por cima do Move. Zero na hora.
        agent.velocity = Vector3.zero;

        // Vira de uma vez. O giro amortecido do PlayerMotor leva quase
        // 0,1 s, e nesse tempo o corpo ja estaria rolando de lado.
        transform.rotation = Quaternion.LookRotation(direction);

        startTime = Time.time;
        traveled = 0f;
        advancing = true;

        // Ligo em meu nome. O Health so desliga quando nenhum dono quiser
        // mais, entao o fim do rolamento nao apaga a invulnerabilidade de
        // outra fonte, e outra fonte acabando nao corta a do rolamento.
        health.LigarInvulnerabilidade(this);

        OnDodged?.Invoke(animationDuration);
        return true;
    }

    private void Update()
    {
        if (!advancing)
            return;

        if (!agent.isOnNavMesh)
        {
            advancing = false;
            health.DesligarInvulnerabilidade(this);
            return;
        }

        float t = Time.time - startTime;

        // A curva e sempre a dos 4 m; a parede so corta onde ela esta.
        float alvo = Mathf.Min(DistanceAt(t), freeDistance);

        agent.Move(direction * (alvo - traveled));
        traveled = alvo;

        if (t < advanceDelay + advanceDuration)
            return;

        // Fim do avanco. Entrego o embalo pro agente: se o clique ainda
        // estiver segurado, ele segue correndo em vez de arrancar do zero; se
        // nao, o proprio agente freia. Medi 0,06 s e uns 10 cm de deslize.
        advancing = false;
        agent.velocity = direction * exitSpeed;

        // Invulneravel so enquanto anda. Levantando ele ja pode levar
        // golpe: senao dava pra ficar rolando e nunca apanhar.
        health.DesligarInvulnerabilidade(this);
    }

    /// <summary>
    /// Quanto o corpo ja andou t segundos depois do aperto, sem contar a
    /// parede.
    ///
    /// No agachar ele segue no embalo que trouxe. No mergulho a velocidade
    /// pula pro pico e dali so diminui ate a de saida, como o rolamento do
    /// Path of Exile 2, que cobre mais chao no comeco que no fim. E uma
    /// curva cubica (Hermite): sai a 1,5x a media e chega na de saida.
    /// Com 1,5x o pico fica igual ao do smoothstep de antes (13,3 parado)
    /// e a velocidade nunca sobe depois do mergulho.
    ///
    /// No ultimo frame o tempo passa do fim e eu fecho em 1: o resto da
    /// curva nao se perde se o frame pular o fim.
    /// </summary>
    private float DistanceAt(float t)
    {
        if (t < advanceDelay)
            return Mathf.Min(entrySpeed * t, crouchDistance);

        float resto = distance - crouchDistance;
        float p = Mathf.Clamp01((t - advanceDelay) / advanceDuration);

        // Velocidades em metros por "curva inteira". A de chegada e a de
        // entrada mesmo contra a parede: a curva e a do chao aberto, e a
        // parede so corta.
        float m0 = 1.5f * resto;
        float m1 = Mathf.Min(entrySpeed * advanceDuration, resto);

        float p2 = p * p;
        float p3 = p2 * p;

        float curva = (p3 - 2f * p2 + p) * m0
                    + (3f * p2 - 2f * p3) * resto
                    + (p3 - p2) * m1;

        return crouchDistance + curva;
    }
}
