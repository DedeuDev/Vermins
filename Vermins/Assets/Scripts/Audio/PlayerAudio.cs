using UnityEngine;

/// <summary>
/// Liga os eventos do Player ao AudioManager. Cada campo de cue aqui e
/// opcional: vazio usa o cue padrao da AudioLibrary.
///
/// Escolhi ouvir os eventos em vez de chamar audio de dentro do combate,
/// da esquiva e das habilidades, pro gameplay nao depender do sistema de som.
/// </summary>
public class PlayerAudio : MonoBehaviour
{
    [Header("Cues opcionais (vazio usa o padrao da AudioLibrary)")]
    [SerializeField] private SoundCue swingCue;
    [SerializeField] private SoundCue hitCue;
    [SerializeField] private SoundCue dodgeCue;
    [SerializeField] private SoundCue groundSlamCue;
    [SerializeField] private SoundCue shockWaveCue;
    [SerializeField] private SoundCue playerHurtCue;
    [SerializeField] private SoundCue playerDeathCue;
    [SerializeField] private SoundCue levelUpCue;
    [SerializeField] private SoundCue goldCue;
    [SerializeField] private SoundCue potionDrinkCue;

    private PlayerCombat combat;
    private Dodge dodge;
    private GroundSlam groundSlam;
    private ShockWave shockWave;
    private Health health;
    private CharacterLevel characterLevel;
    private GoldWallet goldWallet;
    private PotionBelt potionBelt;

    private int lastPotionCount;

    private void Awake()
    {
        combat = FindComponent<PlayerCombat>();
        dodge = FindComponent<Dodge>();
        groundSlam = FindComponent<GroundSlam>();
        shockWave = FindComponent<ShockWave>();
        health = FindComponent<Health>();
        characterLevel = FindComponent<CharacterLevel>();
        goldWallet = FindComponent<GoldWallet>();
        potionBelt = FindComponent<PotionBelt>();

        if (combat != null)
        {
            combat.OnAttack += HandleAttack;
            combat.OnAttackHit += HandleAttackHit;
        }

        if (dodge != null)
            dodge.OnDodged += HandleDodged;

        if (groundSlam != null)
            groundSlam.OnImpact += HandleGroundSlamImpact;

        if (shockWave != null)
            shockWave.OnUsed += HandleShockWaveUsed;

        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }

        if (characterLevel != null)
            characterLevel.OnLevelChanged += HandleLevelChanged;

        if (goldWallet != null)
            goldWallet.OnChanged += HandleGoldChanged;

        if (potionBelt != null)
        {
            lastPotionCount = potionBelt.Count;
            potionBelt.OnChanged += HandlePotionChanged;
        }
    }

    private void Start()
    {
        // Leio de novo aqui caso o PotionBelt.Awake tenha rodado depois do meu.
        if (potionBelt != null)
            lastPotionCount = potionBelt.Count;
    }

    private void OnDestroy()
    {
        if (combat != null)
        {
            combat.OnAttack -= HandleAttack;
            combat.OnAttackHit -= HandleAttackHit;
        }

        if (dodge != null)
            dodge.OnDodged -= HandleDodged;

        if (groundSlam != null)
            groundSlam.OnImpact -= HandleGroundSlamImpact;

        if (shockWave != null)
            shockWave.OnUsed -= HandleShockWaveUsed;

        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }

        if (characterLevel != null)
            characterLevel.OnLevelChanged -= HandleLevelChanged;

        if (goldWallet != null)
            goldWallet.OnChanged -= HandleGoldChanged;

        if (potionBelt != null)
            potionBelt.OnChanged -= HandlePotionChanged;
    }

    private T FindComponent<T>() where T : Component
    {
        T comp = GetComponent<T>();
        return comp != null ? comp : GetComponentInChildren<T>();
    }

    private static AudioManager TryGetManager(out AudioLibrary lib)
    {
        AudioManager manager = AudioManager.Instance;
        lib = manager != null ? manager.Library : null;
        return manager;
    }

    private void HandleAttack(Health _)
    {
        AudioManager manager = TryGetManager(out AudioLibrary lib);
        if (manager != null)
            manager.Play(swingCue != null ? swingCue : (lib != null ? lib.Swing : null));
    }

    private void HandleAttackHit(Health target, float _)
    {
        if (target == null)
            return;

        AudioManager manager = TryGetManager(out AudioLibrary lib);
        if (manager != null)
            manager.PlayAt(hitCue != null ? hitCue : (lib != null ? lib.Hit : null), target.transform.position);
    }

    private void HandleDodged(float _)
    {
        AudioManager manager = TryGetManager(out AudioLibrary lib);
        if (manager != null)
            manager.Play(dodgeCue != null ? dodgeCue : (lib != null ? lib.Dodge : null));
    }

    private void HandleGroundSlamImpact(Vector3 center, float _)
    {
        AudioManager manager = TryGetManager(out AudioLibrary lib);
        if (manager != null)
            manager.PlayAt(groundSlamCue != null ? groundSlamCue : (lib != null ? lib.GroundSlam : null), center);
    }

    private void HandleShockWaveUsed(Ability _)
    {
        AudioManager manager = TryGetManager(out AudioLibrary lib);
        if (manager != null)
            manager.PlayAt(shockWaveCue != null ? shockWaveCue : (lib != null ? lib.ShockWave : null), transform.position);
    }

    private void HandleDamaged(float amount, GameObject source)
    {
        // Golpe que ja matou deixa so o som de morte tocar no mesmo frame.
        if (health != null && health.IsDead)
            return;

        AudioManager manager = TryGetManager(out AudioLibrary lib);
        if (manager != null)
            manager.PlayAt(playerHurtCue != null ? playerHurtCue : (lib != null ? lib.PlayerHurt : null), transform.position);
    }

    private void HandleDied(Health _)
    {
        AudioManager manager = TryGetManager(out AudioLibrary lib);
        if (manager != null)
            manager.PlayAt(playerDeathCue != null ? playerDeathCue : (lib != null ? lib.PlayerDeath : null), transform.position);
    }

    private void HandleLevelChanged(bool gainedByPlaying)
    {
        // Ponto gasto na UI e Restore de save/cena mandam false; nao toco fanfarra neles.
        if (!gainedByPlaying)
            return;

        AudioManager manager = TryGetManager(out AudioLibrary lib);
        if (manager != null)
            manager.Play(levelUpCue != null ? levelUpCue : (lib != null ? lib.LevelUp : null));
    }

    private void HandleGoldChanged(int total, int delta)
    {
        if (delta <= 0)
            return;

        AudioManager manager = TryGetManager(out AudioLibrary lib);
        if (manager != null)
            manager.Play(goldCue != null ? goldCue : (lib != null ? lib.Gold : null));
    }

    private void HandlePotionChanged(int count, int capacity)
    {
        bool drank = count < lastPotionCount;
        lastPotionCount = count;

        if (!drank)
            return;

        AudioManager manager = TryGetManager(out AudioLibrary lib);
        if (manager != null)
            manager.Play(potionDrinkCue != null ? potionDrinkCue : (lib != null ? lib.PotionDrink : null));
    }
}
