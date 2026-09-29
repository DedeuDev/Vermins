using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Numero de dano subindo em cima do inimigo quando a espada acerta. O
/// golpe que mata sai maior e em laranja.
///
/// Os textos sao TextMeshPro no mundo, e nao na tela: assim eles ficam
/// presos no ponto do golpe mesmo com a camera andando atras do jogador.
/// Como a camera nunca gira, basta copiar a rotacao dela uma vez pra o
/// texto ficar de frente.
///
/// Uso um punhado de textos reaproveitados em vez de criar um por golpe.
/// Se todos estiverem ocupados, o mais velho e reciclado.
/// </summary>
[RequireComponent(typeof(PlayerCombat))]
public class NumerosDeDano : MonoBehaviour
{
    [SerializeField] private Color corNormal = new Color(1f, 0.97f, 0.9f, 1f);
    [SerializeField] private Color corDaMorte = new Color(1f, 0.6f, 0.15f, 1f);

    [Tooltip("Tamanho da fonte do TextMeshPro no mundo.")]
    [SerializeField] private float tamanho = 5f;

    [Tooltip("Quanto o golpe que mata sai maior.")]
    [SerializeField] private float escalaDaMorte = 1.4f;

    [Tooltip("Quanto o golpe final do combo sai maior. Fica na cor normal: " +
             "o laranja e so de quem morreu.")]
    [SerializeField] private float escalaDoFinal = 1.25f;

    [Tooltip("Quanto tempo o numero fica na tela.")]
    [SerializeField] private float duracao = 0.8f;

    [Tooltip("Quanto o numero sobe, em metros, ate sumir.")]
    [SerializeField] private float subida = 0.9f;

    [Tooltip("Altura acima do topo do inimigo onde o numero nasce.")]
    [SerializeField] private float folgaAcimaDaCabeca = 0.2f;

    [Tooltip("Espalha um pouco de lado, pra golpes seguidos nao " +
             "escreverem um numero em cima do outro.")]
    [SerializeField] private float espalhamento = 0.35f;

    [SerializeField] private int quantosTextos = 12;

    private class Numero
    {
        public TextMeshPro texto;
        public Vector3 inicio;
        public float nasceu = -1f;
        public float escala;
    }

    private PlayerCombat combat;
    private readonly List<Numero> numeros = new List<Numero>();
    private Transform raiz;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();

        raiz = new GameObject("NumerosDeDano").transform;

        for (int i = 0; i < quantosTextos; i++)
            numeros.Add(CriarNumero(i));
    }

    private void OnEnable()
    {
        combat.OnGolpeAcertou += Acertou;
    }

    private void OnDisable()
    {
        combat.OnGolpeAcertou -= Acertou;
    }

    private void OnDestroy()
    {
        if (raiz != null)
            Destroy(raiz.gameObject);
    }

    private Numero CriarNumero(int i)
    {
        var objeto = new GameObject("Numero" + i);
        objeto.transform.SetParent(raiz, false);

        var texto = objeto.AddComponent<TextMeshPro>();
        texto.alignment = TextAlignmentOptions.Center;
        texto.fontStyle = FontStyles.Bold;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.rectTransform.sizeDelta = new Vector2(4f, 1.5f);

        // Contorno escuro pra ler em cima do chao claro da cidade e do
        // esgoto escuro. Isso cria um material por texto, mas sao so doze.
        texto.outlineWidth = 0.25f;
        texto.outlineColor = new Color32(20, 12, 8, 255);

        objeto.SetActive(false);

        return new Numero { texto = texto };
    }

    private void Acertou(Health alvo, float dano)
    {
        if (dano <= 0f)
            return;

        bool matou = alvo.IsDead;
        Numero n = Livre();

        n.texto.text = Mathf.RoundToInt(dano).ToString();
        n.texto.fontSize = tamanho;
        n.texto.color = matou ? corDaMorte : corNormal;
        n.escala = matou ? escalaDaMorte : combat.GolpeFinal ? escalaDoFinal : 1f;

        Vector3 lado = Random.insideUnitSphere * espalhamento;
        lado.y = 0f;
        n.inicio = TopoDe(alvo.gameObject) + Vector3.up * folgaAcimaDaCabeca + lado;
        n.nasceu = Time.time;

        Camera cam = Camera.main;

        if (cam != null)
            n.texto.transform.rotation = cam.transform.rotation;

        n.texto.gameObject.SetActive(true);
        Desenhar(n, 0f);
    }

    /// <summary>
    /// Um texto desocupado, ou o mais velho se estiverem todos no ar.
    /// </summary>
    private Numero Livre()
    {
        Numero maisVelho = numeros[0];

        foreach (var n in numeros)
        {
            if (n.nasceu < 0f)
                return n;

            if (n.nasceu < maisVelho.nasceu)
                maisVelho = n;
        }

        return maisVelho;
    }

    /// <summary>Topo do corpo pelos renderers, ou 2 m acima do pivo.</summary>
    private static Vector3 TopoDe(GameObject corpo)
    {
        var renderers = corpo.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return corpo.transform.position + Vector3.up * 2f;

        Bounds caixa = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            caixa.Encapsulate(renderers[i].bounds);

        return new Vector3(caixa.center.x, caixa.max.y, caixa.center.z);
    }

    private void Update()
    {
        foreach (var n in numeros)
        {
            if (n.nasceu < 0f)
                continue;

            float t = (Time.time - n.nasceu) / duracao;

            if (t >= 1f)
            {
                n.nasceu = -1f;
                n.texto.gameObject.SetActive(false);
                continue;
            }

            Desenhar(n, t);
        }
    }

    private void Desenhar(Numero n, float t)
    {
        // Sobe rapido e assenta (ease-out). Nasce grande e encolhe no
        // primeiro quinto do tempo, que e o "pulo" que faz o olho ir nele.
        // Apaga so na segunda metade, pra dar tempo de ler.
        float subiu = 1f - (1f - t) * (1f - t);
        float pulo = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t / 0.2f));
        float alfa = 1f - Mathf.Clamp01((t - 0.5f) / 0.5f);

        n.texto.transform.position = n.inicio + Vector3.up * (subida * subiu);
        n.texto.transform.localScale = Vector3.one * (n.escala * pulo);

        Color c = n.texto.color;
        c.a = alfa;
        n.texto.color = c;
    }
}
