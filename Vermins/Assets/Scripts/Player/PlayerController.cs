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
/// Botao direito move, porque o esquerdo ja esta reservado pro
/// Attack no asset.
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

    private InputSystem_Actions input;
    private PlayerMotor motor;
    private Health health;
    private PlayerCombat combat;

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
        input = new InputSystem_Actions();

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

        // Segurar o botao continua valendo, igual ARPG. Nao e so no
        // clique.
        bool atacando = combat != null && input.Player.Attack.IsPressed();
        bool andando = input.Player.MoveTo.IsPressed();

        if (!atacando && !andando)
            return;

        if (IsPointerOverUI())
            return;

        if (atacando)
            TryAttackAtPointer();

        if (andando)
            TryMoveToPointer();
    }

    private void TryMoveToPointer()
    {
        if (viewCamera == null)
            return;

        Vector2 screenPosition = input.Player.Point.ReadValue<Vector2>();
        Ray ray = viewCamera.ScreenPointToRay(screenPosition);

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
        // conseguiria fugir de uma briga.
        if (combat != null)
            combat.ClearTarget();

        OnMoveOrdered?.Invoke(hit.point);
    }

    /// <summary>
    /// Botao esquerdo escolhe em quem bater. Quem persegue e da o golpe
    /// e o PlayerCombat - aqui so traduzo o clique num alvo.
    /// </summary>
    private void TryAttackAtPointer()
    {
        if (viewCamera == null)
            return;

        Vector2 screenPosition = input.Player.Point.ReadValue<Vector2>();
        Ray ray = viewCamera.ScreenPointToRay(screenPosition);

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
            return;

        // InParent porque o collider costuma estar num filho e a vida
        // no objeto raiz.
        Health alvo = hit.collider.GetComponentInParent<Health>();

        if (alvo == null || alvo == health || alvo.IsDead)
            return;

        combat.SetTarget(alvo);
    }

    /// <summary>
    /// O jogador para na hora que morre. Se ele estivesse no meio de
    /// um caminho, o NavMeshAgent continuaria andando com o corpo caido.
    /// </summary>
    private void HandleDied(Health _)
    {
        motor.Stop();
    }

    /// <summary>
    /// Evita que um clique num botao da interface tambem mova o
    /// jogador. Ainda nao temos UI, mas quando tiver isso ja resolve.
    /// </summary>
    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }
}
