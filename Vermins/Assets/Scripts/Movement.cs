using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    private NavMeshAgent agent;

    [Header("Movement Settings")]
    public float moveSpeed = 10f;

    [SerializeField] float sampleDistance = 0.5f;

    public static event System.Action<Vector3> OnGroundTouch;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
    }

    void Update()
    {
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            Ray ray = Camera.main.ScreenPointToRay(mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (NavMesh.SamplePosition(
                    hit.point,
                    out NavMeshHit navMeshHit,
                    sampleDistance,
                    NavMesh.AllAreas))
                {
                    agent.SetDestination(navMeshHit.position);

                    OnGroundTouch?.Invoke(navMeshHit.position);
                }
            }
        }
    }
}