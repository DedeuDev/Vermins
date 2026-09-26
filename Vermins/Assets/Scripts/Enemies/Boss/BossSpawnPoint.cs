using UnityEngine;

public class BossSpawnPoint : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showGizmo = true;

    [Min(0.05f)]
    [SerializeField] private float gizmoRadius = 0.5f;

    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;

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
            transform.forward * 1.25f
        );
    }
}