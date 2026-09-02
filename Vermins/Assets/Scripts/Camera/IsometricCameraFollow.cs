using UnityEngine;

/// <summary>
/// Camera em visao elevada seguindo o jogador, que e o angulo que o
/// GDR pede pra leitura de combate e exploracao.
///
/// Fica em LateUpdate de proposito: se seguisse no Update, a camera
/// poderia rodar antes do jogador ter se movido no frame, e a
/// imagem treme.
/// </summary>
public class IsometricCameraFollow : MonoBehaviour
{
    [Header("Alvo")]
    [SerializeField] private Transform target;

    [Header("Enquadramento")]
    [Tooltip("Deslocamento em relacao ao alvo. Y e a altura, Z o " +
             "quanto a camera fica pra tras.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -10f);

    [Tooltip("Zero gruda a camera no alvo. Valores maiores deixam o " +
             "movimento mais macio, mas com mais atraso.")]
    [SerializeField] private float smoothTime = 0.15f;

    [SerializeField] private bool lookAtTarget = true;

    private Vector3 followVelocity;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            smoothTime
        );

        if (lookAtTarget)
            transform.LookAt(target.position);
    }
}
