using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera interactionCamera;

    [Header("Interaction")]
    [Tooltip(
        "Distância máxima para poder interagir " +
        "com o objeto clicado."
    )]
    [Min(0.1f)]
    [SerializeField]
    private float interactionDistance = 4f;

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
        if (!Input.GetMouseButtonDown(0))
            return;

        /*
         * Evita interagir com objetos do mundo
         * quando o jogador estiver clicando em UI.
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
        // RAY A PARTIR DO CURSOR
        // ========================================

        Ray ray =
            interactionCamera.ScreenPointToRay(
                Input.mousePosition
            );

        RaycastHit hit;

        bool foundObject =
            Physics.Raycast(
                ray,
                out hit,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide
            );

        if (!foundObject)
            return;

        // ========================================
        // PROCURA IINTERACTABLE
        // ========================================

        MonoBehaviour[] behaviours =
            hit.collider.GetComponentsInParent<MonoBehaviour>(
                true
            );

        foreach (MonoBehaviour behaviour in behaviours)
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
}