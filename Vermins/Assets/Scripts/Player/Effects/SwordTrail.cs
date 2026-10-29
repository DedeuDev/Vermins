using UnityEngine;

/// <summary>
/// O rastro da espada: a faixa que segue a ponta da lamina e some, como
/// nos golpes de Diablo e Path of Exile. Da peso ao golpe e mostra o arco.
///
/// Fica num filho na ponta da EspadaGrande, desligado parado. Liga quando
/// um golpe comeca (PlayerCombat.OnAttack ou Ability.OnUsed) e desliga um
/// pouco depois do acerto, no fim do gesto ou se o golpe for cortado. Com
/// o rastro sempre ligado, andar e rolar tambem deixariam faixa.
///
/// So troca o emitting do TrailRenderer: nada de lixo por frame.
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class SwordTrail : MonoBehaviour
{
    [Tooltip("Quanto o rastro continua depois do acerto. Sem isso a faixa " +
             "corta no frame do impacto e a pancada perde o arremate.")]
    [SerializeField, Min(0f)] private float tailAfterHit = 0.1f;

    private TrailRenderer trail;
    private PlayerCombat combat;
    private Dodge dodge;
    private Ability[] abilities;

    // Quando desligar. Um horario, como a recarga da Ability.
    private float offTime;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        combat = GetComponentInParent<PlayerCombat>();

        if (combat != null)
        {
            dodge = combat.GetComponent<Dodge>();
            abilities = combat.GetComponents<Ability>();
        }

        trail.emitting = false;
    }

    private void OnEnable()
    {
        if (combat != null)
        {
            combat.OnAttack += HandleAttack;
            combat.OnAttackHit += HandleHit;
            combat.OnAttackInterrupted += Stop;
        }

        if (dodge != null)
            dodge.OnDodged += HandleDodged;

        if (abilities != null)
        {
            foreach (Ability habilidade in abilities)
                habilidade.OnUsed += HandleAbility;
        }
    }

    private void OnDisable()
    {
        if (combat != null)
        {
            combat.OnAttack -= HandleAttack;
            combat.OnAttackHit -= HandleHit;
            combat.OnAttackInterrupted -= Stop;
        }

        if (dodge != null)
            dodge.OnDodged -= HandleDodged;

        if (abilities != null)
        {
            foreach (Ability habilidade in abilities)
                habilidade.OnUsed -= HandleAbility;
        }

        trail.emitting = false;
    }

    private void Update()
    {
        if (trail.emitting && Time.time >= offTime)
            trail.emitting = false;
    }

    // O AttackDuration e o teto: golpe que errou o alvo nao dispara o
    // acerto, e o rastro tem que desligar mesmo assim.
    private void HandleAttack(Health _)
    {
        Begin(combat.AttackDuration);
    }

    private void HandleAbility(Ability habilidade)
    {
        Begin(habilidade.CastTime);
    }

    // O acerto da habilidade tambem passa por aqui, pelo ReportHit, entao
    // a pancada do golpe em area encurta o rastro do mesmo jeito.
    private void HandleHit(Health _, float __)
    {
        offTime = Mathf.Min(offTime, Time.time + tailAfterHit);
    }

    private void HandleDodged(float _)
    {
        Stop();
    }

    private void Begin(float duracao)
    {
        offTime = Time.time + duracao;
        trail.emitting = true;
    }

    private void Stop()
    {
        offTime = Time.time;
        trail.emitting = false;
    }
}
