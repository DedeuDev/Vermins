using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Refaz o NavMesh da HUBCity no mesmo asset, ligando por um instante as
/// malhas de ajuda que so existem pro NavMesh.
///
/// Fiz como menu porque o botao Bake do NavMeshSurface erra em dois
/// pontos nesta cena:
/// - O surface coleta RenderMeshes e ignora renderer desligado. As duas
///   rampas das escadas e o enchimento do Andar3 ficam com o renderer
///   desligado pra nao aparecer no jogo. Sem eles no bake, o Player
///   engancha nas escadas e, no Andar3, vai parar dentro da plataforma
///   (o oco dela tem so 2,5 m e o Height Mesh pega o chao de dentro).
/// - O Bake apaga o NavMesh-Hub.asset e cria outro com o nome do objeto
///   ("NavMesh-Hub jogo provisoria"). Aqui eu reescrevo o mesmo asset.
///
/// Nao salvo a cena: o surface continua apontando pro mesmo asset e os
/// renderers voltam como estavam. Quem salva e quem chamou.
///
/// Menu: Vermins > Cenario > Refazer NavMesh da HUB
/// </summary>
public static class HubNavMeshRebuild
{
    private const string SceneName = "HUBCity";

    // Malhas que o NavMesh precisa ver e o jogador nao.
    private static readonly string[] HelperMeshes =
    {
        "RampaNavMeshEscada",
        "RampaNavMeshEscada2",
        "Enchimento Andar3",
    };

    private static AsyncOperation pending;
    private static NavMeshData pendingData;
    private static double startTime;

    [MenuItem("Vermins/Cenario/Refazer NavMesh da HUB")]
    private static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[NavMesh da HUB] Saia do Play antes.");
            return;
        }

        if (pending != null)
        {
            Debug.LogWarning("[NavMesh da HUB] Ja tem um bake rodando.");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            Debug.LogWarning($"[NavMesh da HUB] Abra a {SceneName} antes.");
            return;
        }

        var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        if (surfaces.Length != 1)
        {
            Debug.LogError($"[NavMesh da HUB] Esperava 1 NavMeshSurface e achei {surfaces.Length}.");
            return;
        }

        var surface = surfaces[0];
        var data = surface.navMeshData;
        if (data == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(data)))
        {
            // Se o NavMeshData nao for asset, ele vai parar dentro da cena e
            // a Unity passa a salvar a HUBCity em binario. Ja aconteceu.
            Debug.LogError("[NavMesh da HUB] O surface nao aponta pra um asset de NavMesh. Ligue o NavMesh-Hub.asset no campo NavMesh Data e rode de novo.");
            return;
        }

        var helpers = FindHelpers(surface.transform);
        if (helpers.Count != HelperMeshes.Length)
        {
            Debug.LogError($"[NavMesh da HUB] Achei {helpers.Count} de {HelperMeshes.Length} malhas de ajuda ({string.Join(", ", HelperMeshes)}). Nao fiz o bake.");
            return;
        }

        // O UpdateNavMesh coleta a geometria na hora da chamada, entao posso
        // devolver os renderers logo depois, mesmo com o bake ainda rodando.
        var wasEnabled = new bool[helpers.Count];
        for (int i = 0; i < helpers.Count; i++)
        {
            wasEnabled[i] = helpers[i].enabled;
            helpers[i].enabled = true;
        }

        try
        {
            pending = surface.UpdateNavMesh(data);
        }
        finally
        {
            for (int i = 0; i < helpers.Count; i++)
                helpers[i].enabled = wasEnabled[i];
        }

        pendingData = data;
        startTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += WaitForBake;
        Debug.Log("[NavMesh da HUB] Bake comecou.");
    }

    private static void WaitForBake()
    {
        if (pending != null && !pending.isDone)
            return;

        EditorApplication.update -= WaitForBake;
        pending = null;

        if (pendingData == null)
            return;

        EditorUtility.SetDirty(pendingData);
        AssetDatabase.SaveAssetIfDirty(pendingData);

        double seconds = EditorApplication.timeSinceStartup - startTime;
        Debug.Log($"[NavMesh da HUB] Pronto em {seconds:F1} s, salvo em {AssetDatabase.GetAssetPath(pendingData)}. Agora pode fazer o Generate Lighting.");
        pendingData = null;
    }

    private static List<MeshRenderer> FindHelpers(Transform root)
    {
        var found = new List<MeshRenderer>();
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (System.Array.IndexOf(HelperMeshes, renderer.name) >= 0)
                found.Add(renderer);
        }
        return found;
    }
}
