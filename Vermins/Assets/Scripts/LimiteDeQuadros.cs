using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;

/// <summary>
/// Limita quantos quadros por segundo o jogo desenha.
///
/// Sem limite ele desenhava o maximo que a placa de video aguentava:
/// medi entre 403 e 432 quadros por segundo na HUBCity, num monitor que
/// mostra 210. O que passa do monitor nao aparece na tela, so esquenta
/// a maquina e liga a ventoinha.
///
/// Nao uso o Application.targetFrameRate. Foi a minha primeira versao e
/// engasgava: com teto de 210, medi quadros alternando entre 2,3 e 25
/// ms, com o pior em 40 ms, e de 21% a 31% deles a mais de um quarto de
/// distancia dos 4,76 ms que deviam durar. A propria documentacao da
/// Unity avisa que ele esta sujeito a micro engasgos.
///
/// No lugar dele vai uma espera minha, que eu ja usava em outro projeto.
/// Ela fica no comeco do quadro, antes de a Unity ler o relogio, entao o
/// Time.deltaTime ja sai com o intervalo certo e tudo anda de acordo com
/// o que a tela vai mostrar. Ela dorme enquanto sobra bastante tempo e
/// gira em vazio no trecho final, porque um sono so e preciso ate um ou
/// dois milissegundos e um quadro inteiro a 200 por segundo dura 5.
///
/// Medi na HUBCity, no editor, 10 s por rodada. Com teto de 210 a media
/// foi de 157-172 pra 209,5-209,7 quadros por segundo, o desvio de
/// 4,0-4,8 ms pra 0,45-0,70 e o pior quadro de 37-40 ms pra 8-10. Com
/// teto de 60 o desvio foi de 0,7-1,1 ms pra 0,01. O que sobra de
/// variacao perto de 200 nao e da espera: ela solta o quadro com atraso
/// mediano de 0,2 microssegundo, e o proprio jogo sem limite ja tem
/// quadros de 17 ms de vez em quando.
///
/// Fica numa classe estatica, sem objeto em cena, pra valer do menu em
/// diante sem eu ter que por componente em cada cena.
/// </summary>
public static class LimiteDeQuadros
{
    // Zero acompanha o monitor. Qualquer outro numero vira teto fixo,
    // pra quem quiser travar em 60 de proposito.
    private const int LimiteFixo = 0;

    // Alguns monitores nao informam a taxa e devolvem zero. Sem este
    // numero o jogo ficaria sem limite de novo.
    private const int LimiteSemMonitor = 60;

    // Acompanhando o monitor, o teto fica um pouco abaixo da taxa dele:
    // 4%, e nunca menos de 2 quadros. Num monitor de 210 Hz da 202. Em
    // cima da taxa exata sao dois relogios disputando a mesma fronteira,
    // e cada quadro vira sorteio de em qual atualizacao da tela ele cai.
    private const float FolgaDoMonitor = 0.04f;
    private const int FolgaMinima = 2;

    // Quanto tempo ainda sobra quando paro de dormir e comeco a girar,
    // alem do que um sono tem levado ultimamente.
    private const double MargemDoGiro = 0.0005d;

    // O minimo que eu assumo pra um sono de 1 ms. O Windows nunca acorda
    // ninguem antes do pedido.
    private const double CustoMinimoDoSono = 0.001d;

    // De onde a estimativa parte a cada vez que o jogo comeca.
    private const double CustoInicialDoSono = 0.002d;

    // Ate quanto depois da hora um quadro pode ser solto e ainda contar
    // como pontual. Separa o estouro normal do giro (microssegundos) de
    // um atraso de verdade. O valor veio medido do outro projeto.
    private const double AtrasoTolerado = 0.0002d;

