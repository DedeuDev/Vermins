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
/// So desenha no editor e em Development Build. A classe continua
/// existindo na build normal de proposito: se eu sumisse com ela
/// inteira, o Player.prefab ficaria com "Missing Script" na build.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerDebugHUD : MonoBehaviour
{
    [SerializeField] private bool visivel = true;

    [Tooltip("Liga e desliga o HUD, pra ele nao aparecer em print e video.")]
    [SerializeField] private Key teclaParaEsconder = Key.F1;

    [Tooltip("Abre e fecha a loja de debug. Funciona em qualquer cena, " +
             "nao so no hub: e pra testar a conta, nao o lugar.")]
    [SerializeField] private Key teclaDaLoja = Key.F2;

    [Header("Icones das linhas")]
    [Tooltip("Os mesmos de Placeholders/Icones. Vazio, a linha ganha um " +
             "quadradinho na cor do rotulo do item no chao.")]
    [SerializeField] private Texture2D iconeDaPocao;
    [SerializeField] private Texture2D iconeDoOuro;
    [SerializeField] private Texture2D iconeDaJoia;

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
    public static bool CursorNosControles
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

            return controlesNaTela.Contains(cursor) || lojaNaTela.Contains(cursor);
#else
            return false;
#endif
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Onde o painel fica e a largura dele, na tela de referencia de 1080
    // de altura. A altura sai da soma das linhas, em AlturaDoPainel.
    //
    // Fica no canto superior DIREITO. No esquerdo ele ficava por cima do
    // painel de missao do Eduardo (QuestUI): medi em 1920x1080 o painel
    // dele de 276 a 764 px do topo, e o meu ia ate uns 430. O dialogo
    // fica embaixo no centro e o seletor de dungeon no meio, ate x 1477;
    // o canto direito nao bate em nenhum dos tres.
    private const float DistanciaDaBorda = 16f;
    private const float PainelY = 16f;
    private const float PainelLargura = 380f;

    // Medidas de dentro do painel, casadas com o que o GUILayout
    // desenhava antes, pra o painel nao mudar de cara.
    private const float Margem = 6f;
    private const float AlturaDoTexto = 32f;
    private const float AlturaDaBarra = 14f;
    private const float AlturaDoBotao = 28f;
    private const float AlturaDoSlider = 20f;
    private const float Espaco = 6f;

    // O menor teto que o slider oferece, e o maior quando o monitor nao
    // informa a taxa. Com a taxa conhecida o maior e ela.
    private const int TetoMinimo = 30;
    private const int TetoMaximoSemMonitor = 240;

    // Velocidade e distancia do alvo mudam todo frame enquanto alguem
    // anda. Remonto os textos delas so 10 vezes por segundo: a 200 fps
    // ninguem le um numero que troca a cada frame, e cada troca e uma
    // string nova.
    private const float IntervaloDosTextosQueCorrem = 0.1f;

    // Onde os controles do fps foram desenhados, em pixels da tela.
    // Vazio enquanto o HUD esta escondido.
    private static Rect controlesNaTela;

    // O painel da loja inteiro, em pixels da tela. Ele e todo botao,
    // entao barra o clique no painel todo. Vazio com a loja fechada.
    private static Rect lojaNaTela;

    private bool lojaAberta;

    private GUIStyle estilo;
    private GUIStyle estiloDoBotao;
    private GUIStyle estiloDoTrilho;
    private GUIStyle estiloDoPino;

    private Health health;
    private PotionBelt belt;
    private GoldWallet wallet;
    private JewelPouch pouch;
    private CharacterLevel nivel;
    private CharacterEquipment equipamento;
    private HubShop loja;
    private PlayerCombat combat;
    private NavMeshAgent agent;

    // De quanto em quanto tempo a linha do fps troca de numero.
    // Frame a frame o valor treme e nao da pra ler.
    private const float JanelaDoFps = 0.5f;

    private float tempoNaJanela;
    private int framesNaJanela;
    private float piorNaJanela;
    private float fps;
    private float piorFrame;

    private float proximosTextosQueCorrem;

    // Os textos que nunca mudam. Os botoes de ligar alternam entre dois.
    private GUIContent cabecalho;
    private static readonly GUIContent VSyncLigado = new GUIContent("vsync ligado");
    private static readonly GUIContent VSyncDesligado = new GUIContent("vsync desligado");
    private static readonly GUIContent TetoLigado = new GUIContent("limite ligado");
    private static readonly GUIContent TetoDesligado = new GUIContent("limite desligado");
    private readonly Texto vida = new Texto();
    private readonly Texto linhaDoNivel = new Texto();
    private readonly Texto pocoes = new Texto();
    private readonly Texto ouro = new Texto();
    private readonly Texto joias = new Texto();
    private readonly Texto linhaDoEquipamento = new Texto();
    private readonly Texto alvo = new Texto();
    private readonly Texto velocidade = new Texto();
    private readonly Texto linhaDoFps = new Texto();
    private readonly Texto teto = new Texto();
    private readonly Texto padrao = new Texto();

    // Os botoes da loja, com o preco no texto.
    private GUIContent cabecalhoDaLoja;
    private static readonly GUIContent GanharOuro = new GUIContent("+100 de ouro (teste)");
    private readonly Texto botaoDaPocao = new Texto();
    private readonly Texto botaoDasJoias = new Texto();
    private readonly Texto botaoDaArma = new Texto();
    private readonly Texto botaoDaArmadura = new Texto();
    private readonly Texto botaoDoAnel = new Texto();

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
    private sealed class Texto
    {
        private bool montado;
        private int a, b, c, d;

        public readonly GUIContent Conteudo = new GUIContent(string.Empty);

        /// <summary>Verdadeiro se os numeros mudaram e o texto tem que ser remontado.</summary>
        public bool Mudou(int a, int b = 0, int c = 0, int d = 0)
        {
            if (montado && a == this.a && b == this.b && c == this.c && d == this.d)
                return false;

            montado = true;
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            return true;
        }

        public void Trocar(string valor)
        {
            Conteudo.text = valor;
        }
    }

    private void Awake()
    {
        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombat>();
        agent = GetComponent<NavMeshAgent>();
        belt = GetComponent<PotionBelt>();
        wallet = GetComponent<GoldWallet>();
        pouch = GetComponent<JewelPouch>();
        nivel = GetComponent<CharacterLevel>();
        equipamento = GetComponent<CharacterEquipment>();
        loja = GetComponent<HubShop>();

        cabecalho = new GUIContent($"<b>DEBUG</b>   ({teclaParaEsconder} esconde)");
        cabecalhoDaLoja = new GUIContent($"<b>LOJA</b> (debug)   ({teclaDaLoja} fecha)");

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

        if (teclado != null && teclado[teclaParaEsconder].wasPressedThisFrame)
            visivel = !visivel;

        // Sem HubShop no Player, o F2 nao abre nada.
        if (loja != null && teclado != null && teclado[teclaDaLoja].wasPressedThisFrame)
            lojaAberta = !lojaAberta;

        MedirFps();

        if (visivel)
            AtualizarTextos();
    }

    private void OnDisable()
    {
        controlesNaTela = default;
        lojaNaTela = default;
    }

    private void MedirFps()
    {
        // Conto aqui e nao no OnGUI, que roda mais de uma vez por frame.
        // O relogio e o unscaledDeltaTime, pra linha continuar valendo
        // com o jogo pausado. O Stats da Game view nao serve pra isto:
        // com o jogo travado em 192 ele pulou de 183 a 253.
        float frame = Time.unscaledDeltaTime;

        tempoNaJanela += frame;
        framesNaJanela++;
        piorNaJanela = Mathf.Max(piorNaJanela, frame);

        if (tempoNaJanela < JanelaDoFps)
            return;

        fps = framesNaJanela / tempoNaJanela;
        piorFrame = piorNaJanela;

        tempoNaJanela = 0f;
        framesNaJanela = 0;
        piorNaJanela = 0f;
    }

    /// <summary>
    /// Remonta so os textos cujos numeros mudaram. Os numeros vao como
    /// inteiro ja arredondado do jeito que aparecem na tela, entao uma
    /// vida indo de 99,7 pra 99,8 nao gera texto novo.
    /// </summary>
    private void AtualizarTextos()
    {
        int vidaAtual = Mathf.RoundToInt(health.Current);
        int vidaMaxima = Mathf.RoundToInt(health.Max);

        if (vida.Mudou(vidaAtual, vidaMaxima))
            vida.Trocar($"Vida   {vidaAtual} / {vidaMaxima}");

        // O XP do proximo nivel sai do nivel, entao nivel e XP bastam pra
        // saber se o texto mudou.
        if (nivel != null && linhaDoNivel.Mudou(nivel.Nivel, nivel.Xp))
        {
            linhaDoNivel.Trocar(nivel.NoMaximo
                ? $"Nivel   {nivel.Nivel}  (maximo)   XP {nivel.Xp}"
                : $"Nivel   {nivel.Nivel}   XP {nivel.Xp} / {nivel.XpDoProximoNivel}");
        }

        if (belt != null && pocoes.Mudou(belt.Count, belt.Capacity))
            pocoes.Trocar($"Pocoes   {belt.Count} / {belt.Capacity}");

        if (wallet != null && ouro.Mudou(wallet.Amount))
            ouro.Trocar($"Ouro   {wallet.Amount}");

        if (pouch != null && joias.Mudou(pouch.Count, pouch.TotalValue))
            joias.Trocar($"Joias   {pouch.Count}  (valem {pouch.TotalValue})");

        if (equipamento != null)
            AtualizarEquipamento();

        if (lojaAberta)
            AtualizarLoja();

        AtualizarFps();

        if (teto.Mudou(FpsLimiter.Fps))
            teto.Trocar(FpsLimiter.Fps.ToString());

        if (padrao.Mudou(FpsLimiter.FpsPadrao))
            padrao.Trocar($"padrao {FpsLimiter.FpsPadrao}");

        if (Time.unscaledTime < proximosTextosQueCorrem)
            return;

        proximosTextosQueCorrem = Time.unscaledTime + IntervaloDosTextosQueCorrem;

        AtualizarAlvo();
        AtualizarVelocidade();
    }

    private void AtualizarEquipamento()
    {
        // Os pontos somados, e nao os nomes: os tres nomes juntos nao
        // cabem na largura do painel, e o nome ja aparece no rotulo do
        // chao antes de pegar. O que interessa conferir aqui e o numero
        // que vira dano e vida.
        int ocupados = 0;

        if (equipamento.NoSlot(EquipmentSlot.Arma) != null) ocupados++;
        if (equipamento.NoSlot(EquipmentSlot.Armadura) != null) ocupados++;
        if (equipamento.NoSlot(EquipmentSlot.Anel) != null) ocupados++;

        BonusPoints pontos = equipamento.PontosSomados;

        if (linhaDoEquipamento.Mudou(ocupados, pontos.Poder, pontos.Celeridade, pontos.Vitalidade))
            linhaDoEquipamento.Trocar($"Equip   {ocupados}/3   P+{pontos.Poder} C+{pontos.Celeridade} V+{pontos.Vitalidade}");
    }

    private void AtualizarLoja()
    {
        if (botaoDaPocao.Mudou(loja.PrecoDaPocao))
            botaoDaPocao.Trocar($"Comprar pocao   ({loja.PrecoDaPocao})");

        if (botaoDasJoias.Mudou(pouch.Count, loja.ValorDasJoias))
            botaoDasJoias.Trocar($"Vender {pouch.Count} joias   ({loja.ValorDasJoias})");

        AtualizarBotaoDoSlot(botaoDaArma, EquipmentSlot.Arma);
        AtualizarBotaoDoSlot(botaoDaArmadura, EquipmentSlot.Armadura);
        AtualizarBotaoDoSlot(botaoDoAnel, EquipmentSlot.Anel);
    }

    private void AtualizarBotaoDoSlot(Texto botao, EquipmentSlot slot)
    {
        // O id do item entra na comparacao porque dois itens da mesma
        // raridade tem o mesmo preco e nomes diferentes.
        EquipmentItem item = equipamento.NoSlot(slot);
        int id = item != null ? item.GetInstanceID() : 0;
        int preco = loja.ValorDeVenda(item);

        if (!botao.Mudou(id, preco))
            return;

        botao.Trocar(item != null
            ? $"Vender {item.NomeDoItem}   ({preco})"
            : $"{slot}: vazio");
    }

    private void AtualizarFps()
    {
        // Media contra o teto do FpsLimiter, e o frame mais demorado
        // da mesma janela. A media mostra que o teto esta valendo; o pior
        // frame e o que denuncia engasgo, que a media esconde.
        int media = Mathf.RoundToInt(fps);
        int pior = Mathf.RoundToInt(piorFrame * 10000f);
        int tetoEmVigor = FpsLimiter.TetoEmVigor;
        int vsync = FpsLimiter.VSync ? 1 : 0;

        if (!linhaDoFps.Mudou(media, pior, tetoEmVigor, vsync))
            return;

        string limite = vsync == 1 ? "vsync"
            : tetoEmVigor > 0 ? tetoEmVigor.ToString()
            : "sem limite";

        linhaDoFps.Trocar($"FPS   {media} / {limite}   pior {pior / 10f:F1} ms");
    }

    private void AtualizarAlvo()
    {
        // O == null do Unity tambem pega alvo ja destruido, que e o que
        // acontece com o inimigo 2 s depois de morrer.
        Health alvoAtual = combat != null ? combat.Target : null;

        if (alvoAtual == null)
        {
            if (alvo.Mudou(0))
                alvo.Trocar("Alvo   nenhum");

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

        if (!alvo.Mudou(alvoAtual.GetInstanceID(), vidaDoAlvo, maximaDoAlvo, decimetros))
            return;

        string texto = $"Alvo   {alvoAtual.name}  {Mathf.Max(0, vidaDoAlvo)}/{maximaDoAlvo}  a {decimetros / 10f:F1} m";
        alvo.Trocar(vidaDoAlvo < 0 ? texto + "  (morto)" : texto);
    }

    private void AtualizarVelocidade()
    {
        if (agent == null)
        {
            if (velocidade.Mudou(0))
                velocidade.Trocar("Velocidade   sem NavMeshAgent");

            return;
        }

        // A velocidade de verdade do agente contra o maximo dele. Mostro
        // as duas porque "andando a 1.9 de 3.2" e o que denuncia quando
        // a aceleracao ou uma curva estao segurando o personagem.
        Vector3 v = agent.velocity;
        v.y = 0f;

        int atual = Mathf.RoundToInt(v.magnitude * 100f);
        int maxima = Mathf.RoundToInt(agent.speed * 100f);

        if (velocidade.Mudou(atual, maxima))
            velocidade.Trocar($"Velocidade   {atual / 100f:F2} / {maxima / 100f:F2} m/s");
    }

    private float AlturaDoPainel()
    {
        // O cinto, a carteira, a bolsa, o equipamento e o nivel sao opcionais no Player;
        // sem eles a linha so nao aparece.
        int linhas = 5;

        if (belt != null) linhas++;
        if (wallet != null) linhas++;
        if (pouch != null) linhas++;
        if (equipamento != null) linhas++;
        if (nivel != null) linhas++;

        float altura = Margem + linhas * AlturaDoTexto + AlturaDaBarra + Espaco +
                       AlturaDosControles() + Margem;

        // A barra do XP, embaixo da linha do nivel.
        if (nivel != null)
            altura += AlturaDaBarra + Espaco;

        return altura;
    }

    private static float AlturaDosControles()
    {
        return AlturaDoBotao + Espaco + AlturaDoBotao + Espaco + AlturaDoSlider + Espaco;
    }

    private void OnGUI()
    {
        // A loja esconde junto com o HUD, pra nenhum dos dois sair em
        // print.
        if (!visivel)
        {
            controlesNaTela = default;
            lojaNaTela = default;
            return;
        }

        // Escalo pela altura da tela com 1080 de referencia. O OnGUI
        // trabalha em pixel, entao sem isso o texto fica minusculo num
        // monitor 4K e enorme numa janela pequena do editor.
        float escala = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));

        CriarEstilos();

        // A largura da tela na referencia muda com a proporcao do monitor,
        // entao o x do canto direito e calculado aqui, e nao constante.
        float painelX = Screen.width / escala - PainelLargura - DistanciaDaBorda;

        GUI.Box(new Rect(painelX, PainelY, PainelLargura, AlturaDoPainel()), GUIContent.none);

        float x = painelX + Margem;
        float largura = PainelLargura - 2f * Margem;
        float y = PainelY + Margem;

        Escrever(cabecalho, x, largura, ref y);
        Escrever(vida.Conteudo, x, largura, ref y, null, CorDaVida);
        DesenharBarra(new Rect(x, y, largura, AlturaDaBarra), health.Normalized, CorDaVida);
        y += AlturaDaBarra + Espaco;

        if (nivel != null)
        {
            Escrever(linhaDoNivel.Conteudo, x, largura, ref y, null, CorDoNivel);
            DesenharBarra(new Rect(x, y, largura, AlturaDaBarra), nivel.ProgressoNoNivel, CorDoNivel);
            y += AlturaDaBarra + Espaco;
        }

        // Cores dos rotulos dos itens no chao (Pickup), pra a pocao do
        // HUD ter a mesma cor da pocao caida.
        if (belt != null) Escrever(pocoes.Conteudo, x, largura, ref y, iconeDaPocao, new Color(1f, 0.4f, 0.4f));
        if (wallet != null) Escrever(ouro.Conteudo, x, largura, ref y, iconeDoOuro, new Color(1f, 0.85f, 0.3f));
        if (pouch != null) Escrever(joias.Conteudo, x, largura, ref y, iconeDaJoia, new Color(0.45f, 0.85f, 1f));

        // Azul do item magico, pra a linha nao se confundir com a do ouro.
        if (equipamento != null) Escrever(linhaDoEquipamento.Conteudo, x, largura, ref y, null, EquipmentItem.CorDaRaridade(Rarity.Magico));

        Escrever(alvo.Conteudo, x, largura, ref y, null, new Color(0.95f, 0.5f, 0.2f));
        Escrever(velocidade.Conteudo, x, largura, ref y, null, new Color(0.45f, 0.8f, 0.45f));
        Escrever(linhaDoFps.Conteudo, x, largura, ref y, null, new Color(0.55f, 0.7f, 0.95f));

        DesenharControlesDoFps(new Rect(x, y, largura, AlturaDosControles()), escala);

        if (lojaAberta && loja != null)
            DesenharLoja(painelX - PainelLargura - DistanciaDaBorda, escala);
        else
            lojaNaTela = default;
    }

    // Cabecalho e seis botoes.
    private const int BotoesDaLoja = 6;

    /// <summary>
    /// A loja de debug, do mesmo tamanho do HUD e a esquerda dele. So abre
    /// pelo F2, entao pode cobrir o seletor de dungeon do Eduardo (que vai
    /// ate x 1477) sem atrapalhar ninguem que nao pediu.
    ///
    /// Botao que nao daria certo fica apagado, mas a conta continua sendo
    /// da HubShop: o apagado e so dica, e quem recusa de verdade e ela.
    /// </summary>
    private void DesenharLoja(float painelX, float escala)
    {
        float altura = Margem + AlturaDoTexto + BotoesDaLoja * (AlturaDoBotao + Espaco) + Margem;
        var painel = new Rect(painelX, PainelY, PainelLargura, altura);

        GUI.Box(painel, GUIContent.none);

        float x = painelX + Margem;
        float largura = PainelLargura - 2f * Margem;
        float y = PainelY + Margem;

        Escrever(cabecalhoDaLoja, x, largura, ref y);

        bool habilitado = GUI.enabled;

        GUI.enabled = habilitado && !belt.IsFull && wallet.Amount >= loja.PrecoDaPocao;
        if (BotaoDaLoja(botaoDaPocao.Conteudo, x, largura, ref y))
            loja.ComprarPocao();

        GUI.enabled = habilitado && pouch.Count > 0;
        if (BotaoDaLoja(botaoDasJoias.Conteudo, x, largura, ref y))
            loja.VenderJoias();

        BotaoDeVenda(botaoDaArma, EquipmentSlot.Arma, habilitado, x, largura, ref y);
        BotaoDeVenda(botaoDaArmadura, EquipmentSlot.Armadura, habilitado, x, largura, ref y);
        BotaoDeVenda(botaoDoAnel, EquipmentSlot.Anel, habilitado, x, largura, ref y);

        GUI.enabled = habilitado;
        if (BotaoDaLoja(GanharOuro, x, largura, ref y))
            wallet.Add(100);

        lojaNaTela = new Rect(
            painel.x * escala,
            painel.y * escala,
            painel.width * escala,
            painel.height * escala);
    }

    private void BotaoDeVenda(Texto botao, EquipmentSlot slot, bool habilitado, float x, float largura, ref float y)
    {
        GUI.enabled = habilitado && equipamento.NoSlot(slot) != null;

        if (BotaoDaLoja(botao.Conteudo, x, largura, ref y))
            loja.VenderEquipado(slot);
    }

    private bool BotaoDaLoja(GUIContent texto, float x, float largura, ref float y)
    {
        bool clicou = GUI.Button(new Rect(x, y, largura, AlturaDoBotao), texto, estiloDoBotao);
        y += AlturaDoBotao + Espaco;
        return clicou;
    }

    private void CriarEstilos()
    {
        // So da pra criar o estilo aqui dentro: o GUI.skin nao existe
        // fora do OnGUI. O slider padrao do OnGUI tem 12 pixels de
        // altura, dificil de pegar com o mouse, entao engrosso o trilho e
        // o pino.
        estilo ??= new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
        estiloDoBotao ??= new GUIStyle(GUI.skin.button) { fontSize = 16 };
        estiloDoTrilho ??= new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = AlturaDoSlider };
        estiloDoPino ??= new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedHeight = AlturaDoSlider, fixedWidth = AlturaDoSlider };
    }

    private void Escrever(GUIContent texto, float x, float largura, ref float y)
    {
        GUI.Label(new Rect(x, y, largura, AlturaDoTexto), texto, estilo);
        y += AlturaDoTexto;
    }

    // As barras usam a mesma cor do marcador da linha delas.
    private static readonly Color CorDaVida = new Color(0.75f, 0.15f, 0.12f);
    private static readonly Color CorDoNivel = new Color(0.85f, 0.65f, 0.15f);
    private const float TamanhoDoMarcador = 18f;

    /// <summary>
    /// Linha com um marcador na frente: o icone, quando tem, ou um
    /// quadradinho na cor. Bate o olho e acha a linha pela cor, sem ler.
    /// O texto anda pra direita o tamanho do marcador.
    /// </summary>
    private void Escrever(GUIContent texto, float x, float largura, ref float y, Texture2D icone, Color cor)
    {
        float recuo = TamanhoDoMarcador + 8f;
        var marcador = new Rect(x + 2f, y + (AlturaDoTexto - TamanhoDoMarcador) * 0.5f, TamanhoDoMarcador, TamanhoDoMarcador);

        if (icone != null)
            GUI.DrawTexture(marcador, icone, ScaleMode.ScaleToFit, true);
        else
            GUI.DrawTexture(marcador, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, cor, 0f, 4f);

        GUI.Label(new Rect(x + recuo, y, largura - recuo, AlturaDoTexto), texto, estilo);
        y += AlturaDoTexto;
    }

    private void DesenharControlesDoFps(Rect area, float escala)
    {
        float y = area.y;

        // Botao em vez de caixa de marcar: a caixa do OnGUI tem 12 pixels
        // e nao da pra ver de longe se esta marcada.
        GUIContent textoDoVSync = FpsLimiter.VSync ? VSyncLigado : VSyncDesligado;

        if (GUI.Button(new Rect(area.x, y, area.width, AlturaDoBotao), textoDoVSync, estiloDoBotao))
            FpsLimiter.VSync = !FpsLimiter.VSync;

        y += AlturaDoBotao + Espaco;

        // Com o vsync ligado o teto nao vale. Deixo os controles dele
        // apagados em vez de sumir, pra o painel nao pular de tamanho.
        bool habilitado = GUI.enabled;
        GUI.enabled = habilitado && !FpsLimiter.VSync;

        GUIContent textoDoTeto = FpsLimiter.Ligado ? TetoLigado : TetoDesligado;

        if (GUI.Button(new Rect(area.x, y, 150f, AlturaDoBotao), textoDoTeto, estiloDoBotao))
            FpsLimiter.Ligado = !FpsLimiter.Ligado;

        GUI.Label(new Rect(area.x + 156f, y - 2f, 80f, AlturaDoTexto), teto.Conteudo, estilo);

        if (GUI.Button(new Rect(area.xMax - 110f, y, 110f, AlturaDoBotao), padrao.Conteudo, estiloDoBotao))
            FpsLimiter.Fps = FpsLimiter.FpsPadrao;

        y += AlturaDoBotao + Espaco;

        int maximo = FpsLimiter.TaxaDoMonitor > 0
            ? FpsLimiter.TaxaDoMonitor
            : TetoMaximoSemMonitor;

        // Comparo com o numero ja preso na faixa do slider. Um teto fora
        // dela (monitor de 24 Hz, por exemplo) seria reescrito so por
        // ter sido desenhado, sem ninguem encostar no mouse.
        int mostrado = Mathf.Clamp(FpsLimiter.Fps, TetoMinimo, maximo);

        int escolhido = Mathf.RoundToInt(GUI.HorizontalSlider(
            new Rect(area.x, y, area.width, AlturaDoSlider),
            mostrado, TetoMinimo, maximo, estiloDoTrilho, estiloDoPino));

        if (escolhido != mostrado)
            FpsLimiter.Fps = escolhido;

        GUI.enabled = habilitado;

        // A area vem na tela de referencia, entao levo pra pixels de
        // verdade.
        controlesNaTela = new Rect(
            area.x * escala,
            area.y * escala,
            area.width * escala,
            area.height * escala);
    }

    private static void DesenharBarra(Rect fundo, float cheio, Color cor)
    {
        GUI.DrawTexture(fundo, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.15f, 0.15f, 0.15f), 0f, 0f);

        Rect preenchido = fundo;
        preenchido.width *= Mathf.Clamp01(cheio);
        GUI.DrawTexture(preenchido, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, cor, 0f, 0f);
    }
#endif
}
