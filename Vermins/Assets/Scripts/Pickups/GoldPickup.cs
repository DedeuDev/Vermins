using UnityEngine;

/// <summary>
/// Ouro no chao. Quem encosta e tem carteira leva.
///
/// So quem tem GoldWallet pega, entao inimigo passa por cima sem levar.
/// A montagem do trigger e a trava contra entrega dupla estao no
/// Pickup.
/// </summary>
public class GoldPickup : Pickup
{
    [Tooltip("Menor quantia que este monte pode valer. Valor de teste, " +
             "quem vai calibrar e a loja.")]
    [SerializeField, Min(1)] private int minAmount = 5;

    [Tooltip("Maior quantia que este monte pode valer, incluida no sorteio.")]
    [SerializeField, Min(1)] private int maxAmount = 15;

    private int amount;

    protected override string LabelText => $"Ouro {amount}";

    protected override Color LabelColor => new Color(1f, 0.85f, 0.3f);

    private void Awake()
    {
        // Sorteio ao nascer, e nao ao pegar, pra quantia ja estar decidida
        // quando alguem quiser mostrar ela (numero em cima do monte, monte
        // maior pra quantia maior). O +1 e porque o Range de int nao inclui
        // o maximo.
        amount = Random.Range(minAmount, Mathf.Max(minAmount, maxAmount) + 1);
    }

    protected override bool Deliver(Collider quemEncostou)
    {
        GoldWallet carteira = quemEncostou.GetComponentInParent<GoldWallet>();

        if (carteira == null)
            return false;

        carteira.Add(amount);
        return true;
    }
}
