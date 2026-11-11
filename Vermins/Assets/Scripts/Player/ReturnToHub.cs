using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Script do Ian no Player.prefab.
/// Cuida do teleporte de volta a HUB pela tecla T.
///
/// So funciona em cenas de dungeon (que possuem DungeonGenerator/ModularDungeonGenerator).
/// Fora de combate, canaliza por 5 s parado no lugar e carrega a HUBCity mantendo estado.
/// Levar dano ou bater nos ultimos 5 s conta como combate e impede a saida, mostrando aviso
/// por 2 s no PlayerDebugHUD.
/// Qualquer acao (andar, atacar, esquivar, habilidade, dano ou apertar T de novo) cancela o teleporte.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerMotor))]
public class ReturnToHub : MonoBehaviour
{
    [Tooltip("Tempo em segundos de canalizacao necessaria para voltar a HUB.")]
    [SerializeField] private float channelDuration = 5f;

    [Tooltip("Tempo em segundos apos levar ou causar dano considerado em combate.")]
    [SerializeField] private float combatDuration = 5f;

    private Health health;
    private PlayerMotor motor;
    private PlayerCombat combat;
    private Dodge dodge;
    private Ability[] abilities;
    private InputSystem_Actions input;

    private float lastCombatTime = -100f;
    private bool isChanneling;
    private float channelStartTime;
    private float combatWarningUntil = -1f;
    private bool isDungeonScene;
    private int lastTriggerFrame = -1;

    public bool IsChanneling => isChanneling;
    public bool IsInCombat => Time.time < lastCombatTime + combatDuration;
    public bool ShowCombatWarning => Time.time < combatWarningUntil;
    public float ChannelDuration => channelDuration;
    public float CombatDuration => combatDuration;

    public float ChannelProgress => (isChanneling && channelDuration > 0f)
        ? Mathf.Clamp01((Time.time - channelStartTime) / channelDuration)
        : 0f;

    public float ChannelRemaining => isChanneling
        ? Mathf.Max(0f, (channelStartTime + channelDuration) - Time.time)
        : 0f;

    private void Awake()
    {
        health = GetComponent<Health>();
        motor = GetComponent<PlayerMotor>();
        combat = GetComponent<PlayerCombat>();
        dodge = GetComponent<Dodge>();
        abilities = GetComponents<Ability>();
        input = new InputSystem_Actions();
    }

    private void Start()
    {
        // Confere se estamos numa dungeon com gerador. Na HUB e em menus nao faz nada.
        isDungeonScene = FindFirstObjectByType<ModularDungeonGenerator>() != null;
    }

    private void OnEnable()
    {
        input.Player.Enable();
        input.Player.ReturnToHub.performed += HandleReturnToHubPerformed;

        if (health != null)
            health.OnDamaged += HandleDamaged;

        if (combat != null)
        {
            combat.OnAttack += HandleAttack;
            combat.OnAttackHit += HandleAttackHit;
        }

        if (dodge != null)
            dodge.OnDodged += HandleDodged;

        PlayerController.OnMoveOrdered += HandleMoveOrdered;

        if (abilities != null)
        {
            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i] != null)
                    abilities[i].OnUsed += HandleAbilityUsed;
            }
        }
    }

    private void OnDisable()
    {
        input.Player.ReturnToHub.performed -= HandleReturnToHubPerformed;
        input.Player.Disable();
        CancelChannel();

        if (health != null)
            health.OnDamaged -= HandleDamaged;

        if (combat != null)
        {
            combat.OnAttack -= HandleAttack;
            combat.OnAttackHit -= HandleAttackHit;
        }

        if (dodge != null)
            dodge.OnDodged -= HandleDodged;

        PlayerController.OnMoveOrdered -= HandleMoveOrdered;

        if (abilities != null)
        {
            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i] != null)
                    abilities[i].OnUsed -= HandleAbilityUsed;
            }
        }
    }

    private void OnDestroy()
    {
        input.Dispose();
    }

    private void Update()
    {
        if (health != null && health.IsDead)
        {
            if (isChanneling)
                CancelChannel();
            return;
        }

        if (Time.timeScale == 0f)
            return;

        if (input.Player.ReturnToHub.WasPressedThisFrame())
        {
            OnTriggerPressed();
        }

        if (isChanneling)
        {
            if (Time.time >= channelStartTime + channelDuration)
            {
                isChanneling = false;
                ExecuteTeleport();
            }
        }
    }

    private void HandleReturnToHubPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        OnTriggerPressed();
    }

    private void OnTriggerPressed()
    {
        if (Time.frameCount == lastTriggerFrame)
            return;
        lastTriggerFrame = Time.frameCount;

        if (!isDungeonScene)
            return;

        if (isChanneling)
        {
            CancelChannel();
            return;
        }

        if (IsInCombat)
        {
            combatWarningUntil = Time.time + 2f;
            return;
        }

        StartChannel();
    }

    private void StartChannel()
    {
        isChanneling = true;
        channelStartTime = Time.time;

        if (combat != null)
            combat.ClearTarget();

        if (motor != null)
            motor.Stop();
    }

    public void CancelChannel()
    {
        if (!isChanneling)
            return;

        isChanneling = false;
    }

    private void ExecuteTeleport()
    {
        CrossSceneState.Capture();
        SceneManager.LoadScene("HUBCity");
    }

    private void HandleDamaged(float amount, GameObject source)
    {
        lastCombatTime = Time.time;
        if (isChanneling)
            CancelChannel();
    }

    private void HandleAttackHit(Health target, float damage)
    {
        lastCombatTime = Time.time;
    }

    private void HandleMoveOrdered(Vector3 destination)
    {
        if (isChanneling)
            CancelChannel();
    }

    private void HandleAttack(Health target)
    {
        if (isChanneling)
            CancelChannel();
    }

    private void HandleDodged(float duration)
    {
        if (isChanneling)
            CancelChannel();
    }

    private void HandleAbilityUsed(Ability ability)
    {
        if (isChanneling)
            CancelChannel();
    }
}
