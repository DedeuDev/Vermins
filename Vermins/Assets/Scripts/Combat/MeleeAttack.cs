using UnityEngine;

/// <summary>
/// Aplica dano corpo a corpo.
///
/// De proposito ele NAO decide quando bater e NAO checa distancia -
/// quem chama ja fez essas duas contas. Se este script checasse alcance
/// de novo, com um numero diferente do de quem chamou, ia dar aquele bug
/// classico de "o inimigo faz a animacao de ataque e nao tira vida".
///
/// Serve tanto pro inimigo quanto pro ataque do jogador, que e o
/// proximo passo. O dano fica aqui no componente e nao no
/// DadosInimigoSO porque assim uma arma diferente pode trocar o numero
/// sem trocar o tipo do inimigo.
/// </summary>
public class MeleeAttack : MonoBehaviour
{
    [SerializeField] private float damage = 10f;

    public float Damage
    {
        get => damage;
        set => damage = Mathf.Max(0f, value);
    }

    /// <summary>Bate no alvo. Devolve false quando nao tinha em que bater.</summary>
    public bool TryHit(Health target)
    {
        if (target == null || target.IsDead)
            return false;

        target.TakeDamage(damage, gameObject);
        return true;
    }

    /// <summary>
    /// Mesma coisa, mas achando o Health sozinho. Uso o InParent porque
    /// o collider costuma estar num filho e a vida no objeto raiz.
    /// </summary>
    public bool TryHit(Transform target)
    {
        if (target == null)
            return false;

        return TryHit(target.GetComponentInParent<Health>());
    }
}
