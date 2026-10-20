
using UnityEngine;

/// <summary>
/// Equipamento no chao: arma, armadura ou anel que caiu de um inimigo.
///
/// Quem cria e o DropOnDeath, que sorteia o item e chama o SetItem logo
/// depois do Instantiate. O rotulo so e montado no Start do Pickup, um
/// frame depois, entao o nome e a cor ja estao certos quando ele aparece.
///
/// Ao encostar, o jogador equipa o item se o slot correspondente estiver
/// vazio. Se o slot estiver ocupado, o item e guardado na mochila, caso
/// exista espaco. Se nao for possivel equipar nem guardar, ele permanece
/// no chao para que o jogador possa pega-lo depois.
/// </summary>
public class EquipmentPickup : Pickup
{
    private EquipmentItem item;

    public EquipmentItem Item => item;

    protected override string LabelText => item != null ? item.ItemName : "?";

    // Branco, azul e amarelo, como nos dois jogos: da pra saber se vale a
    // pena ir buscar sem ler o nome.
    protected override Color LabelColor =>
        item != null ? EquipmentItem.RarityColor(item.Rarity) : Color.white;

    public void SetItem(EquipmentItem novoItem)
    {
        item = novoItem;
    }

    protected override bool Deliver(Collider quemEncostou)
    {
        // Inimigo passando por cima nao tem equipamento e nao leva.
        var equipamento = quemEncostou.GetComponentInParent<CharacterEquipment>();

        if (item == null || equipamento == null)
            return false;

        // Se o slot estiver vazio, equipa normalmente.
        if (equipamento.GetItem(item.Slot) == null)
        {
            equipamento.Equip(item);
            return true;
        }

        // Com o slot ocupado, tenta guardar o equipamento na mochila.
        var inventario = quemEncostou.GetComponentInParent<InventorySystem>();

        if (inventario != null && inventario.TryAddEquipment(item))
            return true;

        // Se a mochila estiver cheia ou indisponivel, o item fica no chao.
        return false;
    }
}
