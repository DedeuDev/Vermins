using UnityEngine;

public enum SlotDeEquipamento
{
    Arma,
    Armadura,
    Anel
}

/// <summary>
/// Em ordem do mais fraco pro mais forte, pra que comparar raridade com
/// "maior que" funcione quando o drop precisar.
/// </summary>
public enum Raridade
{
    Comum,
    Magico,
    Raro
}

/// <summary>
/// Pontos de atributo que vem de fora da build. Hoje so o equipamento
/// soma; se uma habilidade der buff na fase 3, entra pelo mesmo lugar.
///
/// Nao tem Alcance de proposito (ver o ItemDeEquipamento).
/// </summary>
public readonly struct PontosExtras
{
    public readonly int Poder;
    public readonly int Celeridade;
    public readonly int Vitalidade;

    public PontosExtras(int poder, int celeridade, int vitalidade)
    {
        Poder = poder;
        Celeridade = celeridade;
        Vitalidade = vitalidade;
    }

    public int Total => Poder + Celeridade + Vitalidade;

    public static PontosExtras operator +(PontosExtras a, PontosExtras b)
    {
        return new PontosExtras(
            a.Poder + b.Poder,
            a.Celeridade + b.Celeridade,
            a.Vitalidade + b.Vitalidade);
    }
}

/// <summary>
/// Um item que o Player veste: em que slot vai, quao raro ele e e quantos
/// pontos de atributo ele soma por cima da build.
///
/// A secao 4.3 do GDR diz que o equipamento soma por cima dos atributos,
/// entao o item nao tem dano nem vida proprios. Ele tem pontos, e quem
/// transforma ponto em numero continua sendo o AtributosDeBuild. Assim um
/// ponto de anel vale exatamente um ponto de build, e a conta e uma so.
///
/// SEM ALCANCE DE PROPOSITO. Com Alcance 5 o Player ja bate de 9 m, e o
/// inimigo da dungeon so enxerga ate 5 m. Item com alcance so aumentaria
/// a distancia em que ele mata sem ser visto.
///
/// A raridade diz quantos pontos o item pode ter no total: comum 1,
/// magico 2, raro 3. Com tres raros sao +9, menos da metade dos 20 da
/// build. Quando o drop sortear item (2.2b), e daqui que ele tira o teto.
/// </summary>
[CreateAssetMenu(fileName = "NovoItem", menuName = "Vermins/Item de Equipamento")]
public class ItemDeEquipamento : ScriptableObject
{
    // O teto do raro. Somando com a build, o atributo pode passar de 10:
    // o 10 e o limite da escolha da build, e o equipamento e a recompensa
    // por cima dela.
    public const int MaximoPorAtributo = 3;

    [Header("Identidade")]
    [SerializeField] private string nomeDoItem = "Sem nome";
    [SerializeField] private SlotDeEquipamento slot;
    [SerializeField] private Raridade raridade;

    [Header("Pontos (somam ate o teto da raridade)")]
    [Tooltip("Vira dano por golpe, igual ao Poder da build.")]
    [Range(0, MaximoPorAtributo)]
    [SerializeField] private int poder;

    [Tooltip("Diminui o tempo entre golpes, igual a Celeridade da build.")]
    [Range(0, MaximoPorAtributo)]
    [SerializeField] private int celeridade;

    [Tooltip("Vira vida maxima, igual a Vitalidade da build. Equipar nao " +
             "enche a vida: o teto sobe e a vida atual fica onde estava.")]
    [Range(0, MaximoPorAtributo)]
    [SerializeField] private int vitalidade;

    public string NomeDoItem => string.IsNullOrWhiteSpace(nomeDoItem) ? name : nomeDoItem;
    public SlotDeEquipamento Slot => slot;
    public Raridade Raridade => raridade;

    public PontosExtras Pontos => new PontosExtras(poder, celeridade, vitalidade);

    /// <summary>Quantos pontos um item desta raridade pode somar no total.</summary>
    public static int PontosDaRaridade(Raridade raridade)
    {
        switch (raridade)
        {
            case Raridade.Magico: return 2;
            case Raridade.Raro: return 3;
            default: return 1;
        }
    }

    /// <summary>
    /// A cor do nome do item: comum branco, magico azul, raro amarelo.
    /// Fica aqui, e nao no rotulo do chao, porque o HUD e a tela de
    /// inventario do Rogger vao precisar da mesma cor.
    /// </summary>
    public static Color CorDaRaridade(Raridade raridade)
    {
        switch (raridade)
        {
            case Raridade.Magico: return new Color(0.45f, 0.6f, 1f);
            case Raridade.Raro: return new Color(1f, 0.9f, 0.25f);
            default: return Color.white;
        }
    }

    private void OnValidate()
    {
        int total = poder + celeridade + vitalidade;
        int teto = PontosDaRaridade(raridade);

        // Aviso em vez de cortar, igual a build: mexer no numero sozinho
        // no OnValidate apagaria o que alguem acabou de digitar.
        if (total > teto)
        {
            Debug.LogWarning(
                $"[{name}] Este item {raridade} soma {total} pontos e o teto " +
                $"de {raridade} e {teto}. Ele funciona do mesmo jeito, mas ai " +
                "a raridade deixa de dizer quao forte o item e.", this);
        }
        else if (total == 0)
        {
            Debug.LogWarning(
                $"[{name}] Este item nao soma ponto nenhum. Equipar ele nao " +
                "muda nada.", this);
        }
    }
}
