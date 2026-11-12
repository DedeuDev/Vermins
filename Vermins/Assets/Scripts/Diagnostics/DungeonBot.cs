using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Joga uma dungeon gerada do comeco ao fim, sozinho, e anota o que deu
/// errado. Fiz para testar o que eu nao consigo jogar a mao toda vez:
/// dungeon sem caminho ate o boss, bau ou escada que nao da para alcancar,
/// lugar onde o Player engancha e erro no console.
///
/// Ele entra no lugar do PlayerController: desliga o controle e da as
/// mesmas ordens que o mouse daria (MoveTo, SetTarget, TryUse, TryDodge,
/// TryDrink). Nao simulo o mouse porque o QueueStateEvent falha de vez em
/// quando, e um teste que falha sozinho nao serve. Bau e escada passam
/// pelo PlayerInteractor com um raio saindo da camera, entao o clique
/// neles e o de verdade.
///
/// Prioridade a cada 0,2 s: pocao, inimigo a vista, item no chao, bau
/// perto, sala nao visitada mais perto pelo NavMesh, boss, escada.
///
/// So existe no editor e em build Development. Liga na tecla F3 dentro de
/// uma dungeon, ou por DungeonBot.Run(rodadas, timeScale, "CenaA,CenaB").
///
/// DungeonBot.RunQuestline() joga a historia inteira: fala com o Capitao
/// na HUB, entra na dungeon da quest, joga, volta e entrega, ate nao
/// sobrar quest ou ate travar. Ver a secao QUESTLINE.
/// Nao mexe em nenhum script do jogo: so le o que esta na cena.
/// </summary>
public class DungeonBot : MonoBehaviour
{
    private enum State { Waiting, Fight, Loot, Chest, Explore, Boss, Exit }

    // De quanto em quanto tempo eu decido. O jogador tambem nao reage
    // todo frame, e assim as buscas na cena nao pesam.
    private const float ThinkInterval = 0.2f;
    private const float ListInterval = 1f;

    // Ate onde eu puxo briga com quem esta a vista. O inimigo enxerga a
    // 5 m; com 10 m eu ataco primeiro, como um jogador faria.
    private const float EngageRadius = 10f;
    private const float AggressorRadius = 20f;

    // No modo direto so brigo com quem esta no caminho. O inimigo enxerga
    // a 5 m; com 6 m eu bato em quem ia me ver e passo longe do resto.
    private const float DirectEngageRadius = 6f;
    private const float AggressorMemory = 3f;
    private const float LootRadius = 8f;
    private const float NearChestRadius = 12f;

    // De longe eu ando ate perto e so entao clico, como o jogador.
    private const float ClickDistance = 6f;
    private const float ApproachStop = 3f;
    private const float ReachDistance = 4f;

    private const float ArriveDistance = 1.5f;
    private const float PotionAt = 0.4f;
    private const float DodgeAt = 0.25f;
    private const float KeepAliveAt = 0.3f;
    private const float WaveRange = 8f;

    // O terremoto do Monge pega 3,5 m. Com 5 m eu rolo mesmo estando na
    // borda, e a esquiva de 4 m me deixa fora do circulo.
    private const float TelegraphRadius = 5f;

    // Parado por mais que isso sem bater em ninguem conta como preso.
    private const float StuckSeconds = 5f;
    private const float StuckMove = 0.5f;
    private const int MaxStuck = 12;
    private const float MaxRunSeconds = 900f;
    private const float GenerationTimeout = 30f;

    private static DungeonBot instance;

    /// <summary>Verdadeiro enquanto ainda ha rodada para jogar.</summary>
    public static bool IsRunning => instance != null && instance.botOn;

    /// <summary>O resumo de cada rodada desde o ultimo Run.</summary>
    public static readonly List<string> Summaries = new List<string>();

    public static string LastReportPath { get; private set; } = "";

    private bool botOn;

    // Sem morrer: quando a vida cai de 30%, encho de novo e conto. Fiz
    // depois de 4 rodadas que morreram no Monge antes da escada: assim a
    // rodada testa a dungeon ate o fim e o relatorio diz quantas vezes o
    // Player teria caido. 30% e mais que o terremoto de 25 tira de uma vez.
    private bool keepAlive;

    // Direto: nao visito as salas. Vou no bau, no guarda da quest, no boss
    // e na escada, e so mato quem estiver no caminho. E a rodada de quem
    // ja conhece a dungeon e quer so o objetivo.
    private bool directMode;
    private bool runActive;
    private int runsLeft;
    private int runsDone;
    private float timeScale = 1f;
    private string[] scenes;
    private Coroutine beginRoutine;

    private DungeonBotReport report;
    private State state;
    private string goalName = "";
    private string hudText = "";

    // Player
    private Transform player;
    private PlayerController controller;
    private PlayerMotor motor;
    private PlayerCombat combat;
    private PlayerInteractor interactor;
    private Health playerHealth;
    private PotionBelt belt;
    private Dodge dodge;
    private CharacterLevel level;
    private Ability closeAbility;
    private Ability farAbility;
    private Ability[] abilities;
    private float closeRange = 2.4f;

    // Cena
    private readonly List<DungeonModule> modules = new List<DungeonModule>();
    private readonly List<Vector3> modulePoints = new List<Vector3>();
    private readonly List<bool> moduleVisited = new List<bool>();
    private readonly List<Health> enemies = new List<Health>();
    private readonly HashSet<Health> knownEnemies = new HashSet<Health>();
    private readonly HashSet<Object> ignored = new HashSet<Object>();
    private TreasureChest[] chests = new TreasureChest[0];
    private Pickup[] pickups = new Pickup[0];
    private BossRoomExit exitRoom;
    private Health bossHealth;
    private BossStats bossStats;
    private Vector3 fleePoint;
    private bool hasFleePoint;
    private int bossModule = -1;
    private int exploreGoal = -1;

    // Andamento
    private float nextThink;
    private float nextList;
    private float nextOrder;
    private float nextPotion;
    private Vector3 lastMovePoint;
    private Vector3 anchor;
    private float anchorTime;
    private bool madeProgress;
    private Health aggressor;
    private float aggressorTime;
    private float bossStart = -1f;
    private float lastHealth;
    private float lastMaxHealth;
    private GUIStyle hudStyle;
    private float exitWaitStart = -1f;
    private bool clickMissReported;
    private Object lootTarget;
    private float lootArrivedAt = -1f;

    private NavMeshPath path;
    private readonly Vector3[] corners = new Vector3[128];

    // Questline
    private bool questMode;
    private bool hubActive;
    private float questSeconds;
    private int questDeaths;
    private readonly int[] questEntries = new int[QuestOrder.Length];
    private readonly QuestState[] questStates = new QuestState[QuestOrder.Length];
    private readonly HashSet<QuestObjectiveId> questObjectivesDone = new HashSet<QuestObjectiveId>();
    private readonly List<string> questSteps = new List<string>();
    private CaptainGuardQuestNPC captain;
    private DungeonEntranceInteractable hubEntrance;
    private DeadGuardQuestInteractable deadGuard;
    private string hubStep = "";
    private float hubStepStart;
    private bool hubDirectDone;
    private float dialogueSince = -1f;

