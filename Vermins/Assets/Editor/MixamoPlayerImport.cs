using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configura os FBX do Mixamo pro rig do player.
///
/// O Mixamo entrega tudo como Generic, com o clipe chamado "mixamo.com"
/// e sem loop. Do jeito que vem, o Animator nao consegue misturar um
/// clipe com o outro e o personagem trava no fim de cada animacao.
///
/// Fiz como menu em vez de arrumar na mao porque a gente vai baixar
/// mais animacao do Mixamo ate o fim do projeto. Quem jogar um FBX
/// novo na pasta so precisa rodar isso de novo.
///
/// Menu: Vermins > Player > Configurar FBX do Mixamo
/// </summary>
public static class MixamoPlayerImport
{
    private const string PastaPlayer = "Assets/Placeholders/Player";
    private const string ModeloPath = PastaPlayer + "/PlayerTest.fbx";

    /// <summary>
    /// Animacao que e um ciclo continuo precisa de loop. Se ficar sem,
    /// o personagem da um passo e congela.
    /// </summary>
    private static readonly string[] PalavrasDeCiclo =
    {
        "idle", "walk", "run", "sprint"
    };

    /// <summary>
    /// Estas mandam mais que a lista de cima. "JumpRunning" tem "run" no
    /// nome mas e um pulo, toca uma vez e acaba - se entrasse em loop o
    /// personagem pularia pra sempre.
    /// </summary>
    private static readonly string[] PalavrasDeUmaVezSo =
    {
        "jump", "land", "turn"
    };

    [MenuItem("Vermins/Player/Configurar FBX do Mixamo")]
    public static void Configurar()
    {
        ModelImporter modelo = AssetImporter.GetAtPath(ModeloPath) as ModelImporter;

        if (modelo == null)
        {
            Debug.LogError($"[Mixamo] Nao achei o modelo em {ModeloPath}.");
            return;
        }

        ConfigurarModelo(modelo);

        Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModeloPath);

        if (avatar == null || !avatar.isHuman)
        {
            Debug.LogError(
                "[Mixamo] O avatar humanoide do PlayerTest nao foi gerado. " +
                "Olhe o console: normalmente e osso faltando no rig.");
            return;
        }

        int convertidas = 0;

