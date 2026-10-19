using UnityEngine;

/// <summary>
/// O que o Player esta vestindo: um item por slot.
///
/// Este script so guarda e avisa. Ele nao escreve vida nem dano: quando
/// um slot troca, ele dispara OnEquipamentoMudou e o AtributosDoPersonagem
/// reaplica tudo. Build, nivel e equipamento mexem nos mesmos numeros,
/// entao quem escreve continua sendo um so.
///
/// Os slots sao tres campos com nome, e nao uma lista, pra que o
/// Inspector mostre "Arma, Armadura, Anel" e de pra ver na hora o que o
/// Player esta usando. Tambem da pra deixar um item arrastado no prefab,
/// e ai ele nasce equipado.
///
/// Jogando, o item entra pelo ItemNoChao: encostar veste se o slot dele
/// estiver vazio. Pra testar sem ir atras de drop, arrasta um item em
/// "Item de teste" e usa o menu do componente.
/// </summary>
[DisallowMultipleComponent]
public class EquipamentoDoPersonagem : MonoBehaviour
{
    [Header("Vestindo agora")]
    [SerializeField] private ItemDeEquipamento arma;
    [SerializeField] private ItemDeEquipamento armadura;
    [SerializeField] private ItemDeEquipamento anel;

    [Header("Teste")]
    [Tooltip("Item que o menu 'Equipar item de teste' veste, pra testar " +
             "sem ir atras de drop.")]
    [SerializeField] private ItemDeEquipamento itemDeTeste;

    /// <summary>
    /// Dispara quando algum slot troca. Quem escuta le PontosSomados.
    /// </summary>
    public event System.Action OnEquipamentoMudou;

    /// <summary>
    /// Os pontos dos tres slots somados. Calculo na hora em vez de guardar:
    /// sao tres somas, so roda quando o equipamento muda, e assim nunca
    /// fica desatualizado se alguem mexer no Inspector.
    /// </summary>
    public PontosExtras PontosSomados => PontosDe(arma) + PontosDe(armadura) + PontosDe(anel);

    public ItemDeEquipamento NoSlot(SlotDeEquipamento slot)
    {
        switch (slot)
        {
            case SlotDeEquipamento.Arma: return arma;
            case SlotDeEquipamento.Armadura: return armadura;
            default: return anel;
        }
    }

    /// <summary>
    /// Veste o item no slot dele e devolve o que estava la, ou null. Quem
    /// chama decide o que fazer com o que saiu. O ItemNoChao so chama com o
    /// slot vazio, entao hoje nunca sai nada.
    /// Vestir o que ja esta vestido nao faz nada e nao dispara evento.
    /// </summary>
    public ItemDeEquipamento Equipar(ItemDeEquipamento item)
    {
        if (item == null)
            return null;

        ItemDeEquipamento saiu = NoSlot(item.Slot);
        if (saiu == item)
            return null;

        Colocar(item.Slot, item);
        OnEquipamentoMudou?.Invoke();
        return saiu;
    }

    /// <summary>Tira o item do slot e devolve ele, ou null se estava vazio.</summary>
    public ItemDeEquipamento Desequipar(SlotDeEquipamento slot)
    {
        ItemDeEquipamento saiu = NoSlot(slot);
        if (saiu == null)
            return null;

        Colocar(slot, null);
        OnEquipamentoMudou?.Invoke();
        return saiu;
    }

    private void Colocar(SlotDeEquipamento slot, ItemDeEquipamento item)
    {
        switch (slot)
        {
            case SlotDeEquipamento.Arma: arma = item; break;
            case SlotDeEquipamento.Armadura: armadura = item; break;
            default: anel = item; break;
        }
    }

    private static PontosExtras PontosDe(ItemDeEquipamento item)
    {
        return item != null ? item.Pontos : default;
    }

    [ContextMenu("Equipar item de teste")]
    private void EquiparItemDeTeste()
    {
        if (itemDeTeste == null)
        {
            Debug.LogWarning($"[{name}] Arrasta um item em 'Item de teste' antes.", this);
            return;
        }

        Equipar(itemDeTeste);
    }

    [ContextMenu("Tirar tudo")]
    private void TirarTudo()
    {
        Desequipar(SlotDeEquipamento.Arma);
        Desequipar(SlotDeEquipamento.Armadura);
        Desequipar(SlotDeEquipamento.Anel);
    }
}
