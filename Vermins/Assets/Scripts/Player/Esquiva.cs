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
public class Esquiva : MonoBehaviour
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

    [Tooltip("Quando o jogador volta a mandar no personagem, contado do " +
             "aperto. Um pouco depois do fim do avanco: o resto do clipe e " +
             "ele levantando, e ai ja pode sair andando.")]
    [SerializeField] private float devolveOControle = 0.55f;

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

    // So desligo a invulnerabilidade que eu mesmo liguei. Se um dia
    // outra coisa ligar (cena de dialogo, cheat de teste), a esquiva nao
    // pode apagar no fim do rolamento.
    private bool euDeiInvulnerabilidade;

    /// <summary>
    /// Disparado no aperto, com quanto tempo a animacao tem que durar. O
    /// PlayerAnimator escuta pra tocar o rolamento na velocidade certa.
    /// </summary>
    public event System.Action<float> OnEsquivou;

    /// <summary>
    /// Verdadeiro enquanto o jogador nao manda no personagem. O
    /// PlayerController ignora clique nesse tempo.
    /// </summary>
    public bool EmEsquiva => Time.time < inicio + devolveOControle;

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
        if (euDeiInvulnerabilidade)
        {
            health.Invulneravel = false;
            euDeiInvulnerabilidade = false;
        }

        inicio = -100f;
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
        // na parede e nao escorregar por ela.
        Vector3 fim = transform.position + direcao * distancia;
        distanciaLivre = NavMesh.Raycast(transform.position, fim, out NavMeshHit hit, NavMesh.AllAreas)
            ? Vector3.Distance(transform.position, hit.position)
            : distancia;

        // Esquiva corta tudo: o golpe no meio, o alvo, a ida ate o NPC e o
        // caminho que o agente estava fazendo.
        if (combat != null)
        {
            combat.InterromperGolpe();
            combat.ClearTarget();
        }

        if (interactor != null)
            interactor.ClearTarget();

        motor.Stop();

        // Vira de uma vez. O giro amortecido do PlayerMotor leva quase
        // 0,1 s, e nesse tempo o corpo ja estaria rolando de lado.
        transform.rotation = Quaternion.LookRotation(direcao);

        inicio = Time.time;
        andado = 0f;

        // So sou dono se ela estava desligada quando comecei. Antes eu me
        // marcava como dono em todo rolamento: com a invulnerabilidade ja
        // ligada por fora, o Player rolava e 0,8 s depois ela estava
        // desligada, e um golpe de 10 tirou 10. Se eu ja era dono (um
        // rolamento emendado no outro, com a recarga curta), continuo.
        euDeiInvulnerabilidade = euDeiInvulnerabilidade || !health.Invulneravel;
        health.Invulneravel = true;

        OnEsquivou?.Invoke(duracaoDaAnimacao);
        return true;
    }

    private void Update()
    {
        float t = Time.time - inicio;

        if (t > inicioDoAvanco + duracaoDoAvanco)
        {
            // Invulneravel so enquanto anda. Levantando ele ja pode levar
            // golpe: senao dava pra ficar rolando e nunca apanhar.
            if (euDeiInvulnerabilidade)
            {
                health.Invulneravel = false;
                euDeiInvulnerabilidade = false;
            }

            return;
        }

        if (t < inicioDoAvanco || !agent.isOnNavMesh)
            return;

        // Rapido no meio e devagar nas pontas (smoothstep), que e como o
        // corpo do clipe se mexe: sai do agachado, voa e freia rolando.
        float p = Mathf.Clamp01((t - inicioDoAvanco) / duracaoDoAvanco);
        float alvo = p * p * (3f - 2f * p) * distanciaLivre;

        agent.Move(direcao * (alvo - andado));
        andado = alvo;
    }
}
