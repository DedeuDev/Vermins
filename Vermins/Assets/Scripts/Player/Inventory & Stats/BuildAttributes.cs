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
/// reescrever nada. Hoje quem escolhe e quem arrasta o asset. O
/// equipamento soma pontos por cima destes, que e o que a §4.3 descreve:
/// as contas "Com" abaixo recebem esses pontos.
///
/// O ORCAMENTO E O QUE FAZ ISSO SER ESCOLHA. Toda build gasta os mesmos
/// 20 pontos. Sem isso "subir alcance" seria so ficar mais forte, e nao
/// uma decisao - a build so significa alguma coisa quando ganhar de um
/// lado custa do outro.
/// </summary>
[CreateAssetMenu(fileName = "NovaBuild", menuName = "Vermins/Build do Personagem")]
public class BuildAttributes : ScriptableObject
{
    public const int Budget = 20;
    public const int MinPerAttribute = 1;
    public const int MaxPerAttribute = 10;

    [Header("Identidade")]
    [SerializeField] private string nomeDaBuild = "Sem nome";

    [Tooltip("Como esta build joga, em uma frase. Aparece so aqui, mas " +
             "e o que impede a gente de criar duas builds que na pratica " +
             "sao a mesma coisa com numeros diferentes.")]
    [TextArea(2, 4)]
    [SerializeField] private string comoElaJoga;

    [Header("Atributos (somam 20)")]
    [Tooltip("Forca da magia. Vira dano por golpe.")]
    [Range(MinPerAttribute, MaxPerAttribute)]
    [SerializeField] private int poder = 5;

    [Tooltip("Rapidez do conjurador. Diminui o tempo entre um golpe e o " +
             "proximo. A animacao acompanha sozinha - o Animator le a " +
             "velocidade por parametro e nao gravada.")]
    [Range(MinPerAttribute, MaxPerAttribute)]
    [SerializeField] private int celeridade = 5;

    [Tooltip("Quanto o corpo aguenta. Vira vida maxima.")]
    [Range(MinPerAttribute, MaxPerAttribute)]
    [SerializeField] private int vitalidade = 5;

    [Tooltip("De quao longe a magia sai. E o atributo mais perigoso dos " +
             "quatro: a 9 m o jogador ja mata um inimigo sozinho sem " +
             "levar dano nenhum. So nao desbalanceia porque subir ele " +
             "custa dano ou vida, que e pra isso que o orcamento existe.")]
    [Range(MinPerAttribute, MaxPerAttribute)]
    [SerializeField] private int alcance = 5;

    // As bases foram escolhidas pra que uma build 5/5/5/5 devolva
    // EXATAMENTE os numeros que o jogo ja tinha antes dos atributos
    // existirem: 15 de dano, 1,2 s de cooldown, 100 de vida e 9 m de
    // alcance. Assim da pra provar que ligar isto nao mexeu no
    // balanceamento - a build equilibrada e o jogo de ontem.
    private const float BaseDamage = 5f;
    private const float DamagePerPoint = 2f;

    private const float BaseCooldown = 1.8f;
    private const float HastePerPoint = 0.1f;

    private const float BaseHealth = 50f;
    private const float HealthPerPoint = 10f;

    private const float BaseRange = 4f;
    private const float RangePerPoint = 1f;

    public string BuildName => string.IsNullOrWhiteSpace(nomeDaBuild) ? name : nomeDaBuild;
    public string Playstyle => comoElaJoga;

    public int Power => poder;
    public int Haste => celeridade;
    public int Vitality => vitalidade;
    public int Range => alcance;

    public int TotalSpent => poder + celeridade + vitalidade + alcance;

    // Sem equipamento. E o numero que serve pra comparar uma build com
    // a outra.
    public float Damage => DamageWith(0);
    public float Cooldown => CooldownWith(0);
    public float MaxHealth => MaxHealthWith(0);

    // As contas "Com" recebem os pontos que o equipamento soma por cima
    // da build. Somo no ponto, e nao no resultado, pra que um ponto de
    // anel valha o mesmo que um ponto de build.
    public float DamageWith(int poderExtra) => BaseDamage + (poder + poderExtra) * DamagePerPoint;

    /// <summary>
    /// Segundos entre um golpe e o proximo.
    ///
    /// Divido em vez de subtrair de proposito. Subtraindo, um ponto a
    /// mais sempre tira a mesma fatia e em algum momento o cooldown
    /// chega a zero ou vira negativo, e ai o jogador atira infinitas
    /// vezes por frame. Dividindo, cada ponto rende menos que o
    /// anterior e o numero nunca chega a zero: com celeridade 10 o
    /// cooldown para em 0,9 s.
    ///
    /// Com equipamento a celeridade passa de 10, e e ai que dividir
    /// importa: 10 da build e +9 de tres raros param em 0,62 s.
    /// </summary>
    public float CooldownWith(int celeridadeExtra) =>
        BaseCooldown / (1f + (celeridade + celeridadeExtra) * HastePerPoint);

    public float MaxHealthWith(int vitalidadeExtra) => BaseHealth + (vitalidade + vitalidadeExtra) * HealthPerPoint;

    public float RangeInMeters => BaseRange + alcance * RangePerPoint;

    private void OnValidate()
    {
        if (TotalSpent != Budget)
        {
            Debug.LogWarning(
                $"[{name}] Esta build gasta {TotalSpent} pontos e o orcamento e " +
                $"{Budget}. Ela vai funcionar do mesmo jeito, mas nao da mais " +
                "pra comparar com as outras - quem gasta mais so fica melhor em " +
                "tudo, e ai nao e escolha de build.", this);
        }
    }
}
