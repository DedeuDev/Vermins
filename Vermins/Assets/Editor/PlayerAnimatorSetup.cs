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
/// Menu: Vermins > Player > Montar Animator
/// </summary>
public static class PlayerAnimatorSetup
{
    private const string ControllerPath =
        "Assets/Animation/Player/PlayerLocomotion.controller";

    private const string PastaClipes = "Assets/Placeholders/Player";

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

    /// <summary>
    /// Os clipes que entram na roseta. Idle fica no centro e e
    /// obrigatorio: o Freeform Directional precisa de alguem em (0,0) pra
    /// ter o que tocar quando o player para.
    ///
    /// Nao tem sprint pros lados nem pra tras porque o pack nao tem. Na
    /// pratica quase nao aparece: o corpo vira pra onde anda, entao andar
    /// de lado so acontece durante a virada.
    /// </summary>
    private static readonly string[] Locomocao =
    {
        "Idle",
        "WalkForward", "RunForward", "SprintForward",
        "WalkBack",    "RunBack",
        "WalkLeft",    "RunLeft",
        "WalkRight",   "RunRight",
    };

    /// <summary>
    /// As duas magias, alternadas. Duas e o minimo pra nao parecer
    /// bonequinho: repetir o mesmo gesto e o que mais denuncia
    /// placeholder, e a segunda custa um arquivo.
    /// </summary>
    private static readonly string[] Ataques =
    {
        "1HMagicAttack01", "1HMagicAttack02",
    };

    private const string ClipeDeMorte = "ReactDeathBackward";

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
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (controller == null)
        {
            Debug.LogError($"[Animator] Nao achei o controller em {ControllerPath}.");
            return;
        }

        foreach (string p in new[] { ParamX, ParamZ, ParamSpeed, ParamVariacao })
            GarantirParametro(controller, p, AnimatorControllerParameterType.Float);

        GarantirParametro(controller, ParamAtacar, AnimatorControllerParameterType.Trigger);
        GarantirParametro(controller, ParamMorto, AnimatorControllerParameterType.Bool);

        // Este comeca em 1 e nao em 0 de proposito. Parametro de
        // velocidade em zero congela o estado: se alguem abrir a cena
        // sem o PlayerAnimator, o personagem faria o gesto de ataque
        // parado no primeiro frame, pra sempre.
        GarantirParametro(controller, ParamVelAtaque, AnimatorControllerParameterType.Float);
        DefinirPadraoFloat(controller, ParamVelAtaque, 1f);

        Dictionary<string, AnimationClip> porNome = IndexarClipes();
        AnimatorStateMachine maquina = controller.layers[0].stateMachine;

        // Limpo o que eu mesmo montei antes, senao rodar duas vezes
        // empilha transicao repetida.
        LimparTransicoesDoAnyState(maquina);

        AnimatorState locomocao = AcharEstado(maquina, EstadoLocomocao);

        if (locomocao == null || !(locomocao.motion is BlendTree arvore))
        {
            Debug.LogError($"[Animator] O estado {EstadoLocomocao} nao tem blend " +
                           "tree dentro. Nao vou criar um do zero pra nao apagar " +
                           "o que ja estiver la.");
            return;
        }

        var faltando = new List<string>();

        if (!MontarLocomocao(arvore, porNome, faltando))
        {
            Reclamar(faltando);
            return;
        }

        AnimatorState ataque = MontarAtaque(controller, maquina, porNome, faltando);
        AnimatorState morte = MontarMorte(maquina, porNome, faltando);

        if (faltando.Count > 0)
        {
            Reclamar(faltando);
            return;
        }

        maquina.defaultState = locomocao;

        LigarTudo(maquina, locomocao, ataque, morte);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log(Resumo(arvore, ataque, morte));
    }

    private static void Reclamar(List<string> faltando)
    {
        Debug.LogError("[Animator] Faltou clipe: " + string.Join(", ", faltando) +
                       ". Rode Vermins/Player/Configurar FBX do Mixamo antes.");
    }

    private static bool MontarLocomocao(
        BlendTree arvore,
        Dictionary<string, AnimationClip> porNome,
        List<string> faltando)
    {
        var filhos = new List<ChildMotion>();

        foreach (string nome in Locomocao)
        {
            if (!porNome.TryGetValue(nome, out AnimationClip clipe))
            {
                faltando.Add(nome);
                continue;
            }

            filhos.Add(new ChildMotion
            {
                motion = clipe,
                position = PosicaoDoClipe(nome, clipe),
                timeScale = 1f,
                directBlendParameter = ParamX,
            });
        }

        if (faltando.Count > 0)
            return false;

        // Freeform Directional e nao Simple Directional. Simple aceita um
        // clipe so por direcao, e eu tenho tres pra frente (andar, correr,
        // esprintar) e dois pra cada outro lado. Cartesian ignoraria que o
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
        AnimatorController controller,
        AnimatorStateMachine maquina,
        Dictionary<string, AnimationClip> porNome,
        List<string> faltando)
    {
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

        for (int i = 0; i < Ataques.Length; i++)
        {
            if (!porNome.TryGetValue(Ataques[i], out AnimationClip clipe))
            {
                faltando.Add(Ataques[i]);
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
        estado.transitions = new AnimatorStateTransition[0];

        EditorUtility.SetDirty(arvore);

        return estado;
    }

    private static AnimatorState MontarMorte(
        AnimatorStateMachine maquina,
        Dictionary<string, AnimationClip> porNome,
        List<string> faltando)
    {
        AnimatorState estado = AcharEstado(maquina, EstadoMorte)
                               ?? maquina.AddState(EstadoMorte, new Vector3(60f, 250f, 0f));

        if (!porNome.TryGetValue(ClipeDeMorte, out AnimationClip clipe))
        {
            faltando.Add(ClipeDeMorte);
            return estado;
        }

        estado.motion = clipe;
        estado.writeDefaultValues = true;
        estado.transitions = new AnimatorStateTransition[0];

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
    /// </summary>
    private static Vector2 PosicaoDoClipe(string nome, AnimationClip clipe)
    {
        if (nome == "Idle")
            return Vector2.zero;

        Vector3 v = clipe.averageSpeed;

        return DirecaoDoNome(nome) * new Vector2(v.x, v.z).magnitude;
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

    private static Dictionary<string, AnimationClip> IndexarClipes()
    {
        var mapa = new Dictionary<string, AnimationClip>();

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { PastaClipes }))
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

    private static string Resumo(BlendTree arvore, AnimatorState ataque, AnimatorState morte)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"[Animator] Pronto: {arvore.children.Length} clipes na " +
                      "locomocao, mais ataque e morte.");

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

        return sb.ToString();
    }
}
