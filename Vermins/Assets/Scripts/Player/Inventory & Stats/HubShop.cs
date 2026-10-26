using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// A conta da loja do hub (secao 4.4 do GDR): vender joias e equipamento,
/// comprar pocao.
///
/// So a conta, sem tela. A tela e do Rogger: ela pega este componente no
/// Player e chama os metodos daqui. Cada um devolve true se a troca
/// aconteceu, e false sem mexer em nada. O motivo da recusa a tela le
/// direto (cinto.IsFull, carteira.Amount), entao eu nao repito aqui.
///
/// Fica no Player, e nao num NPC da cena do hub, porque carteira, bolsa,
/// cinto e equipamento ja moram nele. Assim a loja nao depende de achar
/// nada na cena, e o teste roda em qualquer cena com o Player.
///
/// O preco do equipamento sai da raridade, e nao de um campo no item: item
/// novo ja nasce com preco, sem ninguem lembrar de preencher.
///
/// NAO DUPLICA ITEM: vender equipamento paga pelo que o Unequip
/// devolveu. Slot vazio devolve null, e null nao vale ouro. A mochila do
/// InventorySystem (do Rogger) segue a mesma regra pelo TakeItem.
///
/// A mochila e opcional. O InventorySystem ainda nao esta no Player.prefab,
/// so no Player da HUBCity e da DungeonComPlayer. Sem ele, vender da
/// mochila devolve false e o resto da loja funciona igual.
///
/// Os precos sao de teste. Medi pelos drops do Enemy.prefab: cada abate
/// rende uns 4 de ouro (40% de 5 a 15), uns 2 de joia (5% de 30 a 60) e
/// uns 2 de equipamento vendido (15% de chance, 70/25/5 de raridade).
/// Sao ~8 por abate, entao a pocao de 25 custa uns 3 abates e encher o
/// cinto de 5 custa uns 15.
/// </summary>
[RequireComponent(typeof(GoldWallet), typeof(JewelPouch), typeof(PotionBelt))]
[RequireComponent(typeof(CharacterEquipment))]
public class HubShop : MonoBehaviour
{
    [Header("Compra")]
    [Tooltip("Quanto custa uma pocao. Valor de teste, nao e definitivo.")]
    [FormerlySerializedAs("precoDaPocao")]
    [SerializeField, Min(1)] private int potionPrice = 25;

    [Header("Venda de equipamento (por raridade)")]
    [FormerlySerializedAs("vendaComum")]
    [SerializeField, Min(1)] private int commonSellPrice = 10;
    [FormerlySerializedAs("vendaMagico")]
    [SerializeField, Min(1)] private int magicSellPrice = 25;
    [FormerlySerializedAs("vendaRaro")]
    [SerializeField, Min(1)] private int rareSellPrice = 60;

    [Header("Venda de joia")]
    [Tooltip("Fracao do valor guardado que a loja paga. Em 1 paga o valor " +
             "inteiro, que e o que o rotulo da joia promete.")]
    [FormerlySerializedAs("fracaoDaJoia")]
    [SerializeField, Range(0.1f, 1f)] private float jewelSellFraction = 1f;

    private GoldWallet wallet;
    private JewelPouch pouch;
    private PotionBelt belt;
    private CharacterEquipment equipment;
    private InventorySystem backpack;

    public int PotionPrice => potionPrice;

    /// <summary>Quanto o SellJewels pagaria agora, pra tela mostrar antes.</summary>
    public int JewelsValue => Mathf.RoundToInt(pouch.TotalValue * jewelSellFraction);

    /// <summary>Quantos itens a mochila tem. 0 sem mochila.</summary>
    public int BackpackCount
    {
        get
        {
            if (backpack == null)
                return 0;

            int quantos = 0;
            for (int i = 0; i < InventorySystem.SlotCount; i++)
            {
                if (backpack.GetItem(i) != null)
                    quantos++;
            }
            return quantos;
        }
    }

    /// <summary>Quanto o SellBackpack pagaria agora, pra tela mostrar antes.</summary>
    public int BackpackValue
    {
        get
        {
            if (backpack == null)
                return 0;

            int total = 0;
            for (int i = 0; i < InventorySystem.SlotCount; i++)
                total += SellPrice(backpack.GetItem(i));
            return total;
        }
    }

