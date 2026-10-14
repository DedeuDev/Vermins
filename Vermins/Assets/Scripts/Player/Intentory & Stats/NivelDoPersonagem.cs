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

    [Header("Aviso de nivel")]
    [Tooltip("Cor do \"Nivel N\" que sobe da cabeca ao subir de nivel.")]
    [SerializeField] private Color corDoAviso = new Color(1f, 0.8f, 0.25f, 1f);

    [Tooltip("Tamanho da fonte do TextMeshPro no mundo. O numero de dano " +
             "usa 5; pus maior porque subir de nivel e mais raro e mais " +
             "importante que um golpe.")]
    [SerializeField] private float tamanhoDoAviso = 8f;

    [Tooltip("Quanto tempo o aviso fica na tela.")]
    [SerializeField] private float duracaoDoAviso = 2f;

    [Tooltip("Quanto o aviso sobe, em metros, ate sumir.")]
    [SerializeField] private float subidaDoAviso = 1.2f;

    [Tooltip("Altura acima do pivo do Player onde o aviso nasce. O pivo " +
             "fica no peito (baseOffset 0,92, capsula de 2 m), entao com " +
             "1,4 o texto nasce um pouco acima da cabeca.")]
    [SerializeField] private float alturaDoAviso = 1.4f;

    private PlayerCombat combate;
    private int xp;
    private int nivel = 1;

    private TextMeshPro aviso;
    private float avisoNasceu = -1f;

    // O XP com que o Player entrou na cena, e se a proxima cena e o
    // renascimento de uma morte. Sao estaticos porque a morte recarrega a
    // cena, e o Player que morreu deixa de existir.
    private static int xpDaEntrada;
    private static bool renascer;

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

    // Campo estatico nao zera sozinho quando o editor entra em Play sem
    // recarregar o dominio. Sem isto, um Play novo depois de morrer
    // nasceria com o XP da sessao anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Zerar()
    {
        xpDaEntrada = 0;
        renascer = false;
    }

    /// <summary>
    /// Pede pro Player da proxima cena nascer com o XP de quando este
    /// entrou na cena. Quem chama e o PlayerDeath, logo antes de
    /// recarregar.
    /// </summary>
    public static void RenascerNaProximaCena()
    {
        renascer = true;
    }

    private void Awake()
    {
        combate = GetComponent<PlayerCombat>();

        CriarAviso();

        // Morrer recarrega a cena e o Player nasce do zero. Com XP isso
        // seria cruel demais: quem morresse na terceira dungeon voltaria
        // pro nivel 1. Por isso a morte devolve o XP de quando entrei na
        // cena, e perco so o que ganhei nesta tentativa.
        //
        // Faco aqui no Awake de proposito. Todo Awake termina antes do
        // Start do AtributosDoPersonagem, entao a build ja aplica o nivel
        // certo e enche a vida dele, sem precisar de evento.
        if (renascer)
        {
            renascer = false;
            xp = xpDaEntrada;
            nivel = NivelDoXp(xp);
        }
        else
        {
            // Cena aberta sem morte: jogo novo, passagem ou load do save.
            // Os dois ultimos chamam o Restaurar logo depois, e e ele que
            // anota a entrada de verdade.
            xpDaEntrada = 0;
        }
    }

    private void OnEnable()
    {
        combate.OnGolpeAcertou += AoAcertar;
    }

    private void OnDisable()
    {
        combate.OnGolpeAcertou -= AoAcertar;

        // O Update para junto com o componente. Sem isto o aviso ficaria
        // parado no ar pra sempre.
        avisoNasceu = -1f;

        if (aviso != null)
            aviso.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (aviso != null)
            Destroy(aviso.gameObject);
    }

    private void Update()
    {
        if (avisoNasceu < 0f)
            return;

        float t = (Time.time - avisoNasceu) / duracaoDoAviso;

        if (t >= 1f)
        {
            avisoNasceu = -1f;
            aviso.gameObject.SetActive(false);
            return;
        }

        DesenharAviso(t);
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
        MostrarAviso();
    }

    /// <summary>
    /// Atalho de teste: botao direito no componente, em Play. 1000 e o
    /// XP de um boss, e com sete cliques chega no nivel 10 - matando
    /// inimigo de 30 seriam 225.
    /// </summary>
    [ContextMenu("Teste: ganhar 1000 de XP")]
    private void GanharMilDeTeste()
    {
        Ganhar(1000);
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

        // O XP que veio de fora e o da entrada nesta cena: se eu morrer
        // daqui, e pra ele que eu volto.
        xpDaEntrada = xp;

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

    /// <summary>
    /// Monto o texto por codigo, igual o NumerosDeDano, pra o aviso nao
    /// precisar de prefab nem de Canvas. Fica fora do Player e nao como
    /// filho: o Player gira pra todo lado no combate e o texto giraria
    /// junto. Sigo a posicao dele no Update.
    /// </summary>
    private void CriarAviso()
    {
        var objeto = new GameObject("AvisoDeNivel");

        aviso = objeto.AddComponent<TextMeshPro>();
        aviso.alignment = TextAlignmentOptions.Center;
        aviso.fontStyle = FontStyles.Bold;
        aviso.textWrappingMode = TextWrappingModes.NoWrap;
        aviso.rectTransform.sizeDelta = new Vector2(8f, 2f);

        // O mesmo contorno escuro do numero de dano, pra ler em cima do
        // chao claro da cidade e do esgoto escuro.
        aviso.outlineWidth = 0.25f;
        aviso.outlineColor = new Color32(20, 12, 8, 255);

        objeto.SetActive(false);
    }

    private void MostrarAviso()
    {
        // A string nova nasce so aqui, uma vez por nivel, e nao por frame.
        aviso.text = "Nível " + nivel;
        aviso.fontSize = tamanhoDoAviso;
        aviso.color = corDoAviso;
        avisoNasceu = Time.time;

        // A camera nunca gira, entao copiar a rotacao dela uma vez deixa
        // o texto de frente o tempo todo.
        Camera cam = Camera.main;

        if (cam != null)
            aviso.transform.rotation = cam.transform.rotation;

        aviso.gameObject.SetActive(true);
        DesenharAviso(0f);
    }

    private void DesenharAviso(float t)
    {
        // A mesma curva do NumerosDeDano: sobe rapido e assenta, nasce
        // grande e encolhe no primeiro quinto do tempo, e so apaga na
        // segunda metade, pra dar tempo de ler.
        float subiu = 1f - (1f - t) * (1f - t);
        float pulo = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t / 0.2f));
        float alfa = 1f - Mathf.Clamp01((t - 0.5f) / 0.5f);

        aviso.transform.position =
            transform.position + Vector3.up * (alturaDoAviso + subidaDoAviso * subiu);
        aviso.transform.localScale = Vector3.one * pulo;

        Color c = aviso.color;
        c.a = alfa;
        aviso.color = c;
    }
}
