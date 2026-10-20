
using UnityEngine;

[DisallowMultipleComponent]
public class InventorySystem : MonoBehaviour
{
    // Quantidade total de espaços do inventário.
    public const int SlotCount = 12;

    [Header("Referências")]
    [SerializeField] private PotionBelt potionBelt;
    [SerializeField] private CharacterEquipment characterEquipment;

    [Header("Itens no chão")]
    [SerializeField] private EquipmentPickup equipmentPickupPrefab;

    // Cada posição representa um espaço do inventário.
    // Por enquanto, os espaços guardam equipamentos.
    private readonly EquipmentItem[] items = new EquipmentItem[SlotCount];

    // Evento disparado sempre que o inventário muda.
    public event System.Action OnInventoryChanged;

    // Permite que outros scripts acessem os sistemas relacionados.
    public PotionBelt Potions => potionBelt;
    public CharacterEquipment Equipment => characterEquipment;

    private void Awake()
    {
        // Procura automaticamente os componentes no mesmo personagem.
        if (potionBelt == null)
            potionBelt = GetComponent<PotionBelt>();

        if (characterEquipment == null)
            characterEquipment = GetComponent<CharacterEquipment>();
    }

    // Retorna o equipamento de um espaço específico.
    public EquipmentItem GetItem(int index)
    {
        if (!IsValidIndex(index))
            return null;

        return items[index];
    }

    // Verifica se o índice pertence aos 12 espaços disponíveis.
    public bool IsValidIndex(int index)
    {
        return index >= 0 && index < SlotCount;
    }

    // Procura o primeiro espaço vazio.
    public int FindEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (items[i] == null)
                return i;
        }

        // Retorna -1 quando o inventário está cheio.
        return -1;
    }

    // Tenta adicionar um equipamento ao inventário.
    public bool TryAddEquipment(EquipmentItem item)
    {
        if (item == null)
            return false;

        int index = FindEmptySlot();

        // Não permite pegar o item se todos os espaços estiverem ocupados.
        if (index < 0)
            return false;

        items[index] = item;

        Debug.Log("Item guardado no inventário: " + item.ItemName
        + " no slot " + index);

        OnInventoryChanged?.Invoke();
        return true;
    }

    // Tenta consumir uma poção usando o PotionBelt existente.
    public bool TryDrinkPotion()
    {
        if (potionBelt == null)
            return false;

        return potionBelt.TryDrink();
    }

    // Equipa o item selecionado e coloca o equipamento anterior no espaço.
    public bool EquipItem(int index)
{
    if (!IsValidIndex(index) || characterEquipment == null)
        return false;

    EquipmentItem item = items[index];

    if (item == null)
        return false;

    Debug.Log("Item clicado: " + item.ItemName);
    Debug.Log("Item equipado: " +
        characterEquipment.GetItem(item.Slot)?.ItemName);
    Debug.Log("É a mesma referência? " +
        (characterEquipment.GetItem(item.Slot) == item));

    // O sistema de equipamentos devolve o item substituído.
    EquipmentItem previous = characterEquipment.Equip(item);

    // Não altera o inventário se o item já estiver equipado.
    if (characterEquipment.GetItem(item.Slot) != item)
        return false;

    // Guarda o equipamento anterior no espaço usado pelo item equipado.
    items[index] = previous;
    OnInventoryChanged?.Invoke();

    return true;
}

    // Descarta um equipamento no chão, à frente do personagem.
    public bool DropItem(int index)
    {
        if (!IsValidIndex(index))
            return false;

        EquipmentItem item = items[index];

        // Verifica se há item e se o prefab de coleta foi configurado.
        if (item == null || equipmentPickupPrefab == null)
            return false;

        Vector3 dropPosition = transform.position + transform.forward * 1.5f;
        dropPosition.y += 0.3f;

        // Cria o objeto de coleta no mundo.
        EquipmentPickup dropped = Instantiate(
            equipmentPickupPrefab,
            dropPosition,
            Quaternion.identity
        );

        dropped.SetItem(item);

        // Libera o espaço utilizado pelo equipamento descartado.
        items[index] = null;
        OnInventoryChanged?.Invoke();

        return true;
    }

    // Converte os itens para nomes que podem ser guardados no SaveSystem.
    public string[] GetSavedItemNames()
    {
        string[] names = new string[SlotCount];

        for (int i = 0; i < SlotCount; i++)
            names[i] = EquipmentItem.SaveName(items[i]);

        return names;
    }

    // Restaura os itens salvos nos seus respectivos espaços.
    public void RestoreItems(string[] savedNames)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            items[i] = null;

            if (savedNames != null && i < savedNames.Length)
                items[i] = EquipmentItem.FindByName(savedNames[i]);
        }

        OnInventoryChanged?.Invoke();
    }

    
public bool UnequipToInventory(EquipmentSlot slot)
{
    if (characterEquipment == null)
        return false;

    // Verifica se existe um equipamento no slot.
    EquipmentItem equippedItem = characterEquipment.GetItem(slot);

    if (equippedItem == null)
        return false;

    // Verifica se existe espaço na mochila antes de desequipar.
    int emptyIndex = FindEmptySlot();

    if (emptyIndex < 0)
    {
        Debug.LogWarning("Inventário cheio! Não foi possível desequipar.");
        return false;
    }

    // Remove o equipamento do personagem.
    EquipmentItem removedItem = characterEquipment.Unequip(slot);

    if (removedItem == null)
        return false;

    // Guarda o equipamento no primeiro espaço vazio da mochila.
    items[emptyIndex] = removedItem;

    OnInventoryChanged?.Invoke();

    return true;
}

}
