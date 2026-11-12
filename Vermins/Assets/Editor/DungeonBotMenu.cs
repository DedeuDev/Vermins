using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu Vermins > Bot: liga o DungeonBot com as opcoes que o F3 nao tem.
///
/// Em cima, o que rodar (uma dungeon, as duas, a questline). Embaixo, tres
/// opcoes de marcar que valem para qualquer um deles: 3x de velocidade,
/// sem morrer e direto ao objetivo. Fiz de marcar porque um item por
/// combinacao ja dava 12 linhas de menu. Ficam no EditorPrefs.
///
/// Fora do Play o item entra em Play sozinho e liga o bot depois. O pedido
/// fica no SessionState porque entrar em Play recarrega o dominio e apaga
/// qualquer campo estatico.
/// </summary>
public static class DungeonBotMenu
{
    private const string Sewers = "DungeonComPlayer";
    private const string Depths = "DungeonComPlayer2";
    private const string QuestlineKey = "@questline";
    private const string PendingKey = "Vermins.DungeonBotMenu.Pending";

    private const string FastPref = "Vermins.DungeonBotMenu.Fast";
    private const string SafePref = "Vermins.DungeonBotMenu.Safe";
    private const string DirectPref = "Vermins.DungeonBotMenu.Direct";

    private const string FastItem = "Vermins/Bot/Opcao: 3x de velocidade";
    private const string SafeItem = "Vermins/Bot/Opcao: sem morrer";
    private const string DirectItem = "Vermins/Bot/Opcao: direto ao objetivo (boss, quest e bau)";

    // ==================================================
    // O QUE RODAR
    // ==================================================

    [MenuItem("Vermins/Bot/Esgotos: 1 rodada", priority = 0)]
    private static void SewersOnce() { Request(1, Sewers); }

    [MenuItem("Vermins/Bot/Profundezas: 1 rodada", priority = 1)]
    private static void DepthsOnce() { Request(1, Depths); }

    [MenuItem("Vermins/Bot/As duas: 6 rodadas", priority = 2)]
    private static void BothMany() { Request(6, Sewers + "," + Depths); }

    // A questline comeca na HUB, do zero: Capitao, dungeon, boss, entrega.
    [MenuItem("Vermins/Bot/Questline inteira", priority = 3)]
    private static void Questline() { Request(1, QuestlineKey); }

    // ==================================================
    // OPCOES
    // ==================================================

    [MenuItem(FastItem, priority = 20)]
    private static void ToggleFast() { Toggle(FastPref); }

    [MenuItem(FastItem, true)]
    private static bool CheckFast() { return Check(FastItem, FastPref); }

    // Sem morrer: a vida enche quando cai de 30%. Serve para ver a dungeon
    // inteira mesmo quando o boss ganha; o relatorio conta quantas vezes.
    [MenuItem(SafeItem, priority = 21)]
    private static void ToggleSafe() { Toggle(SafePref); }

    [MenuItem(SafeItem, true)]
    private static bool CheckSafe() { return Check(SafeItem, SafePref); }

    // Direto: nao visita as salas. Vai no bau, no guarda da quest, no boss
    // e na escada, e so mata quem estiver no caminho.
    [MenuItem(DirectItem, priority = 22)]
    private static void ToggleDirect() { Toggle(DirectPref); }

    [MenuItem(DirectItem, true)]
    private static bool CheckDirect() { return Check(DirectItem, DirectPref); }

    private static void Toggle(string pref)
    {
        EditorPrefs.SetBool(pref, !EditorPrefs.GetBool(pref, false));
    }

    // O validador roda toda vez que o menu abre: e onde o visto e posto.
    private static bool Check(string item, string pref)
    {
        Menu.SetChecked(item, EditorPrefs.GetBool(pref, false));
        return true;
    }

    // ==================================================
    // PARAR E RELATORIOS
    // ==================================================

    [MenuItem("Vermins/Bot/Parar", priority = 40)]
    private static void StopBot() { DungeonBot.Stop(); }

    [MenuItem("Vermins/Bot/Parar", true)]
    private static bool CanStop() { return Application.isPlaying && DungeonBot.IsRunning; }

    [MenuItem("Vermins/Bot/Abrir pasta dos relatorios", priority = 60)]
    private static void OpenReports()
    {
        string folder = Path.Combine(Application.persistentDataPath, "DungeonBot");
        Directory.CreateDirectory(folder);
        EditorUtility.RevealInFinder(folder);
    }

    // ==================================================
    // PEDIDO
    // ==================================================

    private static void Request(int runs, string scenes)
    {
        string order = runs + "|" +
            (EditorPrefs.GetBool(FastPref, false) ? "3" : "1") + "|" +
            scenes + "|" +
            (EditorPrefs.GetBool(SafePref, false) ? "1" : "0") + "|" +
            (EditorPrefs.GetBool(DirectPref, false) ? "1" : "0");

        if (Application.isPlaying)
        {
            Launch(order);
            return;
        }

        SessionState.SetString(PendingKey, order);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.playModeStateChanged += HandlePlayMode;
    }

    private static void HandlePlayMode(PlayModeStateChange change)
    {
        // Se eu sair do Play antes de o bot ligar, o pedido nao pode ficar
        // guardado para o proximo Play que eu der a mao.
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(PendingKey);
            return;
        }

        if (change != PlayModeStateChange.EnteredPlayMode)
            return;

        string order = SessionState.GetString(PendingKey, "");

        if (order.Length == 0)
            return;

        SessionState.EraseString(PendingKey);

        // Um frame depois: o DungeonBot nasce no AfterSceneLoad.
        EditorApplication.delayCall += () => Launch(order);
    }

    private static void Launch(string order)
    {
        if (!Application.isPlaying)
            return;

        string[] parts = order.Split('|');
        int runs = int.Parse(parts[0]);
        float timeScale = int.Parse(parts[1]);
        bool keepAlive = parts[3] == "1";
        bool direct = parts[4] == "1";

        Debug.Log(parts[2] == QuestlineKey
            ? DungeonBot.RunQuestline(timeScale, keepAlive, direct)
            : DungeonBot.Run(runs, timeScale, parts[2], keepAlive, direct));
    }
}