    // ==================================================
    // LIGAR E DESLIGAR
    // ==================================================

    // Sem recarregar o dominio ao entrar no Play, o estatico fica com o
    // objeto morto da vez anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        Summaries.Clear();
        LastReportPath = "";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // Fora do editor e da build Development o bot nem existe.
        if (!Application.isEditor && !Debug.isDebugBuild)
            return;

        var holder = new GameObject("DungeonBot");
        DontDestroyOnLoad(holder);
        instance = holder.AddComponent<DungeonBot>();
    }

    /// <summary>
    /// Joga tantas rodadas. Sem lista de cenas, repete a cena aberta; com
    /// lista ("DungeonComPlayer,DungeonComPlayer2"), alterna entre elas.
    /// </summary>
    public static string Run(int runs = 1, float timeScale = 1f, string sceneList = null,
                             bool keepAlive = false, bool direct = false)
    {
        if (instance == null)
            return "DungeonBot so existe em Play, no editor ou em build Development.";

        return instance.StartBot(runs, timeScale, sceneList, keepAlive, direct);
    }

    public static void Stop()
    {
        if (instance != null)
            instance.StopBot("parado a mao");
    }

    private void Awake()
    {
        path = new NavMeshPath();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Application.logMessageReceived -= HandleLog;
    }

    private string StartBot(int runs, float scale, string sceneList, bool alive, bool direct)
    {
        if (botOn)
            return "DungeonBot ja esta rodando.";

        keepAlive = alive;
        directMode = direct;

        scenes = string.IsNullOrWhiteSpace(sceneList)
            ? new[] { SceneManager.GetActiveScene().name }
            : sceneList.Split(',');

        for (int i = 0; i < scenes.Length; i++)
            scenes[i] = scenes[i].Trim();

        runsLeft = Mathf.Max(1, runs);
        runsDone = 0;
        timeScale = Mathf.Clamp(scale, 0.1f, 8f);
        botOn = true;
        Summaries.Clear();
        string started = "DungeonBot ligado: " + runsLeft + " rodada(s), timeScale " + timeScale +
                         (directMode ? ", direto ao objetivo." : ".");

        if (SceneManager.GetActiveScene().name == scenes[0])
            beginRoutine = StartCoroutine(BeginRun());
        else
            StartCoroutine(LoadNextScene());

        return started;
    }

    private void StopBot(string why)
    {
        if (!botOn)
            return;

        if (runActive)
            EndRun(why);

        if (questMode)
            CloseQuestline(why);

        if (beginRoutine != null)
            StopCoroutine(beginRoutine);

        beginRoutine = null;
        botOn = false;
        runsLeft = 0;
        Time.timeScale = 1f;
        Unbind();
        Debug.Log("DungeonBot: desligado (" + why + "). Rodadas: " + runsDone + ".");
    }

    // ==================================================
    // RODADA
    // ==================================================

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!botOn || mode != LoadSceneMode.Single)
            return;

        // A cena trocou com a rodada aberta: ou foi a escada, ou alguma
        // coisa me tirou da dungeon.
        if (runActive)
            EndRun(state == State.Exit ? "completa" : "cena trocou sozinha");

        Unbind();

        if (questMode)
        {
            // Na questline quem escolhe a proxima cena e o jogo (a tela de
            // dungeons, a escada, a morte). Eu so jogo a cena que abriu.
            beginRoutine = StartCoroutine(scene.name == HubScene ? BeginHub() : BeginRun());
            return;
        }

        if (runsLeft <= 0)
        {
            StopBot("rodadas completas");
            return;
        }

        if (scene.name == NextScene())
            beginRoutine = StartCoroutine(BeginRun());
        else
            StartCoroutine(LoadNextScene());
    }

    private string NextScene()
    {
        return scenes[runsDone % scenes.Length];
    }

    // Um frame de espera: carregar cena de dentro do sceneLoaded da outra
    // atropela quem ainda esta ouvindo o mesmo evento.
    private IEnumerator LoadNextScene()
    {
        yield return null;
        SceneManager.LoadScene(NextScene());
    }

    private IEnumerator BeginRun()
    {
        state = State.Waiting;
        SetHud("esperando a dungeon");

        report = new DungeonBotReport
        {
            Scene = SceneManager.GetActiveScene().name,
            RunNumber = runsDone + 1,
            Direct = directMode
        };

        runsLeft--;
        float limit = Time.realtimeSinceStartup + GenerationTimeout;

        ModularDungeonGenerator generator = FindFirstObjectByType<ModularDungeonGenerator>();

        if (generator == null)
        {
            report.AddFinding("esta cena nao tem ModularDungeonGenerator; o bot so joga dungeon gerada");
            FinishWithoutPlaying("nao e dungeon");
            yield break;
        }

        RuntimeDungeonNavMesh navMesh = FindFirstObjectByType<RuntimeDungeonNavMesh>();

        while (navMesh != null && !navMesh.HasBuiltNavMesh &&
               Time.realtimeSinceStartup < limit)
            yield return null;

        if (navMesh == null || !navMesh.HasBuiltNavMesh)
        {
            report.AddFinding("o NavMesh da dungeon nao ficou pronto em " + GenerationTimeout + " s");
            FinishWithoutPlaying("nao gerou");
            yield break;
        }

        // Inimigos e boss nascem depois do NavMesh, e o BossRoomExit ainda
        // espera ate 15 frames pelo boss. 1 s cobre os dois.
        yield return new WaitForSecondsRealtime(1f);

        GameObject found = GameObject.FindGameObjectWithTag("Player");

        if (found == null)
        {
            report.AddFinding("nenhum objeto com a tag Player na cena");
            FinishWithoutPlaying("sem Player");
            yield break;
        }

        // A seed aceita fica num campo privado do gerador do Eduardo. Leio
        // por reflexao uma vez por rodada para nao mexer no script dele.
        FieldInfo seedField = typeof(ModularDungeonGenerator).GetField("seed",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (seedField != null)
            report.Seed = (int)seedField.GetValue(generator);

        Bind(found);
        beginRoutine = null;
    }

    private void FinishWithoutPlaying(string result)
    {
        beginRoutine = null;
        report.Result = result;
        Summaries.Add(report.Summary());
        LastReportPath = report.Write();
        runsDone++;
        StopBot(result);
    }

    private void Bind(GameObject found)
    {
        player = found.transform;
        controller = found.GetComponent<PlayerController>();
        motor = found.GetComponent<PlayerMotor>();
        combat = found.GetComponent<PlayerCombat>();
        interactor = found.GetComponent<PlayerInteractor>();
        playerHealth = found.GetComponent<Health>();
        belt = found.GetComponent<PotionBelt>();
        dodge = found.GetComponent<Dodge>();
        level = found.GetComponent<CharacterLevel>();
        abilities = found.GetComponents<Ability>();

        if (controller != null)
        {
            closeAbility = controller.RightClickAbility;
            farAbility = controller.Key1Ability;
            // Quem manda agora sou eu. Com os dois ligados, um clique
            // perdido no Game view trocava a minha ordem.
            controller.enabled = false;
        }

        if (closeAbility is GroundSlam slam)
            closeRange = slam.Radius * 0.8f;

        playerHealth.OnDamaged += HandleDamaged;
        combat.OnAttackHit += HandleAttackHit;

        modules.Clear();
        modulePoints.Clear();
        moduleVisited.Clear();
        enemies.Clear();
        knownEnemies.Clear();
        ignored.Clear();

        BossStats boss = FindFirstObjectByType<BossStats>();
        bossStats = boss;
        bossHealth = boss != null ? boss.GetComponent<Health>() : null;
        hasFleePoint = false;
        exitRoom = FindFirstObjectByType<BossRoomExit>();
        deadGuard = questMode ? FindFirstObjectByType<DeadGuardQuestInteractable>() : null;
        chests = FindObjectsByType<TreasureChest>(FindObjectsSortMode.None);
        report.ChestsTotal = chests.Length;

        CollectModules();
        RefreshLists();

        exploreGoal = -1;
        bossStart = -1f;
        exitWaitStart = -1f;
        clickMissReported = false;
        aggressor = null;
        lootTarget = null;
        anchor = player.position;
        anchorTime = Time.time;
        nextThink = 0f;
        nextOrder = 0f;
        lastMovePoint = Vector3.positiveInfinity;

        if (bossHealth == null)
            report.AddFinding("nenhum BossStats na cena: a dungeon nasceu sem boss");

        if (exitRoom == null)
            report.AddFinding("nenhum BossRoomExit na cena: a dungeon nao tem escada de saida");

        if (questMode && deadGuard == null && GuardObjectiveOpen())
            report.AddFinding("a quest 1 pede o guarda morto e esta dungeon nasceu sem ele");

        Time.timeScale = timeScale;
        runActive = true;
        Debug.Log("DungeonBot: comecou a rodada " + report.RunNumber + " em " +
                  report.Scene + ", seed " + report.Seed + ", " + modules.Count + " modulos.");
    }

    private void Unbind()
    {
        if (playerHealth != null)
            playerHealth.OnDamaged -= HandleDamaged;

        if (combat != null)
            combat.OnAttackHit -= HandleAttackHit;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null)
                enemies[i].OnDied -= HandleEnemyDied;
        }

        enemies.Clear();
        knownEnemies.Clear();

        if (controller != null)
            controller.enabled = true;

        hubActive = false;
        player = null;
        controller = null;
        playerHealth = null;
        combat = null;
    }

    private void EndRun(string result)
    {
        runActive = false;
        report.Result = result;

        // Uso a vida do ultimo frame jogado: quando a rodada acaba pela
        // escada, o Player da dungeon ja foi destruido na troca de cena.
        report.HealthAtEnd = lastHealth;
        report.MaxHealthAtEnd = lastMaxHealth;

        if (bossStart >= 0f)
            report.BossSeconds = Time.time - bossStart;

        report.BossKilled = bossStart >= 0f && (bossHealth == null || bossHealth.IsDead);

        for (int i = 0; i < moduleVisited.Count; i++)
        {
            if (moduleVisited[i])
                report.ModulesVisited++;
        }

        report.ModulesVisited -= report.ModulesUnreachable;

        string summary = report.Summary();
        Summaries.Add(summary);
        LastReportPath = report.Write();
        runsDone++;
        SetHud(result);
        Debug.Log("DungeonBot: " + summary + "\n" + LastReportPath);
    }

    // Depois de uma rodada que nao trocou de cena sozinha (tempo, preso),
    // eu mesmo abro a proxima ou desligo.
    private void EndRunAndContinue(string result)
    {
        EndRun(result);

        // Na questline uma dungeon que nao fecha para a historia inteira.
        if (questMode)
        {
            StopBot("parou na dungeon: " + result);
            return;
        }

        if (runsLeft > 0)
        {
            Unbind();
            StartCoroutine(LoadNextScene());
        }
        else
        {
            StopBot(result);
        }
    }

    // ==================================================
    // UPDATE
    // ==================================================

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
        {
            if (botOn)
                StopBot("F3");
            else
                Debug.Log(StartBot(1, 1f, null, false, false));
        }

        if (questMode)
            questSeconds += Time.deltaTime;

        if (hubActive && player != null)
        {
            HubUpdate();
            return;
        }

        if (!runActive || player == null)
            return;

        report.GameSeconds += Time.deltaTime;
        lastHealth = playerHealth.Current;
        lastMaxHealth = playerHealth.Max;
        ApplyCastSpeed();

        if (Time.timeScale == 0f)
            return;

        if (playerHealth.IsDead)
        {
            // O PlayerDeath recarrega a cena sozinho. A rodada acaba aqui
            // e a proxima, se houver, comeca quando a cena voltar.
            motor.Stop();
            report.Died = true;
            EndRun("morreu");

            if (questMode)
            {
                questDeaths++;
                AddStep("morreu em " + report.Scene + " (" + questDeaths + "a morte)");

                if (questDeaths >= MaxQuestDeaths)
                    StopBot("morreu " + questDeaths + " vezes");
            }
            else if (runsLeft <= 0)
            {
                StopBot("morreu");
            }

            return;
        }

        TrackProgress();

        if (!runActive || Time.time < nextThink)
            return;

        nextThink = Time.time + ThinkInterval;

        if (Time.time >= nextList)
        {
            nextList = Time.time + ListInterval;
            RefreshLists();
        }

        MarkModulesNearby();

        if (report.GameSeconds > MaxRunSeconds)
        {
            report.AddFinding("passou de " + MaxRunSeconds + " s de jogo sem terminar");
            EndRunAndContinue("tempo esgotado");
            return;
        }

        DrinkIfNeeded();

        if (questMode)
        {
            PollQuests();
            ReadDialogue();
        }

        // Rolando ou no gesto da habilidade o corpo nao aceita ordem nova,
        // igual ao controle.
        if ((dodge != null && dodge.IsDodging) || IsCasting())
            return;

        Think();
    }

    // O PlayerController e quem baixa a velocidade durante o gesto da
    // habilidade. Com ele desligado, repito a mesma conta aqui, senao o
    // bot usaria habilidade andando a 100%.
    private void ApplyCastSpeed()
    {
        float scale = 1f;

        if (!playerHealth.IsDead && (dodge == null || !dodge.IsDodging))
        {
            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i].IsCasting && abilities[i].MoveWhileCasting)
                {
                    scale = abilities[i].CastMoveSpeedScale;
                    break;
                }
            }
        }

        motor.SpeedScale = scale;
    }

    private bool IsCasting()
    {
        for (int i = 0; i < abilities.Length; i++)
        {
            if (abilities[i].IsCasting)
                return true;
        }

        return false;
    }

    // ==================================================
    // DECISAO
    // ==================================================

    private void Think()
    {
        SpendPoints();

        Health enemy = PickEnemy();

        if (enemy != null)
        {
            SetState(enemy == bossHealth ? State.Boss : State.Fight, enemy);
            Fight(enemy);
            return;
        }

        Pickup pickup = PickPickup();

        if (pickup != null)
        {
            SetState(State.Loot, pickup);
            TrackLoot(pickup);
            MoveOrder(pickup.transform.position);
            return;
        }

        TreasureChest chest = PickChest(NearChestRadius);

        if (chest != null)
        {
            SetState(State.Chest, chest);
            ClickOn(chest, chest.transform);
            return;
        }

        if (GuardPending() && FlatDistance(deadGuard.transform.position) < NearChestRadius)
        {
            SetState(State.Chest, deadGuard);
            ClickOn(deadGuard, deadGuard.transform);
            return;
        }

        if (directMode)
            exploreGoal = -1;
        else if (exploreGoal < 0 || moduleVisited[exploreGoal])
            exploreGoal = PickModule();

        if (exploreGoal >= 0)
        {
            SetState(State.Explore, modules[exploreGoal]);
            MoveOrder(modulePoints[exploreGoal]);
            return;
        }

        // Nao sobrou sala. Bau longe que ficou para tras entra aqui; no
        // modo direto e por aqui que eu vou em cada bau.
        chest = PickChest(float.MaxValue);

        if (chest != null)
        {
            SetState(State.Chest, chest);
            ClickOn(chest, chest.transform);
            return;
        }

        if (GuardPending())
        {
            SetState(State.Chest, deadGuard);
            ClickOn(deadGuard, deadGuard.transform);
            return;
        }

        if (bossHealth != null && !bossHealth.IsDead && !ignored.Contains(bossHealth))
        {
            SetState(State.Boss, bossHealth);
            Fight(bossHealth);
            return;
        }

        GoToExit();
    }

    // Na primeira rodada o bot chegou no boss com os pontos todos livres e
    // morreu em 20 s. Gasto como um jogador de guerreiro gastaria: um em
    // Poder, um em Vitalidade, alternando.
    private void SpendPoints()
    {
        if (level == null || level.FreePoints <= 0)
            return;

        CharacterLevel.Stat stat = level.SpentPoints.Total % 2 == 0
            ? CharacterLevel.Stat.Power
            : CharacterLevel.Stat.Vitality;

        level.TrySpendPoint(stat);
    }

    /// <summary>
    /// Rola para fora do aviso de ataque em area do boss, como o jogador
    /// faz ao ver o circulo crescer no chao.
    ///
    /// O aviso do Monge e um cilindro primitivo sem nome proprio, criado
    /// na hora, entao procuro por "Cylinder". E fragil: se o Leonardo
    /// trocar o aviso, o bot so para de desviar, sem quebrar. A busca por
    /// nome roda 5 vezes por segundo e so durante a luta com o boss.
    /// </summary>
    private bool DodgeTelegraph()
    {
        if (dodge == null || !dodge.IsReady)
            return false;

        GameObject warning = GameObject.Find("Cylinder");

        if (warning == null)
            return false;

        Vector3 center = warning.transform.position;

        if (FlatDistance(center) > TelegraphRadius)
            return false;

        Vector3 away = player.position - center;
        away.y = 0f;

        if (away.sqrMagnitude < 0.01f)
            away = -player.forward;

        return dodge.TryDodge(player.position + away.normalized * 4f);
    }

    /// <summary>
    /// Contra o boss, parado trocando golpe o bot morreu duas vezes em
    /// 20 s: o Monge tira 12,5 de vida por segundo no corpo a corpo e
    /// tem 1000 de vida. Entao luto como quem conhece o chefe: entro
    /// quando o golpe em area esta pronto, bato, e saio rolando ate ele
    /// recarregar, soltando a onda de longe.
    ///
    /// So na fase 1. Na fase 2 o Monge anda a 6 m/s, mais que o Player
    /// rolando (5,9 m/s na media), e fugir so atrasa a luta.
    ///
    /// Devolve true quando a ordem deste instante ja foi dada.
    /// </summary>
    private bool KiteBoss()
    {
        if (bossStats == null || bossStats.FaseAtual != 1 || closeAbility == null)
            return false;

        Vector3 bossPoint = bossHealth.transform.position;
        float distance = FlatDistance(bossPoint);

        // Com o golpe em area pronto eu entro e bato normalmente. Boss longe
        // tambem nao e luta: no modo direto eu gastava o golpe num inimigo
        // do caminho e "fugia" de um boss a 100 m, recuando 8 a 14 m ate a
        // recarga acabar (medi 4 recuos numa rodada).
        if (closeAbility.IsReady || distance > AggressorRadius)
        {
            hasFleePoint = false;
            return false;
        }

        if (farAbility != null && farAbility.IsReady && distance > 3f &&
            distance <= WaveRange && combat.HasLineOfSight(bossHealth))
        {
            farAbility.TryUse(bossPoint);
            return true;
        }

        if (!PickFleePoint(bossPoint))
            return false;

        if (dodge != null && dodge.IsReady && distance < 3.5f)
        {
            dodge.TryDodge(fleePoint);
            return true;
        }

        MoveOrder(fleePoint);
        return true;
    }

    // Fujo para o centro de uma sala que esteja mais perto de mim do que
    // do boss, a mais longe dele. Sem nenhuma assim, fico e luto.
    private bool PickFleePoint(Vector3 bossPoint)
    {
        if (hasFleePoint && FlatDistance(fleePoint) > 3f &&
            FlatBetween(bossPoint, fleePoint) > FlatDistance(fleePoint) + 1f)
            return true;

        hasFleePoint = false;
        float best = 0f;

        for (int i = 0; i < modulePoints.Count; i++)
        {
            float mine = FlatDistance(modulePoints[i]);
            float his = FlatBetween(bossPoint, modulePoints[i]);

            if (mine < 6f || his < mine + 2f || his <= best)
                continue;

            best = his;
            fleePoint = modulePoints[i];
            hasFleePoint = true;
        }

        return hasFleePoint;
    }

    private static float FlatBetween(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private Health PickEnemy()
    {
        Health current = combat.Target;

        // Fico no alvo que ja escolhi ate ele morrer. Trocar a cada
        // decisao deixava o Player indo e voltando entre dois inimigos.
        if (current != null && !current.IsDead && !ignored.Contains(current) &&
            current != playerHealth && FlatDistance(current.transform.position) < AggressorRadius)
            return current;

        bool aggressorValid = aggressor != null && !aggressor.IsDead &&
                              Time.time < aggressorTime + AggressorMemory;
        Health best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            Health enemy = enemies[i];

            if (enemy == null || enemy.IsDead || ignored.Contains(enemy))
                continue;

            float distance = FlatDistance(enemy.transform.position);
            bool hitMe = aggressorValid && enemy == aggressor && distance < AggressorRadius;

            // O boss fica por ultimo, a nao ser que ele venha me buscar.
            // Depois que a luta comecou eu nao largo mais: na terceira
            // rodada o bot voltou a explorar com o boss nas costas.
            if (enemy == bossHealth)
            {
                if (bossStart >= 0f && distance < AggressorRadius)
                    return enemy;

                if (!hitMe)
                    continue;
            }

            if (!hitMe && (distance > (directMode ? DirectEngageRadius : EngageRadius) ||
                           !combat.HasLineOfSight(enemy)))
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = enemy;
            }
        }

        return best;
    }

    private void Fight(Health enemy)
    {
        // A luta so comeca com o boss a 20 m. Antes eu ligava o relogio ao
        // escolher o boss como destino, e no modo direto o "tempo no boss"
        // saia igual ao da rodada inteira, com a vida da largada.
        if (bossStart < 0f && enemy == bossHealth &&
            FlatDistance(enemy.transform.position) < AggressorRadius)
        {
            bossStart = Time.time;
            report.HealthAtBoss = playerHealth.Current;
        }

        if (enemy == bossHealth && DodgeTelegraph())
            return;

        if (enemy == bossHealth && KiteBoss())
            return;

        if (combat.Target != enemy)
            combat.SetTarget(enemy);

        Vector3 point = enemy.transform.position;
        float distance = FlatDistance(point);

        if (closeAbility != null && closeAbility.IsReady && distance <= closeRange)
        {
            closeAbility.TryUse(point);
            return;
        }

        if (farAbility != null && farAbility.IsReady && distance <= WaveRange &&
            combat.HasLineOfSight(enemy))
        {
            farAbility.TryUse(point);
            return;
        }

        // Sem pocao e quase morto, rolo para longe uma vez por recarga.
        if (dodge != null && dodge.IsReady && belt != null && belt.Count == 0 &&
            playerHealth.Normalized < DodgeAt && distance < 3f)
        {
            Vector3 away = player.position + (player.position - point).normalized * 4f;
            dodge.TryDodge(away);
        }
    }

    private void DrinkIfNeeded()
    {
        if (belt == null || belt.Count == 0 || Time.time < nextPotion)
            return;

        if (playerHealth.Normalized > PotionAt)
            return;

        nextPotion = Time.time + 1f;

        if (belt.TryDrink())
            report.PotionsDrunk++;
    }

    private Pickup PickPickup()
    {
        Pickup best = null;
        float bestDistance = LootRadius;

        for (int i = 0; i < pickups.Length; i++)
        {
            Pickup pickup = pickups[i];

            if (pickup == null || ignored.Contains(pickup))
                continue;

            float distance = FlatDistance(pickup.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pickup;
            }
        }

        return best;
    }

    // Item que eu nao consigo levar (cinto ou mochila cheios) continua no
    // chao depois de eu pisar nele. Espero 1 s em cima e deixo para la.
    private void TrackLoot(Pickup pickup)
    {
        if (lootTarget != pickup)
        {
            lootTarget = pickup;
            lootArrivedAt = -1f;
        }

        if (FlatDistance(pickup.transform.position) > 0.8f)
            return;

        if (lootArrivedAt < 0f)
            lootArrivedAt = Time.time;
        else if (Time.time > lootArrivedAt + 1f)
            ignored.Add(pickup);
    }

    private TreasureChest PickChest(float radius)
    {
        TreasureChest best = null;
        float bestDistance = radius;

        for (int i = 0; i < chests.Length; i++)
        {
            // O bau destroi o proprio componente quando abre.
            if (chests[i] == null || ignored.Contains(chests[i].gameObject))
                continue;

            float distance = FlatDistance(chests[i].transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = chests[i];
            }
        }

        return best;
    }

    /// <summary>
    /// A sala nao visitada mais perto pelo caminho, e nao em linha reta:
    /// em linha reta o Player cruzava a dungeon por causa de uma sala do
    /// outro lado da parede. A sala do boss fica de fora ate o fim.
    /// </summary>
    private int PickModule()
    {
        int best = -1;
        float bestLength = float.MaxValue;

        if (!NavMesh.SamplePosition(player.position, out NavMeshHit from, 2f, NavMesh.AllAreas))
            return -1;

        for (int i = 0; i < modules.Count; i++)
        {
            if (moduleVisited[i] || i == bossModule)
                continue;

            if (!NavMesh.CalculatePath(from.position, modulePoints[i], NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                moduleVisited[i] = true;
                report.ModulesUnreachable++;
                report.AddFinding("sem caminho ate o modulo " + modules[i].name +
                                  " em " + Format(modulePoints[i]));
                continue;
            }

            float length = PathLength();

            if (length < bestLength)
            {
                bestLength = length;
                best = i;
            }
        }

        return best;
    }

    private float PathLength()
    {
        int count = path.GetCornersNonAlloc(corners);
        float length = 0f;

        for (int i = 1; i < count; i++)
            length += Vector3.Distance(corners[i - 1], corners[i]);

        return length;
    }

    private void GoToExit()
    {
        SetState(State.Exit, exitRoom);

        if (exitRoom == null || exitRoom.ExitLadder == null)
        {
            EndRunAndContinue("sem escada");
            return;
        }

        if (exitWaitStart < 0f)
            exitWaitStart = Time.time;

        if (!exitRoom.IsActivated)
        {
            if (Time.time > exitWaitStart + StuckSeconds)
            {
                report.AddFinding("o boss morreu e a escada nao ligou em " + StuckSeconds + " s");
                EndRunAndContinue("escada nao ligou");
            }

            return;
        }

        ClickOn(exitRoom.ExitLadder, exitRoom.ExitLadder.transform);
    }

    // ==================================================
    // QUESTLINE
    // ==================================================

    private static readonly QuestId[] QuestOrder =
        { QuestId.SewerInvestigation, QuestId.DeeperIntoSewers, QuestId.RatPriest };

    private const string HubScene = "HUBCity";
    private const int MaxQuestDeaths = 3;
    private const int MaxQuestEntries = 3;

    // Tempo de jogo com a fala aberta antes de eu fechar, para ela aparecer
    // no video e para o QuestToast ter tempo de entrar.
    private const float DialogueReadSeconds = 1.5f;

    // Na HUB nao ha inimigo nem sala para largar: se em 45 s eu nao consigo
    // clicar no Capitao ou na entrada, anoto e chamo o Interact direto; em
    // 90 s desisto.
    private const float HubDirectAt = 45f;
    private const float HubGiveUpAt = 90f;

    // O painel e os botoes da tela de dungeons sao campos privados do
    // script do Eduardo. Leio por reflexao para nao mexer no script dele.
    private static readonly FieldInfo SelectionPanelField = SelectionField("selectionPanel");
    private static readonly FieldInfo[] DungeonButtonFields =
    {
        SelectionField("dungeon1Button"),
        SelectionField("dungeon2Button"),
        SelectionField("dungeon3Button")
    };

    private static FieldInfo SelectionField(string name)
    {
        return typeof(DungeonSelectionUI).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
    }

    /// <summary>
    /// Joga a questline do comeco: Capitao, dungeon da quest, boss, escada,
    /// entrega, e a proxima quest. Comeca do estado em que as quests
    /// estiverem; num Play novo, sem carregar save, e do zero.
    /// </summary>
    public static string RunQuestline(float timeScale = 1f, bool keepAlive = false,
                                      bool direct = false)
    {
        if (instance == null)
            return "DungeonBot so existe em Play, no editor ou em build Development.";

        return instance.StartQuestline(timeScale, keepAlive, direct);
    }

    private string StartQuestline(float scale, bool alive, bool direct)
    {
        if (botOn)
            return "DungeonBot ja esta rodando.";

        keepAlive = alive;
        directMode = direct;
        questMode = true;
        scenes = new[] { HubScene };
        runsLeft = 99;
        runsDone = 0;
        timeScale = Mathf.Clamp(scale, 0.1f, 8f);
        botOn = true;
        Summaries.Clear();
        questSteps.Clear();
        questObjectivesDone.Clear();
        questSeconds = 0f;
        questDeaths = 0;
        dialogueSince = -1f;

        for (int i = 0; i < QuestOrder.Length; i++)
        {
            questEntries[i] = 0;
            questStates[i] = QuestState.NotStarted;
        }

        string scene = SceneManager.GetActiveScene().name;

        if (scene == HubScene)
            beginRoutine = StartCoroutine(BeginHub());
        else if (FindFirstObjectByType<ModularDungeonGenerator>() != null && QuestManager.Instance != null)
            beginRoutine = StartCoroutine(BeginRun());
        else
            StartCoroutine(LoadNextScene());

        return "DungeonBot ligado: questline, timeScale " + timeScale +
               (directMode ? ", direto ao objetivo." : ".");
    }

    private IEnumerator BeginHub()
    {
        state = State.Waiting;
        SetHud("esperando a cidade");

        // O Player da HUB recebe o CrossSceneState no Start.
        yield return new WaitForSecondsRealtime(1f);

        beginRoutine = null;
        GameObject found = GameObject.FindGameObjectWithTag("Player");

        if (found == null || QuestManager.Instance == null)
        {
            Note("a HUB abriu sem Player ou sem QuestManager");
            StopBot("HUB incompleta");
            yield break;
        }

        player = found.transform;
        controller = found.GetComponent<PlayerController>();
        motor = found.GetComponent<PlayerMotor>();
        combat = found.GetComponent<PlayerCombat>();
        interactor = found.GetComponent<PlayerInteractor>();
        playerHealth = found.GetComponent<Health>();
        dodge = found.GetComponent<Dodge>();
        abilities = found.GetComponents<Ability>();

        if (controller != null)
            controller.enabled = false;

        captain = FindFirstObjectByType<CaptainGuardQuestNPC>();
        hubEntrance = FindFirstObjectByType<DungeonEntranceInteractable>();
        hubStep = "";
        hubStepStart = Time.time;
        hubDirectDone = false;
        clickMissReported = false;
        nextThink = 0f;
        nextOrder = 0f;
        lastMovePoint = Vector3.positiveInfinity;
        Time.timeScale = timeScale;
        hubActive = true;
    }

    private void HubUpdate()
    {
        if (Time.timeScale == 0f || Time.time < nextThink)
            return;

        nextThink = Time.time + ThinkInterval;
        PollQuests();

        if (ReadDialogue())
            return;

        int index = CurrentQuest();

        if (index < 0)
        {
            StopBot("questline completa");
            return;
        }

        DungeonSelectionUI selection = DungeonSelectionUI.Instance;

        if (selection != null && SelectionPanelField != null &&
            SelectionPanelField.GetValue(selection) is GameObject panel && panel.activeSelf)
        {
            PressDungeonButton(selection, index);
            return;
        }

        QuestState questState = questStates[index];
        MonoBehaviour target = questState == QuestState.Active ? hubEntrance : (MonoBehaviour)captain;
        string step = questState == QuestState.Active ? "entrar na dungeon da quest " + (index + 1)
            : questState == QuestState.NotStarted ? "aceitar a quest " + (index + 1)
            : "entregar a quest " + (index + 1);

        if (step != hubStep)
        {
            hubStep = step;
            hubStepStart = Time.time;
            hubDirectDone = false;
            SetHud(step);
        }

        if (target == null)
        {
            Note("a HUB nao tem " + (questState == QuestState.Active
                ? "DungeonEntranceInteractable" : "CaptainGuardQuestNPC"));
            StopBot("HUB incompleta");
            return;
        }

        float waited = Time.time - hubStepStart;

        if (waited > HubGiveUpAt)
        {
            Note("preso na HUB em " + Format(player.position) + " tentando " + step);
            StopBot("preso na HUB");
            return;
        }

        if (waited > HubDirectAt && !hubDirectDone)
        {
            hubDirectDone = true;
            Note("em " + HubDirectAt + " s nao consegui clicar em " + target.name + " em " +
                 Format(target.transform.position) + " (Player em " + Format(player.position) +
                 "); chamei o Interact direto");
            ((IInteractable)target).Interact();
            return;
        }

        ClickOn(target, target.transform);
    }

    // A primeira quest que ainda nao foi entregue, ou -1.
    private int CurrentQuest()
    {
        for (int i = 0; i < QuestOrder.Length; i++)
        {
            if (questStates[i] != QuestState.Completed)
                return i;
        }

        return -1;
    }

    // Aperto o botao como o clique faria: so se ele estiver aceso. Botao
    // apagado e o "BLOQUEADO" ou o "EM BREVE" que o jogador veria.
    private void PressDungeonButton(DungeonSelectionUI selection, int index)
    {
        Button button = DungeonButtonFields[index] != null
            ? DungeonButtonFields[index].GetValue(selection) as Button
            : null;

        if (button == null || !button.interactable)
        {
            Note("a dungeon da quest " + (index + 1) + " nao abre na tela de selecao " +
                 "(cena fora da build ou botao bloqueado)");
            selection.Close();
            StopBot("quest " + (index + 1) + " sem dungeon");
            return;
        }

        questEntries[index]++;

        // Voltar da dungeon com a quest ainda ativa e entrar de novo sem
        // fim: objetivo que nunca fecha, ou morte toda vez.
        if (questEntries[index] > MaxQuestEntries)
        {
            Note("entrei " + MaxQuestEntries + " vezes na dungeon da quest " + (index + 1) +
                 " e ela nao ficou pronta para entregar");
            selection.Close();
            StopBot("quest " + (index + 1) + " nao fecha");
            return;
        }

        AddStep("entrou na dungeon da quest " + (index + 1));
        hubActive = false;
        button.onClick.Invoke();
    }

    // Fecho a fala depois de um tempo, como quem leu. Devolve true enquanto
    // ela esta aberta.
    private bool ReadDialogue()
    {
        DialogueUI dialogue = DialogueUI.Instance;

        if (dialogue == null || !dialogue.IsOpen)
        {
            dialogueSince = -1f;
            return false;
        }

        if (dialogueSince < 0f)
            dialogueSince = Time.time;
        else if (Time.time > dialogueSince + DialogueReadSeconds)
            dialogue.HideDialogue();

        return true;
    }

    // O evento do QuestManager nao diz o que mudou, entao comparo com o que
    // eu tinha visto, 5 vezes por segundo, e anoto cada passo com o tempo.
    private void PollQuests()
    {
        QuestManager manager = QuestManager.Instance;

        if (manager == null)
            return;

        for (int i = 0; i < QuestOrder.Length; i++)
        {
            QuestProgress quest = manager.FindQuest(QuestOrder[i]);

            if (quest == null)
                continue;

            // Objetivo antes do estado, para o arquivo sair na ordem em
            // que o jogador ve: fechou o objetivo, a quest ficou pronta.
            for (int j = 0; j < quest.Objectives.Count; j++)
            {
                QuestObjectiveProgress objective = quest.Objectives[j];

                if (objective != null && objective.Completed &&
                    questObjectivesDone.Add(objective.ObjectiveId))
                    AddStep("objetivo " + objective.ObjectiveId + " concluido");
            }

            if (quest.State != questStates[i])
            {
                questStates[i] = quest.State;

                if (quest.State == QuestState.Active)
                    AddStep("aceitou a quest " + (i + 1) + " (" + QuestOrder[i] + ")");
                else if (quest.State == QuestState.ReadyToTurnIn)
                    AddStep("quest " + (i + 1) + " pronta para entregar");
                else if (quest.State == QuestState.Completed)
                    AddStep("entregou a quest " + (i + 1));
            }
        }
    }

    private bool GuardObjectiveOpen()
    {
        QuestManager manager = QuestManager.Instance;

        return manager != null &&
               manager.GetQuestState(QuestId.SewerInvestigation) == QuestState.Active &&
               !manager.IsObjectiveCompleted(QuestId.SewerInvestigation, QuestObjectiveId.FindDeadGuard);
    }

    private bool GuardPending()
    {
        return questMode && deadGuard != null && !ignored.Contains(deadGuard) && GuardObjectiveOpen();
    }

    private void AddStep(string text)
    {
        questSteps.Add(questSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                       .PadLeft(5) + " s | " + text);
        Debug.Log("DungeonBot: questline, " + text);
    }

    // Achado: vai para o relatorio da dungeon quando ha rodada aberta e
    // para o da questline quando estou na HUB.
    private void Note(string text)
    {
        if (runActive && report != null)
        {
            report.AddFinding(text);
            return;
        }

        if (questMode)
            questSteps.Add("      ACHADO | " + text);

        Debug.LogWarning("DungeonBot: " + text);
    }

    private void CloseQuestline(string result)
    {
        PollQuests();
        AddStep("fim: " + result);

        int delivered = 0;

        for (int i = 0; i < QuestOrder.Length; i++)
        {
            if (questStates[i] == QuestState.Completed)
                delivered++;
        }

        string summary = "questline" + (directMode ? " direta" : "") + " | " + result + " | " +
            questSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
            " s | quests entregues " + delivered + "/" + QuestOrder.Length +
            " | mortes " + questDeaths;

        LastReportPath = DungeonBotReport.WriteQuestline(summary, questSteps, Summaries);
        Summaries.Add(summary);
        questMode = false;
        hubActive = false;
        Debug.Log("DungeonBot: " + summary + "\n" + LastReportPath);
    }

    // ==================================================
    // ORDENS
    // ==================================================

    // O mesmo que o clique de andar faz: larga o alvo e corta o golpe.
    private void MoveOrder(Vector3 point)
    {
        bool samePoint = (point - lastMovePoint).sqrMagnitude < 0.25f;

        if (samePoint && motor.IsMoving)
            return;

        if (samePoint && Time.time < nextOrder)
            return;

        nextOrder = Time.time + 0.5f;

        if (!motor.MoveTo(point, out _))
            return;

        lastMovePoint = point;
        combat.ClearTarget();
        combat.InterruptAttack();

        if (interactor != null)
            interactor.ClearTarget();
    }

    /// <summary>
    /// Clica num bau ou na escada pelo PlayerInteractor, com um raio da
    /// camera ate o objeto. Se o raio nao acha o interagivel, anoto: e o
    /// mesmo clique que falharia para o jogador. Ai sigo pelo Interact
    /// direto, para a rodada nao parar num achado so.
    /// </summary>
    private void ClickOn(Object target, Transform body)
    {
        float distance = FlatDistance(body.position);

        if (distance > ClickDistance)
        {
            if (Time.time < nextOrder && motor.IsMoving)
                return;

            nextOrder = Time.time + 0.5f;
            lastMovePoint = Vector3.positiveInfinity;
            combat.ClearTarget();
            motor.Chase(body.position, ApproachStop);
            return;
        }

        if (Time.time < nextOrder || motor.IsMoving)
            return;

        nextOrder = Time.time + 1.5f;
        lastMovePoint = Vector3.positiveInfinity;
        combat.ClearTarget();
        combat.InterruptAttack();

        Camera view = Camera.main;
        Collider collider = body.GetComponentInChildren<Collider>();
        bool clicked = false;

        if (view != null && collider != null && interactor != null)
        {
            Vector3 origin = view.transform.position;
            clicked = interactor.TryInteract(
                new Ray(origin, (collider.bounds.center - origin).normalized));
        }

        if (clicked)
            return;

        if (!clickMissReported)
        {
            clickMissReported = true;
            Note("o clique da camera nao achou " + body.name + " em " +
                 Format(body.position) + " (algo solido na frente?)");
        }

        if (distance > ReachDistance)
        {
            motor.Chase(body.position, ApproachStop * 0.5f);
            return;
        }

        IInteractable direct = body.GetComponentInChildren<IInteractable>();

        if (direct != null)
            direct.Interact();
    }

    // ==================================================
    // ANDAMENTO E PRESO
    // ==================================================

    private void SetState(State next, Object goal)
    {
        string nextName = goal != null ? goal.name : "";

        if (next == state && nextName == goalName)
            return;

        state = next;
        goalName = nextName;
        madeProgress = true;
        SetHud(next + " " + nextName);
    }

    private void SetHud(string text)
    {
        hudText = (keepAlive ? "BOT sem morrer" : "BOT") + (directMode ? " direto" : "") +
                  " (F3 desliga): " + text;
    }

    private void TrackProgress()
    {
        Vector3 position = player.position;

        if (madeProgress || (position - anchor).sqrMagnitude > StuckMove * StuckMove)
        {
            madeProgress = false;
            anchor = position;
            anchorTime = Time.time;
            return;
        }

        if (state == State.Waiting || Time.time < anchorTime + StuckSeconds)
            return;

        anchorTime = Time.time;
        HandleStuck();
    }

    /// <summary>
    /// Parado 5 s sem andar nem bater. Anoto onde e fazendo o que, largo
    /// esse objetivo e sigo para o proximo, para uma rodada achar varios
    /// problemas em vez de morrer no primeiro.
    /// </summary>
    private void HandleStuck()
    {
        report.StuckCount++;
        report.AddFinding("preso em " + Format(player.position) + " fazendo " +
                          state + " " + goalName);

        switch (state)
        {
            case State.Fight:
                if (combat.Target != null)
                    ignored.Add(combat.Target);

                combat.ClearTarget();
                break;

            case State.Boss:
                EndRunAndContinue("preso no boss");
                return;

            case State.Loot:
                if (lootTarget != null)
                    ignored.Add(lootTarget);

                break;

            case State.Chest:
                // O guarda morto da quest usa o mesmo estado do bau.
                if (deadGuard != null && goalName == deadGuard.name)
                {
                    ignored.Add(deadGuard);
                    break;
                }

                TreasureChest chest = PickChest(float.MaxValue);

                if (chest != null)
                    ignored.Add(chest.gameObject);

                break;

            case State.Explore:
                if (exploreGoal >= 0)
                {
                    moduleVisited[exploreGoal] = true;
                    report.ModulesUnreachable++;
                }

                break;

            case State.Exit:
                EndRunAndContinue("preso na escada");
                return;
        }

        lastMovePoint = Vector3.positiveInfinity;
        nextOrder = 0f;

        if (report.StuckCount >= MaxStuck)
            EndRunAndContinue("preso demais");
    }

    // ==================================================
    // CENA
    // ==================================================

    private void CollectModules()
    {
        DungeonModule[] found = FindObjectsByType<DungeonModule>(FindObjectsSortMode.None);
        bossModule = -1;
        float bossDistance = float.MaxValue;

        for (int i = 0; i < found.Length; i++)
        {
            BoxCollider box = found[i].PlacementBounds;
            Vector3 center = box != null ? box.bounds.center : found[i].transform.position;

            if (box != null)
                center.y = box.bounds.min.y + 0.2f;

            // Modulo sem chao pisavel no meio (um vao, uma coluna grossa)
            // nao e erro por si so; so nao da para usar como destino.
            if (!NavMesh.SamplePosition(center, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                report.AddFinding("modulo " + found[i].name + " sem NavMesh a 4 m do centro " +
                                  Format(center));
                continue;
            }

            modules.Add(found[i]);
            modulePoints.Add(hit.position);
            moduleVisited.Add(false);

            if (bossHealth != null)
            {
                float distance = (bossHealth.transform.position - center).sqrMagnitude;

                if (distance < bossDistance)
                {
                    bossDistance = distance;
                    bossModule = modules.Count - 1;
                }
            }
        }

        report.ModulesTotal = modules.Count;
    }

    // Uma vez por segundo, e nao por frame: as duas buscas alocam. Inimigo
    // que nasce depois (invocado pelo boss) e item que cai entram aqui.
    private void RefreshLists()
    {
        Health[] all = FindObjectsByType<Health>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            Health health = all[i];

            if (health == playerHealth || health.IsDead || !knownEnemies.Add(health))
                continue;

            enemies.Add(health);
            health.OnDied += HandleEnemyDied;
            report.EnemiesTotal++;
        }

        int before = pickups.Length;
        pickups = FindObjectsByType<Pickup>(FindObjectsSortMode.None);

        // Item que sumiu da lista foi pego (so eu ando pela dungeon).
        if (pickups.Length < before)
        {
            report.PickupsCollected += before - pickups.Length;
            madeProgress = true;
        }

        int open = 0;

        for (int i = 0; i < chests.Length; i++)
        {
            if (chests[i] == null)
                open++;
        }

        if (open > report.ChestsOpened)
            madeProgress = true;

        report.ChestsOpened = open;
    }

    private void MarkModulesNearby()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            if (moduleVisited[i])
                continue;

            float distance = FlatDistance(modulePoints[i]);

            // Corredor conta ao pisar dentro da caixa dele. Medi numa rodada:
            // a briga me levou 7 m alem de um Hall_T sem eu passar a 1,5 m
            // do centro, e eu voltei so para tocar nele. Sala continua
            // pedindo o centro, senao eu marcava na porta e nao entrava.
            if (distance < ArriveDistance || InsideCorridor(i) ||
                (distance < 3f && !motor.IsMoving && i == exploreGoal))
            {
                moduleVisited[i] = true;
                madeProgress = true;
            }
        }
    }

    private bool InsideCorridor(int index)
    {
        DungeonModule module = modules[index];

        if (module == null || module.ModuleType != DungeonModuleType.Corridor ||
            module.PlacementBounds == null)
            return false;

        Bounds box = module.PlacementBounds.bounds;
        Vector3 position = player.position;

        return position.x > box.min.x && position.x < box.max.x &&
               position.z > box.min.z && position.z < box.max.z;
    }

    private void HandleDamaged(float amount, GameObject source)
    {
        report.DamageTaken += amount;

        if (keepAlive && !playerHealth.IsDead &&
            playerHealth.Current < playerHealth.Max * KeepAliveAt)
        {
            report.Rescues++;

            if (report.Rescues <= 10)
            {
                report.AddFinding("teria morrido em " + Format(player.position) +
                                  " fazendo " + state + " " + goalName);
            }

            playerHealth.Heal(playerHealth.Max);
        }

        // Apanhando eu nao estou preso. Com o escudo do boss ligado meus
        // golpes nao tiram vida, e 5 s assim contavam como preso.
        madeProgress = true;

        if (source == null)
            return;

        Health attacker = source.GetComponentInParent<Health>();

        if (attacker != null && attacker != playerHealth)
        {
            aggressor = attacker;
            aggressorTime = Time.time;
        }
    }

    private void HandleAttackHit(Health target, float damage)
    {
        report.DamageDealt += damage;
        madeProgress = true;
    }

    private void HandleEnemyDied(Health enemy)
    {
        enemy.OnDied -= HandleEnemyDied;

        // O item cai neste mesmo evento. Sem refazer a lista ja na proxima
        // decisao, eu saia para o proximo objetivo e ate 1 s depois voltava
        // para pegar o item (medi 3 voltas de 5 a 7 m numa rodada).
        nextList = 0f;

        if (runActive)
            report.EnemiesKilled++;
    }

    private void HandleLog(string message, string stackTrace, LogType type)
    {
        if (!runActive || type == LogType.Log || type == LogType.Warning)
            return;

        report.ConsoleErrors++;

        // So os 5 primeiros por extenso: um erro por frame encheria o arquivo.
        if (report.ConsoleErrors <= 5)
            report.Findings.Add("erro no console: " + message);
    }

    private float FlatDistance(Vector3 point)
    {
        Vector3 delta = point - player.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private static string Format(Vector3 point)
    {
        return "(" + point.x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ", " +
               point.y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ", " +
               point.z.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ")";
    }

    // Uma linha no canto para eu saber que quem esta jogando e o bot. O
    // texto so e remontado quando o objetivo muda.
    private void OnGUI()
    {
        if (!botOn)
            return;

        // Letra proporcional a tela: com o tamanho padrao do OnGUI o aviso
        // sumia no Game view de 1440p e no video.
        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            hudStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
        }

        float scale = Screen.height / 1080f;
        hudStyle.fontSize = Mathf.RoundToInt(22f * scale);
        GUI.Label(new Rect(16f * scale, Screen.height - 44f * scale,
            Screen.width * 0.7f, 36f * scale), hudText, hudStyle);
    }
}
