
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private InventorySystem inventory;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Button slotButton;

    private int slotIndex;

    public void Setup(InventorySystem inventorySystem, int index)
    {
        inventory = inventorySystem;
        slotIndex = index;

        if (slotButton == null)
            slotButton = GetComponent<Button>();

        if (slotButton != null)
        {
            slotButton.onClick.RemoveListener(OnSlotClicked);
            slotButton.onClick.AddListener(OnSlotClicked);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (itemIcon == null)
            return;

        EquipmentItem item = inventory != null
            ? inventory.GetItem(slotIndex)
            : null;

        // O fundo pertence ao InventorySlot e nunca é alterado aqui.
        // Apenas o ícone do item é atualizado.
        itemIcon.sprite = item != null ? item.icon : null;
        itemIcon.enabled = item != null && item.icon != null;
    }

    private void OnSlotClicked()
    {
        if (inventory == null)
            return;

        inventory.EquipItem(slotIndex);
        Refresh();
    }

    private void OnDestroy()
    {
        if (slotButton != null)
            slotButton.onClick.RemoveListener(OnSlotClicked);
    }
}
