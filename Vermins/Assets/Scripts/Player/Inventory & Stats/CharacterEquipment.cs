using UnityEngine;

/// <summary>
/// O que o Player esta vestindo: um item por slot.
///
/// Este script so guarda e avisa. Ele nao escreve vida nem dano: quando
/// um slot troca, ele dispara OnEquipmentChanged e o CharacterAttributes
/// reaplica tudo. Build, nivel e equipamento mexem nos mesmos numeros,
/// entao quem escreve continua sendo um so.
///
/// Os slots sao tres campos com nome, e nao uma lista, pra que o
/// Inspector mostre "Weapon, Armor, Ring" e de pra ver na hora o que o
/// Player esta usando. Tambem da pra deixar um item arrastado no prefab,
/// e ai ele nasce equipado.
///
/// Jogando, o item entra pelo EquipmentPickup: encostar veste se o slot dele
/// estiver vazio. Pra testar sem ir atras de drop, arrasta um item em
/// "Item de teste" e usa o menu do componente.
/// </summary>
[DisallowMultipleComponent]
public class CharacterEquipment : MonoBehaviour
{
    [Header("Vestindo agora")]
    [SerializeField] private EquipmentItem weapon;
    [SerializeField] private EquipmentItem armor;
    [SerializeField] private EquipmentItem ring;

    [Header("Teste")]
    [Tooltip("Item que o menu 'Equipar item de teste' veste, pra testar " +
             "sem ir atras de drop.")]
    [SerializeField] private EquipmentItem testItem;

    /// <summary>
    /// Dispara quando algum slot troca. Quem escuta le TotalBonus.
    /// </summary>
    public event System.Action OnEquipmentChanged;

    /// <summary>
    /// Os pontos dos tres slots somados. Calculo na hora em vez de guardar:
    /// sao tres somas, so roda quando o equipamento muda, e assim nunca
    /// fica desatualizado se alguem mexer no Inspector.
    /// </summary>
    public BonusPoints TotalBonus => BonusOf(weapon) + BonusOf(armor) + BonusOf(ring);

    public EquipmentItem GetItem(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: return weapon;
            case EquipmentSlot.Armor: return armor;
            default: return ring;
        }
    }

    /// <summary>
    /// Veste o item no slot dele e devolve o que estava la, ou null. Quem
    /// chama decide o que fazer com o que saiu. O EquipmentPickup so chama com o
    /// slot vazio, entao hoje nunca sai nada.
    /// Vestir o que ja esta vestido nao faz nada e nao dispara evento.
    /// </summary>
    public EquipmentItem Equip(EquipmentItem item)
{
    if (item == null)
        return null;

    EquipmentItem saiu = GetItem(item.Slot);

    SetSlot(item.Slot, item);
    OnEquipmentChanged?.Invoke();

    return saiu;
}

    /// <summary>Tira o item do slot e devolve ele, ou null se estava vazio.</summary>
    public EquipmentItem Unequip(EquipmentSlot slot)
    {
        EquipmentItem saiu = GetItem(slot);
        if (saiu == null)
            return null;

        SetSlot(slot, null);
        OnEquipmentChanged?.Invoke();
        return saiu;
    }

    /// <summary>
    /// Poe os tres slots de uma vez, vazio incluido. E o que o load do save
    /// e a troca de cena usam. Dispara um evento so, e so se algo mudou,
    /// em vez de tres recalculos seguidos.
    /// </summary>
    public void Restore(EquipmentItem novaArma, EquipmentItem novaArmadura, EquipmentItem novoAnel)
    {
        if (weapon == novaArma && armor == novaArmadura && ring == novoAnel)
            return;

        weapon = novaArma;
        armor = novaArmadura;
        ring = novoAnel;
        OnEquipmentChanged?.Invoke();
    }

    private void SetSlot(EquipmentSlot slot, EquipmentItem item)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: weapon = item; break;
            case EquipmentSlot.Armor: armor = item; break;
            default: ring = item; break;
        }
    }

    private static BonusPoints BonusOf(EquipmentItem item)
    {
        return item != null ? item.Bonus : default;
    }

    [ContextMenu("Equipar item de teste")]
    private void EquipTestItem()
    {
        if (testItem == null)
        {
            Debug.LogWarning($"[{name}] Arrasta um item em 'Item de teste' antes.", this);
            return;
        }

        Equip(testItem);
    }

    [ContextMenu("Tirar tudo")]
    private void UnequipAll()
    {
        Unequip(EquipmentSlot.Weapon);
        Unequip(EquipmentSlot.Armor);
        Unequip(EquipmentSlot.Ring);
    }
}
