using TMPro;
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
/// numero de XP (mais os pontos gastos, ver abaixo).
///
/// Este script nao mexe em vida nem em dano. Quem aplica o bonus e o
/// CharacterAttributes, junto com a build - ver la o porque.
///
/// So o corpo a corpo da XP: a magia nao dispara o OnAttackHit. Hoje
/// isso nao pesa porque o PlayerMagia.prefab esta guardado.
///
/// Cada nivel acima do 1 tambem da um ponto livre, que o jogador gasta
/// em Poder, Celeridade ou Vitalidade. Os mesmos tres do item, e pelo
/// mesmo caminho: o CharacterAttributes soma os pontos gastos aos do
/// equipamento, antes do bonus do nivel. Sem Alcance porque ele so vale
/// pra magia, que esta guardada. Sem respec: ponto gasto fica gasto.
///
/// Guardo os pontos gastos, e nao os livres. Os livres saem do nivel
/// menos os gastos, entao subir de nivel nao precisa mexer em nada.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCombat))]
public class CharacterLevel : MonoBehaviour
{
    /// <summary>Os atributos em que um ponto livre pode ser gasto.</summary>
    public enum Stat
    {
        Power,
        Haste,
        Vitality
    }

    // Quantos pontos livres cada nivel acima do 1 da. Com 1, o nivel 10
    // tem 9: perto da metade dos 20 da build, o mesmo tamanho dos +9 de
    // tres itens raros.
    public const int PointsPerLevel = 1;

    [Header("Curva")]
    [Tooltip("XP pra passar do nivel 1 pro 2. Do nivel n pro n+1 custa " +
             "isto vezes n, entao o nivel 10 pede 6750 no total. Medi 42 " +
             "inimigos por dungeon: com o boss sao 2260 de XP em cada uma, " +
             "e quem mata tudo chega no nivel 10 no fim da terceira.")]
    [SerializeField, Min(1)] private int firstLevelXp = 150;

    [Tooltip("Daqui pra cima o XP para de subir.")]
    [SerializeField, Min(1)] private int maxLevel = 10;

    [Header("Bonus")]
    [Tooltip("Quanto cada nivel acima do 1 soma de vida maxima e de dano, " +
             "em cima do que a build da. Soma e nao compoe: o nivel 10 e " +
             "+90%, e nao 1,1 elevado a 9.")]
    [SerializeField, Range(0f, 1f)] private float bonusPerLevel = 0.1f;

    [Header("Aviso de nivel")]
    [Tooltip("Cor do \"Nivel N\" que sobe da cabeca ao subir de nivel.")]
    [SerializeField] private Color levelUpColor = new Color(1f, 0.8f, 0.25f, 1f);

    [Tooltip("Tamanho da fonte do TextMeshPro no mundo. O numero de dano " +
             "usa 5; pus maior porque subir de nivel e mais raro e mais " +
             "importante que um golpe.")]
    [SerializeField] private float levelUpSize = 8f;

    [Tooltip("Quanto tempo o aviso fica na tela.")]
    [SerializeField] private float levelUpDuration = 2f;

    [Tooltip("Quanto o aviso sobe, em metros, ate sumir.")]
    [SerializeField] private float levelUpRise = 1.2f;

    [Tooltip("Altura acima do pivo do Player onde o aviso nasce. O pivo " +
             "fica no peito (baseOffset 0,92, capsula de 2 m), entao com " +
             "1,4 o texto nasce um pouco acima da cabeca.")]
    [SerializeField] private float levelUpHeight = 1.4f;

    private PlayerCombat combat;
    private int xp;
    private int level = 1;
    private BonusPoints spentPoints;

    private TextMeshPro levelUpText;
    private float levelUpStartTime = -1f;

    // O XP e os pontos gastos com que o Player entrou na cena, e se a
    // proxima cena e o renascimento de uma morte. Sao estaticos porque a
    // morte recarrega a cena, e o Player que morreu deixa de existir.
    private static int xpOnEntry;
    private static BonusPoints spentOnEntry;
    private static bool pendingRespawn;

    public int Xp => xp;
    public int Level => level;
    public int MaxLevel => maxLevel;
    public bool IsMaxLevel => level >= maxLevel;

    /// <summary>Os pontos que o jogador ja gastou, por atributo.</summary>
    public BonusPoints SpentPoints => spentPoints;

    /// <summary>
    /// Pontos ainda por gastar. Nunca negativo: um save de quando o
    /// PointsPerLevel era maior so fica sem ponto livre, sem tirar nada.
    /// </summary>
    public int FreePoints => Mathf.Max(0, (level - 1) * PointsPerLevel - spentPoints.Total);

    /// <summary>
    /// Por quanto multiplicar a vida maxima e o dano da build. Vale 1 no
    /// nivel 1, entao sem XP nenhum o jogo fica igual ao de antes.
    /// </summary>
    public float Multiplier => 1f + (level - 1) * bonusPerLevel;

    /// <summary>XP total em que o nivel atual comecou.</summary>
    public int CurrentLevelXp => XpForLevel(level);

