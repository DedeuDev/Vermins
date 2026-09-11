using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Monta o Animator do player inteiro: a locomocao, o ataque e a morte.
///
/// Fiz como menu em vez de arrastar na janela do Animator porque quase
/// nada aqui e numero inventado - as posicoes dos clipes na roseta saem
/// da velocidade medida em cada um. Se alguem trocar um clipe, e so
/// rodar de novo. Rodar duas vezes da o mesmo resultado.
///
/// O que este menu NAO decide mais e a velocidade do golpe. Ela dependia
/// do cooldown, e o cooldown virou atributo que muda em runtime - entao
/// agora sai por parametro e quem calcula e o PlayerAnimator.
///
/// Monta dois controllers com a MESMA estrutura, um por perfil: o da
/// magia (Vampire) e o do Paladino, com espada. Troquei o combate pra
/// corpo a corpo, mas quis guardar o da magia inteiro em vez de apagar -
/// ele continua saindo daqui igualzinho, e se a magia voltar como
/// habilidade o controller esta pronto. Os parametros, estados e camadas
/// tem os mesmos nomes nos dois, entao o PlayerAnimator serve pra
/// qualquer um.
///
/// Menu: Vermins > Player > Montar Animator            (magia)
///       Vermins > Player > Montar Animator do Paladino
/// </summary>
public static class PlayerAnimatorSetup
{
    /// <summary>
    /// De onde vem cada clipe e pra onde vai o controller. E so isso que
    /// muda de um personagem pro outro; o resto do menu e igual.
    /// </summary>
    private sealed class Perfil
    {
        public string nome;
        public string controllerPath;
        public string pastaClipes;

        /// <summary>
        /// Os clipes que entram na roseta. Idle fica no centro e e
        /// obrigatorio: o Freeform Directional precisa de alguem em (0,0)
        /// pra ter o que tocar quando o player para.
        /// </summary>
        public string[] locomocao;
        public string[] ataques;
        public string clipeDeMorte;
        public string clipeDeReacao;

        /// <summary>
        /// Se a posicao de cada clipe na roseta sai do pe (AnaliseDeClipe)
        /// em vez do root motion. Ver PosicaoDoClipe.
        /// </summary>
        public bool posicaoPeloPe;
    }

    /// <summary>
    /// O da magia, guardado. Nao tem sprint pros lados nem pra tras
    /// porque o pack nao tem. Na pratica quase nao aparece: o corpo vira
    /// pra onde anda, entao andar de lado so acontece durante a virada.
    ///
    /// As duas magias se alternam. Duas e o minimo pra nao parecer
    /// bonequinho: repetir o mesmo gesto e o que mais denuncia
    /// placeholder, e a segunda custa um arquivo.
    /// </summary>
    private static readonly Perfil Magia = new Perfil
    {
        nome = "magia",
        controllerPath = "Assets/Animation/Player/PlayerLocomotion.controller",
        pastaClipes = "Assets/Placeholders/Player",
        locomocao = new[]
        {
            "Idle",
            "WalkForward", "RunForward", "SprintForward",
            "WalkBack",    "RunBack",
            "WalkLeft",    "RunLeft",
            "WalkRight",   "RunRight",
        },
        ataques = new[] { "1HMagicAttack01", "1HMagicAttack02" },
        clipeDeMorte = "ReactDeathBackward",
        clipeDeReacao = "ReactSmallFromFront",
        posicaoPeloPe = false,
    };

