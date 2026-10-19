using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Rolamento no Espaco, como o evade do Diablo 4: o jogador avanca uns
/// metros na direcao do mouse, fica invulneravel durante o avanco e corta
/// o golpe que estiver fazendo.
///
/// Quem anda aqui continua sendo o NavMeshAgent, pelo agent.Move. O Move
/// respeita a borda do NavMesh, entao rolar contra a parede para na
/// parede em vez de atravessar. O destino tambem e cortado antes pelo
/// NavMesh.Raycast, pra a animacao nao prometer 4 m e o corpo andar 1.
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
    [SerializeField] private float distancia = 4f;

    [Tooltip("Espera entre apertar e o corpo comecar a andar. E o " +
             "agachar do clipe antes do mergulho.")]
    [SerializeField] private float inicioDoAvanco = 0.05f;

    [Tooltip("Quanto tempo o corpo leva pra andar a distancia toda. Casa " +
             "com o mergulho e o rolamento do clipe na velocidade da " +
             "duracaoDaAnimacao.")]
    [SerializeField] private float duracaoDoAvanco = 0.45f;

    [Tooltip("Quanto a animacao inteira dura na tela. O clipe tem 1,63 s " +
             "e o PlayerAnimator acelera ele pra caber aqui.")]
    [SerializeField] private float duracaoDaAnimacao = 0.8f;

    [Tooltip("Tempo minimo entre uma esquiva e a proxima, contado do " +
             "aperto.")]
    [SerializeField] private float recarga = 1f;

    private NavMeshAgent agent;
    private PlayerMotor motor;
    private PlayerCombat combat;
    private PlayerInteractor interactor;
    private Health health;

    private float inicio = -100f;
    private Vector3 direcao;
    private float distanciaLivre;
    private float andado;

    // Velocidade com que o corpo entra e sai do rolamento, na direcao
    // dele. Correndo e 3,2; parado ou rolando pra tras e zero.
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
    public event System.Action<float> OnEsquivou;

    /// <summary>
    /// Verdadeiro enquanto o jogador nao manda no personagem. O
    /// PlayerController ignora clique nesse tempo.
    ///
    /// Acaba junto com o avanco. Antes voltava 0,05 s depois, e medi o
    /// corpo parado 4 frames esperando o clique segurado valer.
    /// </summary>
    public bool EmEsquiva => Time.time < inicio + inicioDoAvanco + duracaoDoAvanco;

    public bool Pronta => Time.time >= inicio + recarga;

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

        inicio = -100f;
        advancing = false;
    }

    /// <summary>
    /// Rola na direcao do ponto, que e onde o mouse esta no chao. Devolve
    /// false se ainda estiver na recarga ou nao tiver como rolar.
    /// </summary>
    public bool TentarEsquivar(Vector3 ponto)
    {
        if (!Pronta || health.IsDead || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        Vector3 dir = ponto - transform.position;
        dir.y = 0f;

        // Mouse em cima do proprio personagem: rola pra frente.
        if (dir.sqrMagnitude < 0.09f)
            dir = transform.forward;

        direcao = dir.normalized;

        // Corto o destino no NavMesh antes de sair, pra o rolamento parar
        // na parede e nao escorregar por ela. Meco so no plano: o pivo fica
        // 0,92 m acima do NavMesh, e em 3D essa altura entrava na conta.
        // Parede a 0,5 m: planejava 1,04 m e batia na borda a 3,5 m/s.
        Vector3 fim = transform.position + direcao * distancia;
        distanciaLivre = NavMesh.Raycast(transform.position, fim, out NavMeshHit hit, NavMesh.AllAreas)
            ? Vector3.ProjectOnPlane(hit.position - transform.position, Vector3.up).magnitude
            : distancia;

        // Dodge corta tudo: o golpe no meio, o alvo, a ida ate o NPC e o
        // caminho que o agente estava fazendo.
        if (combat != null)
        {
            combat.InterromperGolpe();
            combat.ClearTarget();
        }

        if (interactor != null)
            interactor.ClearTarget();

        // O embalo que o corpo traz, so na direcao do rolamento. Leio antes
        // do Stop. Rolar pra tras de uma corrida da zero: ai ele sai do
        // agachado como se estivesse parado.
        entrySpeed = Mathf.Clamp(Vector3.Dot(agent.velocity, direcao), 0f, agent.speed);

        // Contra a parede, o corpo chega parado. Sem parede, ele sai com a
        // mesma velocidade com que entrou, e quem rolou correndo continua
        // correndo.
        exitSpeed = distanciaLivre < distancia ? 0f : entrySpeed;

        crouchDistance = Mathf.Min(entrySpeed * inicioDoAvanco, distanciaLivre);

        motor.Stop();

        // O Stop so freia: o agente continuava andando sozinho por 4
        // frames (3,2, 2,1 e 1,0 m/s) por cima do Move. Zero na hora.
        agent.velocity = Vector3.zero;

        // Vira de uma vez. O giro amortecido do PlayerMotor leva quase
        // 0,1 s, e nesse tempo o corpo ja estaria rolando de lado.
        transform.rotation = Quaternion.LookRotation(direcao);

        inicio = Time.time;
        andado = 0f;
        advancing = true;

        // Ligo em meu nome. O Health so desliga quando nenhum dono quiser
        // mais, entao o fim do rolamento nao apaga a invulnerabilidade de
        // outra fonte, e outra fonte acabando nao corta a do rolamento.
        health.LigarInvulnerabilidade(this);

        OnEsquivou?.Invoke(duracaoDaAnimacao);
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

        float t = Time.time - inicio;
        float alvo = DistanciaNoTempo(t);

        agent.Move(direcao * (alvo - andado));
        andado = alvo;

        if (t < inicioDoAvanco + duracaoDoAvanco)
            return;

        // Fim do avanco. Entrego o embalo pro agente: se o clique ainda
        // estiver segurado, ele segue a 3,2 em vez de arrancar do zero; se
        // nao, o proprio agente freia. Medi 0,06 s e uns 10 cm de deslize.
        advancing = false;
        agent.velocity = direcao * exitSpeed;

        // Invulneravel so enquanto anda. Levantando ele ja pode levar
        // golpe: senao dava pra ficar rolando e nunca apanhar.
        health.DesligarInvulnerabilidade(this);
    }

    /// <summary>
    /// Quanto o corpo ja andou t segundos depois do aperto.
    ///
    /// No agachar ele segue no embalo que trouxe. Depois e uma curva
    /// cubica (Hermite) que sai na velocidade de entrada e chega na de
    /// saida. Parado, as duas sao zero e ela vira o smoothstep de antes:
    /// sai do agachado, voa e freia rolando. Correndo, o smoothstep
    /// derrubava o corpo de 3,2 pra 0 duas vezes, e medi 192 ms por
    /// rolamento abaixo da velocidade de corrida.
    ///
    /// No ultimo frame o tempo passa do fim e eu fecho em 1: antes o resto
    /// da curva se perdia se o frame pulasse o fim.
    /// </summary>
    private float DistanciaNoTempo(float t)
    {
        if (t < inicioDoAvanco)
            return Mathf.Min(entrySpeed * t, crouchDistance);

        float resto = distanciaLivre - crouchDistance;
        float p = Mathf.Clamp01((t - inicioDoAvanco) / duracaoDoAvanco);

        // Velocidades em metros por "curva inteira". O teto de 2x o resto
        // garante que a curva nunca volta pra tras, que e o que acontece
        // com rolamento curto e embalo alto (parede a 0,5 m correndo).
        float m0 = Mathf.Min(entrySpeed * duracaoDoAvanco, 2f * resto);
        float m1 = Mathf.Min(exitSpeed * duracaoDoAvanco, 2f * resto);

        float p2 = p * p;
        float p3 = p2 * p;

        float curva = (p3 - 2f * p2 + p) * m0
                    + (3f * p2 - 2f * p3) * resto
                    + (p3 - p2) * m1;

        return crouchDistance + curva;
    }
}
