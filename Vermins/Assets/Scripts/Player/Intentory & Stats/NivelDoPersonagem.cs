using UnityEngine;

/// <summary>
/// Experiencia e nivel do Player.
///
/// O GDR promete que o personagem fica perceptivelmente mais forte entre
/// a primeira e a terceira dungeon, e ate aqui nada mudava de uma pra
/// outra. Agora todo inimigo que a espada mata da XP, e cada nivel soma
/// 10% de vida maxima e de dano.
///
/// O XP sai da vida maxima de quem morreu: o inimigo da dungeon vale 30
/// e o boss vale 1000. Assim nao precisei mexer em prefab de inimigo nem
/// criar um campo de "quanto XP eu valho" - bicho mais duro ja vale mais.
///
/// Guardo so o XP total. O nivel sai dele pela curva, entao os dois
/// nunca discordam, e o save e a passagem de cena so precisam levar um
/// numero.
///
/// Este script nao mexe em vida nem em dano. Quem aplica o bonus e o
/// AtributosDoPersonagem, junto com a build - ver la o porque.
///
/// So o corpo a corpo da XP: a magia nao dispara o OnGolpeAcertou. Hoje
/// isso nao pesa porque o PlayerMagia.prefab esta guardado.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCombat))]
public class NivelDoPersonagem : MonoBehaviour
{
    [Header("Curva")]
    [Tooltip("XP pra passar do nivel 1 pro 2. Do nivel n pro n+1 custa " +
             "isto vezes n, entao o nivel 10 pede 6750 no total. Medi 42 " +
             "inimigos por dungeon: com o boss sao 2260 de XP em cada uma, " +
             "e quem mata tudo chega no nivel 10 no fim da terceira.")]
    [SerializeField, Min(1)] private int xpDoPrimeiroNivel = 150;

    [Tooltip("Daqui pra cima o XP para de subir.")]
    [SerializeField, Min(1)] private int nivelMaximo = 10;

    [Header("Bonus")]
    [Tooltip("Quanto cada nivel acima do 1 soma de vida maxima e de dano, " +
             "em cima do que a build da. Soma e nao compoe: o nivel 10 e " +
             "+90%, e nao 1,1 elevado a 9.")]
    [SerializeField, Range(0f, 1f)] private float bonusPorNivel = 0.1f;

    private PlayerCombat combate;
    private int xp;
    private int nivel = 1;

    public int Xp => xp;
    public int Nivel => nivel;
    public int NivelMaximo => nivelMaximo;
    public bool NoMaximo => nivel >= nivelMaximo;

    /// <summary>
    /// Por quanto multiplicar a vida maxima e o dano da build. Vale 1 no
    /// nivel 1, entao sem XP nenhum o jogo fica igual ao de antes.
    /// </summary>
    public float Multiplicador => 1f + (nivel - 1) * bonusPorNivel;

    /// <summary>XP total em que o nivel atual comecou.</summary>
    public int XpDoNivelAtual => XpParaChegarNoNivel(nivel);

    /// <summary>XP total do proximo nivel. No maximo, o do proprio nivel maximo.</summary>
    public int XpDoProximoNivel => XpParaChegarNoNivel(Mathf.Min(nivel + 1, nivelMaximo));

    /// <summary>
    /// Quanto do nivel atual ja foi, de 0 a 1. E esse numero que a barra
    /// de XP quer.
    /// </summary>
    public float ProgressoNoNivel
    {
        get
        {
            if (NoMaximo)
                return 1f;

            int inicio = XpDoNivelAtual;
            return (float)(xp - inicio) / (XpDoProximoNivel - inicio);
        }
    }

    /// <summary>
    /// O nivel mudou e o bonus tem que ser aplicado de novo. O bool diz
    /// se subiu jogando (true) ou se o XP veio de fora, do save ou da
    /// passagem de cena (false). So subir jogando enche a vida.
    /// </summary>
    public event System.Action<bool> OnNivelMudou;

    private void Awake()
    {
        combate = GetComponent<PlayerCombat>();
    }

    private void OnEnable()
    {
        combate.OnGolpeAcertou += AoAcertar;
    }

    private void OnDisable()
    {
        combate.OnGolpeAcertou -= AoAcertar;
    }

    private void AoAcertar(Health alvo, float dano)
    {
        // Golpe que nao matou nao da nada. A mesma morte nao conta duas
        // vezes: o Health ignora dano em quem ja morreu, entao o acerto
        // com o alvo morto so chega uma vez, no golpe que matou.
        if (!alvo.IsDead)
            return;

        Ganhar(Mathf.RoundToInt(alvo.Max));
    }

    /// <summary>
    /// Soma XP. E publico pra quest e debug poderem dar XP tambem, sem
    /// fingir uma morte.
    /// </summary>
    public void Ganhar(int quantia)
    {
        if (quantia <= 0 || NoMaximo)
            return;

        int antes = nivel;

        // Paro o XP no nivel maximo em vez de deixar acumulando. Sem o
        // teto o HUD mostraria um XP que nao serve pra nada.
        xp = Mathf.Min(xp + quantia, XpParaChegarNoNivel(nivelMaximo));
        nivel = NivelDoXp(xp);

        if (nivel == antes)
            return;

        Debug.Log($"[{name}] Subiu pro nivel {nivel} com {xp} de XP.", this);

        OnNivelMudou?.Invoke(true);
    }

    /// <summary>
    /// Poe o XP que veio de fora: do save ou da passagem de cena. Nao
    /// enche a vida, porque quem restaura devolve a vida que estava logo
    /// em seguida.
    ///
    /// Chamar ANTES de devolver a vida. O Revive corta a vida no teto, e
    /// o teto depende do nivel: com o nivel 1 ainda valendo, uma vida
    /// salva de 150 voltaria 100.
    /// </summary>
    public void Restaurar(int xpTotal)
    {
        xp = Mathf.Clamp(xpTotal, 0, XpParaChegarNoNivel(nivelMaximo));
        nivel = NivelDoXp(xp);

        OnNivelMudou?.Invoke(false);
    }

    /// <summary>
    /// XP total pra chegar no nivel. Cada nivel custa xpDoPrimeiroNivel
    /// vezes o nivel de onde se sai, entao o total e a soma de 1 ate n-1:
    /// com 150, o nivel 2 pede 150, o 3 pede 450 e o 4 pede 900.
    /// </summary>
    public int XpParaChegarNoNivel(int alvo)
    {
        alvo = Mathf.Clamp(alvo, 1, nivelMaximo);

        // (n - 1) * n e sempre par, entao a divisao nao perde nada.
        return xpDoPrimeiroNivel * ((alvo - 1) * alvo / 2);
    }

    private int NivelDoXp(int xpTotal)
    {
        int n = 1;

        while (n < nivelMaximo && xpTotal >= XpParaChegarNoNivel(n + 1))
            n++;

        return n;
    }
}