    /// <summary>
    /// O Paladino, com a espada de duas maos. O pack dele nao tem sprint,
    /// entao a ponta da roseta pra frente e o RunForward.
    ///
    /// Os ataques foram escolhidos medindo a ponta da espada, quadro a
    /// quadro: tem que haver uma pancada rapida NA FRENTE do corpo, a uns
    /// 2 m. O 01 (great sword slash) desce de cima pra baixo aos 51%, reto
    /// na frente, a 1,89 m. O 02 e o great sword slash (5), um golpe baixo
    /// que varre a frente aos 37%, a 2,31 m.
    ///
    /// O 02 ja foi o great sword attack, e estava errado: a lamina ia pra
    /// tras do ombro direito e nunca descia na frente. Na tela parecia
    /// "ia bater num lugar e bateu em outro". Eu tinha escolhido so pela
    /// duracao e por nao sair do lugar, sem olhar onde a lamina caia.
    ///
    /// Entre os que caem na frente, o slash (5) ganhou pela duracao. Os
    /// dois golpes dividem o estado Ataque, e o PlayerAnimator acelera o
    /// estado pelo clipe MAIS LONGO pra caber no cooldown: com o slash (5),
    /// de 1,43 s, os dois tocam a 1,25x; com o slash (3), de 1,83 s,
    /// tocariam a 1,61x e o 01 ficaria corrido. Ficaram de fora os que
    /// avancam demais - o slash (4) leva o quadril 1,07 m pra frente, pra
    /// fora do collider, e quem manda na posicao e o NavMeshAgent.
    ///
    /// A reacao e o unico impacto do pack que fica em pe. Ela toca so no
    /// torso, e um impacto agachado em cima de pernas andando vira boneco
    /// quebrado.
    /// </summary>
    private static readonly Perfil Paladino = new Perfil
    {
        nome = "Paladino",
        controllerPath = "Assets/Animation/Player/PaladinoLocomotion.controller",
        pastaClipes = "Assets/Placeholders/Paladino/Animacoes",
        locomocao = new[]
        {
            "Idle",
            "WalkForward", "RunForward",
            "WalkBack",    "RunBack",
            "WalkLeft",    "RunLeft",
            "WalkRight",   "RunRight",
        },
        ataques = new[] { "GreatSwordAttack01", "GreatSwordAttack02" },
        clipeDeMorte = "GreatSwordDeathBackward",
        clipeDeReacao = "GreatSwordImpact",
        posicaoPeloPe = true,
    };

    public const string ParamX = "VelX";
    public const string ParamZ = "VelZ";
    public const string ParamSpeed = "Speed";
    public const string ParamAtacar = "Atacar";
    public const string ParamMorto = "Morto";
    public const string ParamVariacao = "Variacao";

    /// <summary>
    /// Multiplicador de velocidade do estado de ataque.
    ///
    /// Antes esta conta era feita aqui e GRAVADA no clipe: eu lia o
    /// cooldown do PlayerCombat na hora de montar e escrevia o timeScale
    /// fixo. Funcionava enquanto o cooldown so mudava no Inspector.
    ///
    /// Com o atributo Celeridade o cooldown passou a mudar em runtime, e
    /// um numero gravado nao acompanha - o golpe seguinte reiniciaria a
    /// animacao antes dela soltar a magia e o personagem ficaria
    /// carregando pra sempre. Entao a velocidade virou parametro e quem
    /// calcula agora e o PlayerAnimator, a cada golpe.
    /// </summary>
    public const string ParamVelAtaque = "VelAtaque";

    private const string EstadoLocomocao = "Locomocao";
    private const string EstadoAtaque = "Ataque";
    private const string EstadoMorte = "Morte";

    public const string ParamApanhar = "Apanhar";

    private const string CamadaDaReacao = "Reacao";
    private const string EstadoVazio = "Vazio";
    private const string EstadoReagir = "Reagir";

    private const string MascaraPath =
        "Assets/Animation/Player/TorsoParaCima.mask";

    /// <summary>
    /// As partes do esqueleto que a reacao a dano controla. O que fica de
    /// fora continua vindo da camada de baixo - e por isso que as pernas
    /// seguem andando enquanto o tronco leva o tranco.
    ///
    /// Root fica de fora de proposito, e nao por esquecimento: o clipe de
    /// reacao tem root motion, e ligar Root empurraria o personagem pra
    /// tras a cada golpe. Isso seria knockback, que ninguem pediu e que
    /// brigaria com o NavMeshAgent pela posicao do corpo.
    /// </summary>
    private static readonly AvatarMaskBodyPart[] OTorso =
    {
        AvatarMaskBodyPart.Body,
        AvatarMaskBodyPart.Head,
        AvatarMaskBodyPart.LeftArm,
        AvatarMaskBodyPart.RightArm,
        AvatarMaskBodyPart.LeftFingers,
        AvatarMaskBodyPart.RightFingers,
    };

