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

        Debug.Log($"[Mixamo] Pronto: modelo humanoide + {convertidas} animacoes ligadas no avatar '{avatar.name}'.");
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
            clipes[i].heightFromFeet = true;

            // So os ciclos assam a rotacao na pose, e ai o personagem
            // encara sempre o +Z do objeto. Nas de uma vez so isso seria
            // errado: a de virar 90 graus giraria e voltaria de tranco.
            clipes[i].lockRootRotation = ciclo;
            clipes[i].keepOriginalOrientation = false;
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
}
