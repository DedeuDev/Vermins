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

    [Header("Camera")]
    [Tooltip("Deixe vazio pra usar a Camera.main.")]
    [SerializeField] private Camera viewCamera;

    private InputSystem_Actions input;
    private PlayerMotor motor;

    /// <summary>
    /// Disparado toda vez que o jogador manda andar pra um ponto.
    /// Serve pra VFX de destino, som de passo, tutorial, o que for -
    /// sem ninguem precisar mexer aqui dentro.
    /// </summary>
    public static event System.Action<Vector3> OnMoveOrdered;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
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
    }

    private void OnDisable()
    {
        input.Player.Disable();
    }

    private void OnDestroy()
    {
        input.Dispose();
    }

    private void Update()
    {
        // Segurar o botao continua andando, igual ARPG. Nao e so
        // no clique.
        if (!input.Player.MoveTo.IsPressed())
            return;

        if (IsPointerOverUI())
            return;

        TryMoveToPointer();
    }

    private void TryMoveToPointer()
    {
        if (viewCamera == null)
            return;

        Vector2 screenPosition = input.Player.Point.ReadValue<Vector2>();
        Ray ray = viewCamera.ScreenPointToRay(screenPosition);

        bool hitGround = Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxRayDistance,
            groundMask
        );

        if (!hitGround)
            return;

        if (!motor.MoveTo(hit.point))
            return;

        OnMoveOrdered?.Invoke(hit.point);
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
