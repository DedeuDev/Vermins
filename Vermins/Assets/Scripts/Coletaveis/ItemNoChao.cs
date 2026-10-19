using UnityEngine;

/// <summary>
/// Equipamento no chao: arma, armadura ou anel que caiu de um inimigo.
///
/// Quem cria e o DropOnDeath, que sorteia o item e chama o Definir logo
/// depois do Instantiate. O rotulo so e montado no Start do Coletavel, um
/// frame depois, entao o nome e a cor ja estao certos quando ele aparece.
///
/// Por enquanto ninguem pega: o Entregar devolve false e o item fica no
/// chao. Equipar ao pegar e o 2.2c.
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
        // Fica no chao ate o 2.2c ligar o EquipamentoDoPersonagem aqui.
        return false;
    }
}
