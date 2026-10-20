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
public class DamageNumbers : MonoBehaviour
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

    [Tooltip("Cor do critico. Amarelo, como no Diablo 4. Se o critico " +
             "mata, fica o laranja da morte e o ! continua dizendo que foi " +
             "critico.")]
    [SerializeField] private Color corDoCritico = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Quanto o critico sai maior.")]
    [SerializeField] private float escalaDoCritico = 1.5f;

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

    private class Entry
    {
        public TextMeshPro label;
        public Vector3 startPosition;
        public float spawnTime = -1f;
        public float scale;
    }

    private PlayerCombat combat;
    private readonly List<Entry> entries = new List<Entry>();
    private Transform root;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();

        root = new GameObject("DamageNumbers").transform;

        for (int i = 0; i < quantosTextos; i++)
            entries.Add(CreateEntry(i));
    }

    private void OnEnable()
    {
        combat.OnAttackHit += HandleHit;
    }

    private void OnDisable()
    {
        combat.OnAttackHit -= HandleHit;
    }

    private void OnDestroy()
    {
        if (root != null)
            Destroy(root.gameObject);
    }

    private Entry CreateEntry(int i)
    {
        var objeto = new GameObject("Numero" + i);
        objeto.transform.SetParent(root, false);

        var label = objeto.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(4f, 1.5f);

        // Contorno escuro pra ler em cima do chao claro da cidade e do
        // esgoto escuro. Isso cria um material por texto, mas sao so doze.
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(20, 12, 8, 255);

        objeto.SetActive(false);

        return new Entry { label = label };
    }

    private void HandleHit(Health alvo, float dano)
    {
        if (dano <= 0f)
            return;

        bool matou = alvo.IsDead;
        bool critico = combat.LastHitWasCrit;
        Entry n = GetFreeEntry();

        n.label.text = critico ? Mathf.RoundToInt(dano) + "!" : Mathf.RoundToInt(dano).ToString();
        n.label.fontSize = tamanho;
        n.label.color = matou ? corDaMorte : critico ? corDoCritico : corNormal;
        n.scale = Mathf.Max(
            matou ? escalaDaMorte : 1f,
            critico ? escalaDoCritico : 1f,
            combat.IsFinisher ? escalaDoFinal : 1f);

        Vector3 lado = Random.insideUnitSphere * espalhamento;
        lado.y = 0f;
        n.startPosition = TopOf(alvo.gameObject) + Vector3.up * folgaAcimaDaCabeca + lado;
        n.spawnTime = Time.time;

        Camera cam = Camera.main;

        if (cam != null)
            n.label.transform.rotation = cam.transform.rotation;

        n.label.gameObject.SetActive(true);
        Draw(n, 0f);
    }

    /// <summary>
    /// Um texto desocupado, ou o mais velho se estiverem todos no ar.
    /// </summary>
    private Entry GetFreeEntry()
    {
        Entry maisVelho = entries[0];

        foreach (var n in entries)
        {
            if (n.spawnTime < 0f)
                return n;

            if (n.spawnTime < maisVelho.spawnTime)
                maisVelho = n;
        }

        return maisVelho;
    }

    /// <summary>Topo do corpo pelos renderers, ou 2 m acima do pivo.</summary>
    private static Vector3 TopOf(GameObject corpo)
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
        foreach (var n in entries)
        {
            if (n.spawnTime < 0f)
                continue;

            float t = (Time.time - n.spawnTime) / duracao;

            if (t >= 1f)
            {
                n.spawnTime = -1f;
                n.label.gameObject.SetActive(false);
                continue;
            }

            Draw(n, t);
        }
    }

    private void Draw(Entry n, float t)
    {
        // Sobe rapido e assenta (ease-out). Nasce grande e encolhe no
        // primeiro quinto do tempo, que e o "pulo" que faz o olho ir nele.
        // Apaga so na segunda metade, pra dar tempo de ler.
        float subiu = 1f - (1f - t) * (1f - t);
        float pulo = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t / 0.2f));
        float alfa = 1f - Mathf.Clamp01((t - 0.5f) / 0.5f);

        n.label.transform.position = n.startPosition + Vector3.up * (subida * subiu);
        n.label.transform.localScale = Vector3.one * (n.scale * pulo);

        Color c = n.label.color;
        c.a = alfa;
        n.label.color = c;
    }
}
