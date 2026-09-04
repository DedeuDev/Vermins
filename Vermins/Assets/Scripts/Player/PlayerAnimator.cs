using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Liga a animacao na velocidade real do NavMeshAgent.
///
/// Nao le input e nao decide nada: se o agente esta indo a 3 m/s pra
/// frente e 1 m/s pro lado, ele avisa o Animator disso e o blend tree
/// escolhe a mistura de andar, correr, esprintar e andar de lado. Do
/// mesmo jeito que o PlayerMotor nao sabe do mouse, aqui nao se sabe se
/// quem mandou andar foi o clique, o PlayerCombat ou uma cutscene - a
/// animacao sai certa nos tres casos de graca.
///
/// Manda a velocidade em DUAS componentes, em espaco local, e nao um
/// numero so. O motivo e que o corpo nao aponta pra onde ele anda o tempo
/// todo: numa curva de 90 o corpo leva 0,2 s pra alcancar a direcao nova,
/// e numa inversao de 180 leva 0,32 s. Nesse meio tempo ele anda de lado.
/// Com uma componente so a animacao so sabia dizer "esta indo rapido" e
/// tocava corrida pra frente enquanto o corpo ia de banda.
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
    private static readonly int VelXId = Animator.StringToHash("VelX");
    private static readonly int VelZId = Animator.StringToHash("VelZ");
    private static readonly int AtacarId = Animator.StringToHash("Atacar");
    private static readonly int MortoId = Animator.StringToHash("Morto");
    private static readonly int VariacaoId = Animator.StringToHash("Variacao");
    private static readonly int VelAtaqueId = Animator.StringToHash("VelAtaque");

    /// <summary>
    /// Folga entre o fim da animacao e o golpe seguinte. Sem ela o clipe
    /// fecharia exatamente em cima do proximo golpe, e num frame ruim os
    /// dois se encavalam.
    /// </summary>
    private const float FolgaDoGolpe = 0.95f;

    // Duracao do clipe de magia mais longo, lida do controller uma vez.
    // Leio em vez de deixar campo serializado porque este numero muda
    // sozinho quando alguem recorta o clipe, e um campo ficaria mentindo
    // sem ninguem notar.
    private float duracaoDoAtaque;

    private NavMeshAgent agent;
    private Health health;
    private PlayerCombat combate;

    // Qual das duas magias vai sair no proximo golpe. Alterno porque
    // repetir o mesmo gesto e o que mais faz parecer bonequinho.
    private int variacao;

    // Amorteco o MODULO da velocidade, nunca a direcao.
    //
    // Cheguei nisso errando. Primeiro amorteci o vetor inteiro, achando
    // que estava protegendo o modulo. So que 0,06 s de atraso e da mesma
    // ordem do tempo de uma virada, e ai o vetor atrasado ainda aponta
    // pra direcao velha enquanto o corpo ja girou pra nova. Visto de
    // dentro do corpo isso inverte o sinal do lado: medi uma curva de 90
    // pra direita em que o corpo andava 2,21 m/s pra DIREITA e a arvore
    // pedia 0,72 m/s de passo pra ESQUERDA. Ficava pior que o blend 1D,
    // que pelo menos nao chutava lado nenhum.
    //
    // A direcao nao precisa de amortecimento: quem a suaviza ja e a
    // aceleracao do NavMeshAgent. Quem tremia era o modulo perto do zero,
    // e e so ele que eu seguro aqui.
    private float moduloSuave;
    private float aceleracaoSuave;

    // Ultima direcao valida. Quando o agente para, a velocidade zera e
    // nao sobra direcao pra normalizar. Sem guardar esta, VelX e VelZ
    // cairiam pra zero de um frame pro outro e o personagem pularia da
    // corrida pro parado; guardando, o modulo desce sozinho e o blend
    // desce a rampa ate o Idle.
    private Vector3 direcaoMundo = Vector3.forward;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        combate = GetComponent<PlayerCombat>();

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

        string faltando = ParametroQueFalta();

        if (faltando != null)
        {
            Debug.LogError($"[{name}] O Animator Controller nao tem o float " +
                           $"'{faltando}'. Rode o menu " +
                           "Vermins/Player/Montar Animator.", this);
            enabled = false;
            return;
        }

        MedirOClipeDeAtaque();
    }

    /// <summary>
    /// Acha a duracao do clipe de magia mais longo dentro do controller.
    ///
    /// Pego o mais longo e nao o primeiro porque a velocidade tem que
    /// caber os DOIS ataques no cooldown; dimensionando pelo curto, o
    /// longo estouraria.
    ///
    /// Se nao achar, aviso mas nao desligo o componente. Sem este numero
    /// o ataque toca na velocidade natural - fica feio se o cooldown for
    /// curto, mas locomocao, morte e o resto continuam funcionando, e
    /// derrubar tudo por causa disso seria pior.
    /// </summary>
    private void MedirOClipeDeAtaque()
    {
        RuntimeAnimatorController rac = animator.runtimeAnimatorController;

        if (rac != null)
        {
            foreach (AnimationClip c in rac.animationClips)
            {
                if (c != null && c.name.Contains("MagicAttack") && c.length > duracaoDoAtaque)
                    duracaoDoAtaque = c.length;
            }
        }

        if (duracaoDoAtaque <= 0f)
        {
            Debug.LogWarning($"[{name}] Nao achei clipe de magia no controller, " +
                             "entao nao sei encurtar o golpe pra caber no cooldown. " +
                             "Rode Vermins/Player/Montar Animator.", this);
        }
    }

    /// <summary>
    /// Quantas vezes mais rapido o estado de ataque tem que tocar pra
    /// caber entre um golpe e o proximo.
    ///
    /// Os clipes de magia tem 2,2 s e soltam perto do fim. Com cooldown
    /// menor que isso, o golpe seguinte reiniciaria a animacao antes dela
    /// chegar a lancar e o personagem ficaria carregando pra sempre.
    ///
    /// Nunca devolvo menos que 1: se um dia o cooldown ficar mais longo
    /// que o clipe, o certo e o personagem esperar parado, nao se mexer
    /// em camera lenta como se estivesse na agua.
    /// </summary>
    private float VelocidadeDoGolpe()
    {
        if (duracaoDoAtaque <= 0f || combate == null)
            return 1f;

        float cooldown = combate.AttackCooldown;

        if (cooldown <= 0f)
            return 1f;

        return Mathf.Max(1f, duracaoDoAtaque / (cooldown * FolgaDoGolpe));
    }

    private void OnEnable()
    {
        if (combate != null)
            combate.OnAttack += Golpear;
    }

    private void OnDisable()
    {
        if (combate != null)
            combate.OnAttack -= Golpear;
    }

    /// <summary>
    /// O PlayerCombat avisa no golpe que acertou, e aqui vira animacao.
    ///
    /// Vale saber que o dano ja foi aplicado quando isto chega: o
    /// PlayerCombat tira a vida e SO ENTAO dispara o evento. Entao a
    /// magia acerta um pouco antes de a mao terminar de lancar. Cortei os
    /// clipes pra encurtar essa distancia (o disparo cai a meio segundo
    /// do inicio do golpe em vez de a um e meio), mas ela nao zera daqui.
    /// Zerar exige o dano sair de um evento de animacao, e o dano hoje
    /// mora no MeleeAttack, que o inimigo tambem usa - nao vou mexer nele
    /// so por causa da animacao do player.
    /// </summary>
    private void Golpear(Health _)
    {
        if (health != null && health.IsDead)
            return;

        // Recalculo a cada golpe em vez de uma vez no Awake porque a
        // Celeridade da build mexe no cooldown, e um dia um buff vai
        // mexer no meio da luta. Uma divisao por golpe nao custa nada.
        animator.SetFloat(VelAtaqueId, VelocidadeDoGolpe());

        animator.SetFloat(VariacaoId, variacao);
        animator.SetTrigger(AtacarId);

        variacao = 1 - variacao;
    }

    private void Update()
    {
        // Bool e nao trigger de proposito: assim o Revive desliga isto
        // sozinho e o corpo levanta, sem ninguem precisar lembrar de
        // mandar um aviso separado no respawn.
        animator.SetBool(MortoId, health != null && health.IsDead);

        Vector3 velocidade = VelocidadeNoChao();

        if (velocidade.sqrMagnitude > 0f)
            direcaoMundo = velocidade.normalized;

        moduloSuave = Mathf.SmoothDamp(
            moduloSuave,
            velocidade.magnitude,
            ref aceleracaoSuave,
            suavizacao,
            Mathf.Infinity,
            Time.deltaTime);

        Vector3 local = transform.InverseTransformDirection(direcaoMundo) * moduloSuave;

        animator.SetFloat(VelXId, local.x);
        animator.SetFloat(VelZId, local.z);

        // O Speed sobra do blend tree 1D e eu mantive de proposito. Uma
        // condicao de transicao do Mecanim so sabe comparar UM parametro,
        // entao nao da pra escrever "quando parar" a partir de VelX e VelZ
        // juntos. Quando entrar o estado de ataque, e por aqui que ele vai
        // saber se o personagem esta parado.
        animator.SetFloat(SpeedId, moduloSuave);
    }

    private Vector3 VelocidadeNoChao()
    {
        if (health != null && health.IsDead)
            return Vector3.zero;

        Vector3 velocidade = agent.velocity;

        // So o plano interessa. Rampa e degrau do NavMesh metem Y na
        // conta e isso viraria "corrida" numa descida.
        velocidade.y = 0f;

        return velocidade.magnitude < velocidadeMinima ? Vector3.zero : velocidade;
    }

    /// <summary>
    /// Devolve o nome do primeiro parametro que falta no controller, ou
    /// null se estiver tudo la.
    /// </summary>
    private bool TemParametro(int id, AnimatorControllerParameterType tipo)
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == id && p.type == tipo)
                return true;
        }

        return false;
    }

    private string ParametroQueFalta()
    {
        foreach (int id in new[] { SpeedId, VelXId, VelZId, VariacaoId })
        {
            bool achou = false;

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == id && p.type == AnimatorControllerParameterType.Float)
                {
                    achou = true;
                    break;
                }
            }

            if (!achou)
            {
                return id == SpeedId ? "Speed"
                     : id == VelXId ? "VelX"
                     : id == VelZId ? "VelZ"
                     : "Variacao";
            }
        }

        if (!TemParametro(AtacarId, AnimatorControllerParameterType.Trigger))
            return "Atacar";

        if (!TemParametro(MortoId, AnimatorControllerParameterType.Bool))
            return "Morto";

        return null;
    }
}
