using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// Deixo os atalhos nesta cena para testar sem mexer no controle do Player.
public sealed class SandboxTools : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject bossTemplate;
    [SerializeField] private Transform bossSpawn;
    [SerializeField] private BossStats currentBoss;
    [SerializeField] private LayerMask groundMask = 1 << 8;

    private Health health;
    private CharacterLevel level;
    private GoldWallet wallet;
    private Camera viewCamera;
    private bool godMode;
    private bool slowMotion;
    private bool legendVisible = true;
    private GUIContent legend;
    private GUIStyle legendStyle;
    private string status;

    public bool GodMode => godMode;
    public bool SlowMotion => slowMotion;
    public bool LegendVisible => legendVisible;
    public BossStats CurrentBoss => currentBoss;

    private void Awake()
    {
        health = player.GetComponent<Health>();
        level = player.GetComponent<CharacterLevel>();
        wallet = player.GetComponent<GoldWallet>();
        viewCamera = Camera.main;
        status = "Deus: OFF | Tempo: 1x";
        legend = new GUIContent(
            "SANDBOX  |  F12 esconde\n" +
            "F5 inimigo no mouse    F6 reinicia boss\n" +
            "F7 cura    F8 modo deus    F9 +1 nivel\n" +
            "F10 +100 ouro    F11 tempo 0,25x\n" +
            "I inventario    ESC pausa    F2 loja\n" +
            "Direito: area    1: onda    Espaco: rolar\n" +
            "Noroeste: treino | Nordeste: visao e boss\n" +
            "Sudoeste: arena | Sudeste: obstaculos\n" +
            "Sul: loot e loja");
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.f5Key.wasPressedThisFrame) SpawnEnemyUnderMouse();
        if (keyboard.f6Key.wasPressedThisFrame) ResetBoss();
        if (keyboard.f7Key.wasPressedThisFrame) HealPlayer();
        if (keyboard.f8Key.wasPressedThisFrame) ToggleGodMode();
        if (keyboard.f9Key.wasPressedThisFrame) AddLevel();
        if (keyboard.f10Key.wasPressedThisFrame) AddGold();
        if (keyboard.f11Key.wasPressedThisFrame) ToggleSlowMotion();
        if (keyboard.f12Key.wasPressedThisFrame) legendVisible = !legendVisible;
    }

    public void SpawnEnemyUnderMouse()
    {
        if (Mouse.current == null || viewCamera == null) return;
        Ray ray = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundMask,
            QueryTriggerInteraction.Ignore)) SpawnEnemyAt(hit.point);
    }

    public GameObject SpawnEnemyAt(Vector3 point)
    {
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            return null;
        GameObject enemy = Instantiate(enemyPrefab, hit.position, Quaternion.identity);
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null) agent.Warp(hit.position);
        return enemy;
    }

    public void ResetBoss()
    {
        if (currentBoss != null)
        {
            // Paro a IA antes do Destroy adiado para nao haver dois ataques no frame.
            currentBoss.gameObject.SetActive(false);
            Destroy(currentBoss.gameObject);
        }
        GameObject boss = Instantiate(bossTemplate, bossSpawn.position, bossSpawn.rotation);
        boss.name = "SandboxMonk";
        boss.SetActive(true);
        currentBoss = boss.GetComponent<BossStats>();
    }

    public void HealPlayer()
    {
        if (!health.IsDead) health.Heal(health.Max);
    }

    public void ToggleGodMode()
    {
        godMode = !godMode;
        if (godMode) health.AddInvulnerability(this);
        else health.RemoveInvulnerability(this);
        RefreshStatus();
    }

    public void AddLevel()
    {
        if (!level.IsMaxLevel) level.AddXp(level.NextLevelXp - level.Xp);
    }

    public void AddGold()
    {
        wallet.Add(100);
    }

    public void ToggleSlowMotion()
    {
        slowMotion = !slowMotion;
        // Nao tiro o jogo da pausa quando testo F11 com o menu aberto.
        if (Time.timeScale > 0f) Time.timeScale = slowMotion ? 0.25f : 1f;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        // So remonto texto quando um atalho muda o estado.
        status = (godMode ? "Deus: ON" : "Deus: OFF") +
            (slowMotion ? " | Tempo: 0,25x" : " | Tempo: 1x");
    }

    private void OnGUI()
    {
        if (!legendVisible) return;
        if (legendStyle == null)
        {
            legendStyle = new GUIStyle(GUI.skin.box);
            legendStyle.alignment = TextAnchor.UpperLeft;
            legendStyle.fontSize = 16;
            legendStyle.padding = new RectOffset(12, 12, 10, 10);
        }
        GUI.Box(new Rect(12, 12, 410, 222), legend, legendStyle);
        GUI.Label(new Rect(24, 236, 390, 25), status);
    }

    private void OnDisable()
    {
        if (health != null) health.RemoveInvulnerability(this);
        // Devolvo o tempo ao sair ou recarregar a Sandbox.
        Time.timeScale = 1f;
    }
}
