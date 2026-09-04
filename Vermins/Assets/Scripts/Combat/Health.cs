using UnityEngine;

/// <summary>
/// Vida de qualquer coisa que pode morrer: jogador, inimigo, barril,
/// o que aparecer. Fiz generico de proposito pra ninguem precisar
/// escrever um "VidaDoInimigo" separado depois.
///
/// Este script so guarda o numero e avisa quem se importa. Quem decide
/// COMO morre - sumir da cena, tocar animacao, dropar item, abrir tela
/// de game over - escuta o OnDied e faz do seu jeito.
/// </summary>
[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Morte")]
    [Tooltip("Pra inimigo geralmente sim. Pro jogador nao, senao ele " +
             "some da cena e nao sobra nada pra respawnar.")]
    [SerializeField] private bool destroyOnDeath = false;

    [Tooltip("Segundos entre morrer e sumir. Deixa espaco pra animacao " +
             "de morte quando a gente tiver uma.")]
    [SerializeField] private float destroyDelay = 2f;

    public float Max => maxHealth;
    public float Current { get; private set; }

    /// <summary>Vida de 0 a 1. E esse numero que a barra de vida quer.</summary>
    public float Normalized => maxHealth <= 0f ? 0f : Current / maxHealth;

    public bool IsDead => Current <= 0f;

    /// <summary>
    /// (vida atual, vida maxima). Dispara em dano, cura e revive.
    ///
    /// Pra quem for fazer a UI: assina esse evento E le Current/Max uma
    /// vez ao ligar, porque o evento so avisa quando muda - se voce so
    /// assinar, a barra comeca vazia ate levar o primeiro dano.
    /// </summary>
    public event System.Action<float, float> OnChanged;

    /// <summary>
    /// (dano recebido, quem causou). Pra som de impacto, VFX, tremer a
    /// camera, numero de dano flutuante.
    /// </summary>
    public event System.Action<float, GameObject> OnDamaged;

    public event System.Action<Health> OnDied;

    private void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(float amount, GameObject source = null)
    {
        // Morto nao leva mais dano. Sem essa linha o OnDied dispararia
        // de novo a cada golpe no corpo, e contagem de kill, drop e
        // quest sairiam todos dobrados.
        if (IsDead || amount <= 0f)
            return;

        Current = Mathf.Max(0f, Current - amount);

        OnDamaged?.Invoke(amount, source);
        OnChanged?.Invoke(Current, maxHealth);

        if (IsDead)
            Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        Current = Mathf.Min(maxHealth, Current + amount);
        OnChanged?.Invoke(Current, maxHealth);
    }

    /// <summary>
    /// Troca a vida maxima. Quem chama hoje e o sistema de atributos,
    /// no comeco da partida.
    ///
    /// O encherAVida existe porque mexer no teto tem duas respostas
    /// certas, dependendo de quando. Ligando a build, o personagem tem
    /// que nascer com a barra cheia. Ganhando +20 de vida maxima de um
    /// item no meio da dungeon, o teto sobe mas a barra nao deveria
    /// encher de graca - senao equipar e desequipar vira cura infinita.
    /// </summary>
    public void DefinirVidaMaxima(float nova, bool encherAVida)
    {
        // Teto zero deixaria o personagem morto no instante em que
        // nasce, e sem jeito de reviver.
        maxHealth = Mathf.Max(1f, nova);

        Current = encherAVida ? maxHealth : Mathf.Min(Current, maxHealth);

        OnChanged?.Invoke(Current, maxHealth);
    }

    /// <summary>
    /// Volta a viver. Sem argumento volta com a vida cheia.
    /// Vai servir pro respawn e pro load do save.
    /// </summary>
    public void Revive(float health = -1f)
    {
        Current = health <= 0f
            ? maxHealth
            : Mathf.Min(health, maxHealth);

        OnChanged?.Invoke(Current, maxHealth);
    }

    private void Die()
    {
        OnDied?.Invoke(this);

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }
}
