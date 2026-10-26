using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Troca o cursor do mouse conforme o que esta embaixo dele: espada em
/// inimigo, balao em NPC, mao em item no chao, seta normal no resto.
///
/// Quem decide inimigo e NPC e o PlayerController.GetCursorTarget, o
/// mesmo teste que o clique usa, entao o cursor nunca promete uma coisa
/// e o clique faz outra. O item eu olho aqui, porque o clique nao tem
/// ordem de pegar: clicar no item e andar ate ele, e quem pega e o
/// trigger.
///
/// So chamo o Cursor.SetCursor quando o tipo muda, nao todo frame.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class ContextualCursor : MonoBehaviour
{
    private enum CursorKind { Normal, Attack, Talk, PickUp }

    [FormerlySerializedAs("cursorDeAtaque")]
    [SerializeField] private Texture2D attackCursor;
    [FormerlySerializedAs("pontaDoAtaque")]
    [SerializeField] private Vector2 attackHotspot = new Vector2(3f, 3f);

    [FormerlySerializedAs("cursorDeConversa")]
    [SerializeField] private Texture2D talkCursor;
    [FormerlySerializedAs("pontaDaConversa")]
    [SerializeField] private Vector2 talkHotspot = new Vector2(3f, 3f);

    [FormerlySerializedAs("cursorDePegar")]
    [SerializeField] private Texture2D pickUpCursor;
    [FormerlySerializedAs("pontaDoPegar")]
    [SerializeField] private Vector2 pickUpHotspot = new Vector2(12f, 4f);

    [FormerlySerializedAs("alcanceDoRaio")]
    [SerializeField] private float maxRayDistance = 200f;

    /// <summary>
    /// O inimigo que esta embaixo do mouse neste frame, ou null. O
    /// TargetHighlight le daqui pra nao refazer os mesmos raios.
    /// </summary>
    public Health EnemyUnderMouse { get; private set; }

    /// <summary>
    /// O item no chao que esta embaixo do mouse neste frame, ou null. O
    /// TargetHighlight le daqui pra por o contorno nele.
    /// </summary>
    public Pickup PickupUnderMouse { get; private set; }

    private PlayerController controller;
    private Health health;
    private CursorKind current = CursorKind.Normal;

    // O raio do item atravessa trigger (a pocao e trigger), e na dungeon
    // tem trigger grande cobrindo a sala. Entao pego varios acertos e
    // olho em ordem de distancia, em vez de so o primeiro.
    private readonly RaycastHit[] hits = new RaycastHit[16];

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        health = GetComponent<Health>();
    }

    private void OnDisable()
    {
        // Sem isso, trocar de cena ou morrer com o mouse num inimigo
        // deixava a espada presa no menu.
        Apply(CursorKind.Normal);
    }

    private void Update()
    {
        Apply(Classify());
    }

    private CursorKind Classify()
    {
        EnemyUnderMouse = null;
        PickupUnderMouse = null;

        // Mesmos cortes do Update do PlayerController: morto ou pausado
        // o clique nao faz nada, entao o cursor tambem nao promete nada.
        if (health != null && health.IsDead)
            return CursorKind.Normal;

        if (Time.timeScale == 0f)
            return CursorKind.Normal;

        switch (controller.GetCursorTarget(out Ray ray, out Health inimigo))
        {
            case PlayerController.CursorTarget.Interactable:
                return CursorKind.Talk;

            case PlayerController.CursorTarget.Enemy:
                EnemyUnderMouse = inimigo;
                return CursorKind.Attack;

            case PlayerController.CursorTarget.Ground:
                return HasPickup(ray) ? CursorKind.PickUp : CursorKind.Normal;

            default:
                return CursorKind.Normal;
        }
    }

    private bool HasPickup(Ray ray)
    {
        int quantos = Physics.RaycastNonAlloc(
            ray,
            hits,
            maxRayDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );

        // Ordeno na mao: o Array.Sort com comparador guardado ainda dava
        // 128 B de lixo por frame com o mouse no chao (medi na HUB). Com
        // no maximo o tamanho do buffer, a insercao e de graca.
        for (int i = 1; i < quantos; i++)
        {
            RaycastHit atual = hits[i];
            int j = i - 1;

            while (j >= 0 && hits[j].distance > atual.distance)
            {
                hits[j + 1] = hits[j];
                j--;
            }

            hits[j + 1] = atual;
        }

        for (int i = 0; i < quantos; i++)
        {
            Collider c = hits[i].collider;
            Pickup item = c.GetComponentInParent<Pickup>();

            if (item != null)
            {
                PickupUnderMouse = item;
                return true;
            }

            // Chao, parede ou inimigo na frente: o item atras dele nao
            // conta.
            if (!c.isTrigger)
                return false;
        }

        return false;
    }

    private void Apply(CursorKind tipo)
    {
        if (tipo == current)
            return;

        current = tipo;

        switch (tipo)
        {
            case CursorKind.Attack:
                Cursor.SetCursor(attackCursor, attackHotspot, CursorMode.Auto);
                break;
            case CursorKind.Talk:
                Cursor.SetCursor(talkCursor, talkHotspot, CursorMode.Auto);
                break;
            case CursorKind.PickUp:
                Cursor.SetCursor(pickUpCursor, pickUpHotspot, CursorMode.Auto);
                break;
            default:
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                break;
        }
    }
}