        foreach (string path in CaminhosDasAnimacoes())
        {
            if (ConfigurarAnimacao(path, avatar))
                convertidas++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[Mixamo] Pronto: modelo humanoide + {convertidas} animacoes " +
                  $"ligadas no avatar '{avatar.name}'.");
    }

    private static void ConfigurarModelo(ModelImporter modelo)
    {
        modelo.animationType = ModelImporterAnimationType.Human;
        modelo.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        // O FBX do corpo nao carrega animacao nenhuma, so a pose T.
        modelo.importAnimation = false;

        // Sem isso o Unity gera um Animator no prefab do FBX e a gente
        // acaba com dois Animator no mesmo personagem.
        modelo.importConstraints = false;

        modelo.SaveAndReimport();
    }

    private static bool ConfigurarAnimacao(string path, Avatar avatar)
    {
        ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;

        if (imp == null)
            return false;

        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        imp.sourceAvatar = avatar;
        imp.importAnimation = true;

        // Cada FBX de animacao do Mixamo vem com uma copia da malha e do
        // material junto. O corpo que vale e o do PlayerTest, entao aqui
        // eu so quero a animacao - o resto vira material duplicado no
        // projeto e confunde na hora de escolher no Inspector.
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.importCameras = false;
        imp.importLights = false;

        ModelImporterClipAnimation[] clipes = imp.defaultClipAnimations;

        if (clipes.Length == 0)
            return false;

        string nome = NomeDoClipe(path);
        bool ciclo = EhCiclo(nome);

        for (int i = 0; i < clipes.Length; i++)
        {
            // Um FBX do Mixamo so tem um take, entao o primeiro fica com
            // o nome limpo e qualquer extra ganha sufixo.
            clipes[i].name = i == 0 ? nome : $"{nome}_{i}";

            clipes[i].loopTime = ciclo;

            // loopPose costura a ultima pose na primeira. E o que tira o
            // tranco que aparece a cada volta do ciclo de caminhada.
            clipes[i].loopPose = ciclo;

            // Quem anda aqui e o NavMeshAgent, nunca a animacao. Entao
            // o deslocamento do clipe tem que sair como root motion pro
            // Animator poder jogar fora (applyRootMotion desligado).
            //
            // "Bake Into Pose" faz o contrario: guarda o deslocamento
            // DENTRO do corpo. Medi e o quadril andava 0,9 m por ciclo
            // de corrida - o personagem ia embora do proprio pe.
            clipes[i].lockRootPositionXZ = false;

            // Y e a excecao: quero o sobe-e-desce do passo no corpo. Se
            // virar root motion tambem, ele some e o personagem desliza
            // rigido igual estatua em carrinho.
            clipes[i].lockRootHeightY = true;
            clipes[i].heightFromFeet = false;

            // So os ciclos assam a rotacao na pose. Nas de uma vez so
            // isso seria errado: a de virar 90 graus giraria e voltaria
            // de tranco.
            clipes[i].lockRootRotation = ciclo;

            // "Original" e nao "Body Orientation". Body Orientation tira
            // a referencia da postura media do corpo, e nesse rig ela sai
            // torta: medi o WalkForward andando 41 graus de lado do lugar
            // pra onde o personagem olhava, e na tela isso vira o
            // personagem deslizando de banda. Com Original a referencia e
            // a que o Mixamo exportou, e o desvio cai pra menos de 3
            // graus na caminhada e na corrida.
            clipes[i].keepOriginalOrientation = true;
        }

        imp.clipAnimations = clipes;
        imp.SaveAndReimport();

        return true;
    }

    private static IEnumerable<string> CaminhosDasAnimacoes()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { PastaPlayer });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (path != ModeloPath)
                yield return path;
        }
    }

    /// <summary>
    /// "Standing Walk Forward.fbx" vira "WalkForward". Nome de arquivo do
    /// Mixamo tem espaco e prefixo, e nome de clipe com espaco atrapalha
    /// na hora de procurar pelo codigo.
    /// </summary>
    private static string NomeDoClipe(string path)
    {
        string nome = System.IO.Path.GetFileNameWithoutExtension(path);

        nome = nome.Replace("Standing ", string.Empty)
                   .Replace("standing ", string.Empty)
                   .Replace(" ", string.Empty);

        if (nome.Length > 0)
            nome = char.ToUpperInvariant(nome[0]) + nome.Substring(1);

        return nome;
    }

    private static bool EhCiclo(string nome)
    {
        string minusculo = nome.ToLowerInvariant();

        foreach (string palavra in PalavrasDeUmaVezSo)
        {
            if (minusculo.Contains(palavra))
                return false;
        }

        foreach (string palavra in PalavrasDeCiclo)
        {
            if (minusculo.Contains(palavra))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Conferidor. Nao muda nada: so mede e imprime.
    ///
    /// Isto existe por causa de um erro meu, e deixo escrito pra ninguem
    /// repetir. O personagem parece estar meio de lado quando anda, e a
    /// tentacao e girar os clipes pra endireitar - eu tentei duas vezes,
    /// mirando no root motion e depois no tronco, e as duas pioraram.
    ///
    /// O motivo esta na coluna da passada aqui embaixo. Sem offset
    /// nenhum, os nove clipes de locomocao varrem o chao EXATAMENTE no
    /// eixo certo: WalkForward 180,3 graus, RunForward 181,6,
    /// SprintForward 179,9, os de tras em -0,3 e -0,1, os da esquerda em
    /// 93,7 e 90,3, os da direita em -90,6 e -88,9. Nove de nove. O
    /// clipe esta certo; girar ele so tira a perna do lugar. Quando eu
    /// alinhei pelo tronco, a passada saiu 30 a 38 graus pra fora e na
    /// tela ele andava de caranguejo.
    ///
    /// O tronco fica mesmo virado (23 graus no sprint, 54 parado), mas
    /// isso e postura de combate, e como o Mixamo fez estes clipes - o
    /// prefixo "Standing" deles quer dizer exatamente isso. Nao e defeito
    /// de import: o corpo do PlayerTest tem a linha dos ombros em 0,0
    /// grau, e dar avatar proprio pro clipe em vez de copiar o do
    /// PlayerTest muda o angulo em 1,5 grau, ou seja, esta no dado.
    ///
    /// Se um dia isso incomodar de verdade, o conserto e clipe novo, nao
    /// numero novo.
    ///
    /// Menu: Vermins > Player > Conferir Clipes
    /// </summary>
    [MenuItem("Vermins/Player/Conferir Clipes")]
    public static void Conferir()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("[Mixamo] Conferencia dos clipes de ciclo.");
        sb.AppendLine("  passada: pra onde o pe varre o chao, visto de dentro do corpo.");
        sb.AppendLine("           frente=180  tras=0  esquerda=90  direita=-90");
        sb.AppendLine("  tronco:  quantos graus os ombros e o quadril estao girados.");
        sb.AppendLine();
        sb.AppendLine("  clipe            passada   erro   tronco   vel");

        using (var lab = new AnaliseDeClipe())
        {
            if (!lab.Pronto)
            {
                Debug.LogError("[Mixamo] Nao consegui montar o modelo pra medir.");
                return;
            }

            foreach (string path in CaminhosDasAnimacoes())
            {
                string nome = NomeDoClipe(path);

                if (!EhCiclo(nome))
                    continue;

                AnimationClip clipe = Clipe(path);

                if (clipe == null)
                    continue;

                AnaliseDeClipe.Medida m = lab.Medir(clipe);

                if (!m.valido || m.amostrasNoChao == 0)
                {
                    sb.AppendLine($"  {nome,-14}   (sem passada legivel)");
                    continue;
                }

                float esperada = PassadaEsperada(nome);
                string erro = float.IsNaN(esperada)
                    ? "   -"
                    : Mathf.DeltaAngle(esperada, m.direcaoDaPassada).ToString("F1", ci);

                sb.AppendLine(string.Format(ci, "  {0,-14} {1,8:F1} {2,6} {3,8:F1} {4,5:F2}",
                    nome, m.direcaoDaPassada, erro, m.anguloDoCorpo, m.velocidade));
            }
        }

        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Pra onde a passada deveria ir, lido do nome. NaN quer dizer que o
    /// clipe nao anda pra lado nenhum (o Idle).
    /// </summary>
    private static float PassadaEsperada(string nome)
    {
        string m = nome.ToLowerInvariant();

        // O pe varre ao contrario de pra onde o personagem vai.
        if (m.Contains("forward")) return 180f;
        if (m.Contains("back")) return 0f;
        if (m.Contains("left")) return 90f;
        if (m.Contains("right")) return -90f;

        return float.NaN;
    }

    private static AnimationClip Clipe(string path)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                return c;
        }

        return null;
    }
}
