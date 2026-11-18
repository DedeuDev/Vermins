using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Onda de choque na tecla 1 (3.3): o Player bate a espada no chao e uma
/// onda corre reta pro lado do mouse, tirando o dano da arma de cada um que
/// estiver no caminho. Parede para a onda.
///
/// Comecou como uma bola de magia saindo do peito, e como habilidade de
/// guerreiro de espadao ficou estranho. Copiei o Sunder do Path of Exile 2 e
/// o Earthquake do Barbaro do Diablo: o golpe e da espada, e o que viaja e o
/// chao. A logica e a mesma da bola; mudou o prefab (OndaDeChoque) e o
/// pierce do Projectile.
///
/// Atravessar e o que separa esta do golpe em area: la e um circulo em volta
/// do Player, aqui e uma linha pra longe.
///
/// O gesto e o golpe 01 da espada, e a onda sai por tempo no frame do
/// LandHit dele, como no GroundSlam. O GreatSwordAttack01 tem 1,27 s e o
/// LandHit cai aos 0,646 s; encaixado em 0,6 s ele toca a 2,22x e o
/// impactDelay fica em 0,29 s. Esquiva no meio corta a onda.
///
/// O critico e sorteado no lancamento: a onda carrega o dano pronto. O
/// acerto volta pelo onHit e passa pelo PlayerCombat.ReportHit, entao
/// numero, sangue e XP funcionam igual ao golpe de espada.
///
/// Numeros de teste: 22,5 em cada um (arma x 1,5), ate 10 m, recarga de 4 s,
/// gesto de 0,6 s. Fica entre a bola (pegava um so, 2 s) e o golpe em area
/// (5 s). Comecou em x 1 e, pelo mesmo motivo do GroundSlam, quase nao
/// somava dano; com 22,5 ainda nao mata sozinha o inimigo de 30.
/// </summary>
[RequireComponent(typeof(MeleeAttack), typeof(PlayerCombat), typeof(PlayerMotor))]
public class ShockWave : DelayedAbility
{
    [Tooltip("A onda. O OndaDeChoque.prefab: um Projectile com pierce " +
             "ligado, largo e baixo.")]
    [SerializeField] private Projectile projectilePrefab;

    [Tooltip("Dano em vezes o dano da arma, em cada um que a onda pegar. O " +
             "critico multiplica por cima.")]
    [SerializeField, Min(0f)] private float damageMultiplier = 1.5f;

    [Tooltip("Quanto na frente do centro do Player a onda nasce. O centro " +
             "fica na altura do peito; meio metro tira ela de dentro da " +
             "capsula.")]
    [SerializeField, Min(0f)] private float spawnForward = 0.5f;

    [Tooltip("Quanto o golpe trava o Player, do aperto ate poder andar de " +
             "novo. O PlayerAnimator encaixa o golpe 01 da espada neste " +
             "tempo.")]
    [SerializeField, Min(0.1f)] private float castTime = 0.6f;

    public override float CastTime => castTime;

    // O primeiro golpe do combo: um golpe reto, de cima pra baixo, sem
    // levantar a espada como o final.
    public override int AttackClip => 0;

    // A recarga mora na base, com 3 s de padrao. Mesmo esquema do
    // GroundSlam: o Reset poe os valores desta quando o componente entra.
    private void Reset()
    {
        cooldown = 4f;
        impactDelay = 0.29f;
    }

    protected override bool CanActivate(Vector3 point) => projectilePrefab != null;

    protected override void Impact(Vector3 point)
    {
        Vector3 origem = transform.position + transform.forward * spawnForward;
        Vector3 direcao = AimDirection(origem, point);

        bool critico = Random.value < combat.CritChance;
        float dano = weapon.Damage * damageMultiplier * (critico ? combat.CritMultiplier : 1f);

        Projectile onda = Instantiate(projectilePrefab, origem, Quaternion.LookRotation(direcao));
        LimitAtWall(onda, direcao);

        // A closure guarda o critico desta onda. E um new por uso, a cada
        // 4 s no minimo; nao e por frame.
        onda.Launch(direcao, dano, gameObject,
            (alvo, perdeu) => combat.ReportHit(alvo, perdeu, critico));
    }

    /// <summary>
    /// Para a onda na borda do NavMesh. A parede da dungeon nao tem
    /// collider, entao sigo a mesma regra do golpe de espada.
    /// </summary>
    private void LimitAtWall(Projectile onda, Vector3 direcao)
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit chao, 1.5f, NavMesh.AllAreas))
            return;

        Vector3 dir = direcao.sqrMagnitude < 0.0001f ? transform.forward : direcao.normalized;
        Vector3 fim = chao.position + dir * (onda.MaxRange + spawnForward);

        if (NavMesh.Raycast(chao.position, fim, out NavMeshHit borda, NavMesh.AllAreas))
            onda.LimitRange(borda.distance - spawnForward);
    }

    /// <summary>
    /// Pra onde a onda vai, saindo de origem (no peito) com o mouse no
    /// ponto do chao. O Projectile normaliza; a direcao zerada vira frente.
    /// </summary>
    private Vector3 AimDirection(Vector3 origem, Vector3 pontoNoChao)
    {
        // Mirei reto, na altura do peito: o ponto do mouse e do chao, e
        // mirar nele fazia a bola descer e bater no chao antes do inimigo.
        Vector3 direcao = pontoNoChao - origem;
        direcao.y = 0f;
        return direcao;
    }
}
