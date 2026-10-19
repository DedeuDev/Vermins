using UnityEngine;

/// <summary>
/// Equipamento no chao: arma, armadura ou anel que caiu de um inimigo.
///
/// Quem cria e o DropOnDeath, que sorteia o item e chama o Definir logo
/// depois do Instantiate. O rotulo so e montado no Start do Coletavel, um
/// frame depois, entao o nome e a cor ja estao certos quando ele aparece.
///
/// Encostar veste o item se o slot dele estiver vazio. Com o slot
/// ocupado o item fica no chao: trocar pelo que ja esta vestido e coisa
/// da tela de inventario do Rogger, que ainda nao existe.
/// </summary>
public class ItemNoChao : Coletavel
{
    private ItemDeEquipamento item;

    public ItemDeEquipamento Item => item;

    protected override string TextoDoRotulo => item != null ? item.NomeDoItem : "?";

    // Branco, azul e amarelo, como nos dois jogos: da pra saber se vale a
    // pena ir buscar sem ler o nome.
    protected override Color CorDoRotulo =>
        item != null ? ItemDeEquipamento.CorDaRaridade(item.Raridade) : Color.white;

    public void Definir(ItemDeEquipamento novoItem)
    {
        item = novoItem;
    }

    protected override bool Entregar(Collider quemEncostou)
    {
        // Inimigo passando por cima nao tem equipamento e nao leva.
        var equipamento = quemEncostou.GetComponentInParent<EquipamentoDoPersonagem>();

        if (item == null || equipamento == null)
            return false;

        // Nao troco sozinho. Trocar soltaria o item velho aqui mesmo, em
        // cima do jogador, e ele pegaria de volta na hora ao andar.
        if (equipamento.NoSlot(item.Slot) != null)
            return false;

        equipamento.Equipar(item);
        return true;
    }
}
