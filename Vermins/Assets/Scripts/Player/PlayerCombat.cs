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
///
/// Desde que entrou o projetil, "bater" virou duas coisas separadas no
/// tempo. Aqui eu so decido lancar e mando a animacao comecar; a bola
/// nasce depois, no frame em que a mao solta, por um Animation Event que
/// chama SoltarProjetil. Isso consertou de quebra um defeito antigo: o
/// dano era aplicado ANTES da animacao comecar, entao a magia acertava
/// com o braco ainda abaixado.
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

    [Header("Magia")]
    [Tooltip("A bola que sai da mao. Sem prefab aqui o ataque volta a " +
             "tirar vida na hora, de longe e sem nada aparecer - " +
             "funciona, mas e o comportamento antigo.")]
    [SerializeField] private Projetil projetil;

    [Tooltip("Contra o que testar se a visao esta limpa. Deixei so o " +
             "Obstaculo porque no blockout as 50 paredes estao nele e o " +
             "chao esta na ground - se eu incluisse a Default, os " +
             "proprios inimigos e metade da cenografia barrariam o " +
             "tiro. Quem colocar parede nova fora da Obstaculo tem que " +
             "lembrar de marcar aqui, senao da pra atirar atraves dela.")]
    [SerializeField] private LayerMask oQueBloqueiaVisao = 1 << 3;

    [Tooltip("Sobe o raio da linha de visao. Deixei em zero porque o " +
             "transform do personagem JA esta na altura do peito, e nao " +
             "no pe: o baseOffset do agente e 0,92 e a capsula de 2 m " +
             "esta centrada nele. Como o alvo usa a mesma convencao, o " +
             "raio ja sai reto de peito a peito e nao raspa no chao. " +
             "Sobrou como knob pro dia em que entrar um inimigo baixo " +
             "demais e o raio comecar a passar por cima dele.")]
    [SerializeField] private float alturaDoRaio;

    [Header("Perseguicao")]
    [Tooltip("So refaz o caminho quando o alvo anda mais que isso. " +
             "Sem isso a gente calcularia rota nova todo frame, e com " +
             "uma horda na tela isso pesa.")]
    [SerializeField] private float repathThreshold = 0.25f;

    private PlayerMotor motor;
    private MeleeAttack weapon;
    private Health ownHealth;
    private Transform mao;

    private Health target;
    private NavMeshAgent targetAgent;
    private Vector3 lastChasePoint;
    private float nextAttackTime;

    // O alvo travado no comeco do golpe. A bola so nasce meio segundo
    // depois, no Animation Event, e nesse meio tempo o jogador pode ter
    // clicado em outro bicho - o golpe que ja comecou termina em quem o
    // comecou, senao a magia troca de destino no meio do gesto.
    private Health alvoDoCast;

    // Vigia do Animation Event. Se o clipe nao tiver o evento, o
    // personagem faz o gesto e nao sai nada - e o tipo de coisa que a
    // gente passa meia hora procurando no lugar errado. Aqui eu percebo
    // e reclamo em vez de deixar quieto.
    private bool esperandoOEvento;
    private float prazoDoEvento;
    private bool jaReclameiDoEvento;

    public Health Target => target;
    public bool HasTarget => target != null && !target.IsDead;

    /// <summary>
    /// Quanto tempo entre um golpe e o proximo.
    ///
    /// Tem setter porque a Celeridade da build mexe nisto. Quem escreve
    /// aqui nao precisa mexer na animacao: o PlayerAnimator recalcula a
    /// velocidade do clipe de magia a cada golpe a partir deste numero.
    /// O piso de 0,1 s existe pra nenhuma build conseguir pedir um
    /// cooldown zero e disparar infinitos golpes por frame.
    /// </summary>
    public float AttackCooldown
    {
        get => attackCooldown;
        set => attackCooldown = Mathf.Max(0.1f, value);
    }

    /// <summary>
    /// De quao longe o golpe sai. Tem setter pelo mesmo motivo: e o que
    /// o atributo Alcance mexe.
    /// </summary>
    public float AttackRange
    {
        get => attackRange;
        set => attackRange = Mathf.Max(0.5f, value);
    }

    /// <summary>
    /// Disparado quando o golpe COMECA, e nao quando acerta.
    ///
    /// Mudou de significado quando entrou o projetil. Antes o dano ja
    /// tinha sido aplicado quando isto disparava, o que era justamente o
    /// defeito: a magia acertava antes de a mao subir. Hoje isto e o
    /// "comecou o cast" - quem quiser reagir ao dano que aconteceu deve
    /// escutar o Health.OnDamaged do alvo, que e onde a verdade mora.
    /// </summary>
    public event System.Action<Health> OnAttack;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        weapon = GetComponent<MeleeAttack>();
        ownHealth = GetComponent<Health>();

        // O avatar do Mixamo e Humanoid, entao da pra pedir a mao pelo
        // nome do osso em vez de deixar um campo pra alguem arrastar e
        // esquecer. Se um dia o modelo virar Generic isto devolve null e
        // a magia sai do peito, que e feio mas nao quebra.
        Animator animator = GetComponentInChildren<Animator>();

        if (animator != null && animator.isHuman)
            mao = animator.GetBoneTransform(HumanBodyBones.RightHand);
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

    /// <summary>
    /// Faz a bola nascer. Quem chama e o Animation Event do clipe de
    /// magia, atraves do EventoDeAnimacao que mora no modelo - o Mecanim
    /// so alcanca componente que esteja no mesmo objeto do Animator.
    ///
    /// A mira e calculada AGORA e nao no comeco do golpe. Ou seja: a
    /// bola sai apontada pra onde o bicho esta no instante em que a mao
    /// solta, e dali em diante voa reto. Se ele desviar durante o voo,
    /// erra - e isso que faz o alcance de 9 m custar alguma coisa.
    /// </summary>
    public void SoltarProjetil()
    {
        esperandoOEvento = false;

        if (ownHealth != null && ownHealth.IsDead)
            return;

        Vector3 origem = OrigemDaMagia();

        bool alvoVivo = alvoDoCast != null && !alvoDoCast.IsDead;

        // Alvo que morreu ou sumiu no meio do gesto nao cancela o golpe:
        // a bola sai pra frente e morre no ar. Cancelar deixava o
        // personagem terminando a animacao de mao vazia.
        Vector3 mira = alvoVivo
            ? alvoDoCast.transform.position
            : origem + transform.forward;

        if (projetil == null)
        {
            AvisarSemProjetil();

            // Sem prefab volto pro comportamento antigo, de tirar vida
            // na hora. Feio, mas melhor que o ataque simplesmente nao
            // funcionar pra quem abrir a cena sem o prefab ligado.
            if (alvoVivo)
                weapon.TryHit(alvoDoCast);

            return;
        }

        Projetil bola = Instantiate(projetil, origem, Quaternion.identity);
        bola.Lancar(mira - origem, weapon.Damage, gameObject);
    }

    private bool jaReclameiDoPrefab;

    private void AvisarSemProjetil()
    {
        if (jaReclameiDoPrefab)
            return;

        jaReclameiDoPrefab = true;
        Debug.LogWarning(
            $"[{name}] Nao tem prefab de projetil ligado no PlayerCombat. " +
            "O ataque volta a tirar vida na hora, de 9 m, sem nada sair " +
            "da mao.", this);
    }

    /// <summary>
    /// De onde a bola nasce. A mao do rig quando existe; o peito virado
    /// pra frente quando nao existe.
    /// </summary>
    private Vector3 OrigemDaMagia()
    {
        if (mao != null)
            return mao.position;

        return transform.position + transform.forward * 0.5f;
    }

    /// <summary>
    /// Se o golpe comecou e o Animation Event nunca chegou, o clipe nao
    /// tem o evento. Reclamo uma vez e solto a bola do mesmo jeito, pra
    /// o jogo continuar jogavel enquanto ninguem arruma o clipe.
    /// </summary>
    private void VigiarOEvento()
    {
        if (!esperandoOEvento || Time.time < prazoDoEvento)
            return;

        if (!jaReclameiDoEvento)
        {
            jaReclameiDoEvento = true;
            Debug.LogWarning(
                $"[{name}] O golpe comecou e o Animation Event nunca chegou. " +
                "Falta o evento 'SoltarMagia' no clipe de magia: abre o FBX, " +
                "aba Animation, poe o evento no frame em que a mao solta. " +
                "Enquanto isso a bola sai atrasada, no fim do cooldown.", this);
        }

        SoltarProjetil();
    }

    private void Update()
    {
        if (ownHealth != null && ownHealth.IsDead)
        {
            // Morreu no meio do gesto: o golpe nao sai.
            esperandoOEvento = false;
            ClearTarget();
            return;
        }

        VigiarOEvento();

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
            Chase(alvo, attackRange * 0.8f);
            return;
        }

        // Sem visao limpa eu nao lanco, e de proposito NAO gasto o
        // cooldown: o jogador chega mais perto pra contornar a parede e
        // atira assim que abrir. Se gastasse, ele ficaria plantado
        // fazendo gesto pro muro no ritmo do cooldown.
        if (!VisaoLimpa(target))
        {
            Chase(alvo, attackRange * 0.3f);
            return;
        }

        StopChasing();
        FaceTarget(alvo);

        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        alvoDoCast = target;
        esperandoOEvento = true;

        // Dou o cooldown inteiro de prazo pro evento chegar. E folgado
        // de proposito: o clipe cabe dentro do cooldown, entao se
        // passou disso o evento nao existe mesmo.
        prazoDoEvento = Time.time + attackCooldown;

        OnAttack?.Invoke(target);
    }

    /// <summary>
    /// Tem parede entre eu e o alvo?
    ///
    /// Testo so contra a Obstaculo. Nao da pra testar contra tudo porque
    /// os personagens estao na mesma Default de metade do cenario, e ai
    /// um inimigo passando na frente cancelaria o tiro do outro.
    ///
    /// Nao somo altura nenhuma por padrao: o transform do personagem ja
    /// esta na altura do peito, nao no pe (baseOffset 0,92, capsula de 2
    /// m centrada). Como os dois lados usam a mesma convencao, o raio ja
    /// sai reto de peito a peito.
    /// </summary>
    private bool VisaoLimpa(Health alvo)
    {
        Vector3 de = transform.position + Vector3.up * alturaDoRaio;
        Vector3 para = alvo.transform.position + Vector3.up * alturaDoRaio;

        return !Physics.Linecast(de, para, oQueBloqueiaVisao,
                                 QueryTriggerInteraction.Ignore);
    }

    private void Chase(Vector3 alvo, float distanciaDeParada)
    {
        Vector3 ponto = ChasePoint(alvo, distanciaDeParada);

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
    /// A segunda: paro antes de andar pra dentro do inimigo. Fica melhor
    /// de ver e evita os dois agentes ficarem se empurrando.
    ///
    /// A distancia de parada e argumento e nao constante porque os dois
    /// motivos de perseguir querem numeros diferentes: quando e so falta
    /// de alcance, paro na beirada; quando e parede na frente, preciso
    /// chegar bem mais perto pra dobrar a quina e enxergar o bicho.
    /// </summary>
    private Vector3 ChasePoint(Vector3 alvo, float distanciaDeParada)
    {
        if (targetAgent != null)
            alvo.y -= targetAgent.baseOffset;

        Vector3 direcao = alvo - transform.position;
        direcao.y = 0f;

        if (direcao.sqrMagnitude < 0.0001f)
            return alvo;

        return alvo - direcao.normalized * distanciaDeParada;
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
