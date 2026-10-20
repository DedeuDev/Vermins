using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

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
/// CharacterAttributes, junto com a build - ver la o porque.
///
/// So o corpo a corpo da XP: a magia nao dispara o OnAttackHit. Hoje
/// isso nao pesa porque o PlayerMagia.prefab esta guardado.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCombat))]
public class CharacterLevel : MonoBehaviour
{
    [Header("Curva")]
    [Tooltip("XP pra passar do nivel 1 pro 2. Do nivel n pro n+1 custa " +
             "isto vezes n, entao o nivel 10 pede 6750 no total. Medi 42 " +
             "inimigos por dungeon: com o boss sao 2260 de XP em cada uma, " +
             "e quem mata tudo chega no nivel 10 no fim da terceira.")]
    [FormerlySerializedAs("xpDoPrimeiroNivel")]
    [SerializeField, Min(1)] private int firstLevelXp = 150;

    [Tooltip("Daqui pra cima o XP para de subir.")]
    [FormerlySerializedAs("nivelMaximo")]
    [SerializeField, Min(1)] private int maxLevel = 10;

    [Header("Bonus")]
    [Tooltip("Quanto cada nivel acima do 1 soma de vida maxima e de dano, " +
             "em cima do que a build da. Soma e nao compoe: o nivel 10 e " +
             "+90%, e nao 1,1 elevado a 9.")]
    [FormerlySerializedAs("bonusPorNivel")]
    [SerializeField, Range(0f, 1f)] private float bonusPerLevel = 0.1f;

    [Header("Aviso de nivel")]
    [Tooltip("Cor do \"Nivel N\" que sobe da cabeca ao subir de nivel.")]
    [FormerlySerializedAs("corDoAviso")]
    [SerializeField] private Color levelUpColor = new Color(1f, 0.8f, 0.25f, 1f);

    [Tooltip("Tamanho da fonte do TextMeshPro no mundo. O numero de dano " +
             "usa 5; pus maior porque subir de nivel e mais raro e mais " +
             "importante que um golpe.")]
    [FormerlySerializedAs("tamanhoDoAviso")]
    [SerializeField] private float levelUpSize = 8f;

    [Tooltip("Quanto tempo o aviso fica na tela.")]
    [FormerlySerializedAs("duracaoDoAviso")]
    [SerializeField] private float levelUpDuration = 2f;

    [Tooltip("Quanto o aviso sobe, em metros, ate sumir.")]
    [FormerlySerializedAs("subidaDoAviso")]
    [SerializeField] private float levelUpRise = 1.2f;

    [Tooltip("Altura acima do pivo do Player onde o aviso nasce. O pivo " +
             "fica no peito (baseOffset 0,92, capsula de 2 m), entao com " +
             "1,4 o texto nasce um pouco acima da cabeca.")]
    [FormerlySerializedAs("alturaDoAviso")]
    [SerializeField] private float levelUpHeight = 1.4f;

    private PlayerCombat combat;
    private int xp;
    private int level = 1;

    private TextMeshPro levelUpText;
    private float levelUpStartTime = -1f;

    // O XP com que o Player entrou na cena, e se a proxima cena e o
    // renascimento de uma morte. Sao estaticos porque a morte recarrega a
    // cena, e o Player que morreu deixa de existir.
    private static int xpOnEntry;
    private static bool pendingRespawn;

    public int Xp => xp;
    public int Level => level;
    public int MaxLevel => maxLevel;
    public bool IsMaxLevel => level >= maxLevel;

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
    /// O nivel mudou e o bonus tem que ser aplicado de novo. O bool diz
    /// se subiu jogando (true) ou se o XP veio de fora, do save ou da
    /// passagem de cena (false). So subir jogando enche a vida.
    /// </summary>
    public event System.Action<bool> OnLevelChanged;

    // Campo estatico nao zera sozinho quando o editor entra em Play sem
    // recarregar o dominio. Sem isto, um Play novo depois de morrer
    // nasceria com o XP da sessao anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        xpOnEntry = 0;
        pendingRespawn = false;
    }

    /// <summary>
    /// Pede pro Player da proxima cena nascer com o XP de quando este
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
        // cena, e perco so o que ganhei nesta tentativa.
        //
        // Faco aqui no Awake de proposito. Todo Awake termina antes do
        // Start do CharacterAttributes, entao a build ja aplica o nivel
        // certo e enche a vida dele, sem precisar de evento.
        if (pendingRespawn)
        {
            pendingRespawn = false;
            xp = xpOnEntry;
            level = LevelForXp(xp);
        }
        else
        {
            // Cena aberta sem morte: jogo novo, passagem ou load do save.
            // Os dois ultimos chamam o Restore logo depois, e e ele que
            // anota a entrada de verdade.
            xpOnEntry = 0;
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
    /// Poe o XP que veio de fora: do save ou da passagem de cena. Nao
    /// enche a vida, porque quem restaura devolve a vida que estava logo
    /// em seguida.
    ///
    /// Chamar ANTES de devolver a vida. O Revive corta a vida no teto, e
    /// o teto depende do nivel: com o nivel 1 ainda valendo, uma vida
    /// salva de 150 voltaria 100.
    /// </summary>
    public void Restore(int xpTotal)
    {
        xp = Mathf.Clamp(xpTotal, 0, XpForLevel(maxLevel));
        level = LevelForXp(xp);

        // O XP que veio de fora e o da entrada nesta cena: se eu morrer
        // daqui, e pra ele que eu volto.
        xpOnEntry = xp;

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
