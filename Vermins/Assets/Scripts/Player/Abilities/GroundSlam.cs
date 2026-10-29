using System.Collections;
using UnityEngine;

/// <summary>
/// Golpe em area no botao direito (3.2): o Player para, vira pro mouse,
/// desce a espada e todo inimigo a ate 3 m dele leva o dano da arma x 2,5.
///
/// O centro e o Player, e nao o mouse. E golpe de espada: o mouse so diz
/// pra que lado o corpo vira.
///
/// O dano sai num tempo fixo depois do aperto, e nao do Animation Event do
/// clipe. O LandHit do clipe vai pro PlayerCombat, que so aceita quando um
/// golpe de espada dele esta esperando, entao aqui ele chega e e ignorado.
/// O impactDelay foi medido pra cair no mesmo frame do LandHit.
///
/// O acerto passa pelo PlayerCombat.ReportHit. Numero, sangue, tremor e XP
/// escutam o OnAttackHit de la, e assim funcionam sem saber do golpe em
/// area.
///
/// Numeros de teste: 37,5 de dano no nivel 1 matam o inimigo de 30 numa
/// pancada, entao limpar um grupo e um botao. Comecou em x 1,5 e somava
/// pouco: a habilidade corta o golpe de espada que estava no meio, e com
/// 22,5 o que ela ganhava sumia nesse corte (medi o Monge caindo de 80 s
/// so pra 69 s com as duas habilidades). Como no Diablo 4, a habilidade e
/// o dano e o golpe comum preenche o intervalo. O raio de 3 m e o mesmo do
/// terremoto do Monge. A recarga de 5 s da uns 4 golpes de espada entre um
/// uso e outro.
/// </summary>
[RequireComponent(typeof(MeleeAttack), typeof(PlayerCombat), typeof(PlayerMotor))]
public class GroundSlam : Ability
{
    [Tooltip("Ate onde o golpe pega, em metros, do centro do Player ao " +
             "centro do inimigo, medido no chao.")]
    [SerializeField, Min(0.5f)] private float radius = 3f;

    [Tooltip("Dano em vezes o dano da arma. Com 15 de arma sai 37,5. O " +
             "critico multiplica por cima, igual ao golpe normal.")]
    [SerializeField, Min(0f)] private float damageMultiplier = 2.5f;

    [Tooltip("Quanto o golpe trava o Player, do aperto ate poder andar de " +
             "novo. O PlayerAnimator encaixa o golpe final da espada neste " +
             "tempo.")]
    [SerializeField, Min(0.1f)] private float castTime = 1f;

    [Tooltip("Do aperto ate a pancada. O GreatSwordAttack03 tem 1,83 s e o " +
             "LandHit dele cai aos 0,935 s. Encaixado em 1 s, ele toca a " +
             "1,93x e o LandHit cai aos 0,48 s. Mudou o castTime, mude aqui.")]
    [SerializeField, Min(0f)] private float impactDelay = 0.48f;

    // Buffers fixos pra pancada nao gerar lixo. 32 sobra num raio de 3 m. O
    // segundo guarda quem ja levou, porque inimigo com mais de um collider
    // apareceria mais de uma vez no Overlap.
    private static readonly Collider[] Overlaps = new Collider[32];
    private static readonly Health[] AlreadyHit = new Health[32];

    private MeleeAttack weapon;
    private PlayerCombat combat;
    private PlayerMotor motor;
    private Health ownHealth;
    private Dodge dodge;

    private Coroutine pendingImpact;

    public override float CastTime => castTime;

    /// <summary>O raio que vale agora.</summary>
    public float Radius => radius;

    /// <summary>
    /// Disparado na pancada, com o centro e o raio que valeram. O anel no
    /// chao (SlamRing) escuta daqui, e o golpe nao precisa saber dele.
    /// </summary>
    public event System.Action<Vector3, float> OnImpact;

    // A recarga mora na base, com 3 s de padrao. O Reset roda quando o
    // componente e adicionado no editor e poe os 5 s deste golpe. Sem ele o
    // prefab nasceu com 3 (medi no teste).
    private void Reset()
    {
        cooldown = 5f;
    }

