using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// HUD de debug do jogador: vida, pocoes, ouro, joias, alvo, velocidade e fps num canto da tela.
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

    /// <summary>
    /// Se o mouse esta em cima dos controles do teto de fps.
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

            return controlesNaTela.Contains(cursor);
#else
            return false;
#endif
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Onde o painel fica, na tela de referencia de 1080 de altura.
    private static readonly Rect Painel = new Rect(16f, 16f, 380f, 382f);

    // O menor teto que o slider oferece, e o maior quando o monitor nao
    // informa a taxa. Com a taxa conhecida o maior e ela.
    private const int TetoMinimo = 30;
    private const int TetoMaximoSemMonitor = 240;

    // Onde os controles do fps foram desenhados, em pixels da tela.
    // Vazio enquanto o HUD esta escondido.
    private static Rect controlesNaTela;

    private GUIStyle estiloDoBotao;
    private GUIStyle estiloDoTrilho;
    private GUIStyle estiloDoPino;

    private Health health;
    private PotionBelt belt;
    private GoldWallet wallet;
    private JewelPouch pouch;
    private PlayerCombat combat;
    private NavMeshAgent agent;
    private GUIStyle estilo;

    // De quanto em quanto tempo a linha do fps troca de numero.
    // Frame a frame o valor treme e nao da pra ler.
    private const float JanelaDoFps = 0.5f;

    private float tempoNaJanela;
    private int framesNaJanela;
    private float piorNaJanela;
    private float fps;
    private float piorFrame;

    private void Awake()
    {
        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombat>();
        agent = GetComponent<NavMeshAgent>();
        belt = GetComponent<PotionBelt>();
        wallet = GetComponent<GoldWallet>();
        pouch = GetComponent<JewelPouch>();
    }

    private void Update()
    {
        // Leio o teclado direto em vez de criar uma acao no
        // InputSystem_Actions. E ferramenta de debug e vai sumir - nao
        // quero deixar acao sobrando no asset do jogo depois.
        Keyboard teclado = Keyboard.current;

        if (teclado != null && teclado[teclaParaEsconder].wasPressedThisFrame)
            visivel = !visivel;

        MedirFps();
    }

    private void OnDisable()
    {
        controlesNaTela = default;
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

    private void OnGUI()
    {
        if (!visivel)
        {
            controlesNaTela = default;
            return;
        }

        // Escalo pela altura da tela com 1080 de referencia. O OnGUI
        // trabalha em pixel, entao sem isso o texto fica minusculo num
        // monitor 4K e enorme numa janela pequena do editor.
        float escala = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));

        // So da pra criar o estilo aqui dentro: o GUI.skin nao existe
        // fora do OnGUI.
        estilo ??= new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };

        GUILayout.BeginArea(Painel, GUI.skin.box);

        GUILayout.Label($"<b>DEBUG</b>   ({teclaParaEsconder} esconde)", estilo);

        GUILayout.Label($"Vida   {health.Current:F0} / {health.Max:F0}", estilo);
        DesenharBarra(health.Normalized, new Color(0.75f, 0.15f, 0.12f));

        // O cinto e opcional no Player; sem ele a linha so nao aparece.
        if (belt != null)
            GUILayout.Label($"Pocoes   {belt.Count} / {belt.Capacity}", estilo);

        if (wallet != null)
            GUILayout.Label($"Ouro   {wallet.Amount}", estilo);

        if (pouch != null)
            GUILayout.Label($"Joias   {pouch.Count}  (valem {pouch.TotalValue})", estilo);

        GUILayout.Label($"Alvo   {DescreverAlvo()}", estilo);

        GUILayout.Label($"Velocidade   {DescreverVelocidade()}", estilo);

        GUILayout.Label($"FPS   {DescreverFps()}", estilo);

        DesenharControlesDoFps(escala);

        GUILayout.EndArea();
    }

    private void DesenharControlesDoFps(float escala)
    {
        // O slider padrao do OnGUI tem 12 pixels de altura, dificil de
        // pegar com o mouse. Engrosso o trilho e o pino.
        estiloDoBotao ??= new GUIStyle(GUI.skin.button) { fontSize = 16 };
        estiloDoTrilho ??= new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 20f };
        estiloDoPino ??= new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedHeight = 20f, fixedWidth = 20f };

        GUILayout.BeginVertical();

        string estadoDoVSync = LimiteDeFps.VSync ? "ligado" : "desligado";

        if (GUILayout.Button($"vsync {estadoDoVSync}", estiloDoBotao))
            LimiteDeFps.VSync = !LimiteDeFps.VSync;

        // Com o vsync ligado o teto nao vale. Deixo os controles dele
        // apagados em vez de sumir, pra o painel nao pular de tamanho.
        bool habilitado = GUI.enabled;
        GUI.enabled = habilitado && !LimiteDeFps.VSync;

        GUILayout.BeginHorizontal();

        // Botao em vez de caixa de marcar: a caixa do OnGUI tem 12
        // pixels e nao da pra ver de longe se esta marcada.
        string estado = LimiteDeFps.Ligado ? "ligado" : "desligado";

        if (GUILayout.Button($"teto {estado}", estiloDoBotao))
            LimiteDeFps.Ligado = !LimiteDeFps.Ligado;

        GUILayout.Label($"{LimiteDeFps.Fps}", estilo);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button($"padrao {LimiteDeFps.FpsPadrao}", estiloDoBotao))
            LimiteDeFps.Fps = LimiteDeFps.FpsPadrao;

        GUILayout.EndHorizontal();

        int maximo = LimiteDeFps.TaxaDoMonitor > 0
            ? LimiteDeFps.TaxaDoMonitor
            : TetoMaximoSemMonitor;

        // Comparo com o numero ja preso na faixa do slider. Um teto fora
        // dela (monitor de 24 Hz, por exemplo) seria reescrito so por
        // ter sido desenhado, sem ninguem encostar no mouse.
        int mostrado = Mathf.Clamp(LimiteDeFps.Fps, TetoMinimo, maximo);

        int escolhido = Mathf.RoundToInt(GUILayout.HorizontalSlider(
            mostrado, TetoMinimo, maximo, estiloDoTrilho, estiloDoPino));

        if (escolhido != mostrado)
            LimiteDeFps.Fps = escolhido;

        GUI.enabled = habilitado;

        GUILayout.EndVertical();

        // O retangulo so vale no Repaint. Ele vem relativo ao painel e
        // na tela de referencia, entao levo pra pixels de verdade.
        if (Event.current.type != EventType.Repaint)
            return;

        Rect faixa = GUILayoutUtility.GetLastRect();

        controlesNaTela = new Rect(
            (Painel.x + faixa.x) * escala,
            (Painel.y + faixa.y) * escala,
            faixa.width * escala,
            faixa.height * escala);
    }

    private string DescreverFps()
    {
        // Media contra o teto do LimiteDeFps, e o frame mais demorado
        // da mesma janela. A media mostra que o teto esta valendo; o pior
        // frame e o que denuncia engasgo, que a media esconde.
        int teto = LimiteDeFps.TetoEmVigor;
        string limite = LimiteDeFps.VSync ? "vsync"
            : teto > 0 ? teto.ToString()
            : "sem teto";

        return $"{fps:F0} / {limite}   pior {piorFrame * 1000f:F1} ms";
    }

    private void DesenharBarra(float cheio, Color cor)
    {
        Rect fundo = GUILayoutUtility.GetRect(340f, 14f);
        GUI.DrawTexture(fundo, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.15f, 0.15f, 0.15f), 0f, 0f);

        Rect preenchido = fundo;
        preenchido.width *= Mathf.Clamp01(cheio);
        GUI.DrawTexture(preenchido, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, cor, 0f, 0f);
    }

    private string DescreverAlvo()
    {
        // O == null do Unity tambem pega alvo ja destruido, que e o que
        // acontece com o inimigo 2 s depois de morrer.
        Health alvo = combat != null ? combat.Target : null;

        if (alvo == null)
            return "nenhum";

        // Distancia no chao, sem a altura. E essa que o PlayerCombat
        // compara com o alcance, entao e essa que interessa ver.
        Vector3 ate = alvo.transform.position - transform.position;
        ate.y = 0f;

        string texto = $"{alvo.name}  {alvo.Current:F0}/{alvo.Max:F0}  a {ate.magnitude:F1} m";
        return alvo.IsDead ? texto + "  (morto)" : texto;
    }

    private string DescreverVelocidade()
    {
        if (agent == null)
            return "sem NavMeshAgent";

        // A velocidade de verdade do agente contra o maximo dele. Mostro
        // as duas porque "andando a 1.9 de 3.2" e o que denuncia quando
        // a aceleracao ou uma curva estao segurando o personagem.
        Vector3 v = agent.velocity;
        v.y = 0f;

        return $"{v.magnitude:F2} / {agent.speed:F2} m/s";
    }
#endif
}
