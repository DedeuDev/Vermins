using UnityEngine;

/// <summary>
/// Ouro no chao. Quem encosta e tem carteira leva.
///
/// Mesma montagem da pocao (PotionPickup): esfera trigger com Rigidbody
/// kinematic, porque o NavMeshAgent do jogador nao conta pro trigger.
/// So quem tem GoldWallet pega, entao inimigo passa por cima sem levar.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class GoldPickup : MonoBehaviour
{
    [Tooltip("Menor quantia que este monte pode valer. Valor de teste, " +
             "quem vai calibrar e a loja.")]
    [SerializeField, Min(1)] private int minAmount = 5;

    [Tooltip("Maior quantia que este monte pode valer, incluida no sorteio.")]
    [SerializeField, Min(1)] private int maxAmount = 15;

    private int quantia;

    private void Reset()
    {
        Rigidbody corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;

        // Os mesmos numeros da pocao: pega quando o jogador passa a menos
        // de 1,1 m.
        SphereCollider area = GetComponent<SphereCollider>();
        area.isTrigger = true;
        area.center = new Vector3(0f, 0.5f, 0f);
        area.radius = 0.6f;
    }

    private void Awake()
    {
        // Sorteio ao nascer, e nao ao pegar, pra quantia ja estar decidida
        // quando alguem quiser mostrar ela (numero em cima do monte, monte
        // maior pra quantia maior). O +1 e porque o Range de int nao inclui
        // o maximo.
        quantia = Random.Range(minAmount, Mathf.Max(minAmount, maxAmount) + 1);
    }

    private void OnTriggerEnter(Collider other)
    {
        GoldWallet carteira = other.GetComponentInParent<GoldWallet>();

        if (carteira == null)
            return;

        carteira.Add(quantia);
        Destroy(gameObject);
    }
}
