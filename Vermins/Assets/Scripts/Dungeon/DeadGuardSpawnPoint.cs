using UnityEngine;

public class DeadGuardSpawnPoint : MonoBehaviour
{
    [Header("Spawn Point")]
    [SerializeField]
    private bool canSpawn = true;

    [Header("Debug")]
    [SerializeField]
    private bool showGizmo = true;

    [Min(0.05f)]
    [SerializeField]
    private float gizmoRadius = 0.35f;

    public bool CanSpawn =>
        canSpawn;

    public Vector3 Position =>
        transform.position;

    public Quaternion Rotation =>
        transform.rotation;

    private void OnDrawGizmos()
    {
        if (!showGizmo)
            return;

        Gizmos.DrawWireSphere(
            transform.position,
            gizmoRadius
        );

        Gizmos.DrawLine(
            transform.position,
            transform.position +
            transform.forward * 0.8f
        );
    }
}