using UnityEngine;

/// <summary>
/// Liga o Health, que e o que o meu combate entende, ao BossStats do
/// Leo, que e quem cuida das fases do boss.
///
/// O PlayerCombat so mira quem tem Health. O boss guarda a vida no
/// BossStats e leva dano pelo TomarDano, entao o clique nele atravessava
/// e virava passo pro chao. Em vez de ensinar o combate a falar com dois
/// tipos de vida, o boss ganha um Health e eu repasso cada dano pro
/// BossStats. As fases, a morte e o Destroy continuam todos com ele.
///
/// O BossStats cria este componente sozinho no Awake, entao ninguem
/// precisa lembrar de adicionar em cena nem em prefab.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(BossStats))]
public class VidaDoBoss : MonoBehaviour
{
    private Health health;
    private BossStats stats;

    private void Awake()
    {
        health = GetComponent<Health>();
        stats = GetComponent<BossStats>();

        // O teto vem dos dados do boss, pra os dois lados morrerem no
        // mesmo golpe. Se o Health tivesse os 100 padrao e o boss 1000,
        // o Health morreria no decimo golpe e o boss seguiria vivo, mas
        // sem poder ser mirado.
        if (stats.dadosBoss != null)
            health.DefinirVidaMaxima(stats.dadosBoss.vidaMaxima, true);
    }

    private void OnEnable()
    {
        health.OnDamaged += RepassarDano;
    }

    private void OnDisable()
    {
        health.OnDamaged -= RepassarDano;
    }

    private void RepassarDano(float quantidade, GameObject _)
    {
        stats.TomarDano(quantidade);
    }
}
