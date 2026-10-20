using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Da peso pro golpe: quando a espada acerta, o jogador e o alvo congelam
/// uma fracao de segundo e a camera sacode. Quando o jogador apanha, a
/// camera sacode mais forte.
///
/// A pausa congela so os Animators (speed 0), e nao o Time.timeScale. O
/// menu de pausa e o PlayerController usam o timeScale 0 pra saber que o
/// jogo esta pausado, e uma pausa global aqui brigaria com eles: pausar
/// no meio do impacto e o impacto terminar despausaria o menu.
/// </summary>
[RequireComponent(typeof(PlayerCombat))]
public class HitImpact : MonoBehaviour
{
    [Header("Pausa no impacto")]
    [Tooltip("Quanto tempo o jogador e o alvo ficam congelados no acerto. " +
             "Acima de uns 0,1 s ja parece travada, e nao pancada.")]
    [FormerlySerializedAs("pausaNoAcerto")]
    [SerializeField] private float hitPause = 0.06f;

    [Tooltip("Pausa do golpe que mata. Um pouco maior, pra o ultimo golpe " +
             "pesar mais que os outros.")]
    [FormerlySerializedAs("pausaNaMorte")]
    [SerializeField] private float killPause = 0.1f;

    [Header("Tremor de camera (trauma de 0 a 1)")]
    [FormerlySerializedAs("tremorNoAcerto")]
    [SerializeField] private float hitShake = 0.35f;

    [FormerlySerializedAs("tremorNaMorte")]
    [SerializeField] private float killShake = 0.55f;

    [Tooltip("Apanhar treme mais que bater: e o aviso de que a vida caiu.")]
    [FormerlySerializedAs("tremorAoApanhar")]
    [SerializeField] private float hurtShake = 0.5f;

    private PlayerCombat combat;
    private Health health;

    // Quem esta congelado e a velocidade que tinha antes. Devolvo a
    // velocidade antiga em vez de 1 pra nao atropelar quem um dia mexer
    // no speed do Animator por outro motivo.
    private readonly List<Animator> frozen = new List<Animator>();
    private readonly List<float> speeds = new List<float>();
    private float pauseEnd;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        combat.OnAttackHit += HandleHit;

        if (health != null)
            health.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        combat.OnAttackHit -= HandleHit;

        if (health != null)
            health.OnDamaged -= HandleDamaged;

        Unfreeze();
    }

    private void HandleHit(Health alvo, float dano)
    {
        // O golpe final do combo pesa igual ao que mata: e a recompensa
        // de ter fechado a sequencia.
        bool forte = alvo.IsDead || combat.IsFinisher || combat.LastHitWasCrit;

        IsometricCameraFollow.Tremer(forte ? killShake : hitShake);

        Freeze(gameObject);
        Freeze(alvo.gameObject);

        // Golpe emendado estica a pausa, nao soma: dois acertos colados
        // nao podem virar uma travada longa.
        pauseEnd = Mathf.Max(
            pauseEnd,
            Time.unscaledTime + (forte ? killPause : hitPause));
    }

    private void HandleDamaged(float dano, GameObject quemBateu)
    {
        IsometricCameraFollow.Tremer(hurtShake);
    }

    private void Freeze(GameObject quem)
    {
        foreach (var animator in quem.GetComponentsInChildren<Animator>())
        {
            if (frozen.Contains(animator))
                continue;

            frozen.Add(animator);
            speeds.Add(animator.speed);
            animator.speed = 0f;
        }
    }

    private void Update()
    {
        // Tempo sem escala: se o jogo pausar no meio do impacto, a pausa
        // termina igual e ninguem fica congelado depois do menu.
        if (frozen.Count > 0 && Time.unscaledTime >= pauseEnd)
            Unfreeze();
    }

    private void Unfreeze()
    {
        for (int i = 0; i < frozen.Count; i++)
        {
            // O alvo pode ter morrido e sumido durante a pausa.
            if (frozen[i] != null)
                frozen[i].speed = speeds[i];
        }

        frozen.Clear();
        speeds.Clear();
    }
}
