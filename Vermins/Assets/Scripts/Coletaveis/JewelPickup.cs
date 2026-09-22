using UnityEngine;

/// <summary>
/// Joia no chao. Quem encosta e tem bolsa de joias leva.
///
/// Mesma montagem da pocao e do ouro: esfera trigger com Rigidbody
/// kinematic, porque o NavMeshAgent do jogador nao conta pro trigger.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class JewelPickup : MonoBehaviour
{
    [Tooltip("Menor valor de venda desta joia, em ouro. Valor de teste, " +
             "quem vai calibrar e a loja.")]
    [SerializeField, Min(1)] private int minValue = 30;

    [Tooltip("Maior valor de venda, incluido no sorteio.")]
    [SerializeField, Min(1)] private int maxValue = 60;

    private int valor;

    private void Reset()
    {
        Rigidbody corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;

        SphereCollider area = GetComponent<SphereCollider>();
        area.isTrigger = true;
        area.center = new Vector3(0f, 0.5f, 0f);
        area.radius = 0.6f;
    }

    private void Awake()
    {
        // Sorteio ao nascer, igual o ouro, e com o mesmo +1 porque o Range
        // de int nao inclui o maximo.
        valor = Random.Range(minValue, Mathf.Max(minValue, maxValue) + 1);
    }

    private void OnTriggerEnter(Collider other)
    {
        JewelPouch bolsa = other.GetComponentInParent<JewelPouch>();

        if (bolsa == null)
            return;

        bolsa.Add(valor);
        Destroy(gameObject);
    }
}