    // O sono mais lento dos ultimos tempos, em segundos. Um Sleep(1)
    // leva 1 ms com o relogio do sistema fino e ate 15,6 com ele grosso,
    // e isso se decide fora do jogo. Aprender o numero impede o ultimo
    // sono de passar da hora nos dois casos.
    private static double custoDoSono = CustoInicialDoSono;

    // De quando conta o intervalo do quadro anterior, em segundos.
    private static double ultimoInicio;

    // Se a espera esta dentro do PlayerLoop.
    private static bool instalado;

    private static int quadrosPorSegundo;

    /// <summary>
    /// O teto em quadros por segundo, ou 0 pra nenhum.
    ///
    /// So peco o relogio fino do sistema enquanto existe teto pra
    /// segurar, e devolvo assim que nao existe mais.
    /// </summary>
    public static int QuadrosPorSegundo
    {
        get => quadrosPorSegundo;
        set
        {
            quadrosPorSegundo = Math.Max(0, value);

            if (instalado && quadrosPorSegundo > 0)
                SubirPrecisaoDoRelogio();
            else
                DevolverPrecisaoDoRelogio();
        }
    }

    /// <summary>
    /// Quanto a espera segurou este quadro, em segundos. Entra no
    /// Time.unscaledDeltaTime sem fazer parte do que o quadro custou.
    /// </summary>
    public static float UltimaEspera { get; private set; }

    /// <summary>
    /// Quanto depois da hora a espera soltou este quadro, em segundos.
    /// Normalmente alguns microssegundos. Serve pra medir; nada no jogo
    /// le isto.
    /// </summary>
    public static float UltimoAtraso { get; private set; }

    /// <summary>
    /// O teto pra um monitor com essa taxa: a taxa menos a folga. Abaixo
    /// de 30 nao tem o que tirar, e sem taxa conhecida vale o
    /// LimiteSemMonitor.
    /// </summary>
    public static int TetoPara(int taxaDoMonitor)
    {
        if (taxaDoMonitor <= 0)
            return LimiteSemMonitor;

        int folga = Math.Max(FolgaMinima, (int)(taxaDoMonitor * FolgaDoMonitor + 0.5f));

        return taxaDoMonitor - folga >= 30 ? taxaDoMonitor - folga : taxaDoMonitor;
    }

    /// <summary>
    /// Quando um quadro pode comecar, sabendo quando o anterior foi solto
    /// e que horas sao.
    ///
    /// Quadro adiantado espera a vez dele, um intervalo depois do ultimo.
    /// Quadro atrasado comeca na hora e o ritmo recomeca a partir dele.
    /// Nao encurto os proximos pra recuperar o tempo perdido, porque isso
    /// transforma um quadro torto em dois.
    /// </summary>
    public static double InicioDe(double inicioAnterior, double agora, double intervalo)
    {
        double previsto = inicioAnterior + intervalo;
        return previsto > agora ? previsto : agora;
    }

    /// <summary>
    /// De quando conta o intervalo do proximo quadro, sabendo quando este
    /// devia sair e quando a espera soltou de fato.
    ///
    /// Normalmente da hora prevista, pro ritmo ficar numa grade exata em
    /// vez de escorregar os microssegundos que toda soltura passa. Solto
    /// bem depois da hora (um sono que estourou, ou a thread perdendo a
    /// CPU), conta da soltura, pelo mesmo motivo do InicioDe.
    /// </summary>
    public static double AncoraDe(double previsto, double solto)
    {
        return solto - previsto > AtrasoTolerado ? solto : previsto;
    }

    /// <summary>
    /// Se ainda cabe mais um sono, dado o mais lento dos ultimos. Se nao
    /// cabe, o resto da espera e girado.
    /// </summary>
    public static bool DormeCom(double falta, double custo)
    {
        return falta > custo + MargemDoGiro;
    }

    /// <summary>
    /// O custo do sono pra lembrar depois de um sono que levou esse
    /// tempo: o mais lento dos ultimos, esquecido devagar.
    /// </summary>
    public static double CustoDoSonoDepois(double custo, double dormido)
    {
        return Math.Max(dormido, CustoDoSonoEsquecido(custo));
    }