    /// <summary>
    /// Em que ponto do clipe de reacao ele ja pode comecar a voltar pro
    /// Vazio. Faltando 15% a volta ja comeca, e como a transicao dura
    /// 0,15 s o fim do tranco e a volta pra locomocao se sobrepoem. Com
    /// 1,0 aqui o torso da um solavanco no ultimo frame.
    /// </summary>
    private const float SaidaDaReacao = 0.85f;

    /// <summary>
    /// A camada nasce pesando zero, e quem levanta o peso e o
    /// PlayerAnimator enquanto a reacao esta em cena.
    ///
    /// Deixei em 1 primeiro, achando que o estado Vazio bastava pra
    /// camada nao interferir. Nao basta: camada Override com peso 1
    /// parada num estado sem motion escreve a pose de bind nos ossos da
    /// mascara, e o personagem anda com o tronco travado. Peso zero e o
    /// unico jeito de a camada realmente sumir quando nao esta em uso.
    /// </summary>
    private const float PesoDaReacao = 0f;

    /// <summary>
    /// Em que ponto do clipe de ataque ele ja pode comecar a voltar pra
    /// locomocao. E fracao e nao segundo de proposito: o estado inteiro
    /// e esticado ou encurtado pelo ParamVelAtaque conforme o cooldown,
    /// entao 80% continua sendo 80% pra qualquer build. Em segundo, este
    /// numero teria que ser recalculado toda vez que a Celeridade
    /// mudasse.
    /// </summary>
    private const float SaidaDoAtaque = 0.80f;

    [MenuItem("Vermins/Player/Montar Animator")]
    public static void Montar()
    {
        Montar(Magia);
    }

    [MenuItem("Vermins/Player/Montar Animator do Paladino")]
    public static void MontarPaladino()
    {
        Montar(Paladino);
    }

    private static void Montar(Perfil perfil)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(perfil.controllerPath);

        // Controller que nao existe eu crio: num arquivo novo nao tem nada
        // pra apagar. O cuidado de nao recriar e so com o que ja existe,
        // mais abaixo.
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(perfil.controllerPath);

        foreach (string p in new[] { ParamX, ParamZ, ParamSpeed, ParamVariacao })
            GarantirParametro(controller, p, AnimatorControllerParameterType.Float);

        GarantirParametro(controller, ParamAtacar, AnimatorControllerParameterType.Trigger);
        GarantirParametro(controller, ParamMorto, AnimatorControllerParameterType.Bool);
        GarantirParametro(controller, ParamApanhar, AnimatorControllerParameterType.Trigger);

        // Este comeca em 1 e nao em 0 de proposito. Parametro de
        // velocidade em zero congela o estado: se alguem abrir a cena
        // sem o PlayerAnimator, o personagem faria o gesto de ataque
        // parado no primeiro frame, pra sempre.
        GarantirParametro(controller, ParamVelAtaque, AnimatorControllerParameterType.Float);
        DefinirPadraoFloat(controller, ParamVelAtaque, 1f);

        Dictionary<string, AnimationClip> porNome = IndexarClipes(perfil.pastaClipes);
        AnimatorStateMachine maquina = controller.layers[0].stateMachine;

        // Limpo o que eu mesmo montei antes, senao rodar duas vezes
        // empilha transicao repetida.
        LimparTransicoesDoAnyState(maquina);

        AnimatorState locomocao = AcharEstado(maquina, EstadoLocomocao);
        BlendTree arvore;

        if (locomocao == null)
        {
            // Mesmo metodo do ataque: estado e arvore nascem juntos como
            // sub-asset do controller, senao a arvore fica orfa.
            locomocao = controller.CreateBlendTreeInController(EstadoLocomocao, out arvore, 0);
        }
        else if (locomocao.motion is BlendTree existente)
        {
            arvore = existente;
        }
        else
        {
            Debug.LogError($"[Animator] O estado {EstadoLocomocao} nao tem blend " +
                           "tree dentro. Nao vou criar um do zero pra nao apagar " +
                           "o que ja estiver la.");
            return;
        }

