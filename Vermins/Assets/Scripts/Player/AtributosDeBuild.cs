using UnityEngine;

/// <summary>
/// Uma build do personagem: quatro atributos e as contas que transformam
/// eles nos numeros que o combate usa de verdade.
///
/// Existe porque a §4.2 do GDR pede "pelo menos uma forma clara de
/// diferenciacao da build", e era o unico item do combate que ainda nao
/// tinha nada. O texto tambem pede pra limitar escopo, entao aqui nao
/// tem arvore de habilidade nem classe - sao quatro numeros.
///
/// E ScriptableObject e nao campo no Player porque assim da pra ter
/// varias builds prontas lado a lado e trocar de uma pra outra sem
/// reescrever nada. Hoje quem escolhe e quem arrasta o asset; quando o
/// inventario do grupo existir, o equipamento passa a somar pontos por
/// cima destes - que e o que a §4.3 descreve.
///
/// O ORCAMENTO E O QUE FAZ ISSO SER ESCOLHA. Toda build gasta os mesmos
/// 20 pontos. Sem isso "subir alcance" seria so ficar mais forte, e nao
/// uma decisao - a build so significa alguma coisa quando ganhar de um
/// lado custa do outro.
/// </summary>
[CreateAssetMenu(fileName = "NovaBuild", menuName = "Vermins/Build do Personagem")]
public class AtributosDeBuild : ScriptableObject
{
    public const int Orcamento = 20;
    public const int MinimoPorAtributo = 1;
    public const int MaximoPorAtributo = 10;

    [Header("Identidade")]
    [SerializeField] private string nomeDaBuild = "Sem nome";

    [Tooltip("Como esta build joga, em uma frase. Aparece so aqui, mas " +
             "e o que impede a gente de criar duas builds que na pratica " +
             "sao a mesma coisa com numeros diferentes.")]
    [TextArea(2, 4)]
    [SerializeField] private string comoElaJoga;

    [Header("Atributos (somam 20)")]
    [Tooltip("Forca da magia. Vira dano por golpe.")]
    [Range(MinimoPorAtributo, MaximoPorAtributo)]
    [SerializeField] private int poder = 5;

    [Tooltip("Rapidez do conjurador. Diminui o tempo entre um golpe e o " +
             "proximo. A animacao acompanha sozinha - o Animator le a " +
             "velocidade por parametro e nao gravada.")]
    [Range(MinimoPorAtributo, MaximoPorAtributo)]
    [SerializeField] private int celeridade = 5;

    [Tooltip("Quanto o corpo aguenta. Vira vida maxima.")]
    [Range(MinimoPorAtributo, MaximoPorAtributo)]
    [SerializeField] private int vitalidade = 5;

    [Tooltip("De quao longe a magia sai. E o atributo mais perigoso dos " +
             "quatro: a 9 m o jogador ja mata um inimigo sozinho sem " +
             "levar dano nenhum. So nao desbalanceia porque subir ele " +
             "custa dano ou vida, que e pra isso que o orcamento existe.")]
    [Range(MinimoPorAtributo, MaximoPorAtributo)]
    [SerializeField] private int alcance = 5;

    // As bases foram escolhidas pra que uma build 5/5/5/5 devolva
    // EXATAMENTE os numeros que o jogo ja tinha antes dos atributos
    // existirem: 15 de dano, 1,2 s de cooldown, 100 de vida e 9 m de
    // alcance. Assim da pra provar que ligar isto nao mexeu no
    // balanceamento - a build equilibrada e o jogo de ontem.
    private const float DanoBase = 5f;
    private const float DanoPorPonto = 2f;

    private const float CooldownBase = 1.8f;
    private const float CeleridadePorPonto = 0.1f;

    private const float VidaBase = 50f;
    private const float VidaPorPonto = 10f;

    private const float AlcanceBase = 4f;
    private const float AlcancePorPonto = 1f;

    public string NomeDaBuild => string.IsNullOrWhiteSpace(nomeDaBuild) ? name : nomeDaBuild;
    public string ComoElaJoga => comoElaJoga;

    public int Poder => poder;
    public int Celeridade => celeridade;
    public int Vitalidade => vitalidade;
    public int Alcance => alcance;

    public int TotalGasto => poder + celeridade + vitalidade + alcance;

    public float Dano => DanoBase + poder * DanoPorPonto;

    /// <summary>
    /// Segundos entre um golpe e o proximo.
    ///
    /// Divido em vez de subtrair de proposito. Subtraindo, um ponto a
    /// mais sempre tira a mesma fatia e em algum momento o cooldown
    /// chega a zero ou vira negativo, e ai o jogador atira infinitas
    /// vezes por frame. Dividindo, cada ponto rende menos que o
    /// anterior e o numero nunca chega a zero: com celeridade 10 o
    /// cooldown para em 0,9 s.
    /// </summary>
    public float Cooldown => CooldownBase / (1f + celeridade * CeleridadePorPonto);

    public float VidaMaxima => VidaBase + vitalidade * VidaPorPonto;

    public float AlcanceEmMetros => AlcanceBase + alcance * AlcancePorPonto;

    private void OnValidate()
    {
        if (TotalGasto != Orcamento)
        {
            Debug.LogWarning(
                $"[{name}] Esta build gasta {TotalGasto} pontos e o orcamento e " +
                $"{Orcamento}. Ela vai funcionar do mesmo jeito, mas nao da mais " +
                "pra comparar com as outras - quem gasta mais so fica melhor em " +
                "tudo, e ai nao e escolha de build.", this);
        }
    }
}
