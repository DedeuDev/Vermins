using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Arquivo do Ian. Completa o objetivo de quest quando o boss da
/// dungeon morre.
///
/// Testei em 03/11 na DungeonComPlayer: o BossPlaceholder chegava a 0
/// de vida, ficava de pe e o DefeatBoss1 nao completava, entao a quest 1
/// nunca fechava e as quests 2 e 3 eram inalcancaveis. O BossStats dele
/// nao tem dadosBoss (sem isso nunca chega na derrota) e o
/// onBossDerrotado esta vazio.
///
/// As duas dungeons usam o mesmo gerador e o mesmo EndRoom, entao o
/// boss e sempre o mesmo prefab. Por isso o objetivo sai de uma tabela
/// por cena: sem ela, matar o boss da dungeon 1 durante a quest 2
/// contaria como o lider da quest 2.
///
/// Escuto o Health.OnDied, que ja dispara na capsula, e tambem o
/// OnBossMorto do BossStats, pra quando os bosses do Leonardo entrarem
/// com dados. So completa uma vez.
///
/// O interrogatorio da quest 2 completa junto com a morte e mostra uma
/// fala na DialogueUI. O texto e provisorio meu; o Eduardo troca.
/// </summary>
[RequireComponent(typeof(Health))]
public class BossQuestObjective : MonoBehaviour
{
    [Serializable]
    private struct SceneObjectives
    {
        public string sceneName;
        public QuestId quest;
        public QuestObjectiveId[] objectives;

        [Tooltip("Vazio = sem fala.")]
        public string speaker;

        [TextArea(2, 5)]
        public string line;
    }

    [SerializeField]
    private SceneObjectives[] scenes =
    {
        new SceneObjectives
        {
            sceneName = "DungeonComPlayer",
            quest = QuestId.SewerInvestigation,
            objectives = new[] { QuestObjectiveId.DefeatBoss1 }
        },
        new SceneObjectives
        {
            sceneName = "DungeonComPlayer2",
            quest = QuestId.DeeperIntoSewers,
            objectives = new[]
            {
                QuestObjectiveId.DefeatBoss2,
                QuestObjectiveId.InterrogateBoss2
            },
            speaker = "Líder dos Ratos",
            line = "Chega... chega! Não fomos nós que juntamos os clãs. " +
                   "Foi o Sacerdote. Ele promete os túneis... e a cidade " +
                   "lá em cima. Se quer ele, procure o templo debaixo dos esgotos."
        }
    };

    [Tooltip("Some com o boss depois de completar. A capsula ficava de pe com 0 de vida.")]
    [SerializeField] private bool destroyOnDefeat = true;

    private Health health;
    private BossStats stats;
    private bool done;

    /// <summary>Se o objetivo ja foi completado por esta morte. O teste le daqui.</summary>
    public bool Completed { get; private set; }

    private void Awake()
    {
        health = GetComponent<Health>();
        stats = GetComponent<BossStats>();
    }

    private void OnEnable()
    {
        health.OnDied += HandleHealthDied;

        if (stats != null)
            stats.OnBossMorto += HandleDefeat;
    }

    private void OnDisable()
    {
        health.OnDied -= HandleHealthDied;

        if (stats != null)
            stats.OnBossMorto -= HandleDefeat;
    }

    private void HandleHealthDied(Health _)
    {
        HandleDefeat();
    }

    private void HandleDefeat()
    {
        if (done)
            return;

        done = true;

        CompleteForThisScene();

        if (destroyOnDefeat)
            Destroy(gameObject);
    }

    private void CompleteForThisScene()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogWarning("BossQuestObjective: QuestManager nao encontrado.", this);
            return;
        }

        string scene = SceneManager.GetActiveScene().name;

        foreach (SceneObjectives entry in scenes)
        {
            if (entry.sceneName != scene)
                continue;

            // So conta se a quest da cena estiver ativa: voltar a uma
            // dungeon velha e matar o boss de novo nao faz nada.
            bool any = false;

            foreach (QuestObjectiveId objective in entry.objectives)
                any |= QuestManager.Instance.CompleteObjective(entry.quest, objective);

            Completed = any;

            if (any && !string.IsNullOrEmpty(entry.line) && DialogueUI.Instance != null)
                DialogueUI.Instance.ShowDialogue(entry.speaker, entry.line);

            return;
        }
    }
}
