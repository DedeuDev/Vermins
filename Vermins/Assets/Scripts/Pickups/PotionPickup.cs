using UnityEngine;

/// <summary>
/// Pocao no chao. Quem encosta e tem cinto leva ela.
///
/// Filtro por "tem PotionBelt", e nao pela tag Player. Inimigo tambem
/// passa por cima da pocao, e nao ter cinto ja separa um do outro. A
/// montagem do trigger e a trava contra entrega dupla estao no Pickup.
/// </summary>
public class PotionPickup : Pickup
{
    // Tento de novo se o jogador beber sem sair de cima da pocao.
    protected override bool RetryWhileInside => true;

    protected override string LabelText => "Pocao";

    protected override Color LabelColor => new Color(1f, 0.4f, 0.4f);

    protected override bool Deliver(Collider quemEncostou)
    {
        PotionBelt cinto = quemEncostou.GetComponentInParent<PotionBelt>();

        return cinto != null && cinto.TryStore();
    }
}