    /// <summary>XP total do proximo nivel. No maximo, o do proprio nivel maximo.</summary>
    public int NextLevelXp => XpForLevel(Mathf.Min(level + 1, maxLevel));

    /// <summary>
    /// Quanto do nivel atual ja foi, de 0 a 1. E esse numero que a barra
    /// de XP quer.
    /// </summary>
    public float LevelProgress
    {
        get
        {
            if (IsMaxLevel)
                return 1f;

            int inicio = CurrentLevelXp;
            return (float)(xp - inicio) / (NextLevelXp - inicio);
        }
    }

    /// <summary>
    /// O nivel ou os pontos gastos mudaram e o bonus tem que ser aplicado
    /// de novo. O bool diz se subiu jogando (true) ou se foi um ponto
    /// gasto ou XP vindo de fora, do save ou da passagem de cena (false).
    /// So subir jogando enche a vida.
    /// </summary>
    public event System.Action<bool> OnLevelChanged;

    // Campo estatico nao zera sozinho quando o editor entra em Play sem
    // recarregar o dominio. Sem isto, um Play novo depois de morrer
    // nasceria com o XP da sessao anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        xpOnEntry = 0;
        spentOnEntry = default;
        pendingRespawn = false;
    }

    /// <summary>
    /// Pede pro Player da proxima cena nascer com o XP e os pontos de quando este
    /// entrou na cena. Quem chama e o PlayerDeath, logo antes de
    /// recarregar.
    /// </summary>
    public static void RespawnOnNextScene()
    {
        pendingRespawn = true;
    }

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();

        CreateLevelUpText();

        // Morrer recarrega a cena e o Player nasce do zero. Com XP isso
        // seria cruel demais: quem morresse na terceira dungeon voltaria
        // pro nivel 1. Por isso a morte devolve o XP de quando entrei na
        // cena, e perco so o que ganhei nesta tentativa. Os pontos gastos
        // voltam junto: com o XP de volta, um ponto de um nivel perdido
        // ficaria gasto num nivel que nao existe mais.
        //
        // Faco aqui no Awake de proposito. Todo Awake termina antes do
        // Start do CharacterAttributes, entao a build ja aplica o nivel
        // certo e enche a vida dele, sem precisar de evento.
        if (pendingRespawn)
        {
            pendingRespawn = false;
            xp = xpOnEntry;
            level = LevelForXp(xp);
            spentPoints = spentOnEntry;
        }
        else
        {
            // Cena aberta sem morte: jogo novo, passagem ou load do save.
            // Os dois ultimos chamam o Restore logo depois, e e ele que
            // anota a entrada de verdade.
            xpOnEntry = 0;
            spentOnEntry = default;
        }
    }

    private void OnEnable()
    {
        combat.OnAttackHit += HandleHit;
    }

    private void OnDisable()
    {
        combat.OnAttackHit -= HandleHit;

        // O Update para junto com o componente. Sem isto o aviso ficaria
        // parado no ar pra sempre.
        levelUpStartTime = -1f;

        if (levelUpText != null)
            levelUpText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (levelUpText != null)
            Destroy(levelUpText.gameObject);
    }

    private void Update()
    {
        if (levelUpStartTime < 0f)
            return;

        float t = (Time.time - levelUpStartTime) / levelUpDuration;

        if (t >= 1f)
        {
            levelUpStartTime = -1f;
            levelUpText.gameObject.SetActive(false);
            return;
        }

        DrawLevelUp(t);
    }

    private void HandleHit(Health alvo, float dano)
    {
        // Golpe que nao matou nao da nada. A mesma morte nao conta duas
        // vezes: o Health ignora dano em quem ja morreu, entao o acerto
        // com o alvo morto so chega uma vez, no golpe que matou.
        if (!alvo.IsDead)
            return;

        AddXp(Mathf.RoundToInt(alvo.Max));
    }

    /// <summary>
    /// Soma XP. E publico pra quest e debug poderem dar XP tambem, sem
    /// fingir uma morte.
    /// </summary>
    public void AddXp(int quantia)
    {
        if (quantia <= 0 || IsMaxLevel)
            return;

        int antes = level;

        // Paro o XP no nivel maximo em vez de deixar acumulando. Sem o
        // teto o HUD mostraria um XP que nao serve pra nada.
        xp = Mathf.Min(xp + quantia, XpForLevel(maxLevel));
        level = LevelForXp(xp);

        if (level == antes)
            return;

        Debug.Log($"[{name}] Subiu pro nivel {level} com {xp} de XP.", this);

        OnLevelChanged?.Invoke(true);
        ShowLevelUp();
    }

    /// <summary>
    /// Atalho de teste: botao direito no componente, em Play. 1000 e o
    /// XP de um boss, e com sete cliques chega no nivel 10 - matando
    /// inimigo de 30 seriam 225.
    /// </summary>
    [ContextMenu("Teste: ganhar 1000 de XP")]
    private void AddTestXp()
    {
        AddXp(1000);
    }

    /// <summary>
    /// Gasta um ponto livre no atributo. Sem ponto livre devolve false e
    /// nada muda. Nao enche a vida: a Vitalidade sobe o teto e a vida
    /// atual fica onde estava, igual a equipar um item.
    /// </summary>
    public bool TrySpendPoint(Stat atributo)
    {
        if (FreePoints <= 0)
            return false;

        spentPoints += OnePoint(atributo);

        OnLevelChanged?.Invoke(false);
        return true;
    }

    private static BonusPoints OnePoint(Stat atributo)
    {
        switch (atributo)
        {
            case Stat.Power: return new BonusPoints(1, 0, 0);
            case Stat.Haste: return new BonusPoints(0, 1, 0);
            default: return new BonusPoints(0, 0, 1);
        }
    }

    /// <summary>
    /// Poe o XP e os pontos gastos que vieram de fora: do save ou da
    /// passagem de cena. Nao enche a vida, porque quem restaura devolve a
    /// vida que estava logo em seguida.
    ///
    /// Chamar ANTES de devolver a vida. O Revive corta a vida no teto, e
    /// o teto depende do nivel e da Vitalidade: com o nivel 1 ainda
    /// valendo, uma vida salva de 150 voltaria 100.
    /// </summary>
    public void Restore(int xpTotal, BonusPoints gastos)
    {
        xp = Mathf.Clamp(xpTotal, 0, XpForLevel(maxLevel));
        level = LevelForXp(xp);

        // Numero negativo so viria de um save editado na mao.
        spentPoints = new BonusPoints(
            Mathf.Max(0, gastos.Power),
            Mathf.Max(0, gastos.Haste),
            Mathf.Max(0, gastos.Vitality));

        // O que veio de fora e o da entrada nesta cena: se eu morrer
        // daqui, e pra ele que eu volto.
        xpOnEntry = xp;
        spentOnEntry = spentPoints;

        OnLevelChanged?.Invoke(false);
    }

    /// <summary>
    /// XP total pra chegar no nivel. Cada nivel custa firstLevelXp
    /// vezes o nivel de onde se sai, entao o total e a soma de 1 ate n-1:
    /// com 150, o nivel 2 pede 150, o 3 pede 450 e o 4 pede 900.
    /// </summary>
    public int XpForLevel(int alvo)
    {
        alvo = Mathf.Clamp(alvo, 1, maxLevel);

        // (n - 1) * n e sempre par, entao a divisao nao perde nada.
        return firstLevelXp * ((alvo - 1) * alvo / 2);
    }

    private int LevelForXp(int xpTotal)
    {
        int n = 1;

        while (n < maxLevel && xpTotal >= XpForLevel(n + 1))
            n++;

        return n;
    }

    /// <summary>
    /// Monto o texto por codigo, igual o DamageNumbers, pra o aviso nao
    /// precisar de prefab nem de Canvas. Fica fora do Player e nao como
    /// filho: o Player gira pra todo lado no combate e o texto giraria
    /// junto. Sigo a posicao dele no Update.
    /// </summary>
    private void CreateLevelUpText()
    {
        var objeto = new GameObject("AvisoDeNivel");

        levelUpText = objeto.AddComponent<TextMeshPro>();
        levelUpText.alignment = TextAlignmentOptions.Center;
        levelUpText.fontStyle = FontStyles.Bold;
        levelUpText.textWrappingMode = TextWrappingModes.NoWrap;
        levelUpText.rectTransform.sizeDelta = new Vector2(8f, 2f);

        // O mesmo contorno escuro do numero de dano, pra ler em cima do
        // chao claro da cidade e do esgoto escuro.
        levelUpText.outlineWidth = 0.25f;
        levelUpText.outlineColor = new Color32(20, 12, 8, 255);

        objeto.SetActive(false);
    }

    private void ShowLevelUp()
    {
        // A string nova nasce so aqui, uma vez por nivel, e nao por frame.
        levelUpText.text = "Nível " + level;
        levelUpText.fontSize = levelUpSize;
        levelUpText.color = levelUpColor;
        levelUpStartTime = Time.time;

        // A camera nunca gira, entao copiar a rotacao dela uma vez deixa
        // o texto de frente o tempo todo.
        Camera cam = Camera.main;

        if (cam != null)
            levelUpText.transform.rotation = cam.transform.rotation;

        levelUpText.gameObject.SetActive(true);
        DrawLevelUp(0f);
    }

    private void DrawLevelUp(float t)
    {
        // A mesma curva do DamageNumbers: sobe rapido e assenta, nasce
        // grande e encolhe no primeiro quinto do tempo, e so apaga na
        // segunda metade, pra dar tempo de ler.
        float subiu = 1f - (1f - t) * (1f - t);
        float pulo = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t / 0.2f));
        float alfa = 1f - Mathf.Clamp01((t - 0.5f) / 0.5f);

        levelUpText.transform.position =
            transform.position + Vector3.up * (levelUpHeight + levelUpRise * subiu);
        levelUpText.transform.localScale = Vector3.one * pulo;

        Color c = levelUpText.color;
        c.a = alfa;
        levelUpText.color = c;
    }
}
