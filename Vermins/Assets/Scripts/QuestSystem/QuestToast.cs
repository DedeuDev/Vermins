using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Arquivo do Ian. Aviso no alto da tela quando a quest anda: missao
/// nova, objetivo concluido, hora de voltar ao Capitao e missao
/// concluida.
///
/// Antes a unica pista era o [ ] da QuestUI virar [X], no canto, e no
/// meio da luta ninguem via. O aviso aparece com fade, fica 2,5 s e
/// some.
///
/// Escuta o mesmo OnQuestUpdated da QuestUI. O evento nao diz o que
/// mudou, entao guardo o estado de cada quest e de cada objetivo e
/// comparo. So aviso o que andou pra frente: o reset de debug so
/// atualiza a foto.
///
/// Fica no Canvas da HUB, que a QuestUI leva pras dungeons.
/// </summary>
public class QuestToast : MonoBehaviour
{
    [Tooltip("CanvasGroup do aviso. O fade mexe no alpha dele.")]
    [SerializeField] private CanvasGroup group;

    [SerializeField] private TMP_Text messageText;

    [Tooltip("Tempo com o aviso inteiro na tela, sem contar o fade.")]
    [SerializeField, Min(0.1f)] private float holdSeconds = 2.5f;

    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.35f;

    private static readonly QuestId[] Quests =
        (QuestId[])Enum.GetValues(typeof(QuestId));

    private static readonly int ObjectiveCount =
        Enum.GetValues(typeof(QuestObjectiveId)).Length;

    private const string HeaderColor = "<color=#FFD24D>";

    private QuestState[] lastStates;
    private bool[] lastObjectives;
    private bool subscribed;
    private bool showing;
    private float shownAt;

    private void Awake()
    {
        lastStates = new QuestState[Quests.Length];
        lastObjectives = new bool[ObjectiveCount];

        if (group != null)
            group.alpha = 0f;
    }

    private void Update()
    {
        // A QuestUI faz igual: o QuestManager pode nascer depois.
        if (!subscribed)
            TrySubscribe();

        if (!showing || group == null)
            return;

        // Tempo sem escala: o aviso nao congela no menu de pausa.
        float t = Time.unscaledTime - shownAt;
        float alpha;

        if (t < fadeSeconds)
            alpha = t / fadeSeconds;
        else if (t < fadeSeconds + holdSeconds)
            alpha = 1f;
        else
            alpha = 1f - (t - fadeSeconds - holdSeconds) / fadeSeconds;

        if (alpha <= 0f)
        {
            alpha = 0f;
            showing = false;
        }

        group.alpha = alpha;
    }

    private void TrySubscribe()
    {
        if (QuestManager.Instance == null)
            return;

        QuestManager.Instance.OnQuestUpdated += HandleQuestUpdated;
        subscribed = true;

        // A foto inicial nao vira aviso: e o que ja estava feito.
        TakeSnapshot();
    }

    private void HandleQuestUpdated()
    {
        string message = FindChange();

        TakeSnapshot();

        if (message != null)
            Show(message);
    }

    /// <summary>
    /// Procura o que andou desde a ultima foto. Um evento costuma
    /// trazer uma mudanca so; se vier mais de uma, fico com a mais
    /// importante pro jogador.
    /// </summary>
    private string FindChange()
    {
        QuestManager manager = QuestManager.Instance;
        string started = null;
        string objective = null;

        for (int i = 0; i < Quests.Length; i++)
        {
            QuestProgress quest = manager.FindQuest(Quests[i]);

            if (quest == null)
                continue;

            QuestState before = lastStates[i];

            if (quest.State == QuestState.Completed && before != QuestState.Completed)
                return Format("Missão concluída", quest.Title);

            if (quest.State == QuestState.ReadyToTurnIn && before == QuestState.Active)
                return Format("Missão pronta", "Volte ao Capitão da Guarda na cidade");

            if (quest.State == QuestState.Active && before == QuestState.NotStarted)
                started = Format("Nova missão", quest.Title);

            foreach (QuestObjectiveProgress progress in quest.Objectives)
            {
                if (progress == null || !progress.Completed)
                    continue;

                if (!lastObjectives[(int)progress.ObjectiveId])
                    objective = Format("Objetivo concluído", progress.Description);
            }
        }

        return objective ?? started;
    }

    private void TakeSnapshot()
    {
        QuestManager manager = QuestManager.Instance;

        Array.Clear(lastObjectives, 0, lastObjectives.Length);

        for (int i = 0; i < Quests.Length; i++)
        {
            QuestProgress quest = manager.FindQuest(Quests[i]);

            if (quest == null)
            {
                lastStates[i] = QuestState.NotStarted;
                continue;
            }

            lastStates[i] = quest.State;

            foreach (QuestObjectiveProgress progress in quest.Objectives)
            {
                if (progress != null && progress.Completed)
                    lastObjectives[(int)progress.ObjectiveId] = true;
            }
        }
    }

    private static string Format(string header, string body)
    {
        return $"{HeaderColor}{header}</color>\n{body}";
    }

    private void Show(string message)
    {
        if (messageText == null || group == null)
            return;

        messageText.text = message;
        shownAt = Time.unscaledTime;
        showing = true;
        group.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (subscribed && QuestManager.Instance != null)
            QuestManager.Instance.OnQuestUpdated -= HandleQuestUpdated;

        subscribed = false;
    }
}
