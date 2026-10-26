using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Le o input e traduz em ordens pro PlayerMotor.
///
/// Tudo passa pelo asset InputSystem_Actions em vez de ler
/// Mouse.current direto. Da mais trabalho agora, mas quando a gente
/// precisar remapear tecla, ou ligar a tela de opcoes, ou travar o
/// input durante um dialogo, ja vai estar no lugar certo.
///
/// Os controles seguem o Diablo 4 e o Path of Exile 2: o botao
/// esquerdo faz tudo, e o que ele faz depende do que esta embaixo do
/// cursor quando desce - NPC interage, inimigo ataca, chao anda. Antes
/// era o direito que andava, herdado do Movement.cs antigo, e o
/// esquerdo so atacava. O direito agora solta a habilidade (Ability), fora
/// da fila do esquerdo: ele nao olha o que esta embaixo do cursor, so
/// manda a habilidade pro ponto do chao.
/// </summary>
[RequireComponent(typeof(PlayerMotor))]
public class PlayerController : MonoBehaviour
{
    [Header("Clique pra mover")]
    [Tooltip("So o chao conta como destino. Sem isso, clicar numa " +
             "parede ou num inimigo faz o jogador tentar andar pra " +
             "dentro dele.")]
    [SerializeField] private LayerMask groundMask;

    [SerializeField] private float maxRayDistance = 200f;

    [Header("Clique pra atacar")]
    [Tooltip("Tudo menos a Ignore Raycast. Nao existe layer de inimigo " +
             "ainda, entao em vez de filtrar por layer eu exijo um " +
             "Health no que foi clicado - o que nao tem vida nao vira " +
             "alvo. Quando a gente criar a layer, e so apertar aqui.")]
    [SerializeField] private LayerMask attackMask = ~(1 << 2);

    [Header("Camera")]
    [Tooltip("Deixe vazio pra usar a Camera.main.")]
    [SerializeField] private Camera viewCamera;

    [Header("Comando guardado")]
    [Tooltip("Por quanto tempo a esquiva apertada antes da hora fica " +
             "guardada. Apertando ate isso antes da recarga acabar, ela " +
             "sai sozinha assim que puder, em vez de se perder.")]
    [SerializeField] private float dodgeBufferWindow = 0.2f;

    [Tooltip("O mesmo da esquiva, pra habilidade apertada na recarga. " +
             "Apertada no meio do rolamento, ela espera o fim dele.")]
    [SerializeField] private float abilityBufferWindow = 0.2f;

    [Header("Habilidade")]
    [Tooltip("O que o botao direito solta. Vazio pega a primeira Ability " +
             "do Player.")]
    [SerializeField] private Ability rightClickAbility;

    private InputSystem_Actions input;
    private PlayerMotor motor;
    private Health health;
    private PlayerCombat combat;
    private PotionBelt belt;
    private PlayerInteractor interactor;
    private Dodge dodge;

    /// <summary>
    /// O que o clique decidiu quando o botao desceu. Segurar o botao
    /// repete essa ordem em vez de olhar de novo o que esta embaixo do
    /// cursor - se olhasse, andar segurando o botao por cima de um
    /// inimigo virava ataque sem querer, e quem queria fugir parava pra
    /// brigar.
    /// </summary>
    private enum Order { None, Move, Attack, StandingAttack }

    /// <summary>O que esta embaixo do mouse, pro cursor contextual.</summary>
    public enum CursorTarget { Nothing, Ground, Enemy, Interactable }

    private Order order;

    // Comandos dados quando o personagem ainda nao podia obedecer. Os
    // dois jogos que a gente copia guardam o comando e executam assim que
    // da; aqui eles se perdiam. Medi com clique e tecla simulados: um
    // clique no chao 0,2 s depois do rolamento comecar nao fazia nada (o
    // Player terminou a 9,46 m do ponto, parado), e a esquiva apertada
    // 0,15 s antes da recarga acabar nao saia.
    private bool hasBufferedClick;
    private Ray bufferedClick;
    private float bufferedDodgeUntil = -1f;
    private Vector3 bufferedDodgePoint;
    private float bufferedAbilityUntil = -1f;
    private Vector3 bufferedAbilityPoint;