    /// <summary>
    /// O custo do sono um pouco mais tarde, sem nada novo aprendido.
    ///
    /// Tambem roda uma vez pra cada quadro que nao dormiu. Um custo maior
    /// que a sobra do quadro faz a espera parar de dormir, e dormir era o
    /// unico lugar em que o custo era esquecido. Sem isto, um unico sono
    /// lento deixaria a espera girando um nucleo no maximo pelo resto da
    /// sessao. Esquecendo um centesimo por quadro, um sono de 15,6 ms e
    /// largado em mais ou menos um segundo.
    /// </summary>
    public static double CustoDoSonoEsquecido(double custo)
    {
        return Math.Max(CustoMinimoDoSono, custo * 0.99d);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Instalar()
    {
        UltimaEspera = 0f;
        UltimoAtraso = 0f;
        ultimoInicio = 0d;
        custoDoSono = CustoInicialDoSono;

        PlayerLoopSystem raiz = PlayerLoop.GetCurrentPlayerLoop();
        TirarDe(ref raiz);

        var espera = new PlayerLoopSystem
        {
            type = typeof(LimiteDeQuadros),
            updateDelegate = Esperar
        };

        // Primeira coisa dentro do TimeUpdate, na frente do sistema que
        // le o relogio do quadro. Em qualquer ponto depois, o deltaTime
        // nao enxergaria a espera.
        PlayerLoopSystem[] topo = raiz.subSystemList;
        int relogio = Array.FindIndex(topo, s => s.type == typeof(UnityEngine.PlayerLoop.TimeUpdate));

        if (relogio >= 0)
        {
            var dentro = new List<PlayerLoopSystem>(
                topo[relogio].subSystemList ?? Array.Empty<PlayerLoopSystem>());
            dentro.Insert(0, espera);
            topo[relogio].subSystemList = dentro.ToArray();
        }
        else
        {
            var todos = new List<PlayerLoopSystem>(topo);
            todos.Insert(0, espera);
            topo = todos.ToArray();
        }

        raiz.subSystemList = topo;
        PlayerLoop.SetPlayerLoop(raiz);

        instalado = true;

        // Comeca sem teto. O Aplicar poe o teto logo em seguida, antes
        // da primeira cena.
        QuadrosPorSegundo = 0;

        // Sair do Play mantem o dominio e o PlayerLoop, e o editor nao
        // tem por que ficar limitado.
        Application.quitting -= Desinstalar;
        Application.quitting += Desinstalar;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Aplicar()
    {
        int teto = LimiteFixo;

        if (teto <= 0)
        {
            double taxa = Screen.currentResolution.refreshRateRatio.value;

            teto = TetoPara(double.IsNaN(taxa) || taxa <= 0d
                ? 0
                : Mathf.RoundToInt((float)taxa));
        }

        QuadrosPorSegundo = teto;

        // O limite da Unity fica desligado de vez. No editor ele nao
        // volta sozinho pra -1 entre uma sessao de Play e outra.
        Application.targetFrameRate = -1;
    }

    // Tira a espera do PlayerLoop e devolve o relogio do sistema. Pode
    // chamar sem nada instalado, e mais de uma vez.
    private static void Desinstalar()
    {
        Application.quitting -= Desinstalar;
        instalado = false;
        QuadrosPorSegundo = 0;

        PlayerLoopSystem raiz = PlayerLoop.GetCurrentPlayerLoop();
        TirarDe(ref raiz);
        PlayerLoop.SetPlayerLoop(raiz);
    }

#if UNITY_EDITOR
    // Onde o teto espera uma recompilacao passar. So na sessao do editor.
    private const string ChaveDoTetoGuardado = "Vermins.LimiteDeQuadros.TetoGuardado";

    // Recompilar no meio do Play, que o editor faz quando um script e
    // salvo, joga fora todo campo estatico daqui. Vai junto a anotacao
    // de que subi o relogio do sistema, mas nao a subida, que e do
    // processo do editor e nunca mais seria devolvida. E o PlayerLoop
    // continuaria chamando uma espera que nao existe mais. Entao desfaco
    // os dois antes da recarga, guardo o teto no SessionState e, se o
    // jogo ainda estiver rodando depois, ponho tudo de volta.
    [UnityEditor.InitializeOnLoadMethod]
    private static void SobreviverARecompilacao()
    {
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= AntesDeRecompilar;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += AntesDeRecompilar;

        int guardado = UnityEditor.SessionState.GetInt(ChaveDoTetoGuardado, -1);
        UnityEditor.SessionState.EraseInt(ChaveDoTetoGuardado);

        if (guardado >= 0 && UnityEditor.EditorApplication.isPlaying)
        {
            Instalar();
            QuadrosPorSegundo = guardado;
        }
    }

    private static void AntesDeRecompilar()
    {
        if (instalado && UnityEditor.EditorApplication.isPlaying)
            UnityEditor.SessionState.SetInt(ChaveDoTetoGuardado, QuadrosPorSegundo);

        Desinstalar();
    }
#endif

    private static void TirarDe(ref PlayerLoopSystem sistema)
    {
        if (sistema.subSystemList == null)
            return;

        var mantidos = new List<PlayerLoopSystem>(sistema.subSystemList.Length);

        foreach (PlayerLoopSystem filho in sistema.subSystemList)
        {
            if (filho.type == typeof(LimiteDeQuadros))
                continue;

            PlayerLoopSystem copia = filho;
            TirarDe(ref copia);
            mantidos.Add(copia);
        }

        sistema.subSystemList = mantidos.ToArray();
    }

    private static void Esperar()
    {
        int teto = quadrosPorSegundo;

        if (teto <= 0)
        {
            UltimaEspera = 0f;
            UltimoAtraso = 0f;
            return;
        }

        double chegada = Agora();
        double inicio = InicioDe(ultimoInicio, chegada, 1d / teto);
        bool dormiu = false;

        while (true)
        {
            double falta = inicio - Agora();

            if (falta <= 0d)
                break;

            if (DormeCom(falta, custoDoSono))
            {
                double antes = Agora();
                Thread.Sleep(1);
                custoDoSono = CustoDoSonoDepois(custoDoSono, Agora() - antes);
                dormiu = true;
            }
            else
            {
                Thread.SpinWait(20);
            }
        }

        if (!dormiu)
            custoDoSono = CustoDoSonoEsquecido(custoDoSono);

        double solto = Agora();
        ultimoInicio = AncoraDe(inicio, solto);
        UltimaEspera = (float)(solto - chegada);
        UltimoAtraso = (float)(solto - inicio);
    }

    private static double Agora()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
    }

    // O Windows acorda quem dorme num tique de 15,6 ms, a nao ser que um
    // processo peca um mais fino. Pedir so economiza giro: o custo do
    // sono aprendido acerta o ritmo de qualquer jeito. Cada pedido que da
    // certo e devolvido exatamente uma vez, que e o que o timeEndPeriod
    // exige.
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [System.Runtime.InteropServices.DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint milissegundos);

    [System.Runtime.InteropServices.DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint milissegundos);

    private static bool relogioSubido;

    private static void SubirPrecisaoDoRelogio()
    {
        if (!relogioSubido)
            relogioSubido = timeBeginPeriod(1) == 0;
    }

    private static void DevolverPrecisaoDoRelogio()
    {
        if (relogioSubido)
        {
            timeEndPeriod(1);
            relogioSubido = false;
        }
    }
#else
    private static void SubirPrecisaoDoRelogio()
    {
    }

    private static void DevolverPrecisaoDoRelogio()
    {
    }
#endif
}