        var faltando = new List<string>();

        if (!MontarLocomocao(perfil, arvore, porNome, faltando))
        {
            Reclamar(faltando);
            return;
        }

        AnimatorState ataque = MontarAtaque(perfil, controller, maquina, porNome, faltando);
        AnimatorState morte = MontarMorte(perfil, maquina, porNome, faltando);
        AnimatorState reacao = MontarReacao(perfil, controller, porNome, faltando);

        if (faltando.Count > 0)
        {
            Reclamar(faltando);
            return;
        }

        maquina.defaultState = locomocao;

        LigarTudo(maquina, locomocao, ataque, morte);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log(Resumo(perfil, arvore, ataque, morte, reacao));
    }

    private static void Reclamar(List<string> faltando)
    {
        Debug.LogError("[Animator] Faltou clipe: " + string.Join(", ", faltando) +
                       ". Rode Vermins/Player/Configurar FBX do Mixamo antes.");
    }

    private static bool MontarLocomocao(
        Perfil perfil,
        BlendTree arvore,
        Dictionary<string, AnimationClip> porNome,
        List<string> faltando)
    {
        var filhos = new List<ChildMotion>();

        using (var analise = perfil.posicaoPeloPe ? new AnaliseDeClipe() : null)
        {
            foreach (string nome in perfil.locomocao)
            {
                if (!porNome.TryGetValue(nome, out AnimationClip clipe))
                {
                    faltando.Add(nome);
                    continue;
                }

                filhos.Add(new ChildMotion
                {
                    motion = clipe,
                    position = PosicaoDoClipe(nome, clipe, analise),
                    timeScale = 1f,
                    directBlendParameter = ParamX,
                });
            }
        }

        if (faltando.Count > 0)
            return false;

        // Freeform Directional e nao Simple Directional. Simple aceita um
        // clipe so por direcao, e eu tenho dois ou tres pra frente (andar,
        // correr e, na magia, esprintar). Cartesian ignoraria que o
        // que organiza isto aqui e a direcao.
        arvore.blendType = BlendTreeType.FreeformDirectional2D;
        arvore.blendParameter = ParamX;
        arvore.blendParameterY = ParamZ;
        arvore.useAutomaticThresholds = false;
        arvore.children = filhos.ToArray();

        EditorUtility.SetDirty(arvore);

        return true;
    }

    private static AnimatorState MontarAtaque(
        Perfil perfil,
        AnimatorController controller,
        AnimatorStateMachine maquina,
        Dictionary<string, AnimationClip> porNome,
        List<string> faltando)
    {
        string[] ataques = perfil.ataques;
        AnimatorState estado = AcharEstado(maquina, EstadoAtaque);
        BlendTree arvore = estado?.motion as BlendTree;

        if (arvore == null)
        {
            // Este metodo cria o estado E a arvore como sub-asset do
            // controller de uma vez. Criando a arvore solta ela ficaria
            // orfa e o Unity a perderia ao reabrir o projeto.
            estado = controller.CreateBlendTreeInController(EstadoAtaque, out arvore, 0);
        }

        var filhos = new List<ChildMotion>();

        for (int i = 0; i < ataques.Length; i++)
        {
            if (!porNome.TryGetValue(ataques[i], out AnimationClip clipe))
            {
                faltando.Add(ataques[i]);
                continue;
            }

            filhos.Add(new ChildMotion
            {
                motion = clipe,
                threshold = i,

                // Cada clipe toca na velocidade natural dele. Quem
                // encurta o golpe pra caber no cooldown e a velocidade
                // do ESTADO, que vem por parametro - ver ParamVelAtaque.
                timeScale = 1f,

                directBlendParameter = ParamVariacao,
            });
        }

        // 1D e nao 2D: aqui nao tem mistura nenhuma pra fazer, so escolha.
        // O PlayerAnimator poe a Variacao exatamente em 0 ou 1, entao um
        // clipe fica com peso 1 e o outro com zero.
        arvore.blendType = BlendTreeType.Simple1D;
        arvore.blendParameter = ParamVariacao;
        arvore.useAutomaticThresholds = false;
        arvore.children = filhos.ToArray();

        // A velocidade do golpe sai daqui e nao do clipe. O speed fica em
        // 1 porque ele MULTIPLICA o parametro - deixar os dois mexendo
        // daria pra esquecer um e passar meia hora procurando por que o
        // ataque esta com o dobro da velocidade pedida.
        estado.speed = 1f;
        estado.speedParameterActive = true;
        estado.speedParameter = ParamVelAtaque;

        estado.writeDefaultValues = true;
        LimparTransicoes(estado);

        EditorUtility.SetDirty(arvore);

        return estado;
    }

    private static AnimatorState MontarMorte(
        Perfil perfil,
        AnimatorStateMachine maquina,
        Dictionary<string, AnimationClip> porNome,
        List<string> faltando)
    {
        AnimatorState estado = AcharEstado(maquina, EstadoMorte)
                               ?? maquina.AddState(EstadoMorte, new Vector3(60f, 250f, 0f));

        if (!porNome.TryGetValue(perfil.clipeDeMorte, out AnimationClip clipe))
        {
            faltando.Add(perfil.clipeDeMorte);
            return estado;
        }

        estado.motion = clipe;
        estado.writeDefaultValues = true;
        LimparTransicoes(estado);

        return estado;
    }

    /// <summary>
    /// As ligacoes.
    ///
    /// Ataque e morte saem do Any State de proposito. O ataque porque ele
    /// tem que poder disparar de qualquer lugar, inclusive de dentro dele
    /// mesmo - golpe atras de golpe e o caso normal. A morte porque
    /// morrer no meio de um golpe tem que interromper o golpe.
    /// </summary>
    private static void LigarTudo(
        AnimatorStateMachine maquina,
        AnimatorState locomocao,
        AnimatorState ataque,
        AnimatorState morte)
    {
        AnimatorStateTransition paraAtaque = maquina.AddAnyStateTransition(ataque);
        paraAtaque.hasExitTime = false;
        paraAtaque.duration = 0.10f;
        paraAtaque.canTransitionToSelf = true;
        paraAtaque.AddCondition(AnimatorConditionMode.If, 0f, ParamAtacar);

        // Sem esta segunda condicao, um Atacar que tivesse ficado
        // pendurado na fila mataria a animacao de morte no frame seguinte
        // e o corpo levantaria pra lancar magia.
        paraAtaque.AddCondition(AnimatorConditionMode.IfNot, 0f, ParamMorto);

        AnimatorStateTransition voltaDoAtaque = ataque.AddTransition(locomocao);
        voltaDoAtaque.hasExitTime = true;
        voltaDoAtaque.exitTime = SaidaDoAtaque;
        voltaDoAtaque.duration = 0.15f;

        AnimatorStateTransition paraMorte = maquina.AddAnyStateTransition(morte);
        paraMorte.hasExitTime = false;
        paraMorte.duration = 0.20f;

        // Sem isto ele reentra na morte todo frame enquanto o Morto
        // estiver ligado, e a animacao fica presa no primeiro quadro.
        paraMorte.canTransitionToSelf = false;
        paraMorte.AddCondition(AnimatorConditionMode.If, 0f, ParamMorto);

        // A morte nao tem saida por tempo: o corpo fica caido. A unica
        // saida e o Health.Revive desligar o Morto, que e o que o respawn
        // vai usar.
        AnimatorStateTransition levanta = morte.AddTransition(locomocao);
        levanta.hasExitTime = false;
        levanta.duration = 0.25f;
        levanta.AddCondition(AnimatorConditionMode.IfNot, 0f, ParamMorto);
    }

    /// <summary>
    /// Onde cada clipe de locomocao fica na roseta: a direcao vem do NOME
    /// dele, o tamanho vem da velocidade de raiz medida no proprio clipe.
    ///
    /// Separado assim de proposito. A direcao que o root motion indica
    /// erra alguns graus; o tamanho nao. Conferi medindo a passada do pe,
    /// que e independente do root motion - o WalkLeft deu 1,24 contra 1,25
    /// e o RunLeft 3,25 contra 3,27. Um por cento.
    ///
    /// So o Idle e forcado em (0,0).
    ///
    /// No Paladino o tamanho sai do PE, e nao do root motion. Aquele um
    /// por cento era do pack antigo; no da espada os dois discordam feio:
    /// RunForward 3,22 m/s de root contra 2,58 no pe, RunLeft 2,18 contra
    /// 1,52. Como o root motion e jogado fora (quem anda e o
    /// NavMeshAgent), o numero que importa e a velocidade em que o pe
    /// fica parado no chao - e esse e o do AnaliseDeClipe. Com o root
    /// motion na roseta, o agente a 2,58 cairia entre andar e correr e
    /// tocaria uma mistura dos dois com o pe patinando.
    ///
    /// A magia continua pelo root motion pra sair igual a antes.
    /// </summary>
    private static Vector2 PosicaoDoClipe(string nome, AnimationClip clipe, AnaliseDeClipe analise)
    {
        if (nome == "Idle")
            return Vector2.zero;

        Vector3 v = clipe.averageSpeed;
        float tamanho = new Vector2(v.x, v.z).magnitude;

        if (analise != null)
        {
            AnaliseDeClipe.Medida m = analise.Medir(clipe);

            if (m.valido && m.amostrasNoChao > 0)
                tamanho = m.velocidade;
            else
                Debug.LogWarning($"[Animator] Nao consegui medir o pe no {nome}; " +
                                 "usei o root motion, que neste pack erra.");
        }

        return DirecaoDoNome(nome) * tamanho;
    }

    private static Vector2 DirecaoDoNome(string nome)
    {
        string m = nome.ToLowerInvariant();

        if (m.Contains("forward")) return new Vector2(0f, 1f);
        if (m.Contains("back")) return new Vector2(0f, -1f);
        if (m.Contains("left")) return new Vector2(-1f, 0f);
        if (m.Contains("right")) return new Vector2(1f, 0f);

        return Vector2.zero;
    }

    private static AnimatorState AcharEstado(AnimatorStateMachine maquina, string nome)
    {
        foreach (ChildAnimatorState filho in maquina.states)
        {
            if (filho.state.name == nome)
                return filho.state;
        }

        return null;
    }

    private static void LimparTransicoesDoAnyState(AnimatorStateMachine maquina)
    {
        foreach (AnimatorStateTransition t in maquina.anyStateTransitions)
            maquina.RemoveAnyStateTransition(t);
    }

    /// <summary>
    /// Tira as transicoes de saida de um estado APAGANDO cada uma.
    ///
    /// Antes eu fazia estado.transitions = vazio, e isso so tira da
    /// lista: a transicao continua gravada dentro do .controller, solta,
    /// sem ninguem apontar pra ela. Cada vez que o menu rodava sobravam
    /// tres. Achei quando montei o controller do Paladino e o da magia ja
    /// tinha 21 transicoes no arquivo pra 6 em uso. Nao muda nada no
    /// jogo, mas o arquivo so crescia e o diff de toda rodada vinha cheio
    /// de lixo. O RemoveTransition apaga o objeto junto.
    /// </summary>
    private static void LimparTransicoes(AnimatorState estado)
    {
        foreach (AnimatorStateTransition t in estado.transitions)
            estado.RemoveTransition(t);
    }

    private static void GarantirParametro(
        AnimatorController controller,
        string nome,
        AnimatorControllerParameterType tipo)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name != nome)
                continue;

            if (p.type == tipo)
                return;

            controller.RemoveParameter(p);
            break;
        }

        controller.AddParameter(nome, tipo);
    }

    /// <summary>
    /// O AddParameter nasce com o valor padrao zerado e nao da pra
    /// escolher na chamada. Tenho que reescrever o array inteiro de
    /// parametros porque o que o controller devolve e uma copia.
    /// </summary>
    private static void DefinirPadraoFloat(
        AnimatorController controller,
        string nome,
        float valor)
    {
        AnimatorControllerParameter[] todos = controller.parameters;

        for (int i = 0; i < todos.Length; i++)
        {
            if (todos[i].name == nome)
                todos[i].defaultFloat = valor;
        }

        controller.parameters = todos;
    }

    /// <summary>
    /// A segunda camada, que toca a reacao a dano so no torso.
    ///
    /// Camada de Animator e sobreposicao: uma camada com peso 1
    /// SUBSTITUI o que a de baixo estava fazendo. A AvatarMask e o que
    /// diz "so estes ossos vem daqui" - o resto continua vindo da
    /// locomocao. E por isso que da pra apanhar do tronco pra cima
    /// enquanto as pernas seguem a passada: sao duas animacoes tocando ao
    /// mesmo tempo em partes diferentes do esqueleto.
    ///
    /// Sem mascara, camada nova seria interrupcao. Com mascara, e reacao
    /// por cima - que e o que ARPG faz, porque tirar o controle do
    /// jogador por 1,2 s a cada golpe recebido vira stun-lock com dois
    /// inimigos em cima.
    ///
    /// A camada sai daqui pesando ZERO. Ela so pesa enquanto a reacao
    /// esta tocando, e quem faz essa rampa e o PlayerAnimator - ver o
    /// comentario do PesoDaReacao logo acima.
    ///
    /// A entrada sai do Any State, e nao do Vazio, pra que um segundo
    /// golpe REINICIE a reacao no meio dela. Saindo do Vazio, apanhar
    /// duas vezes seguidas mostraria um tranco so.
    /// </summary>
    private static AnimatorState MontarReacao(
        Perfil perfil,
        AnimatorController controller,
        Dictionary<string, AnimationClip> porNome,
        List<string> faltando)
    {
        AnimatorStateMachine maquina = AcharCamada(controller, CamadaDaReacao);

        if (maquina == null)
        {
            maquina = new AnimatorStateMachine
            {
                name = CamadaDaReacao,
                hideFlags = HideFlags.HideInHierarchy,
            };

            // A maquina de estados e um sub-asset do controller. Sem esta
            // linha ela fica solta e some quando o Unity recarregar.
            AssetDatabase.AddObjectToAsset(maquina, controller);

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = CamadaDaReacao,
                defaultWeight = PesoDaReacao,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = maquina,
                avatarMask = GarantirMascara(),
            });
        }
        else
        {
            // Rodar de novo tem que dar o mesmo resultado, entao eu
            // reaproveito a camada e so reescrevo o conteudo dela.
            AnimatorControllerLayer[] camadas = controller.layers;

            for (int i = 0; i < camadas.Length; i++)
            {
                if (camadas[i].name != CamadaDaReacao)
                    continue;

                camadas[i].defaultWeight = PesoDaReacao;
                camadas[i].blendingMode = AnimatorLayerBlendingMode.Override;
                camadas[i].avatarMask = GarantirMascara();
            }

            controller.layers = camadas;
        }

        // Estado sem motion nenhum: enquanto ele esta ativo a camada nao
        // contribui nada e o torso continua vindo da locomocao. Write
        // Defaults desligado e o que garante isso - ligado, ele
        // escreveria a pose padrao por cima.
        AnimatorState vazio = AcharEstado(maquina, EstadoVazio)
                              ?? maquina.AddState(EstadoVazio, new Vector3(260f, 60f, 0f));

        vazio.motion = null;
        vazio.writeDefaultValues = false;
        LimparTransicoes(vazio);

        AnimatorState reagir = AcharEstado(maquina, EstadoReagir)
                               ?? maquina.AddState(EstadoReagir, new Vector3(260f, 170f, 0f));

        reagir.writeDefaultValues = false;
        LimparTransicoes(reagir);

        maquina.defaultState = vazio;

        LimparTransicoesDoAnyState(maquina);

        if (!porNome.TryGetValue(perfil.clipeDeReacao, out AnimationClip clipe))
        {
            faltando.Add(perfil.clipeDeReacao);
            return reagir;
        }

        reagir.motion = clipe;

        AnimatorStateTransition entrada = maquina.AddAnyStateTransition(reagir);
        entrada.AddCondition(AnimatorConditionMode.If, 0f, ParamApanhar);
        entrada.hasExitTime = false;
        entrada.duration = 0.08f;
        entrada.canTransitionToSelf = true;

        AnimatorStateTransition saida = reagir.AddTransition(vazio);
        saida.hasExitTime = true;
        saida.exitTime = SaidaDaReacao;
        saida.duration = 0.15f;

        return reagir;
    }

    /// <summary>
    /// Cria a mascara do torso se ela nao existir, e reescreve as partes
    /// ligadas de qualquer jeito - assim uma mascara que alguem mexeu na
    /// mao volta pro combinado ao rodar o menu.
    /// </summary>
    private static AvatarMask GarantirMascara()
    {
        var mascara = AssetDatabase.LoadAssetAtPath<AvatarMask>(MascaraPath);
        bool nova = mascara == null;

        if (nova)
            mascara = new AvatarMask();

        var ligadas = new HashSet<AvatarMaskBodyPart>(OTorso);

        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            var parte = (AvatarMaskBodyPart)i;
            mascara.SetHumanoidBodyPartActive(parte, ligadas.Contains(parte));
        }

        if (nova)
            AssetDatabase.CreateAsset(mascara, MascaraPath);
        else
            EditorUtility.SetDirty(mascara);

        return mascara;
    }

    private static AnimatorStateMachine AcharCamada(
        AnimatorController controller,
        string nome)
    {
        foreach (AnimatorControllerLayer camada in controller.layers)
        {
            if (camada.name == nome)
                return camada.stateMachine;
        }

        return null;
    }

    private static Dictionary<string, AnimationClip> IndexarClipes(string pasta)
    {
        var mapa = new Dictionary<string, AnimationClip>();

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { pasta }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                // O Unity guarda um clipe escondido "__preview__Alguma"
                // junto do de verdade. Pegar ele por engano deixa o blend
                // vazio.
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                    mapa[c.name] = c;
            }
        }

        return mapa;
    }

    private static string Resumo(
        Perfil perfil,
        BlendTree arvore,
        AnimatorState ataque,
        AnimatorState morte,
        AnimatorState reacao)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"[Animator] Pronto ({perfil.nome}): {arvore.children.Length} " +
                      "clipes na locomocao, mais ataque e morte. Posicoes pelo " +
                      (perfil.posicaoPeloPe ? "pe." : "root motion."));

        foreach (ChildMotion c in arvore.children)
        {
            sb.AppendLine(string.Format(ci, "  {0,-14} x={1,6:F2}  z={2,6:F2}",
                c.motion.name, c.position.x, c.position.y));
        }

        if (ataque.motion is BlendTree arvoreAtaque)
        {
            foreach (ChildMotion c in arvoreAtaque.children)
            {
                var clipe = (AnimationClip)c.motion;

                // Nao dou o tempo na tela porque daqui eu nao sei: ele
                // depende do VelAtaque, que so existe rodando.
                sb.AppendLine(string.Format(ci,
                    "  ataque: {0}  {1:F2} s no clipe (na tela depende do VelAtaque)",
                    clipe.name, clipe.length));
            }
        }

        if (morte.motion != null)
        {
            sb.AppendLine(string.Format(ci, "  morte:  {0} ({1:F2} s)",
                morte.motion.name, ((AnimationClip)morte.motion).length));
        }

        if (reacao != null && reacao.motion != null)
        {
            sb.AppendLine(string.Format(ci,
                "  reacao: {0} ({1:F2} s) na camada {2}, so no torso",
                reacao.motion.name, ((AnimationClip)reacao.motion).length,
                CamadaDaReacao));
        }

        return sb.ToString();
    }
}
