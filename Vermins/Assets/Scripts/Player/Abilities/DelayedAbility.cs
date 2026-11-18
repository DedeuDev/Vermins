using System.Collections;
using UnityEngine;

/// <summary>
/// Base para habilidades em que o impacto sai depois de um atraso do
/// gesto (GroundSlam e ShockWave).
///
/// As duas repetiam o mesmo ciclo: pegar os componentes de combate no
/// Awake, limpar alvo e golpe ao ativar, virar pro mouse, esperar o
/// impactDelay numa corrotina, soltar o giro na pancada e cancelar tudo
/// se o jogador esquivar no meio. Subi isso pra ca; quem herda so diz
/// o que acontece no Impact e, se precisar, o que impede de ativar.
/// </summary>
[RequireComponent(typeof(MeleeAttack), typeof(PlayerCombat), typeof(PlayerMotor))]
public abstract class DelayedAbility : Ability
{
    [Tooltip("Segundos do aperto ate o impacto. O Animator encaixa o " +
             "clipe no CastTime; mudando o castTime, ajuste aqui pro " +
             "impacto cair no frame do LandHit.")]
    [SerializeField, Min(0f)] protected float impactDelay = 0.4f;

    protected MeleeAttack weapon;
    protected PlayerCombat combat;
    protected PlayerMotor motor;
    protected Health ownHealth;

    // Nome diferente de dodge (que ja existe privado na Ability) pra
    // Unity nao reclamar de campo repetido na heranca.
    private Dodge ownDodge;
    private Coroutine pendingImpact;
    private Vector3 pendingPoint;

    protected override void Awake()
    {
        base.Awake();
        weapon = GetComponent<MeleeAttack>();
        combat = GetComponent<PlayerCombat>();
        motor = GetComponent<PlayerMotor>();
        ownHealth = GetComponent<Health>();
        ownDodge = GetComponent<Dodge>();
    }

    protected virtual void OnEnable()
    {
        if (ownDodge != null)
            ownDodge.OnDodged += HandleDodged;
    }

    protected virtual void OnDisable()
    {
        if (ownDodge != null)
            ownDodge.OnDodged -= HandleDodged;

        CancelImpact();
    }

    /// <summary>
    /// Deixe false se faltar algo pra usar (ex.: prefab vazio); a
    /// recarga nao comeca.
    /// </summary>
    protected virtual bool CanActivate(Vector3 point) => true;

    protected override bool Activate(Vector3 point)
    {
        if (!CanActivate(point))
            return false;

        // Mantive a rota com o gesto movel a 50%; larguei o combate para
        // nao somar um golpe de espada nem perseguir o alvo no meio dele.
        if (!MoveWhileCasting)
            motor.Stop();

        motor.HoldRotation = MoveWhileCasting;
        combat.ClearTarget();
        combat.InterruptAttack();

        // Viro pro mouse no plano. Mouse em cima do proprio personagem:
        // fica virado pra onde estava.
        Vector3 dir = point - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.09f)
            transform.rotation = Quaternion.LookRotation(dir);

        pendingPoint = point;
        pendingImpact = StartCoroutine(ImpactAfterDelay());
        return true;
    }

    private IEnumerator ImpactAfterDelay()
    {
        // Um WaitForSeconds por uso, nao por frame.
        yield return new WaitForSeconds(impactDelay);

        pendingImpact = null;
        motor.HoldRotation = false;

        if (ownHealth != null && ownHealth.IsDead)
            yield break;

        Impact(pendingPoint);
    }

    /// <summary>
    /// Executado depois do impactDelay, ja com o giro solto e o
    /// personagem vivo.
    /// </summary>
    protected abstract void Impact(Vector3 point);

    // Mantive so a esquiva cortando o impacto; andar nao cancela o gesto
    // e a recarga continua correndo.
    private void HandleDodged(float _)
    {
        CancelImpact();
    }

    private void CancelImpact()
    {
        // Soltei tambem depois do impacto: a esquiva corta o resto do gesto.
        if (motor != null)
            motor.HoldRotation = false;

        if (pendingImpact != null)
        {
            StopCoroutine(pendingImpact);
            pendingImpact = null;
        }

        EndCast();
    }
}
