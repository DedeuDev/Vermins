using UnityEngine;

/// <summary>
/// Pocao no chao. Quem encosta e tem cinto leva ela.
///
/// Filtro por "tem PotionBelt", e nao pela tag Player. Inimigo tambem
/// passa por cima da pocao, e nao ter cinto ja separa um do outro. A
/// montagem do trigger e a trava contra entrega dupla estao no Coletavel.
/// </summary>
public class PotionPickup : Coletavel
{
    // Tento de novo se o jogador beber sem sair de cima da pocao.
    protected override bool TentarDeNovoEmCima => true;

    protected override bool Entregar(Collider quemEncostou)
    {
        PotionBelt cinto = quemEncostou.GetComponentInParent<PotionBelt>();

        return cinto != null && cinto.TryStore();
    }
}
