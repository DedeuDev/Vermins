using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// HUD de debug do jogador: vida, alvo e velocidade num canto da tela.
///
/// E TEMPORARIO. A UI de verdade e do Rogger - isto aqui existe so pra
/// gente testar combate enxergando os numeros ate a barra dele ficar
/// pronta. Usei OnGUI de proposito: nao cria Canvas nem objeto nenhum,
/// entao nao tem como conflitar com o que ele montar, e o visual cinza
/// nao engana ninguem de que e a UI final. Quando a dele chegar, e so
/// tirar este componente do Player.prefab e apagar o arquivo.
///
/// So desenha no editor e em Development Build. A classe continua
/// existindo na build normal de proposito: se eu sumisse com ela
/// inteira, o Player.prefab ficaria com "Missing Script" na build.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerDebugHUD : MonoBehaviour
{
    [SerializeField] private bool visivel = true;

    [Tooltip("Liga e desliga o HUD, pra ele nao aparecer em print e video.")]
    [SerializeField] private Key teclaParaEsconder = Key.F1;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private Health health;
    private PlayerCombat combat;
    private NavMeshAgent agent;
    private GUIStyle estilo;

    private void Awake()
    {
        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombat>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        // Leio o teclado direto em vez de criar uma acao no
        // InputSystem_Actions. E ferramenta de debug e vai sumir - nao
        // quero deixar acao sobrando no asset do jogo depois.
        Keyboard teclado = Keyboard.current;

        if (teclado != null && teclado[teclaParaEsconder].wasPressedThisFrame)
            visivel = !visivel;
    }

    private void OnGUI()
    {
        if (!visivel)
            return;

        // Escalo pela altura da tela com 1080 de referencia. O OnGUI
        // trabalha em pixel, entao sem isso o texto fica minusculo num
        // monitor 4K e enorme numa janela pequena do editor.
        float escala = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));

        // So da pra criar o estilo aqui dentro: o GUI.skin nao existe
        // fora do OnGUI.
        estilo ??= new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };

        GUILayout.BeginArea(new Rect(16f, 16f, 380f, 190f), GUI.skin.box);

        GUILayout.Label($"<b>DEBUG</b>   ({teclaParaEsconder} esconde)", estilo);

        GUILayout.Label($"Vida   {health.Current:F0} / {health.Max:F0}", estilo);
        DesenharBarra(health.Normalized, new Color(0.75f, 0.15f, 0.12f));

        GUILayout.Label($"Alvo   {DescreverAlvo()}", estilo);

        GUILayout.Label($"Velocidade   {DescreverVelocidade()}", estilo);

        GUILayout.EndArea();
    }

    private void DesenharBarra(float cheio, Color cor)
    {
        Rect fundo = GUILayoutUtility.GetRect(340f, 14f);
        GUI.DrawTexture(fundo, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.15f, 0.15f, 0.15f), 0f, 0f);

        Rect preenchido = fundo;
        preenchido.width *= Mathf.Clamp01(cheio);
        GUI.DrawTexture(preenchido, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, cor, 0f, 0f);
    }

    private string DescreverAlvo()
    {
        // O == null do Unity tambem pega alvo ja destruido, que e o que
        // acontece com o inimigo 2 s depois de morrer.
        Health alvo = combat != null ? combat.Target : null;

        if (alvo == null)
            return "nenhum";

        // Distancia no chao, sem a altura. E essa que o PlayerCombat
        // compara com o alcance, entao e essa que interessa ver.
        Vector3 ate = alvo.transform.position - transform.position;
        ate.y = 0f;

        string texto = $"{alvo.name}  {alvo.Current:F0}/{alvo.Max:F0}  a {ate.magnitude:F1} m";
        return alvo.IsDead ? texto + "  (morto)" : texto;
    }

    private string DescreverVelocidade()
    {
        if (agent == null)
            return "sem NavMeshAgent";

        // A velocidade de verdade do agente contra o maximo dele. Mostro
        // as duas porque "andando a 1.9 de 3.2" e o que denuncia quando
        // a aceleracao ou uma curva estao segurando o personagem.
        Vector3 v = agent.velocity;
        v.y = 0f;

        return $"{v.magnitude:F2} / {agent.speed:F2} m/s";
    }
#endif
}
