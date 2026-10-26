using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// HUD de debug do jogador: vida, nivel, pocoes, ouro, joias, equipamento, alvo, velocidade e fps num canto da tela.
///
/// E TEMPORARIO. A UI de verdade e do Rogger - isto aqui existe so pra
/// gente testar combate enxergando os numeros ate a barra dele ficar
/// pronta. Usei OnGUI de proposito: nao cria Canvas nem objeto nenhum,
/// entao nao tem como conflitar com o que ele montar, e o visual cinza
/// nao engana ninguem de que e a UI final. Quando a dele chegar, e so
/// tirar este componente do Player.prefab e apagar o arquivo.
///
/// Tem tambem os controles do fps: vsync, teto ligado ou desligado e o
/// numero do teto. Sao pra comparar o jogo com e sem limite na mesma sessao; a
/// escolha nao e salva e volta pro padrao quando o jogo abre de novo.
///
/// O F2 abre a loja de debug, um segundo painel a esquerda com botoes
/// que chamam a HubShop. Serve pra testar a conta da loja antes da
/// tela do Rogger existir, e sai junto com este HUD.
///
/// Embaixo do XP fica a linha dos pontos livres do nivel, com os botoes
/// +P +C +V na mesma linha enquanto sobrar ponto. Na mesma linha pra o
/// painel nao mudar de altura quando os botoes somem. Com o mouse em cima,
/// cada botao mostra o que faz e a previa, tipo "dano 19,5 -> 22,1": sem
/// respec, quem gasta tem que ver o numero antes de clicar.
///
/// So desenha no editor e em Development Build. A classe continua
/// existindo na build normal de proposito: se eu sumisse com ela
/// inteira, o Player.prefab ficaria com "Missing Script" na build.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerDebugHUD : MonoBehaviour
{
    [SerializeField] private bool visible = true;

    [Tooltip("Liga e desliga o HUD, pra ele nao aparecer em print e video.")]
    [SerializeField] private Key toggleKey = Key.F1;

    [Tooltip("Abre e fecha a loja de debug. Funciona em qualquer cena, " +
             "nao so no hub: e pra testar a conta, nao o lugar.")]
    [SerializeField] private Key shopKey = Key.F2;

    [Header("Icones das linhas")]
    [Tooltip("Os mesmos de Placeholders/Icones. Vazio, a linha ganha um " +
             "quadradinho na cor do rotulo do item no chao.")]
    [SerializeField] private Texture2D potionIcon;
    [SerializeField] private Texture2D goldIcon;
    [SerializeField] private Texture2D jewelIcon;

    /// <summary>
    /// Se o mouse esta em cima dos controles do teto de fps ou da loja
    /// de debug.
    ///
    /// O PlayerController pergunta isto antes de transformar o clique em
    /// ordem. O EventSystem nao sabe do OnGUI, entao sem a pergunta o
    /// clique no slider tambem mandava o jogador andar pro canto da tela.
    /// So os controles barram: o resto do painel continua deixando o
    /// clique passar pro chao, como sempre deixou.
    /// </summary>
    public static bool IsCursorOverControls
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Mouse mouse = Mouse.current;

            if (mouse == null)
                return false;

            // O OnGUI conta a altura de cima pra baixo e o mouse de baixo
            // pra cima.
            Vector2 cursor = mouse.position.ReadValue();
            cursor.y = Screen.height - cursor.y;

            return controlsRect.Contains(cursor) || shopRect.Contains(cursor) ||
                   pointsRect.Contains(cursor);
#else
            return false;
#endif
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Onde o painel fica e a largura dele, na tela de referencia de 1080
    // de altura. A altura sai da soma das linhas, em PanelHeight.
    //
    // Fica no canto superior DIREITO. No esquerdo ele ficava por cima do
    // painel de missao do Eduardo (QuestUI): medi em 1920x1080 o painel
    // dele de 276 a 764 px do topo, e o meu ia ate uns 430. O dialogo
    // fica embaixo no centro e o seletor de dungeon no meio, ate x 1477;
    // o canto direito nao bate em nenhum dos tres.
    private const float ScreenMargin = 16f;
    private const float PanelY = 16f;
    private const float PanelWidth = 380f;

    // Medidas de dentro do painel, casadas com o que o GUILayout
    // desenhava antes, pra o painel nao mudar de cara.
    private const float Padding = 6f;
    private const float TextHeight = 32f;
    private const float BarHeight = 14f;
    private const float ButtonHeight = 28f;
    private const float SliderHeight = 20f;
    private const float Spacing = 6f;

    // O menor teto que o slider oferece, e o maior quando o monitor nao
    // informa a taxa. Com a taxa conhecida o maior e ela.
    private const int MinFpsCap = 30;
    private const int FallbackMaxFpsCap = 240;

    // Velocidade e distancia do alvo mudam todo frame enquanto alguem
    // anda. Remonto os textos delas so 10 vezes por segundo: a 200 fps
    // ninguem le um numero que troca a cada frame, e cada troca e uma
    // string nova.
    private const float LiveTextInterval = 0.1f;

    // Onde os controles do fps foram desenhados, em pixels da tela.
    // Vazio enquanto o HUD esta escondido.
    private static Rect controlsRect;

    // O painel da loja inteiro, em pixels da tela. Ele e todo botao,
    // entao barra o clique no painel todo. Vazio com a loja fechada.
    private static Rect shopRect;

    // Os tres botoes dos pontos livres juntos, em pixels da tela. Vazio
    // sem ponto livre, que e quando os botoes nao aparecem.
    private static Rect pointsRect;

    // Largura de cada botao +P +C +V e o vao entre eles.
    private const float PointButtonWidth = 40f;
    private const float PointButtonGap = 4f;

    private bool isShopOpen;

    private GUIStyle style;
    private GUIStyle buttonStyle;
    private GUIStyle trackStyle;
    private GUIStyle thumbStyle;

    private Health health;
    private PotionBelt belt;
    private GoldWallet wallet;
    private JewelPouch pouch;
    private CharacterLevel level;
    private CharacterEquipment equipment;
    private HubShop shop;
    private PlayerCombat combat;
    private CharacterAttributes attributes;
    private NavMeshAgent agent;

    // De quanto em quanto tempo a linha do fps troca de numero.
    // Frame a frame o valor treme e nao da pra ler.
    private const float FpsWindow = 0.5f;

    private float windowTime;
    private int windowFrames;
    private float windowWorst;
    private float fps;
    private float worstFrame;

    private float nextLiveTextTime;

    // Os textos que nunca mudam. Os botoes de ligar alternam entre dois.
    private GUIContent header;
    private static readonly GUIContent VSyncOn = new GUIContent("vsync ligado");
    private static readonly GUIContent VSyncOff = new GUIContent("vsync desligado");
    private static readonly GUIContent CapOn = new GUIContent("limite ligado");
    private static readonly GUIContent CapOff = new GUIContent("limite desligado");
    private readonly CachedText healthLine = new CachedText();
    private readonly CachedText levelLine = new CachedText();
    private readonly CachedText pointsLine = new CachedText();

    // Os botoes dos pontos. O texto e fixo; o que muda e o tooltip, com a
    // previa do numero.
    private readonly CachedText powerButton = new CachedText();
    private readonly CachedText hasteButton = new CachedText();
    private readonly CachedText vitalityButton = new CachedText();

    // O tooltip que o OnGUI desenha. Reaproveito o mesmo GUIContent e so
    // troco a referencia do texto, pra nao criar um por frame.
    private readonly GUIContent tooltipContent = new GUIContent(string.Empty);
    private GUIStyle tooltipStyle;
    private const float TooltipWidth = 380f;
    private const float TooltipLineHeight = 26f;
    private readonly CachedText potionsLine = new CachedText();
    private readonly CachedText goldLine = new CachedText();
    private readonly CachedText jewelsLine = new CachedText();
    private readonly CachedText equipmentLine = new CachedText();
    private readonly CachedText targetLine = new CachedText();
    private readonly CachedText speedLine = new CachedText();
    private readonly CachedText fpsLine = new CachedText();
    private readonly CachedText capLine = new CachedText();
    private readonly CachedText defaultCapLine = new CachedText();

    // Os botoes da loja, com o preco no texto.
    private GUIContent shopHeader;
    private static readonly GUIContent AddGoldLabel = new GUIContent("+100 de ouro (teste)");
    private readonly CachedText potionButton = new CachedText();
    private readonly CachedText jewelsButton = new CachedText();
    private readonly CachedText weaponButton = new CachedText();
    private readonly CachedText armorButton = new CachedText();
    private readonly CachedText ringButton = new CachedText();
    private readonly CachedText backpackButton = new CachedText();

    /// <summary>
    /// Um texto do painel e os numeros de que ele foi montado. So vira
    /// string nova quando algum numero muda.
    ///
    /// Existe por causa do lixo. Antes cada linha era montada com $"..."
    /// dentro do OnGUI, que roda mais de uma vez por frame, e o painel
    /// ainda passava pelo GUILayout. Medi no Profiler: 8051 bytes de lixo
    /// por frame so no OnGUI do HUD. Na build isso dava uma coleta de
    /// lixo por segundo, e com o Graphics Jobs ligado no D3D12 cada coleta
    /// virava um engasgo na tela.
    ///
    /// Guardo o GUIContent pronto, e nao so a string. Medi: um GUI.Label
    /// com string gera uns 120 bytes por chamada, e com o GUIContent
    /// guardado uns 50. Esses 50 sao do proprio Unity desenhando o texto,
    /// e daqui nao da pra tirar.
    ///
    /// Com isto, o fim do GUILayout e os textos remontados so quando
    /// mudam, o HUD caiu de 9,5 KB de lixo por frame (8051 no OnGUI e 1433
    /// no BeginGUI/EndGUI do Unity) pra uns 730 bytes, parado ou andando.
    /// </summary>
    private sealed class CachedText
    {
        private bool isBuilt;
        private int a, b, c, d;

        public readonly GUIContent Content = new GUIContent(string.Empty);

        /// <summary>Verdadeiro se os numeros mudaram e o texto tem que ser remontado.</summary>
        public bool HasChanged(int a, int b = 0, int c = 0, int d = 0)
        {
            if (isBuilt && a == this.a && b == this.b && c == this.c && d == this.d)
                return false;

            isBuilt = true;
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            return true;
        }

        public void Set(string valor)
        {
            Content.text = valor;
        }

        public void SetTooltip(string valor)
        {
            Content.tooltip = valor;
        }
    }

    private void Awake()
    {
        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombat>();
        attributes = GetComponent<CharacterAttributes>();
        agent = GetComponent<NavMeshAgent>();
        belt = GetComponent<PotionBelt>();
        wallet = GetComponent<GoldWallet>();
        pouch = GetComponent<JewelPouch>();
        level = GetComponent<CharacterLevel>();
        equipment = GetComponent<CharacterEquipment>();
        shop = GetComponent<HubShop>();

        header = new GUIContent($"<b>DEBUG</b>   ({toggleKey} esconde)");
        shopHeader = new GUIContent($"<b>LOJA</b> (debug)   ({shopKey} fecha)");
        powerButton.Set("+P");
        hasteButton.Set("+C");
        vitalityButton.Set("+V");

        // Sem a passada de Layout. Tudo aqui e desenhado em retangulo
        // fixo, e o GUILayout era a outra parte do lixo.
        useGUILayout = false;
    }

    private void Update()
    {
        // Leio o teclado direto em vez de criar uma acao no
        // InputSystem_Actions. E ferramenta de debug e vai sumir - nao
        // quero deixar acao sobrando no asset do jogo depois.
        Keyboard teclado = Keyboard.current;

        if (teclado != null && teclado[toggleKey].wasPressedThisFrame)
            visible = !visible;

        // Sem HubShop no Player, o F2 nao abre nada.
        if (shop != null && teclado != null && teclado[shopKey].wasPressedThisFrame)
            isShopOpen = !isShopOpen;

        MeasureFps();

        if (visible)
            UpdateTexts();
    }

    private void OnDisable()
    {
        controlsRect = default;
        shopRect = default;
        pointsRect = default;
    }

    private void MeasureFps()
    {
        // Conto aqui e nao no OnGUI, que roda mais de uma vez por frame.
        // O relogio e o unscaledDeltaTime, pra linha continuar valendo
        // com o jogo pausado. O Stats da Game view nao serve pra isto:
        // com o jogo travado em 192 ele pulou de 183 a 253.
        float frame = Time.unscaledDeltaTime;

        windowTime += frame;
        windowFrames++;
        windowWorst = Mathf.Max(windowWorst, frame);

        if (windowTime < FpsWindow)
            return;

        fps = windowFrames / windowTime;
        worstFrame = windowWorst;

        windowTime = 0f;
        windowFrames = 0;
        windowWorst = 0f;
    }

    /// <summary>
    /// Remonta so os textos cujos numeros mudaram. Os numeros vao como
    /// inteiro ja arredondado do jeito que aparecem na tela, entao uma
    /// vida indo de 99,7 pra 99,8 nao gera texto novo.
    /// </summary>
    private void UpdateTexts()
    {
        int vidaAtual = Mathf.RoundToInt(health.Current);
        int vidaMaxima = Mathf.RoundToInt(health.Max);

        if (healthLine.HasChanged(vidaAtual, vidaMaxima))
            healthLine.Set($"Vida   {vidaAtual} / {vidaMaxima}");

        // O XP do proximo nivel sai do nivel, entao nivel e XP bastam pra
        // saber se o texto mudou.
        if (level != null && levelLine.HasChanged(level.Level, level.Xp))
        {
            levelLine.Set(level.IsMaxLevel
                ? $"Nivel   {level.Level}  (maximo)   XP {level.Xp}"
                : $"Nivel   {level.Level}   XP {level.Xp} / {level.NextLevelXp}");
        }

        if (level != null)
        {
            BonusPoints gastos = level.SpentPoints;

            if (pointsLine.HasChanged(level.FreePoints, gastos.Power, gastos.Haste, gastos.Vitality))
                pointsLine.Set($"Pontos   {level.FreePoints}   P{gastos.Power} C{gastos.Haste} V{gastos.Vitality}");

            // Sem build a previa nao tem de onde partir; os botoes
            // continuam funcionando, so sem tooltip.
            if (level.FreePoints > 0 && attributes != null && attributes.Build != null)
                UpdatePointTooltips();
        }

        if (belt != null && potionsLine.HasChanged(belt.Count, belt.Capacity))
            potionsLine.Set($"Pocoes   {belt.Count} / {belt.Capacity}");

        if (wallet != null && goldLine.HasChanged(wallet.Amount))
            goldLine.Set($"Ouro   {wallet.Amount}");

        if (pouch != null && jewelsLine.HasChanged(pouch.Count, pouch.TotalValue))
            jewelsLine.Set($"Joias   {pouch.Count}  (valem {pouch.TotalValue})");

        if (equipment != null)
            UpdateEquipment();

        if (isShopOpen)
            UpdateShop();

        UpdateFps();

        if (capLine.HasChanged(FpsLimiter.Fps))
            capLine.Set(FpsLimiter.Fps.ToString());

        if (defaultCapLine.HasChanged(FpsLimiter.FpsPadrao))
            defaultCapLine.Set($"padrao {FpsLimiter.FpsPadrao}");

        if (Time.unscaledTime < nextLiveTextTime)
            return;

        nextLiveTextTime = Time.unscaledTime + LiveTextInterval;

        UpdateTarget();
        UpdateSpeed();
    }

    private void UpdateEquipment()
    {
        // Os pontos somados, e nao os nomes: os tres nomes juntos nao
        // cabem na largura do painel, e o nome ja aparece no rotulo do
        // chao antes de pegar. O que interessa conferir aqui e o numero
        // que vira dano e vida.
        int ocupados = 0;

        if (equipment.GetItem(EquipmentSlot.Weapon) != null) ocupados++;
        if (equipment.GetItem(EquipmentSlot.Armor) != null) ocupados++;
        if (equipment.GetItem(EquipmentSlot.Ring) != null) ocupados++;

        BonusPoints pontos = equipment.TotalBonus;

        if (equipmentLine.HasChanged(ocupados, pontos.Power, pontos.Haste, pontos.Vitality))
            equipmentLine.Set($"Equip   {ocupados}/3   P+{pontos.Power} C+{pontos.Haste} V+{pontos.Vitality}");
    }

    private void UpdateShop()
    {
        if (potionButton.HasChanged(shop.PotionPrice))
            potionButton.Set($"Comprar pocao   ({shop.PotionPrice})");

        if (jewelsButton.HasChanged(pouch.Count, shop.JewelsValue))
            jewelsButton.Set($"Vender {pouch.Count} joias   ({shop.JewelsValue})");

        UpdateSlotButton(weaponButton, EquipmentSlot.Weapon);
        UpdateSlotButton(armorButton, EquipmentSlot.Armor);
        UpdateSlotButton(ringButton, EquipmentSlot.Ring);

        int naMochila = shop.BackpackCount;
        int valorDaMochila = shop.BackpackValue;
        if (backpackButton.HasChanged(naMochila, valorDaMochila))
        {
            backpackButton.Set(naMochila > 0
                ? $"Vender mochila: {naMochila} itens   ({valorDaMochila})"
                : "Mochila: vazia");
        }
    }

    private void UpdateSlotButton(CachedText botao, EquipmentSlot slot)
    {
        // O id do item entra na comparacao porque dois itens da mesma
        // raridade tem o mesmo preco e nomes diferentes.
        EquipmentItem item = equipment.GetItem(slot);
        int id = item != null ? item.GetInstanceID() : 0;
        int preco = shop.SellPrice(item);

        if (!botao.HasChanged(id, preco))
            return;

        botao.Set(item != null
            ? $"Vender {item.ItemName}   ({preco})"
            : $"{slot}: vazio");
    }

    private void UpdateFps()
    {
        // Media contra o teto do FpsLimiter, e o frame mais demorado
        // da mesma janela. A media mostra que o teto esta valendo; o pior
        // frame e o que denuncia engasgo, que a media esconde.
        int media = Mathf.RoundToInt(fps);
        int pior = Mathf.RoundToInt(worstFrame * 10000f);
        int tetoEmVigor = FpsLimiter.TetoEmVigor;
        int vsync = FpsLimiter.VSync ? 1 : 0;

        if (!fpsLine.HasChanged(media, pior, tetoEmVigor, vsync))
            return;

        string limite = vsync == 1 ? "vsync"
            : tetoEmVigor > 0 ? tetoEmVigor.ToString()
            : "sem limite";

        fpsLine.Set($"FPS   {media} / {limite}   pior {pior / 10f:F1} ms");
    }

    private void UpdateTarget()
    {
        // O == null do Unity tambem pega alvo ja destruido, que e o que
        // acontece com o inimigo 2 s depois de morrer.
        Health alvoAtual = combat != null ? combat.Target : null;

        if (alvoAtual == null)
        {
            if (targetLine.HasChanged(0))
                targetLine.Set("Alvo   nenhum");

            return;
        }

        // Distancia no chao, sem a altura. E essa que o PlayerCombat
        // compara com o alcance, entao e essa que interessa ver.
        Vector3 ate = alvoAtual.transform.position - transform.position;
        ate.y = 0f;

        // -1 na vida marca o morto: arredondada, uma vida de 0,3 tambem
        // daria 0 e ele ainda nao morreu.
        int vidaDoAlvo = alvoAtual.IsDead ? -1 : Mathf.RoundToInt(alvoAtual.Current);
        int maximaDoAlvo = Mathf.RoundToInt(alvoAtual.Max);
        int decimetros = Mathf.RoundToInt(ate.magnitude * 10f);

        if (!targetLine.HasChanged(alvoAtual.GetInstanceID(), vidaDoAlvo, maximaDoAlvo, decimetros))
            return;

        string texto = $"Alvo   {alvoAtual.name}  {Mathf.Max(0, vidaDoAlvo)}/{maximaDoAlvo}  a {decimetros / 10f:F1} m";
        targetLine.Set(vidaDoAlvo < 0 ? texto + "  (morto)" : texto);
    }

    private void UpdateSpeed()
    {
        if (agent == null)
        {
            if (speedLine.HasChanged(0))
                speedLine.Set("Velocidade   sem NavMeshAgent");

            return;
        }

        // A velocidade de verdade do agente contra o maximo dele. Mostro
        // as duas porque "andando a 1.9 de 3.2" e o que denuncia quando
        // a aceleracao ou uma curva estao segurando o personagem.
        Vector3 v = agent.velocity;
        v.y = 0f;

        int atual = Mathf.RoundToInt(v.magnitude * 100f);
        int maxima = Mathf.RoundToInt(agent.speed * 100f);

        if (speedLine.HasChanged(atual, maxima))
            speedLine.Set($"Velocidade   {atual / 100f:F2} / {maxima / 100f:F2} m/s");
    }

    private float PanelHeight()
    {
        // O cinto, a carteira, a bolsa, o equipamento e o nivel sao opcionais no Player;
        // sem eles a linha so nao aparece.
        int linhas = 5;

        if (belt != null) linhas++;
        if (wallet != null) linhas++;
        if (pouch != null) linhas++;
        if (equipment != null) linhas++;
        if (level != null) linhas++;

        float altura = Padding + linhas * TextHeight + BarHeight + Spacing +
                       ControlsHeight() + Padding;

        // A barra do XP, embaixo da linha do nivel, e a linha dos pontos
        // livres embaixo dela.
        if (level != null)
            altura += BarHeight + Spacing + TextHeight;

        return altura;
    }

    private static float ControlsHeight()
    {
        return ButtonHeight + Spacing + ButtonHeight + Spacing + SliderHeight + Spacing;
    }

    private void OnGUI()
    {
        // A loja esconde junto com o HUD, pra nenhum dos dois sair em
        // print.
        if (!visible)
        {
            controlsRect = default;
            shopRect = default;
            pointsRect = default;
            return;
        }

        // Escalo pela altura da tela com 1080 de referencia. O OnGUI
        // trabalha em pixel, entao sem isso o texto fica minusculo num
        // monitor 4K e enorme numa janela pequena do editor.
        float escala = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));

        CreateStyles();

        // A largura da tela na referencia muda com a proporcao do monitor,
        // entao o x do canto direito e calculado aqui, e nao constante.
        float painelX = Screen.width / escala - PanelWidth - ScreenMargin;

        GUI.Box(new Rect(painelX, PanelY, PanelWidth, PanelHeight()), GUIContent.none);

        float x = painelX + Padding;
        float largura = PanelWidth - 2f * Padding;
        float y = PanelY + Padding;

        DrawLine(header, x, largura, ref y);
        DrawLine(healthLine.Content, x, largura, ref y, null, HealthColor);
        DrawBar(new Rect(x, y, largura, BarHeight), health.Normalized, HealthColor);
        y += BarHeight + Spacing;

        if (level != null)
        {
            DrawLine(levelLine.Content, x, largura, ref y, null, LevelColor);
            DrawBar(new Rect(x, y, largura, BarHeight), level.LevelProgress, LevelColor);
            y += BarHeight + Spacing;

            DrawPoints(x, largura, ref y, escala);
        }
        else
        {
            pointsRect = default;
        }

        // Cores dos rotulos dos itens no chao (Pickup), pra a pocao do
        // HUD ter a mesma cor da pocao caida.
        if (belt != null) DrawLine(potionsLine.Content, x, largura, ref y, potionIcon, new Color(1f, 0.4f, 0.4f));
        if (wallet != null) DrawLine(goldLine.Content, x, largura, ref y, goldIcon, new Color(1f, 0.85f, 0.3f));
        if (pouch != null) DrawLine(jewelsLine.Content, x, largura, ref y, jewelIcon, new Color(0.45f, 0.85f, 1f));

        // Azul do item magico, pra a linha nao se confundir com a do ouro.
        if (equipment != null) DrawLine(equipmentLine.Content, x, largura, ref y, null, EquipmentItem.RarityColor(Rarity.Magic));

        DrawLine(targetLine.Content, x, largura, ref y, null, new Color(0.95f, 0.5f, 0.2f));
        DrawLine(speedLine.Content, x, largura, ref y, null, new Color(0.45f, 0.8f, 0.45f));
        DrawLine(fpsLine.Content, x, largura, ref y, null, new Color(0.55f, 0.7f, 0.95f));

        DrawFpsControls(new Rect(x, y, largura, ControlsHeight()), escala);

        if (isShopOpen && shop != null)
            DrawShop(painelX - PanelWidth - ScreenMargin, escala);
        else
            shopRect = default;

        DrawTooltip();
    }

    /// <summary>
    /// A linha dos pontos livres e, enquanto sobrar ponto, os botoes que
    /// gastam um em Poder, Celeridade ou Vitalidade. Quem recusa de
    /// verdade e o CharacterLevel: o botao sumido e so o reflexo disso.
    /// </summary>
    private void DrawPoints(float x, float largura, ref float y, float escala)
    {
        float linhaY = y;
        DrawLine(pointsLine.Content, x, largura, ref y, null, LevelColor);

        if (level.FreePoints <= 0)
        {
            pointsRect = default;
            return;
        }

        float larguraDosBotoes = 3f * PointButtonWidth + 2f * PointButtonGap;
        float bx = x + largura - larguraDosBotoes;
        float by = linhaY + (TextHeight - ButtonHeight) * 0.5f;

        if (GUI.Button(new Rect(bx, by, PointButtonWidth, ButtonHeight), powerButton.Content, buttonStyle))
            level.TrySpendPoint(CharacterLevel.Stat.Power);

        bx += PointButtonWidth + PointButtonGap;

        if (GUI.Button(new Rect(bx, by, PointButtonWidth, ButtonHeight), hasteButton.Content, buttonStyle))
            level.TrySpendPoint(CharacterLevel.Stat.Haste);

        bx += PointButtonWidth + PointButtonGap;

        if (GUI.Button(new Rect(bx, by, PointButtonWidth, ButtonHeight), vitalityButton.Content, buttonStyle))
            level.TrySpendPoint(CharacterLevel.Stat.Vitality);

        pointsRect = new Rect(
            (x + largura - larguraDosBotoes) * escala,
            by * escala,
            larguraDosBotoes * escala,
            ButtonHeight * escala);
    }

    /// <summary>
    /// A previa de cada botao: o numero de agora e o numero com um ponto a
    /// mais. Sai da mesma conta que o CharacterAttributes aplica, entao o
    /// clique faz exatamente o que o tooltip disse, com nivel e
    /// equipamento ja dentro. So remonta quando o numero arredondado muda.
    /// </summary>
    private void UpdatePointTooltips()
    {
        CharacterAttributes.Numbers agora = attributes.NumbersWith(default);
        CharacterAttributes.Numbers comP = attributes.NumbersWith(new BonusPoints(1, 0, 0));
        CharacterAttributes.Numbers comC = attributes.NumbersWith(new BonusPoints(0, 1, 0));
        CharacterAttributes.Numbers comV = attributes.NumbersWith(new BonusPoints(0, 0, 1));

        // Dano em decimos e golpe em milesimos, do jeito que aparecem. O
        // golpe com centesimo enganava: 1,125 s aparecia 1,12.
        int danoAgora = Mathf.RoundToInt(agora.Damage * 10f);
        int danoDepois = Mathf.RoundToInt(comP.Damage * 10f);

        if (powerButton.HasChanged(danoAgora, danoDepois))
            powerButton.SetTooltip($"Poder: mais dano por golpe\ndano {danoAgora / 10f:F1} -> {danoDepois / 10f:F1}");

        int golpeAgora = Mathf.RoundToInt(agora.AttackCooldown * 1000f);
        int golpeDepois = Mathf.RoundToInt(comC.AttackCooldown * 1000f);

        if (hasteButton.HasChanged(golpeAgora, golpeDepois))
            hasteButton.SetTooltip($"Celeridade: golpe mais rapido\num golpe a cada {golpeAgora / 1000f:F3} s -> {golpeDepois / 1000f:F3} s");

        int vidaAgora = Mathf.RoundToInt(agora.MaxHealth);
        int vidaDepois = Mathf.RoundToInt(comV.MaxHealth);

        if (vitalityButton.HasChanged(vidaAgora, vidaDepois))
            vitalityButton.SetTooltip($"Vitalidade: mais vida maxima (nao cura)\nvida max {vidaAgora} -> {vidaDepois}");
    }

    /// <summary>
    /// O tooltip do botao embaixo do mouse, desenhado por ultimo pra ficar
    /// por cima de tudo. Abre a esquerda do mouse porque o painel encosta
    /// na borda direita da tela.
    /// </summary>
    private void DrawTooltip()
    {
        if (string.IsNullOrEmpty(GUI.tooltip))
            return;

        tooltipContent.text = GUI.tooltip;

        Vector2 mouse = Event.current.mousePosition;
        var caixa = new Rect(
            mouse.x - TooltipWidth - 12f,
            mouse.y + 16f,
            TooltipWidth,
            2f * TooltipLineHeight + 2f * Padding);

        // Duas caixas: a do OnGUI e meio transparente, e o texto some em
        // cima do chao claro da cidade.
        GUI.Box(caixa, GUIContent.none);
        GUI.Box(caixa, GUIContent.none);

        caixa.x += Padding;
        caixa.y += Padding;
        caixa.width -= 2f * Padding;
        caixa.height -= 2f * Padding;
        GUI.Label(caixa, tooltipContent, tooltipStyle);
    }

    // Cabecalho e sete botoes.
    private const int ShopButtonCount = 7;

    /// <summary>
    /// A loja de debug, do mesmo tamanho do HUD e a esquerda dele. So abre
    /// pelo F2, entao pode cobrir o seletor de dungeon do Eduardo (que vai
    /// ate x 1477) sem atrapalhar ninguem que nao pediu.
    ///
    /// Botao que nao daria certo fica apagado, mas a conta continua sendo
    /// da HubShop: o apagado e so dica, e quem recusa de verdade e ela.
    /// </summary>
    private void DrawShop(float painelX, float escala)
    {
        float altura = Padding + TextHeight + ShopButtonCount * (ButtonHeight + Spacing) + Padding;
        var painel = new Rect(painelX, PanelY, PanelWidth, altura);

        GUI.Box(painel, GUIContent.none);

        float x = painelX + Padding;
        float largura = PanelWidth - 2f * Padding;
        float y = PanelY + Padding;

        DrawLine(shopHeader, x, largura, ref y);

        bool habilitado = GUI.enabled;

        GUI.enabled = habilitado && !belt.IsFull && wallet.Amount >= shop.PotionPrice;
        if (ShopButton(potionButton.Content, x, largura, ref y))
            shop.BuyPotion();

        GUI.enabled = habilitado && pouch.Count > 0;
        if (ShopButton(jewelsButton.Content, x, largura, ref y))
            shop.SellJewels();

        SellButton(weaponButton, EquipmentSlot.Weapon, habilitado, x, largura, ref y);
        SellButton(armorButton, EquipmentSlot.Armor, habilitado, x, largura, ref y);
        SellButton(ringButton, EquipmentSlot.Ring, habilitado, x, largura, ref y);

        GUI.enabled = habilitado && shop.BackpackCount > 0;
        if (ShopButton(backpackButton.Content, x, largura, ref y))
            shop.SellBackpack();

        GUI.enabled = habilitado;
        if (ShopButton(AddGoldLabel, x, largura, ref y))
            wallet.Add(100);

        shopRect = new Rect(
            painel.x * escala,
            painel.y * escala,
            painel.width * escala,
            painel.height * escala);
    }

    private void SellButton(CachedText botao, EquipmentSlot slot, bool habilitado, float x, float largura, ref float y)
    {
        GUI.enabled = habilitado && equipment.GetItem(slot) != null;

        if (ShopButton(botao.Content, x, largura, ref y))
            shop.SellEquipped(slot);
    }

    private bool ShopButton(GUIContent texto, float x, float largura, ref float y)
    {
        bool clicou = GUI.Button(new Rect(x, y, largura, ButtonHeight), texto, buttonStyle);
        y += ButtonHeight + Spacing;
        return clicou;
    }

    private void CreateStyles()
    {
        // So da pra criar o estilo aqui dentro: o GUI.skin nao existe
        // fora do OnGUI. O slider padrao do OnGUI tem 12 pixels de
        // altura, dificil de pegar com o mouse, entao engrosso o trilho e
        // o pino.
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
        buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 16 };
        tooltipStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = false };
        trackStyle ??= new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = SliderHeight };
        thumbStyle ??= new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedHeight = SliderHeight, fixedWidth = SliderHeight };
    }

    private void DrawLine(GUIContent texto, float x, float largura, ref float y)
    {
        GUI.Label(new Rect(x, y, largura, TextHeight), texto, style);
        y += TextHeight;
    }

    // As barras usam a mesma cor do marcador da linha delas.
    private static readonly Color HealthColor = new Color(0.75f, 0.15f, 0.12f);
    private static readonly Color LevelColor = new Color(0.85f, 0.65f, 0.15f);
    private const float IconSize = 18f;

    /// <summary>
    /// Linha com um marcador na frente: o icone, quando tem, ou um
    /// quadradinho na cor. Bate o olho e acha a linha pela cor, sem ler.
    /// O texto anda pra direita o tamanho do marcador.
    /// </summary>
    private void DrawLine(GUIContent texto, float x, float largura, ref float y, Texture2D icone, Color cor)
    {
        float recuo = IconSize + 8f;
        var marcador = new Rect(x + 2f, y + (TextHeight - IconSize) * 0.5f, IconSize, IconSize);

        if (icone != null)
            GUI.DrawTexture(marcador, icone, ScaleMode.ScaleToFit, true);
        else
            GUI.DrawTexture(marcador, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, cor, 0f, 4f);

        GUI.Label(new Rect(x + recuo, y, largura - recuo, TextHeight), texto, style);
        y += TextHeight;
    }

    private void DrawFpsControls(Rect area, float escala)
    {
        float y = area.y;

        // Botao em vez de caixa de marcar: a caixa do OnGUI tem 12 pixels
        // e nao da pra ver de longe se esta marcada.
        GUIContent textoDoVSync = FpsLimiter.VSync ? VSyncOn : VSyncOff;

        if (GUI.Button(new Rect(area.x, y, area.width, ButtonHeight), textoDoVSync, buttonStyle))
            FpsLimiter.VSync = !FpsLimiter.VSync;

        y += ButtonHeight + Spacing;

        // Com o vsync ligado o teto nao vale. Deixo os controles dele
        // apagados em vez de sumir, pra o painel nao pular de tamanho.
        bool habilitado = GUI.enabled;
        GUI.enabled = habilitado && !FpsLimiter.VSync;

        GUIContent textoDoTeto = FpsLimiter.Ligado ? CapOn : CapOff;

        if (GUI.Button(new Rect(area.x, y, 150f, ButtonHeight), textoDoTeto, buttonStyle))
            FpsLimiter.Ligado = !FpsLimiter.Ligado;

        GUI.Label(new Rect(area.x + 156f, y - 2f, 80f, TextHeight), capLine.Content, style);

        if (GUI.Button(new Rect(area.xMax - 110f, y, 110f, ButtonHeight), defaultCapLine.Content, buttonStyle))
            FpsLimiter.Fps = FpsLimiter.FpsPadrao;

        y += ButtonHeight + Spacing;

        int maximo = FpsLimiter.TaxaDoMonitor > 0
            ? FpsLimiter.TaxaDoMonitor
            : FallbackMaxFpsCap;

        // Comparo com o numero ja preso na faixa do slider. Um teto fora
        // dela (monitor de 24 Hz, por exemplo) seria reescrito so por
        // ter sido desenhado, sem ninguem encostar no mouse.
        int mostrado = Mathf.Clamp(FpsLimiter.Fps, MinFpsCap, maximo);

        int escolhido = Mathf.RoundToInt(GUI.HorizontalSlider(
            new Rect(area.x, y, area.width, SliderHeight),
            mostrado, MinFpsCap, maximo, trackStyle, thumbStyle));

        if (escolhido != mostrado)
            FpsLimiter.Fps = escolhido;

        GUI.enabled = habilitado;

        // A area vem na tela de referencia, entao levo pra pixels de
        // verdade.
        controlsRect = new Rect(
            area.x * escala,
            area.y * escala,
            area.width * escala,
            area.height * escala);
    }

    private static void DrawBar(Rect fundo, float cheio, Color cor)
    {
        GUI.DrawTexture(fundo, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.15f, 0.15f, 0.15f), 0f, 0f);

        Rect preenchido = fundo;
        preenchido.width *= Mathf.Clamp01(cheio);
        GUI.DrawTexture(preenchido, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, cor, 0f, 0f);
    }
#endif
}
