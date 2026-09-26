using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [Header("Spawn Point")]
    [Tooltip(
        "Se desativado, este ponto não poderá " +
        "ser escolhido para spawn."
    )]
    [SerializeField] private bool canSpawn = true;

    [Header("Debug")]
    [SerializeField] private bool showGizmo = true;

    [Min(0.05f)]
    [SerializeField] private float gizmoRadius = 0.25f;

    public bool CanSpawn => canSpawn;

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
            transform.forward * 0.6f
        );
    }
}