    private void Awake()
    {
        wallet = GetComponent<GoldWallet>();
        pouch = GetComponent<JewelPouch>();
        belt = GetComponent<PotionBelt>();
        equipment = GetComponent<CharacterEquipment>();
        backpack = GetComponent<InventorySystem>();
    }

    /// <summary>Quanto a loja paga por este item. Sem item, 0.</summary>
    public int SellPrice(EquipmentItem item)
    {
        if (item == null)
            return 0;

        switch (item.Rarity)
        {
            case Rarity.Magic: return magicSellPrice;
            case Rarity.Rare: return rareSellPrice;
            default: return commonSellPrice;
        }
    }

    /// <summary>
    /// Compra uma pocao. False com o cinto cheio ou sem ouro, e ai nada
    /// muda. Confiro as duas coisas antes de mexer em qualquer uma: se eu
    /// cobrasse primeiro, o cinto cheio comeria o ouro.
    /// </summary>
    public bool BuyPotion()
    {
        if (belt.IsFull || wallet.Amount < potionPrice)
            return false;

        belt.TryStore();
        wallet.TrySpend(potionPrice);
        return true;
    }

    /// <summary>Vende todas as joias. False com a bolsa vazia.</summary>
    public bool SellJewels()
    {
        if (pouch.Count <= 0)
            return false;

        // Leio o valor antes de esvaziar: depois do TakeAll ele e 0.
        int pago = JewelsValue;
        pouch.TakeAll();
        wallet.Add(pago);
        return true;
    }

    /// <summary>
    /// Vende o item vestido neste slot. False com o slot vazio. Tirar o item
    /// baixa vida e dano na hora, pelo OnEquipamentoMudou de sempre; a vida
    /// atual so e cortada se passar do teto novo.
    /// </summary>
    public bool SellEquipped(EquipmentSlot slot)
    {
        EquipmentItem vendido = equipment.Unequip(slot);
        if (vendido == null)
            return false;

        wallet.Add(SellPrice(vendido));
        return true;
    }

    /// <summary>
    /// Vende o item deste espaco da mochila. False com o espaco vazio ou
    /// sem mochila. E o que a tela do inventario chama.
    /// </summary>
    public bool SellFromBackpack(int index)
    {
        if (backpack == null)
            return false;

        EquipmentItem vendido = backpack.TakeItem(index);
        if (vendido == null)
            return false;

        wallet.Add(SellPrice(vendido));
        return true;
    }

    /// <summary>
    /// Vende tudo da mochila de uma vez. False com ela vazia. Com 12
    /// espacos, vender um por um na loja de debug seriam 12 botoes.
    /// </summary>
    public bool SellBackpack()
    {
        bool vendeu = false;
        for (int i = 0; i < InventorySystem.SlotCount; i++)
            vendeu |= SellFromBackpack(i);
        return vendeu;
    }

    // Menus pra testar no jogo antes da tela do Rogger existir. O log so
    // roda no clique, nunca por frame.

    [ContextMenu("Teste: ganhar 100 de ouro")]
    private void AddTestGold()
    {
        wallet.Add(100);
    }

    [ContextMenu("Comprar pocao")]
    private void MenuBuyPotion()
    {
        Debug.Log($"[Loja] Comprar pocao: {BuyPotion()} (ouro {wallet.Amount}, cinto {belt.Count}/{belt.Capacity})", this);
    }

    [ContextMenu("Vender joias")]
    private void MenuSellJewels()
    {
        Debug.Log($"[Loja] Vender joias: {SellJewels()} (ouro {wallet.Amount})", this);
    }

    [ContextMenu("Vender arma")]
    private void MenuSellWeapon() => LogSale(EquipmentSlot.Weapon);

    [ContextMenu("Vender armadura")]
    private void MenuSellArmor() => LogSale(EquipmentSlot.Armor);

    [ContextMenu("Vender anel")]
    private void MenuSellRing() => LogSale(EquipmentSlot.Ring);

    [ContextMenu("Vender mochila")]
    private void MenuSellBackpack()
    {
        Debug.Log($"[Loja] Vender mochila: {SellBackpack()} (ouro {wallet.Amount})", this);
    }

    private void LogSale(EquipmentSlot slot)
    {
        Debug.Log($"[Loja] Vender {slot}: {SellEquipped(slot)} (ouro {wallet.Amount})", this);
    }
}
