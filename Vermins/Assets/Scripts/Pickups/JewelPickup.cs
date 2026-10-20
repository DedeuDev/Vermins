using UnityEngine;

/// <summary>
/// Joia no chao. Quem encosta e tem bolsa de joias leva.
///
/// A montagem do trigger e a trava contra entrega dupla estao no
/// Pickup.
/// </summary>
public class JewelPickup : Pickup
{
    [Tooltip("Menor valor de venda desta joia, em ouro. Valor de teste, " +
             "quem vai calibrar e a loja.")]
    [SerializeField, Min(1)] private int minValue = 30;

    [Tooltip("Maior valor de venda, incluido no sorteio.")]
    [SerializeField, Min(1)] private int maxValue = 60;

    private int value;

    // So o nome. O valor e de venda, e quem mostra e a loja.
    protected override string LabelText => "Joia";

    protected override Color LabelColor => new Color(0.45f, 0.85f, 1f);

    private void Awake()
    {
        // Sorteio ao nascer, igual o ouro, e com o mesmo +1 porque o Range
        // de int nao inclui o maximo.
        value = Random.Range(minValue, Mathf.Max(minValue, maxValue) + 1);
    }

    protected override bool Deliver(Collider quemEncostou)
    {
        JewelPouch bolsa = quemEncostou.GetComponentInParent<JewelPouch>();

        if (bolsa == null)
            return false;

        bolsa.Add(value);
        return true;
    }
}
