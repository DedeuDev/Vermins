using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// O que o DungeonBot anotou numa rodada: uma dungeon, do nascimento ate
/// a escada, a morte ou a desistencia.
///
/// Fiz separado do bot para o bot so decidir e este so contar. Grava um
/// .txt por rodada e uma linha num resumo.csv, na pasta DungeonBot do
/// persistentDataPath, que existe no editor e na build.
/// </summary>
public class DungeonBotReport
{
    public string Scene;
    public int Seed;
    public int RunNumber;
    public string Result = "em andamento";

    // Rodada direta ao objetivo: salas e inimigos ficam para tras de
    // proposito, entao esses dois numeros nao sao comparaveis com os da
    // rodada que limpa tudo.
    public bool Direct;

    // Tempo de jogo, que e o que o jogador sentiria. O real muda com o
    // timeScale da rodada.
    public float GameSeconds;
    public float BossSeconds;

    public int ModulesTotal;
    public int ModulesVisited;
    public int ModulesUnreachable;
    public int EnemiesTotal;
    public int EnemiesKilled;
    public int ChestsTotal;
    public int ChestsOpened;
    public int PickupsCollected;
    public int PotionsDrunk;
    public int StuckCount;

    // Quantas vezes o modo sem morrer encheu a vida. Zero quer dizer que
    // a rodada valeu como um jogador de verdade.
    public int Rescues;
    public int ConsoleErrors;

    public float DamageTaken;
    public float DamageDealt;
    public float HealthAtBoss = -1f;
    public float HealthAtEnd;
    public float MaxHealthAtEnd;
    public bool BossKilled;
    public bool Died;

    // O que merece ser olhado: preso, modulo sem caminho, clique que nao
    // acha o bau, erro no console.
    public readonly List<string> Findings = new List<string>();

    private const int MaxFindings = 60;

    public void AddFinding(string text)
    {
        if (Findings.Count < MaxFindings)
            Findings.Add(text);

        Debug.LogWarning("DungeonBot: " + text);
    }

    /// <summary>Uma linha, para o console e para quem roda varias seguidas.</summary>
    public string Summary()
    {
        return string.Format(CultureInfo.InvariantCulture,
            "rodada {0}{17} | {1} seed {2} | {3} | {4:0} s | salas {5}/{6} | " +
            "inimigos {7}/{8} | baus {9}/{10} | dano levado {11:0} | " +
            "vida no boss {12:0} | preso {13}x | erros {14} | achados {15} | salvo {16}x",
            RunNumber, Scene, Seed, Result, GameSeconds,
            ModulesVisited, ModulesTotal, EnemiesKilled, EnemiesTotal,
            ChestsOpened, ChestsTotal, DamageTaken, HealthAtBoss,
            StuckCount, ConsoleErrors, Findings.Count, Rescues,
            Direct ? " direta" : "");
    }

    /// <summary>Grava o .txt da rodada e a linha no resumo. Devolve o caminho.</summary>
    public string Write()
    {
        string folder = Path.Combine(Application.persistentDataPath, "DungeonBot");
        Directory.CreateDirectory(folder);

        string stamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss",
            CultureInfo.InvariantCulture);
        string path = Path.Combine(folder,
            "bot-" + stamp + "-" + Scene + "-seed" + Seed + ".txt");

        var text = new StringBuilder(1024);
        CultureInfo inv = CultureInfo.InvariantCulture;
        text.AppendLine(Summary());
        text.AppendLine();
        text.AppendFormat(inv, "cena: {0}\nseed: {1}\nresultado: {2}\n", Scene, Seed, Result);
        text.AppendLine(Direct ? "modo: direto ao objetivo (bau, quest, boss)" : "modo: limpa a dungeon inteira");
        text.AppendFormat(inv, "tempo de jogo: {0:0.0} s (boss: {1:0.0} s)\n", GameSeconds, BossSeconds);
        text.AppendFormat(inv, "salas visitadas: {0} de {1} (sem caminho: {2})\n",
            ModulesVisited, ModulesTotal, ModulesUnreachable);
        text.AppendFormat(inv, "inimigos mortos: {0} de {1}\n", EnemiesKilled, EnemiesTotal);
        text.AppendFormat(inv, "baus abertos: {0} de {1}\n", ChestsOpened, ChestsTotal);
        text.AppendFormat(inv, "itens pegos: {0}\npocoes bebidas: {1}\n", PickupsCollected, PotionsDrunk);
        text.AppendFormat(inv, "dano causado: {0:0}\ndano levado: {1:0}\n", DamageDealt, DamageTaken);
        text.AppendFormat(inv, "vida ao chegar no boss: {0:0}\nvida no fim: {1:0} de {2:0}\n",
            HealthAtBoss, HealthAtEnd, MaxHealthAtEnd);
        text.AppendFormat(inv, "boss morto: {0}\nmorreu: {1}\n", BossKilled, Died);
        text.AppendFormat(inv, "vezes preso: {0}\nerros no console: {1}\n", StuckCount, ConsoleErrors);
        text.AppendFormat(inv, "salvo pelo modo sem morrer: {0} vez(es)", Rescues);
        text.AppendLine();
        text.AppendLine();
        text.AppendLine("achados:");

        if (Findings.Count == 0)
            text.AppendLine("  nenhum");

        for (int i = 0; i < Findings.Count; i++)
            text.Append("  - ").AppendLine(Findings[i]);

        File.WriteAllText(path, text.ToString());

        string csv = Path.Combine(folder, "resumo.csv");

        if (!File.Exists(csv))
        {
            File.WriteAllText(csv,
                "quando;cena;seed;resultado;segundos;salas;salasTotal;inimigos;" +
                "inimigosTotal;baus;bausTotal;danoLevado;vidaNoBoss;preso;erros;achados;salvo\n");
        }

        File.AppendAllText(csv, string.Format(inv,
            "{0};{1};{2};{3};{4:0};{5};{6};{7};{8};{9};{10};{11:0};{12:0};{13};{14};{15};{16}\n",
            stamp, Scene, Seed, Result, GameSeconds, ModulesVisited, ModulesTotal,
            EnemiesKilled, EnemiesTotal, ChestsOpened, ChestsTotal, DamageTaken,
            HealthAtBoss, StuckCount, ConsoleErrors, Findings.Count, Rescues));

        return path;
    }

    /// <summary>
    /// O arquivo da questline: o resumo, cada passo da historia com o tempo
    /// de jogo em que aconteceu, e o resumo de cada dungeon jogada no meio.
    /// </summary>
    public static string WriteQuestline(string summary, List<string> steps, List<string> runs)
    {
        string folder = Path.Combine(Application.persistentDataPath, "DungeonBot");
        Directory.CreateDirectory(folder);

        string stamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss",
            CultureInfo.InvariantCulture);
        string path = Path.Combine(folder, "questline-" + stamp + ".txt");

        var text = new StringBuilder(1024);
        text.AppendLine(summary);
        text.AppendLine();
        text.AppendLine("passos:");

        for (int i = 0; i < steps.Count; i++)
            text.Append("  ").AppendLine(steps[i]);

        text.AppendLine();
        text.AppendLine("dungeons jogadas:");

        if (runs.Count == 0)
            text.AppendLine("  nenhuma");

        for (int i = 0; i < runs.Count; i++)
            text.Append("  - ").AppendLine(runs[i]);

        File.WriteAllText(path, text.ToString());
        return path;
    }
}