    /// <summary>
    /// Disparado toda vez que o jogador manda andar pra um ponto.
    /// Serve pra VFX de destino, som de passo, tutorial, o que for -
    /// sem ninguem precisar mexer aqui dentro.
    /// </summary>
    public static event System.Action<Vector3> OnMoveOrdered;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombat>();
        belt = GetComponent<PotionBelt>();
        interactor = GetComponent<PlayerInteractor>();
        dodge = GetComponent<Dodge>();
        input = new InputSystem_Actions();

        if (rightClickAbility == null)
            rightClickAbility = GetComponent<Ability>();

        if (viewCamera == null)
            viewCamera = Camera.main;

        if (viewCamera == null)
        {
            Debug.LogError(
                $"{name}: nenhuma camera encontrada. Marque a camera " +
                $"da cena como MainCamera ou preencha o campo View Camera.",
                this
            );
        }
    }

    private void OnEnable()
    {
        input.Player.Enable();

        if (health != null)
            health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        input.Player.Disable();

        if (health != null)
            health.OnDied -= HandleDied;
    }

    private void OnDestroy()
    {
        input.Dispose();
    }

    private void Update()
    {
        // Morto nao anda nem bate. Checo aqui em vez de desligar o
        // componente porque assim, quando o Revive existir, o controle
        // volta sozinho sem ninguem precisar religar nada.
        if (health != null && health.IsDead)
            return;

        // Pausado tambem nao. O menu de pausa so zera o timeScale, e o que
        // e instantaneo, como beber pocao, continuaria funcionando com o
        // jogo parado.
        if (Time.timeScale == 0f)
            return;

        // Pocao e toque unico: WasPressedThisFrame, e nao IsPressed como o
        // clique. Segurando o Q, o IsPressed beberia uma pocao por frame
        // ate a vida encher. Fica antes do filtro de clique porque e
        // tecla - da pra beber andando, brigando ou com o mouse na UI.
        if (belt != null && input.Player.DrinkPotion.WasPressedThisFrame())
            belt.TryDrink();

        if (viewCamera == null)
            return;

        // Dodge vai pro lado do mouse, esteja ele no chao, num inimigo
        // ou em cima da UI - e tecla, nao clique.
        if (dodge != null)
            TryBufferedDodge();

        // Antes do return do rolamento: apertada rolando, a habilidade tem
        // que ficar guardada, igual ao clique.
        if (rightClickAbility != null)
            TryBufferedAbility();

        // Rolando, o clique espera. Nao zero a ordem: se o botao continua
        // segurado, o jogador volta a andar ou a bater sozinho no fim do
        // rolamento, que e o que se espera de quem rolou no meio da briga.
        // Clique novo no meio do rolamento fica guardado e vale no fim.
        if (dodge != null && dodge.IsDodging)
        {
            if (input.Player.Click.WasPressedThisFrame() && !IsPointerOverUI())
            {
                // Guardo o raio e nao a posicao na tela: a camera anda
                // junto com o rolamento, e o mesmo pixel ja estaria
                // apontando pra outro lugar no fim.
                bufferedClick = CursorRay();
                hasBufferedClick = true;
            }

            return;
        }

        if (hasBufferedClick)
        {
            hasBufferedClick = false;
            order = DecideOrder(bufferedClick);
            return;
        }

        if (input.Player.Click.WasPressedThisFrame())
        {
            order = IsPointerOverUI() ? Order.None : DecideOrder(CursorRay());
            return;
        }

        // Segurar o botao continua valendo, igual ARPG. Nao e so no
        // clique.
        if (!input.Player.Click.IsPressed())
        {
            order = Order.None;
            return;
        }

        if (IsPointerOverUI())
            return;

        if (order == Order.Move)
            TryMoveToPointer(CursorRay());
        else if (order == Order.Attack)
            TryAttackAtPointer(CursorRay());
        else if (order == Order.StandingAttack)
            combat.StandingAttack(GroundPoint(CursorRay()));
    }

    /// <summary>
    /// Olha o que esta embaixo do cursor e da a ordem certa. A
    /// prioridade e interagivel, depois inimigo, depois chao.
    /// </summary>
    private Order DecideOrder(Ray ray)
    {
        // Shift manda mais que tudo: e o "fica parado" do Diablo 4 e do
        // Path of Exile 2. O personagem golpeia pro lado do cursor sem
        // andar, tenha inimigo, NPC ou chao embaixo. Como o resto, vale o
        // que estava apertado no clique: soltar o Shift com o botao ainda
        // segurado nao faz ele sair andando.
        if (combat != null && input.Player.AtacarParado.IsPressed())
        {
            if (interactor != null)
                interactor.ClearTarget();

            combat.StandingAttack(GroundPoint(ray));
            return Order.StandingAttack;
        }

        // NPC primeiro. O collider do CaptainGuard e trigger, e os raios
        // do ataque e do chao ignoram trigger: se o interactor nao olhasse
        // antes, o clique no NPC atravessava ele e virava passo pro chao
        // de tras.
        if (interactor != null && interactor.TryInteract(ray))
        {
            if (combat != null)
            {
                combat.ClearTarget();
                combat.InterruptAttack();
            }

            return Order.None;
        }

        if (TryAttackAtPointer(ray))
            return Order.Attack;

        // Clique no vazio tambem vira Move: se a pessoa arrastar o
        // cursor pro chao segurando o botao, o jogador vai atras.
        TryMoveToPointer(ray);
        return Order.Move;
    }

    /// <summary>
    /// Dodge e tecla: vai pro lado do mouse, esteja ele no chao, num
    /// inimigo ou em cima da UI. Apertada na recarga, fica guardada pela
    /// dodgeBufferWindow e sai sozinha quando der, pro lado em que o mouse
    /// estava no aperto.
    /// </summary>
    private void TryBufferedDodge()
    {
        if (input.Player.Esquiva.WasPressedThisFrame())
        {
            bufferedDodgePoint = CursorGroundPoint();
            bufferedDodgeUntil = Time.time + dodgeBufferWindow;
        }

        if (Time.time > bufferedDodgeUntil || !dodge.TryDodge(bufferedDodgePoint))
            return;

        bufferedDodgeUntil = -1f;

        // A esquiva nova corta tudo, entao o clique guardado na anterior
        // tambem cai. Vale o comando mais novo.
        hasBufferedClick = false;

        // E a habilidade guardada antes dela tambem.
        bufferedAbilityUntil = -1f;
    }

    /// <summary>
    /// Botao direito: solta a habilidade no ponto do chao embaixo do mouse.
    /// Apertado na recarga, fica guardado pela abilityBufferWindow, como a
    /// esquiva. Apertado rolando, espera o rolamento acabar e sai no fim,
    /// pro ponto em que o mouse estava no aperto.
    /// </summary>
    private void TryBufferedAbility()
    {
        // E botao do mouse, entao clique na UI nao conta, ao contrario da
        // esquiva, que e tecla.
        if (input.Player.Ability1.WasPressedThisFrame() && !IsPointerOverUI())
        {
            bufferedAbilityPoint = CursorGroundPoint();
            bufferedAbilityUntil = Time.time + abilityBufferWindow;
        }

        if (bufferedAbilityUntil < 0f)
            return;

        // O rolamento dura 0,5 s, mais que a janela. Sem empurrar o prazo,
        // a habilidade apertada no comeco dele venceria antes do fim.
        if (dodge != null && dodge.IsDodging)
        {
            bufferedAbilityUntil = Mathf.Max(bufferedAbilityUntil, Time.time + abilityBufferWindow);
            return;
        }

        if (Time.time > bufferedAbilityUntil)
        {
            bufferedAbilityUntil = -1f;
            return;
        }

        if (rightClickAbility.TryUse(bufferedAbilityPoint))
            bufferedAbilityUntil = -1f;
    }

    private Ray CursorRay()
    {
        Vector2 screenPosition = input.Player.Point.ReadValue<Vector2>();
        return viewCamera.ScreenPointToRay(screenPosition);
    }

    /// <summary>
    /// O que um clique faria agora, sem dar ordem nenhuma. O
    /// ContextualCursor usa isto pra trocar o cursor. Passa pelos mesmos
    /// testes do DecideOrder e na mesma ordem, entao o cursor nunca
    /// promete uma coisa e o clique faz outra.
    /// </summary>
    public CursorTarget GetCursorTarget(out Ray ray, out Health inimigo)
    {
        ray = default;
        inimigo = null;

        if (viewCamera == null || IsPointerOverUI())
            return CursorTarget.Nothing;

        ray = CursorRay();

        if (interactor != null && interactor.TemInteragivel(ray))
            return CursorTarget.Interactable;

        if (combat != null && FindEnemy(ray, out inimigo))
            return CursorTarget.Enemy;

        return CursorTarget.Ground;
    }

    /// <summary>
    /// Onde o mouse esta no chao. Sem chao embaixo (mouse no ceu, fora do
    /// mapa), um metro na frente do jogador.
    /// </summary>
    private Vector3 CursorGroundPoint()
    {
        return GroundPoint(CursorRay());
    }

    private Vector3 GroundPoint(Ray ray)
    {
        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxRayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return transform.position + transform.forward;
    }

    private void TryMoveToPointer(Ray ray)
    {
        // Trigger nunca e destino de clique. Hoje a groundMask sozinha
        // ja daria conta, porque ela so aceita a layer do chao - isso
        // aqui e pra quando alguem puser uma zona de agua ou de dano
        // nessa mesma layer e o clique parar de responder sem motivo
        // aparente.
        bool hitGround = Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxRayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (!hitGround)
            return;

        if (!motor.MoveTo(hit.point))
            return;

        // Mandar andar desiste do alvo. Sem isso o PlayerCombat
        // sobrescreveria o destino no frame seguinte e o jogador nao
        // conseguiria fugir de uma briga. E corta o golpe que estiver no
        // meio, senao as pernas terminam o gesto com o corpo deslizando.
        if (combat != null)
        {
            combat.ClearTarget();
            combat.InterruptAttack();
        }

        // Mesma coisa com a ida ate um NPC.
        if (interactor != null)
            interactor.ClearTarget();

        OnMoveOrdered?.Invoke(hit.point);
    }

    /// <summary>
    /// Escolhe em quem bater. Quem persegue e da o golpe e o
    /// PlayerCombat - aqui so traduzo o clique num alvo. Devolve false
    /// quando nao tinha inimigo embaixo do cursor.
    /// </summary>
    private bool TryAttackAtPointer(Ray ray)
    {
        if (combat == null)
            return false;

        if (!FindEnemy(ray, out Health alvo))
            return false;

        combat.SetTarget(alvo);

        if (interactor != null)
            interactor.ClearTarget();

        return true;
    }

    /// <summary>
    /// Acha um inimigo vivo embaixo do raio. Separado do
    /// TryAttackAtPointer pra o cursor usar o mesmo teste do clique.
    /// </summary>
    private bool FindEnemy(Ray ray, out Health alvo)
    {
        alvo = null;

        // Sem isto o clique nao acha alvo nenhum dentro da dungeon. Os
        // modulos tem um BoxCollider "PlacementBounds" que e trigger,
        // esta na layer Default e cobre a sala ate 3 m de altura - e a
        // attackMask aceita Default. Testei com uma caixa igual: o raio
        // batia nela, que nao tem Health, e o ataque morria ali.
        bool acertouAlgo = Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxRayDistance,
            attackMask,
            QueryTriggerInteraction.Ignore
        );

        if (!acertouAlgo)
            return false;

        // InParent porque o collider costuma estar num filho e a vida
        // no objeto raiz.
        Health achado = hit.collider.GetComponentInParent<Health>();

        if (achado == null || achado == health || achado.IsDead)
            return false;

        alvo = achado;
        return true;
    }

    /// <summary>
    /// O jogador para na hora que morre. Se ele estivesse no meio de
    /// um caminho, o NavMeshAgent continuaria andando com o corpo caido.
    /// </summary>
    private void HandleDied(Health _)
    {
        motor.Stop();

        if (interactor != null)
            interactor.ClearTarget();
    }

    /// <summary>
    /// Evita que um clique num botao da interface tambem mova o
    /// jogador. Ainda nao temos UI, mas quando tiver isso ja resolve.
    /// </summary>
    private static bool IsPointerOverUI()
    {
        // Os controles do HUD de debug sao OnGUI, e o EventSystem nao
        // enxerga OnGUI.
        if (PlayerDebugHUD.IsCursorOverControls)
            return true;

        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }
}
