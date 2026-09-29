using System.Collections.Generic;
using UnityEngine;

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
public class ImpactoDoGolpe : MonoBehaviour
{
    [Header("Pausa no impacto")]
    [Tooltip("Quanto tempo o jogador e o alvo ficam congelados no acerto. " +
             "Acima de uns 0,1 s ja parece travada, e nao pancada.")]
    [SerializeField] private float pausaNoAcerto = 0.06f;

    [Tooltip("Pausa do golpe que mata. Um pouco maior, pra o ultimo golpe " +
             "pesar mais que os outros.")]
    [SerializeField] private float pausaNaMorte = 0.1f;

    [Header("Tremor de camera (trauma de 0 a 1)")]
    [SerializeField] private float tremorNoAcerto = 0.35f;

    [SerializeField] private float tremorNaMorte = 0.55f;

    [Tooltip("Apanhar treme mais que bater: e o aviso de que a vida caiu.")]
    [SerializeField] private float tremorAoApanhar = 0.5f;

    private PlayerCombat combat;
    private Health health;

    // Quem esta congelado e a velocidade que tinha antes. Devolvo a
    // velocidade antiga em vez de 1 pra nao atropelar quem um dia mexer
    // no speed do Animator por outro motivo.
    private readonly List<Animator> congelados = new List<Animator>();
    private readonly List<float> velocidades = new List<float>();
    private float fimDaPausa;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        combat.OnGolpeAcertou += Acertou;

        if (health != null)
            health.OnDamaged += Apanhou;
    }

    private void OnDisable()
    {
        combat.OnGolpeAcertou -= Acertou;

        if (health != null)
            health.OnDamaged -= Apanhou;

        Descongelar();
    }

    private void Acertou(Health alvo, float dano)
    {
        bool matou = alvo.IsDead;

        IsometricCameraFollow.Tremer(matou ? tremorNaMorte : tremorNoAcerto);

        Congelar(gameObject);
        Congelar(alvo.gameObject);

        // Golpe emendado estica a pausa, nao soma: dois acertos colados
        // nao podem virar uma travada longa.
        fimDaPausa = Mathf.Max(
            fimDaPausa,
            Time.unscaledTime + (matou ? pausaNaMorte : pausaNoAcerto));
    }

    private void Apanhou(float dano, GameObject quemBateu)
    {
        IsometricCameraFollow.Tremer(tremorAoApanhar);
    }

    private void Congelar(GameObject quem)
    {
        foreach (var animator in quem.GetComponentsInChildren<Animator>())
        {
            if (congelados.Contains(animator))
                continue;

            congelados.Add(animator);
            velocidades.Add(animator.speed);
            animator.speed = 0f;
        }
    }

    private void Update()
    {
        // Tempo sem escala: se o jogo pausar no meio do impacto, a pausa
        // termina igual e ninguem fica congelado depois do menu.
        if (congelados.Count > 0 && Time.unscaledTime >= fimDaPausa)
            Descongelar();
    }

    private void Descongelar()
    {
        for (int i = 0; i < congelados.Count; i++)
        {
            // O alvo pode ter morrido e sumido durante a pausa.
            if (congelados[i] != null)
                congelados[i].speed = velocidades[i];
        }

        congelados.Clear();
        velocidades.Clear();
    }
}
