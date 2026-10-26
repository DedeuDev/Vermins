using UnityEngine;

// Ian: quem le o clique agora e so o PlayerController. Os controles
// seguem o esquema do Diablo 4: o botao esquerdo anda, ataca ou interage,
// conforme o que estiver embaixo do cursor. Antes este script lia o
// botao esquerdo por conta propria e o PlayerController tambem, entao o
// mesmo clique no NPC falava com ele e dava espadada. Aqui ficou a regra
// da interacao: o que conta como interagivel, a distancia e a ida ate o
// NPC quando o clique vem de longe.
[RequireComponent(typeof(PlayerMotor))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip(
        "Distância máxima entre o Player e " +
        "o objeto para permitir interação."
    )]
    [Min(0.1f)]
    [SerializeField]
    private float interactionDistance = 4f;

    [Tooltip(
        "Distância máxima que o Raycast da câmera " +
        "pode percorrer."
    )]
    [Min(1f)]
    [SerializeField]
    private float raycastDistance = 500f;

    [SerializeField]
    private LayerMask interactionLayers = ~0;

    [Header("Debug")]
    [SerializeField]
    private bool logInteraction = false;

    // Ian: o que o jogador clicou de longe e ainda esta indo buscar.
    private IInteractable pendente;
    private Collider colliderDoPendente;

    private PlayerMotor motor;

    // Ian: 16 sobra. Na HUB o raio do centro da tela pega 2 colliders.
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    // ==================================================
    // AWAKE
    // ==================================================

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
    }

    // ==================================================
    // UPDATE
    // ==================================================

    // Ian: so acompanha a ida ate o que foi clicado de longe. Interage
    // quando chega: dentro da distancia E parado. Se interagisse assim
    // que entrasse na distancia, o jogador frearia a 4 m e falaria com o
    // NPC de longe.
    private void Update()
    {
        if (pendente == null)
            return;

        // O objeto pode ter sumido no caminho.
        if (colliderDoPendente == null)
        {
            ClearTarget();
            return;
        }

        if (motor.IsMoving)
            return;

        if (DistanceTo(colliderDoPendente) > interactionDistance)
            return;

        IInteractable alvo = pendente;
        Collider colliderDoAlvo = colliderDoPendente;

        ClearTarget();
        Interact(alvo, colliderDoAlvo);
    }

    // ==================================================
    // INTERAÇÃO
    // ==================================================

    /// <summary>
    /// Ian: o PlayerController chama isto quando o botao esquerdo desce.
    /// Devolve true se o clique pegou algo interagivel, e ai ele nao anda
    /// nem ataca. Perto, interage na hora. Longe, manda o Player ir ate
    /// la e o Update termina o servico.
    /// </summary>
    public bool TryInteract(Ray ray)
    {
        // ========================================
        // RAY DA CAMERA ATÉ O CURSOR
        // ========================================

        if (!AcharNoRaio(
                ray,
                out IInteractable interactable,
                out RaycastHit hit))
            return false;

        // ========================================
        // DISTÂNCIA DO PLAYER AO OBJETO
        // ========================================

        // Ian: mede ate o corpo do objeto, e nao ate onde o clique bateu.
        // O collider do CaptainGuard tem 3,6 m de altura: clicando na
        // cabeca dele, o ponto do clique ficava longe mesmo com o Player
        // encostado.
        if (DistanceTo(hit.collider) <= interactionDistance)
        {
            ClearTarget();
            motor.Stop();
            Interact(interactable, hit.collider);

            return true;
        }

        // Ian: longe demais. Antes o clique morria aqui; agora o Player
        // vai ate la, igual no Diablo 4. Paro na metade da distancia de
        // interacao: perto o bastante pra parecer conversa, e com folga
        // pra continuar valendo com o NPC em cima de um degrau.
        Transform corpo = ((Component)interactable).transform;

        if (!motor.Chase(
                corpo.position,
                interactionDistance * 0.5f))
        {
            if (logInteraction)
            {
                Debug.Log(
                    "Sem caminho até: " +
                    hit.collider.gameObject.name,
                    hit.collider.gameObject
                );
            }

            // Mesmo sem caminho o clique foi no NPC, entao nao vira
            // ataque nem passo.
            return true;
        }

        pendente = interactable;
        colliderDoPendente = hit.collider;

        if (logInteraction)
        {
            Debug.Log(
                "Indo até: " +
                hit.collider.gameObject.name,
                hit.collider.gameObject
            );
        }

        return true;
    }

    /// <summary>
    /// Ian: mesmo raio do TryInteract, so que sem interagir nem andar.
    /// O cursor usa isto pra mostrar o balao em cima do NPC.
    /// </summary>
    public bool TemInteragivel(Ray ray)
    {
        return AcharNoRaio(ray, out _, out _);
    }

    /// <summary>
    /// Ian: o primeiro interagivel no raio, do mais perto pro mais longe.
    /// Trigger que nao e interagivel nao segura o raio: cada sala da
    /// dungeon tem um PlacementBounds, um trigger do tamanho da sala que
    /// o gerador usa pra ver se as salas se encostam, e com um Raycast so
    /// o clique parava nele e nunca chegava na escada de saida nem no
    /// guarda morto. Collider solido continua segurando, pra ninguem
    /// interagir atraves de parede.
    /// </summary>
    private bool AcharNoRaio(
        Ray ray,
        out IInteractable interactable,
        out RaycastHit hit)
    {
        // Ian: buffer fixo e ordenacao na mao. O cursor chama isto todo
        // frame, e o RaycastAll com o Sort davam 352 B de lixo por chamada
        // (medi na HUB, raio com 2 colliders). O Array.Sort sozinho, mesmo
        // com comparador guardado, ainda dava 128 B; com no maximo 16
        // acertos a insercao e de graca.
        int quantos = Physics.RaycastNonAlloc(
            ray,
            hitBuffer,
            raycastDistance,
            interactionLayers,
            QueryTriggerInteraction.Collide
        );

        for (int i = 1; i < quantos; i++)
        {
            RaycastHit atual = hitBuffer[i];
            int j = i - 1;

            while (j >= 0 && hitBuffer[j].distance > atual.distance)
            {
                hitBuffer[j + 1] = hitBuffer[j];
                j--;
            }

            hitBuffer[j + 1] = atual;
        }

        for (int i = 0; i < quantos; i++)
        {
            RaycastHit h = hitBuffer[i];

            interactable = FindInteractable(h.collider);

            if (interactable != null)
            {
                hit = h;
                return true;
            }

            if (!h.collider.isTrigger)
                break;
        }

        interactable = null;
        hit = default;
        return false;
    }

    /// <summary>
    /// Ian: desiste de ir ate o NPC. O PlayerController chama quando o
    /// jogador manda andar ou atacar no meio do caminho.
    /// </summary>
    public void ClearTarget()
    {
        pendente = null;
        colliderDoPendente = null;
    }

    // Ian: o GetComponentInParent acha o mesmo componente, na mesma ordem,
    // sem montar o array de MonoBehaviour que o GetComponentsInParent montava.
    private IInteractable FindInteractable(Collider collider)
    {
        return collider.GetComponentInParent<IInteractable>(true);
    }

    private float DistanceTo(Collider collider)
    {
        return Vector3.Distance(
            transform.position,
            collider.ClosestPoint(transform.position)
        );
    }

    private void Interact(
        IInteractable interactable,
        Collider collider
    )
    {
        if (logInteraction)
        {
            Debug.Log(
                "Interação com: " +
                collider.gameObject.name,
                collider.gameObject
            );
        }

        interactable.Interact();
    }

    // ==================================================
    // DEBUG
    // ==================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            interactionDistance
        );
    }
}
