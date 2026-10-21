using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class BossStats : MonoBehaviour
{
    [Header("Dados")]
    public BossDataSO dadosBoss;

    [Header("Derrota")]
    [Tooltip(
        "Se ativado, o GameObject do boss será destruído " +
        "quando a vida chegar a zero."
    )]
    [SerializeField]
    private bool destruirAoSerDerrotado = true;

    [Tooltip(
        "Eventos disparados quando o boss é derrotado. " +
        "Pode ser usado para completar objetivos de quest."
    )]
    [SerializeField]
    private UnityEvent onBossDerrotado;

    private float vidaAtual;

    public event Action<int> OnMudancaFase;

    /*
     * Evento C# mantido porque outros componentes
     * podem querer reagir à derrota diretamente.
     *
     * O Boss 2, por exemplo, usa isso para parar
     * a IA e ficar disponível para interrogatório.
     */
    public event Action OnBossMorto;

    public int FaseAtual
    {
        get;
        private set;
    } = 1;

    public bool EstaDerrotado
    {
        get;
        private set;
    }

    // ==================================================
    // AWAKE
    // ==================================================

    private void Awake()
    {
        if (GetComponent<BossHealth>() == null)
        {
            gameObject.AddComponent<BossHealth>();
        }
    }

    // ==================================================
    // START
    // ==================================================

    private void Start()
    {
        if (dadosBoss != null)
        {
            vidaAtual =
                dadosBoss.vidaMaxima;
        }

        EstaDerrotado =
            false;
    }

    // ==================================================
    // UPDATE
    // ==================================================

    private void Update()
    {
        if (EstaDerrotado)
            return;

        if (dadosBoss == null)
            return;

        if (Keyboard.current == null)
            return;

        // ========================================
        // DEBUG - FASE 2
        // ========================================

        if (
            Keyboard.current.yKey.wasPressedThisFrame &&
            FaseAtual < 2
        )
        {
            Debug.Log(
                "<color=yellow>" +
                "[DEBUG] Pressionou Y: Ativando Fase 2!" +
                "</color>"
            );

            TomarDano(
                dadosBoss.vidaMaxima * 0.45f
            );
        }

        // ========================================
        // DEBUG - FASE 3
        // ========================================

        if (
            Keyboard.current.uKey.wasPressedThisFrame &&
            FaseAtual < 3
        )
        {
            Debug.Log(
                "<color=red>" +
                "[DEBUG] Pressionou U: Ativando Fase 3!" +
                "</color>"
            );

            TomarDano(
                dadosBoss.vidaMaxima * 0.75f
            );
        }
    }

    // ==================================================
    // TOMAR DANO
    // ==================================================

    public void TomarDano(
        float quantidade
    )
    {
        if (EstaDerrotado)
            return;

        if (dadosBoss == null)
        {
            Debug.LogError(
                "BossStats: dadosBoss não foi definido.",
                this
            );

            return;
        }

        vidaAtual -=
            quantidade;

        vidaAtual =
            Mathf.Max(
                vidaAtual,
                0f
            );

        Debug.Log(
            $"[BossStats] Vida Atual: " +
            $"{vidaAtual}/{dadosBoss.vidaMaxima} " +
            $"({(vidaAtual / dadosBoss.vidaMaxima) * 100f}%)"
        );

        VerificarMudancaFase();

        if (vidaAtual <= 0f)
        {
            ProcessarDerrota();
        }
    }

    // ==================================================
    // DERROTA
    // ==================================================

    private void ProcessarDerrota()
    {
        if (EstaDerrotado)
            return;

        EstaDerrotado =
            true;

        Debug.Log(
            "<color=red>[BOSS] Boss derrotado!</color>",
            this
        );

        // ========================================
        // EVENTO C#
        // ========================================

        OnBossMorto?.Invoke();

        // ========================================
        // UNITY EVENT
        //
        // Aqui podemos chamar diretamente:
        //
        // QuestObjectiveTrigger.TriggerObjective()
        //
        // pelo Inspector.
        // ========================================

        onBossDerrotado?.Invoke();

        // ========================================
        // DESTRUIÇÃO
        // ========================================

        if (destruirAoSerDerrotado)
        {
            Destroy(
                gameObject
            );
        }
    }

    // ==================================================
    // MUDANÇA DE FASE
    // ==================================================

    private void VerificarMudancaFase()
    {
        if (
            dadosBoss == null ||
            dadosBoss.vidaMaxima <= 0f
        )
        {
            return;
        }

        float porcentagemVida =
            (vidaAtual / dadosBoss.vidaMaxima) *
            100f;

        if (
            porcentagemVida <= 30f &&
            FaseAtual != 3
        )
        {
            FaseAtual =
                3;

            Debug.Log(
                "<color=orange>" +
                "[BOSS] MUDANÇA PARA A FASE 3!" +
                "</color>"
            );

            OnMudancaFase?.Invoke(
                3
            );
        }
        else if (
            porcentagemVida <= 60f &&
            FaseAtual < 2
        )
        {
            FaseAtual =
                2;

            Debug.Log(
                "<color=yellow>" +
                "[BOSS] MUDANÇA PARA A FASE 2!" +
                "</color>"
            );

            OnMudancaFase?.Invoke(
                2
            );
        }
    }

    // ==================================================
    // GET VIDA
    // ==================================================

    public float GetPorcentagemVida()
    {
        if (
            dadosBoss == null ||
            dadosBoss.vidaMaxima <= 0f
        )
        {
            return 0f;
        }

        return
            vidaAtual /
            dadosBoss.vidaMaxima;
    }
}