using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Monta o blend tree 2D de locomocao do player.
///
/// Antes era 1D: um float Speed decidia entre parado, andar, correr e
/// esprintar, sempre pra frente. O problema e que o corpo do player nao
/// aponta pra onde ele anda o tempo todo - ele leva 0,2 s pra virar numa
/// curva de 90 e 0,32 s numa inversao de 180 (ver o tempoDeGiro no
/// PlayerMotor). Durante esse tempo ele anda de lado e a animacao insiste
/// em tocar corrida pra frente. Num jogo de clicar pra andar isso acontece
/// a cada clique, entao nao e um caso raro.
///
/// Com o 2D eu mando as duas componentes da velocidade em espaco local e
/// deixo o Mecanim escolher a mistura de frente/tras/lados.
///
/// Fiz como menu em vez de arrastar na mao na janela do Animator porque
/// as posicoes dos clipes nao sao numero inventado: sao a velocidade de
/// raiz medida de cada clipe. Se alguem trocar um clipe, e so rodar de
/// novo que as posicoes se ajustam sozinhas.
///
/// Menu: Vermins > Player > Montar Blend Tree 2D
/// </summary>
public static class PlayerBlendTree2D
{
    private const string ControllerPath =
        "Assets/Animation/Player/PlayerLocomotion.controller";

    private const string PastaClipes = "Assets/Placeholders/Player";

    private const string NomeDoEstado = "Locomocao";

    public const string ParamX = "VelX";
    public const string ParamZ = "VelZ";
    public const string ParamSpeed = "Speed";

    /// <summary>
    /// Os clipes que entram na roseta. Idle fica no centro e e obrigatorio:
    /// o Freeform Directional precisa de alguem em (0,0) pra ter o que
    /// tocar quando o player para.
    ///
    /// Nao tem sprint pros lados nem pra tras porque o pack nao tem. O
    /// limite disso esta escrito no fim do metodo Montar().
    /// </summary>
    private static readonly string[] Clipes =
    {
        "Idle",
        "WalkForward", "RunForward", "SprintForward",
        "WalkBack",    "RunBack",
        "WalkLeft",    "RunLeft",
        "WalkRight",   "RunRight",
    };

    [MenuItem("Vermins/Player/Montar Blend Tree 2D")]
    public static void Montar()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (controller == null)
        {
            Debug.LogError($"[BlendTree2D] Nao achei o controller em {ControllerPath}.");
            return;
        }

        GarantirParametro(controller, ParamX);
        GarantirParametro(controller, ParamZ);
        GarantirParametro(controller, ParamSpeed);

        BlendTree arvore = AcharArvore(controller);

        if (arvore == null)
        {
            Debug.LogError($"[BlendTree2D] O estado '{NomeDoEstado}' nao tem " +
                           "blend tree dentro. Nao vou criar um do zero pra " +
                           "nao apagar o que ja estiver la.");
            return;
        }

        Dictionary<string, AnimationClip> porNome = IndexarClipes();

        var filhos = new List<ChildMotion>();
        var faltando = new List<string>();

        foreach (string nome in Clipes)
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
                cycleOffset = 0f,
                mirror = false,
                directBlendParameter = ParamX,
            });
        }

        if (faltando.Count > 0)
        {
            Debug.LogError("[BlendTree2D] Faltou clipe: " +
                           string.Join(", ", faltando) +
                           ". Rode 'Vermins/Player/Configurar FBX do Mixamo' antes.");
            return;
        }

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
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[BlendTree2D] Pronto: {filhos.Count} clipes em Freeform Directional ({ParamX}/{ParamZ}).");

        foreach (ChildMotion c in arvore.children)
        {
            sb.AppendLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "  {0,-14} x={1,6:F2}  z={2,6:F2}  |v|={3:F2}",
                c.motion.name, c.position.x, c.position.y, c.position.magnitude));
        }

        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Onde cada clipe fica na roseta: a direcao vem do NOME dele, o
    /// tamanho vem da velocidade de raiz medida no proprio clipe.
    ///
    /// Separado assim de proposito. O root motion destes clipes esta
    /// girado (por isso o menu do Mixamo endireita cada um pelo corpo),
    /// entao a DIRECAO que ele indica nao presta. O TAMANHO presta:
    /// o erro e uma rotacao, e rotacao nao muda modulo. Conferi medindo a
    /// passada do pe, que e independente do root motion - nos clipes onde
    /// os dois concordam em direcao, o WalkLeft deu 1,24 contra 1,25 e o
    /// RunLeft 3,25 contra 3,27. Um por cento.
    ///
    /// So o Idle e forcado em (0,0): o Freeform Directional precisa de
    /// alguem exatamente na origem pra ter o que tocar com o player
    /// parado.
    /// </summary>
    private static Vector2 PosicaoDoClipe(string nome, AnimationClip clipe)
    {
        if (nome == "Idle")
            return Vector2.zero;

        Vector3 v = clipe.averageSpeed;
        float velocidade = new Vector2(v.x, v.z).magnitude;

        return DirecaoDoNome(nome) * velocidade;
    }

    /// <summary>
    /// "RunLeft" vira (-1, 0). Se um dia entrar clipe diagonal, e aqui
    /// que ele tem que ser ensinado.
    /// </summary>
    private static Vector2 DirecaoDoNome(string nome)
    {
        string m = nome.ToLowerInvariant();

        if (m.Contains("forward")) return new Vector2(0f, 1f);
        if (m.Contains("back")) return new Vector2(0f, -1f);
        if (m.Contains("left")) return new Vector2(-1f, 0f);
        if (m.Contains("right")) return new Vector2(1f, 0f);

        return Vector2.zero;
    }

    private static BlendTree AcharArvore(AnimatorController controller)
    {
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            foreach (ChildAnimatorState filho in layer.stateMachine.states)
            {
                if (filho.state.name != NomeDoEstado)
                    continue;

                return filho.state.motion as BlendTree;
            }
        }

        return null;
    }

    private static void GarantirParametro(AnimatorController controller, string nome)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name == nome)
                return;
        }

        controller.AddParameter(nome, AnimatorControllerParameterType.Float);
    }

    private static Dictionary<string, AnimationClip> IndexarClipes()
    {
        var mapa = new Dictionary<string, AnimationClip>();

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { PastaClipes }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                // O Unity guarda um clipe escondido "__preview__Alguma" junto
                // do de verdade. Se eu pegar ele por engano o blend fica vazio.
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                    mapa[c.name] = c;
            }
        }

        return mapa;
    }
}
