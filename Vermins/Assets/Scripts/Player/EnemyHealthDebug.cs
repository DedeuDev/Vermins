using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Barra de vida do inimigo no topo da tela, como no Diablo 4 e no Path
/// of Exile 2: uma barra so, do inimigo que importa agora, em vez de uma
/// em cima de cada um.
///
/// Quem aparece, nesta ordem:
/// 1. o inimigo sob o mouse (ContextualCursor.EnemyUnderMouse);
/// 2. o alvo travado do PlayerCombat;
/// 3. o ultimo que eu acertei, por alguns segundos (PlayerCombat.OnAttackHit,
///    que tambem pega o golpe em area e a onda de choque).
///
/// Atras da vermelha tem uma barra clara que desce devagar, mostrando
/// quanto o ultimo golpe tirou.
///
/// E debug, igual ao PlayerDebugHUD: OnGUI, sem Canvas nem objeto nenhum,
/// so no editor e em Development Build, e o mesmo F1 esconde. Quando a UI
/// do Rogger tiver barra de inimigo, e so tirar do Player.prefab.
/// </summary>
[RequireComponent(typeof(ContextualCursor))]
[RequireComponent(typeof(PlayerCombat))]
public class EnemyHealthDebug : MonoBehaviour
{
    [SerializeField] private bool visible = true;

    [Tooltip("Mesma tecla do PlayerDebugHUD, pra os dois sumirem juntos no print.")]
    [SerializeField] private Key toggleKey = Key.F1;

    [Tooltip("Segundos que o ultimo inimigo acertado continua na barra " +
             "depois que o mouse sai dele.")]
    [SerializeField] private float lastHitDuration = 3f;

    [Tooltip("Segundos que a barra clara espera antes de comecar a descer.")]
    [SerializeField] private float trailDelay = 0.4f;

    [Tooltip("Quanto da barra a parte clara desce por segundo (1 = a barra toda).")]
    [SerializeField] private float trailSpeed = 0.8f;

    private ContextualCursor cursor;
    private PlayerCombat combat;

    private Health lastHit;
    private float lastHitTime = float.NegativeInfinity;

    private Health shown;
    private float trailFill;
    private float trailWaitUntil;

    private void Awake()
    {
        cursor = GetComponent<ContextualCursor>();
        combat = GetComponent<PlayerCombat>();
    }

    private void OnEnable()
    {
        combat.OnAttackHit += HandleAttackHit;
    }

    private void OnDisable()
    {
        combat.OnAttackHit -= HandleAttackHit;
        shown = null;
        lastHit = null;
    }

    private void HandleAttackHit(Health alvo, float dano)
    {
        lastHit = alvo;
        lastHitTime = Time.time;

        // A barra clara so espera se o golpe foi no inimigo que ja esta
        // na tela; num inimigo novo o Update zera ela de qualquer jeito.
        if (alvo == shown)
            trailWaitUntil = Time.time + trailDelay;
    }

    private void Update()
    {
        Keyboard teclado = Keyboard.current;

        if (teclado != null && teclado[toggleKey].wasPressedThisFrame)
            visible = !visible;

        Health alvo = PickTarget();

        if (alvo != shown)
        {
            shown = alvo;
            trailFill = alvo != null ? alvo.Normalized : 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // So monta o nome quando o alvo troca, nunca por frame.
            nameText = alvo != null ? CleanName(alvo.gameObject.name) : string.Empty;
            shownCurrent = -1;
#endif
            return;
        }

        if (shown == null)
            return;

        // A clara nunca fica abaixo da vermelha: se o inimigo curar (o
        // boneco da Sandbox enche em 3 s), ela sobe junto na hora.
        float cheio = shown.Normalized;

        if (trailFill < cheio)
            trailFill = cheio;
        else if (Time.time >= trailWaitUntil)
            trailFill = Mathf.MoveTowards(trailFill, cheio, trailSpeed * Time.deltaTime);
    }

    private Health PickTarget()
    {
        Health sobMouse = cursor.EnemyUnderMouse;

        if (IsAlive(sobMouse))
            return sobMouse;

        if (combat.HasTarget)
            return combat.Target;

        if (IsAlive(lastHit) && Time.time - lastHitTime <= lastHitDuration)
            return lastHit;

        return null;
    }

    // O "!= null" do Unity tambem pega o inimigo destruido.
    private static bool IsAlive(Health h) => h != null && !h.IsDead;

    private static string CleanName(string nome)
    {
        int clone = nome.IndexOf("(Clone)", System.StringComparison.Ordinal);
        return clone >= 0 ? nome.Substring(0, clone).TrimEnd() : nome;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Tela de referencia de 1080 de altura, igual ao PlayerDebugHUD. No
    // topo e no centro: o painel de missao do Eduardo fica na esquerda
    // (276 a 764 px), o HUD de debug na direita e o dialogo embaixo.
    private const float BarWidth = 520f;
    private const float BarHeight = 22f;
    private const float BarY = 20f;
    private const float TextHeight = 28f;

    private static readonly Color BackColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
    private static readonly Color TrailColor = new Color(0.95f, 0.85f, 0.7f);
    private static readonly Color HealthColor = new Color(0.75f, 0.1f, 0.08f);

    private GUIStyle nameStyle;
    private GUIStyle numberStyle;

    private string nameText = string.Empty;
    private readonly GUIContent nameContent = new GUIContent(string.Empty);
    private readonly GUIContent numberContent = new GUIContent(string.Empty);

    // Mesma ideia do CachedText do HUD: o texto dos numeros so e
    // remontado quando a vida inteira ou o maximo mudam, nunca por frame.
    private int shownCurrent = -1;
    private int shownMax = -1;

    private void OnGUI()
    {
        if (!visible || shown == null)
            return;

        float escala = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));

        nameStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.LowerCenter,
        };
        numberStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
        };

        int atual = Mathf.CeilToInt(shown.Current);
        int maximo = Mathf.CeilToInt(shown.Max);

        if (atual != shownCurrent || maximo != shownMax)
        {
            shownCurrent = atual;
            shownMax = maximo;
            numberContent.text = atual + " / " + maximo;
        }

        nameContent.text = nameText;

        float x = (Screen.width / escala - BarWidth) * 0.5f;
        var barra = new Rect(x, BarY + TextHeight, BarWidth, BarHeight);

        GUI.Label(new Rect(x, BarY, BarWidth, TextHeight), nameContent, nameStyle);

        Fill(barra, 1f, BackColor);
        Fill(barra, trailFill, TrailColor);
        Fill(barra, shown.Normalized, HealthColor);

        GUI.Label(barra, numberContent, numberStyle);
    }

    private static void Fill(Rect fundo, float cheio, Color cor)
    {
        fundo.width *= Mathf.Clamp01(cheio);
        GUI.DrawTexture(fundo, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, cor, 0f, 0f);
    }
#endif
}
