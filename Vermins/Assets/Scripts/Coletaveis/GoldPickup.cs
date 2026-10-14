using UnityEngine;

/// <summary>
/// Ouro no chao. Quem encosta e tem carteira leva.
///
/// So quem tem GoldWallet pega, entao inimigo passa por cima sem levar.
/// A montagem do trigger e a trava contra entrega dupla estao no
/// Coletavel.
/// </summary>
public class GoldPickup : Coletavel
{
    [Tooltip("Menor quantia que este monte pode valer. Valor de teste, " +
             "quem vai calibrar e a loja.")]
    [SerializeField, Min(1)] private int minAmount = 5;

    [Tooltip("Maior quantia que este monte pode valer, incluida no sorteio.")]
    [SerializeField, Min(1)] private int maxAmount = 15;

    private int quantia;

    private void Awake()
    {
        // Sorteio ao nascer, e nao ao pegar, pra quantia ja estar decidida
        // quando alguem quiser mostrar ela (numero em cima do monte, monte
        // maior pra quantia maior). O +1 e porque o Range de int nao inclui
        // o maximo.
        quantia = Random.Range(minAmount, Mathf.Max(minAmount, maxAmount) + 1);
    }

    protected override bool Entregar(Collider quemEncostou)
    {
        GoldWallet carteira = quemEncostou.GetComponentInParent<GoldWallet>();

        if (carteira == null)
            return false;

        carteira.Add(quantia);
        return true;
    }
}
