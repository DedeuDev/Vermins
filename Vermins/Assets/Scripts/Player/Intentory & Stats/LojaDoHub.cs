using UnityEngine;

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
/// NAO DUPLICA ITEM: vender equipamento paga pelo que o Desequipar
/// devolveu. Slot vazio devolve null, e null nao vale ouro.
///
/// Os precos sao de teste. Medi pelos drops do Enemy.prefab: cada abate
/// rende uns 4 de ouro (40% de 5 a 15), uns 2 de joia (5% de 30 a 60) e
/// uns 2 de equipamento vendido (15% de chance, 70/25/5 de raridade).
/// Sao ~8 por abate, entao a pocao de 25 custa uns 3 abates e encher o
/// cinto de 5 custa uns 15.
/// </summary>
[RequireComponent(typeof(GoldWallet), typeof(JewelPouch), typeof(PotionBelt))]
[RequireComponent(typeof(EquipamentoDoPersonagem))]
public class LojaDoHub : MonoBehaviour
{
    [Header("Compra")]
    [Tooltip("Quanto custa uma pocao. Valor de teste, nao e definitivo.")]
    [SerializeField, Min(1)] private int precoDaPocao = 25;

    [Header("Venda de equipamento (por raridade)")]
    [SerializeField, Min(1)] private int vendaComum = 10;
    [SerializeField, Min(1)] private int vendaMagico = 25;
    [SerializeField, Min(1)] private int vendaRaro = 60;

    [Header("Venda de joia")]
    [Tooltip("Fracao do valor guardado que a loja paga. Em 1 paga o valor " +
             "inteiro, que e o que o rotulo da joia promete.")]
    [SerializeField, Range(0.1f, 1f)] private float fracaoDaJoia = 1f;

    private GoldWallet carteira;
    private JewelPouch bolsa;
    private PotionBelt cinto;
    private EquipamentoDoPersonagem equipamento;

    public int PrecoDaPocao => precoDaPocao;

    /// <summary>Quanto o VenderJoias pagaria agora, pra tela mostrar antes.</summary>
    public int ValorDasJoias => Mathf.RoundToInt(bolsa.TotalValue * fracaoDaJoia);

    private void Awake()
    {
        carteira = GetComponent<GoldWallet>();
        bolsa = GetComponent<JewelPouch>();
        cinto = GetComponent<PotionBelt>();
        equipamento = GetComponent<EquipamentoDoPersonagem>();
    }

    /// <summary>Quanto a loja paga por este item. Sem item, 0.</summary>
    public int ValorDeVenda(ItemDeEquipamento item)
    {
        if (item == null)
            return 0;

        switch (item.Raridade)
        {
            case Raridade.Magico: return vendaMagico;
            case Raridade.Raro: return vendaRaro;
            default: return vendaComum;
        }
    }

    /// <summary>
    /// Compra uma pocao. False com o cinto cheio ou sem ouro, e ai nada
    /// muda. Confiro as duas coisas antes de mexer em qualquer uma: se eu
    /// cobrasse primeiro, o cinto cheio comeria o ouro.
    /// </summary>
    public bool ComprarPocao()
    {
        if (cinto.IsFull || carteira.Amount < precoDaPocao)
            return false;

        cinto.TryStore();
        carteira.TryGastar(precoDaPocao);
        return true;
    }

    /// <summary>Vende todas as joias. False com a bolsa vazia.</summary>
    public bool VenderJoias()
    {
        if (bolsa.Count <= 0)
            return false;

        // Leio o valor antes de esvaziar: depois do TirarTodas ele e 0.
        int pago = ValorDasJoias;
        bolsa.TirarTodas();
        carteira.Add(pago);
        return true;
    }

    /// <summary>
    /// Vende o item vestido neste slot. False com o slot vazio. Tirar o item
    /// baixa vida e dano na hora, pelo OnEquipamentoMudou de sempre; a vida
    /// atual so e cortada se passar do teto novo.
    /// </summary>
    public bool VenderEquipado(SlotDeEquipamento slot)
    {
        ItemDeEquipamento vendido = equipamento.Desequipar(slot);
        if (vendido == null)
            return false;

        carteira.Add(ValorDeVenda(vendido));
        return true;
    }

    // Menus pra testar no jogo antes da tela do Rogger existir. O log so
    // roda no clique, nunca por frame.

    [ContextMenu("Teste: ganhar 100 de ouro")]
    private void GanharOuroDeTeste()
    {
        carteira.Add(100);
    }

    [ContextMenu("Comprar pocao")]
    private void MenuComprarPocao()
    {
        Debug.Log($"[Loja] Comprar pocao: {ComprarPocao()} (ouro {carteira.Amount}, cinto {cinto.Count}/{cinto.Capacity})", this);
    }

    [ContextMenu("Vender joias")]
    private void MenuVenderJoias()
    {
        Debug.Log($"[Loja] Vender joias: {VenderJoias()} (ouro {carteira.Amount})", this);
    }

    [ContextMenu("Vender arma")]
    private void MenuVenderArma() => LogVenda(SlotDeEquipamento.Arma);

    [ContextMenu("Vender armadura")]
    private void MenuVenderArmadura() => LogVenda(SlotDeEquipamento.Armadura);

    [ContextMenu("Vender anel")]
    private void MenuVenderAnel() => LogVenda(SlotDeEquipamento.Anel);

    private void LogVenda(SlotDeEquipamento slot)
    {
        Debug.Log($"[Loja] Vender {slot}: {VenderEquipado(slot)} (ouro {carteira.Amount})", this);
    }
}
