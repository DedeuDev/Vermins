using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera interactionCamera;

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

    // ==================================================
    // AWAKE
    // ==================================================

    private void Awake()
    {
        FindCameraIfNeeded();
    }

    // ==================================================
    // UPDATE
    // ==================================================

    private void Update()
    {
        /*
         * New Input System.
         *
         * Verifica se existe um mouse conectado
         * e se o botão esquerdo foi pressionado
         * neste frame.
         */
        if (Mouse.current == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        /*
         * Evita interagir com objetos do mundo
         * quando o clique estiver sobre uma UI.
         */
        if (
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject()
        )
        {
            return;
        }

        TryInteract();
    }

    // ==================================================
    // PROCURA CAMERA
    // ==================================================

    private bool FindCameraIfNeeded()
    {
        if (interactionCamera != null)
            return true;

        interactionCamera = Camera.main;

        return interactionCamera != null;
    }

    // ==================================================
    // INTERAÇÃO
    // ==================================================

    private void TryInteract()
    {
        if (!FindCameraIfNeeded())
        {
            Debug.LogError(
                "PlayerInteractor: nenhuma câmera foi encontrada.",
                this
            );

            return;
        }

        // ========================================
        // POSIÇÃO DO MOUSE
        // NEW INPUT SYSTEM
        // ========================================

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        // ========================================
        // RAY DA CAMERA ATÉ O CURSOR
        // ========================================

        Ray ray =
            interactionCamera.ScreenPointToRay(
                mousePosition
            );

        RaycastHit hit;

        bool foundObject =
            Physics.Raycast(
                ray,
                out hit,
                raycastDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide
            );

        if (!foundObject)
            return;

        // ========================================
        // DISTÂNCIA DO PLAYER AO OBJETO
        // ========================================

        float distanceFromPlayer =
            Vector3.Distance(
                transform.position,
                hit.point
            );

        if (
            distanceFromPlayer >
            interactionDistance
        )
        {
            if (logInteraction)
            {
                Debug.Log(
                    "Objeto interativo está longe demais. " +
                    $"Distância: {distanceFromPlayer:F2}",
                    hit.collider.gameObject
                );
            }

            return;
        }

        // ========================================
        // PROCURA IINTERACTABLE
        // ========================================

        MonoBehaviour[] behaviours =
            hit.collider.GetComponentsInParent<MonoBehaviour>(
                true
            );

        foreach (
            MonoBehaviour behaviour
            in behaviours
        )
        {
            if (behaviour == null)
                continue;

            IInteractable interactable =
                behaviour as IInteractable;

            if (interactable == null)
                continue;

            if (logInteraction)
            {
                Debug.Log(
                    "Interação com: " +
                    hit.collider.gameObject.name,
                    hit.collider.gameObject
                );
            }

            interactable.Interact();

            return;
        }
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