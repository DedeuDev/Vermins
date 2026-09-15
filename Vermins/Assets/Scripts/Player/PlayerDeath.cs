using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// O que acontece quando o jogador morre: a tela escurece, aparece
/// "Voce morreu" e a cena recarrega.
///
/// Escolhi recarregar em vez de reviver no lugar pra entrega da etapa 2.
/// Recarregar zera tudo de uma vez - inimigos, flechas, poca de veneno,
/// dungeon gerada - sem eu precisar lembrar do estado de cada coisa.
/// Se o GDR pedir respawn de verdade depois, o Health.Revive ja existe e
/// o PlayerController e o PlayerAnimator ja voltam sozinhos com ele.
///
/// Fica no Player.prefab, entao vale em qualquer cena que usar o prefab.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerDeath : MonoBehaviour
{
    [Header("Tempo")]
    [Tooltip("Segundos entre morrer e a tela comecar a escurecer. O " +
             "clipe de morte tem 3.63 s, mas nao precisa ver ate o fim.")]
    [SerializeField] private float esperaAntesDeEscurecer = 1.2f;

    [SerializeField] private float duracaoDoEscurecer = 1f;

    [Tooltip("Segundos com a tela escura antes de recarregar, pro " +
             "jogador conseguir ler.")]
    [SerializeField] private float esperaNaTelaEscura = 1.8f;

    [Header("Tela")]
    [SerializeField] private string mensagem = "Você morreu";
    [SerializeField] private Color corDoTexto = new Color(0.62f, 0.1f, 0.08f);

    [Tooltip("Nao vai ate 1 de proposito: o corpo caido continua " +
             "aparecendo por baixo.")]
    [Range(0f, 1f)]
    [SerializeField] private float escuroMaximo = 0.8f;

    private Health health;
    private bool recarregando;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        health.OnDied -= HandleDied;
    }

    private void HandleDied(Health _)
    {
        // O Health ja nao dispara OnDied duas vezes, mas se um dia
        // alguem chamar Revive e o jogador morrer de novo durante a
        // espera, sem isso seriam dois recarregamentos na fila.
        if (recarregando)
            return;

        recarregando = true;
        StartCoroutine(EscurecerERecarregar());
    }

    private IEnumerator EscurecerERecarregar()
    {
        yield return new WaitForSeconds(esperaAntesDeEscurecer);

        CanvasGroup tela = CriarTela();

        for (float t = 0f; t < duracaoDoEscurecer; t += Time.deltaTime)
        {
            tela.alpha = t / duracaoDoEscurecer;
            yield return null;
        }

        tela.alpha = 1f;

        yield return new WaitForSeconds(esperaNaTelaEscura);

        Recarregar();
    }

    /// <summary>
    /// Monto a tela por codigo em vez de deixar um Canvas pronto no
    /// prefab. Assim o Player.prefab continua funcionando sozinho em
    /// qualquer cena, sem ninguem precisar lembrar de arrastar uma UI
    /// junto. Quando tiver arte de verdade pra essa tela, troco por um
    /// prefab de UI.
    /// </summary>
    private CanvasGroup CriarTela()
    {
        var raiz = new GameObject("TelaDeMorte");

        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var escala = raiz.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);

        // Sem GraphicRaycaster e com blocksRaycasts desligado: a tela
        // nao tem botao, entao nao tem por que engolir clique.
        var grupo = raiz.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;
        grupo.interactable = false;

        var fundo = CriarFilho<Image>(raiz, "Fundo");
        fundo.color = new Color(0f, 0f, 0f, escuroMaximo);
        fundo.raycastTarget = false;

        var texto = CriarFilho<Text>(raiz, "Mensagem");
        texto.text = mensagem;
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = 96;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = corDoTexto;
        texto.raycastTarget = false;

        return grupo;
    }

    private static T CriarFilho<T>(GameObject pai, string nome) where T : Graphic
    {
        var filho = new GameObject(nome, typeof(RectTransform));
        filho.transform.SetParent(pai.transform, false);

        // Esticado na tela inteira.
        var rect = (RectTransform)filho.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return filho.AddComponent<T>();
    }

    private static void Recarregar()
    {
        // Pelo caminho e nao pelo buildIndex. A IA_Test_Scene nao esta
        // no Build Settings e mesmo assim recarregou normal no editor
        // desse jeito. O buildIndex dela no Play veio 3, que e so a
        // proxima vaga depois das 3 cenas da lista - um numero que o
        // editor inventa, entao prefiro nao depender dele.
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }
}
