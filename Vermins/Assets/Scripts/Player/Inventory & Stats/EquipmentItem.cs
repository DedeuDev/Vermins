using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum EquipmentSlot
{
    Weapon,
    Armor,
    Ring
}

/// <summary>
/// Em ordem do mais fraco pro mais forte, pra que comparar raridade com
/// "maior que" funcione quando o drop precisar.
/// </summary>
public enum Rarity
{
    Common,
    Magic,
    Rare
}

/// <summary>
/// Pontos de atributo que vem de fora da build. Hoje somam o equipamento
/// e os pontos livres do nivel; se uma habilidade der buff na fase 3,
/// entra pelo mesmo lugar.
///
/// Nao tem Alcance de proposito (ver o EquipmentItem).
/// </summary>
public readonly struct BonusPoints
{
    public readonly int Power;
    public readonly int Haste;
    public readonly int Vitality;

    public BonusPoints(int power, int haste, int vitality)
    {
        Power = power;
        Haste = haste;
        Vitality = vitality;
    }

    public int Total => Power + Haste + Vitality;

    public static BonusPoints operator +(BonusPoints a, BonusPoints b)
    {
        return new BonusPoints(
            a.Power + b.Power,
            a.Haste + b.Haste,
            a.Vitality + b.Vitality);
    }
}

/// <summary>
/// Um item que o Player veste: em que slot vai, quao raro ele e e quantos
/// pontos de atributo ele soma por cima da build.
///
/// A secao 4.3 do GDR diz que o equipamento soma por cima dos atributos,
/// entao o item nao tem dano nem vida proprios. Ele tem pontos, e quem
/// transforma ponto em numero continua sendo o BuildAttributes. Assim um
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
public class EquipmentItem : ScriptableObject
{
    // O teto do raro. Somando com a build, o atributo pode passar de 10:
    // o 10 e o limite da escolha da build, e o equipamento e a recompensa
    // por cima dela.
    public const int MaxPerAttribute = 3;

    [Header("Interface")]
    public Sprite icon;

    [Header("Identidade")]
    [FormerlySerializedAs("nomeDoItem")]
    [SerializeField] private string itemName = "Sem nome";
    [SerializeField] private EquipmentSlot slot;
    [FormerlySerializedAs("raridade")]
    [SerializeField] private Rarity rarity;

    [Header("Pontos (somam ate o teto da raridade)")]
    [Tooltip("Vira dano por golpe, igual ao Poder da build.")]
    [Range(0, MaxPerAttribute)]
    [FormerlySerializedAs("poder")]
    [SerializeField] private int power;

    [Tooltip("Diminui o tempo entre golpes, igual a Celeridade da build.")]
    [Range(0, MaxPerAttribute)]
    [FormerlySerializedAs("celeridade")]
    [SerializeField] private int haste;

    [Tooltip("Vira vida maxima, igual a Vitalidade da build. Equipar nao " +
             "enche a vida: o teto sobe e a vida atual fica onde estava.")]
    [Range(0, MaxPerAttribute)]
    [FormerlySerializedAs("vitalidade")]
    [SerializeField] private int vitality;

    public string ItemName => string.IsNullOrWhiteSpace(itemName) ? name : itemName;
    public EquipmentSlot Slot => slot;
    public Rarity Rarity => rarity;

    public BonusPoints Bonus => new BonusPoints(power, haste, vitality);

    /// <summary>Quantos pontos um item desta raridade pode somar no total.</summary>
    public static int PointsForRarity(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Magic: return 2;
            case Rarity.Rare: return 3;
            default: return 1;
        }
    }

    // Pasta dentro de um Resources. O save grava o nome do asset, e nao o
    // asset, porque ScriptableObject nao vai pro JSON. Pus os itens num
    // Resources em vez de numa lista arrastada no Player: item novo e so
    // soltar o arquivo na pasta, sem lembrar de ligar nada. O custo e que
    // renomear um asset faz ele sumir dos saves antigos (o slot volta
    // vazio, com aviso).
    private const string ResourcesFolder = "Items";

    private static Dictionary<string, EquipmentItem> byName;

    // Campo estatico nao zera sozinho quando o editor entra em Play sem
    // recarregar o dominio. Sem isto, um item criado entre dois Play nao
    // entraria no dicionario.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        byName = null;
    }

    /// <summary>
    /// Acha o item pelo nome do asset, o mesmo que o save grava. Nome vazio
    /// devolve null (slot vazio). Monta o dicionario na primeira chamada:
    /// so roda no load, nunca por frame.
    /// </summary>
    public static EquipmentItem FindByName(string nomeDoAsset)
    {
        if (string.IsNullOrEmpty(nomeDoAsset))
            return null;

        if (byName == null)
        {
            byName = new Dictionary<string, EquipmentItem>();

            foreach (EquipmentItem item in Resources.LoadAll<EquipmentItem>(ResourcesFolder))
                byName[item.name] = item;
        }

        if (byName.TryGetValue(nomeDoAsset, out EquipmentItem achado))
            return achado;

        Debug.LogWarning(
            $"Item '{nomeDoAsset}' do save nao existe em Resources/{ResourcesFolder}. " +
            "Foi renomeado ou apagado? O slot fica vazio.");
        return null;
    }

    /// <summary>O que o save grava: o nome do asset, ou vazio sem item.</summary>
    public static string SaveName(EquipmentItem item)
    {
        return item != null ? item.name : string.Empty;
    }

    /// <summary>
    /// A cor do nome do item: comum branco, magico azul, raro amarelo.
    /// Fica aqui, e nao no rotulo do chao, porque o HUD e a tela de
    /// inventario do Rogger vao precisar da mesma cor.
    /// </summary>
    public static Color RarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Magic: return new Color(0.45f, 0.6f, 1f);
            case Rarity.Rare: return new Color(1f, 0.9f, 0.25f);
            default: return Color.white;
        }
    }

    private void OnValidate()
    {
        int total = power + haste + vitality;
        int teto = PointsForRarity(rarity);

        // Aviso em vez de cortar, igual a build: mexer no numero sozinho
        // no OnValidate apagaria o que alguem acabou de digitar.
        if (total > teto)
        {
            Debug.LogWarning(
                $"[{name}] Este item {rarity} soma {total} pontos e o teto " +
                $"de {rarity} e {teto}. Ele funciona do mesmo jeito, mas ai " +
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