    protected override void Awake()
    {
        base.Awake();
        weapon = GetComponent<MeleeAttack>();
        combat = GetComponent<PlayerCombat>();
        motor = GetComponent<PlayerMotor>();
        ownHealth = GetComponent<Health>();
        dodge = GetComponent<Dodge>();
    }

    private void OnEnable()
    {
        if (dodge != null)
            dodge.OnDodged += HandleDodged;
    }

    private void OnDisable()
    {
        if (dodge != null)
            dodge.OnDodged -= HandleDodged;

        CancelImpact();
    }

    protected override bool Activate(Vector3 point)
    {
        // Larga o que estava fazendo. O golpe de espada no meio seria uma
        // segunda pancada por cima desta, e o alvo travado faria o
        // PlayerCombat sair perseguindo no fim do gesto.
        motor.Stop();
        combat.ClearTarget();
        combat.InterruptAttack();

        Vector3 dir = point - transform.position;
        dir.y = 0f;

        // Mouse em cima do proprio personagem: fica virado pra onde estava.
        if (dir.sqrMagnitude > 0.09f)
            transform.rotation = Quaternion.LookRotation(dir);

        pendingImpact = StartCoroutine(ImpactAfterDelay());
        return true;
    }

    private IEnumerator ImpactAfterDelay()
    {
        // Um WaitForSeconds por uso, a cada 5 s no minimo. Nao e por frame.
        yield return new WaitForSeconds(impactDelay);

        pendingImpact = null;
        Impact();
    }

    /// <summary>
    /// A pancada: todo vivo a ate radius do Player, sem parede no meio,
    /// leva o dano. Mesmos testes de parede do golpe de espada.
    /// </summary>
    private void Impact()
    {
        if (ownHealth.IsDead)
            return;

        // Aviso antes do dano, pra o anel aparecer no mesmo frame do numero.
        OnImpact?.Invoke(transform.position, radius);

        int quantos = Physics.OverlapSphereNonAlloc(
            transform.position, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);

        int vistos = 0;

        for (int i = 0; i < quantos; i++)
        {
            Health alvo = Overlaps[i].GetComponentInParent<Health>();
            Overlaps[i] = null;

            if (alvo == null || alvo == ownHealth || alvo.IsDead || WasSeen(alvo, vistos))
                continue;

            if (vistos < AlreadyHit.Length)
                AlreadyHit[vistos++] = alvo;

            // O Overlap pega pelo collider; o raio vale pelo centro, no
            // plano. Assim um inimigo num degrau nao escapa pela altura, e o
            // raio e o mesmo que o jogador mede no chao.
            Vector3 ate = alvo.transform.position - transform.position;
            ate.y = 0f;

            if (ate.sqrMagnitude > radius * radius)
                continue;

            if (!combat.HasLineOfSight(alvo))
                continue;

            Hit(alvo);
        }

        for (int i = 0; i < vistos; i++)
            AlreadyHit[i] = null;
    }

    private void Hit(Health alvo)
    {
        // Mede o que a vida perdeu, como o LandHit: no inimigo com 5 de
        // vida sai 5, e no invulneravel sai zero e nao conta.
        float vidaAntes = alvo.Current;

        bool critico = Random.value < combat.CritChance;
        float multiplicador = damageMultiplier * (critico ? combat.CritMultiplier : 1f);

        if (!weapon.TryHit(alvo, multiplicador))
            return;

        combat.ReportHit(alvo, vidaAntes - alvo.Current, critico);
    }

    private static bool WasSeen(Health alvo, int vistos)
    {
        for (int i = 0; i < vistos; i++)
        {
            if (AlreadyHit[i] == alvo)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Esquiva no meio do gesto corta a pancada, como a ordem de andar corta
    /// o golpe de espada. A recarga continua correndo.
    /// </summary>
    private void HandleDodged(float _)
    {
        CancelImpact();
    }

    private void CancelImpact()
    {
        if (pendingImpact == null)
            return;

        StopCoroutine(pendingImpact);
        pendingImpact = null;
        EndCast();
    }
}
