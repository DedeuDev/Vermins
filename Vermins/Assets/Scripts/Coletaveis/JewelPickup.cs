using UnityEngine;

/// <summary>
/// Joia no chao. Quem encosta e tem bolsa de joias leva.
///
/// A montagem do trigger e a trava contra entrega dupla estao no
/// Coletavel.
/// </summary>
public class JewelPickup : Coletavel
{
    [Tooltip("Menor valor de venda desta joia, em ouro. Valor de teste, " +
             "quem vai calibrar e a loja.")]
    [SerializeField, Min(1)] private int minValue = 30;

    [Tooltip("Maior valor de venda, incluido no sorteio.")]
    [SerializeField, Min(1)] private int maxValue = 60;

    private int valor;

    private void Awake()
    {
        // Sorteio ao nascer, igual o ouro, e com o mesmo +1 porque o Range
        // de int nao inclui o maximo.
        valor = Random.Range(minValue, Mathf.Max(minValue, maxValue) + 1);
    }

    protected override bool Entregar(Collider quemEncostou)
    {
        JewelPouch bolsa = quemEncostou.GetComponentInParent<JewelPouch>();

        if (bolsa == null)
            return false;

        bolsa.Add(valor);
        return true;
    }
}
