
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private InventorySystem inventory;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private Transform slotGrid;

    private InventorySlotUI[] slots;

    private void Start()
    {
        if (inventory == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
                inventory = player.GetComponent<InventorySystem>();
        }

        if (slotGrid != null)
        {
            slots = slotGrid.GetComponentsInChildren<InventorySlotUI>(true);
        }
        else
        {
            slots = new InventorySlotUI[0];
        }

        for (int i = 0; i < slots.Length; i++)
            slots[i].Setup(inventory, i);

        if (inventory != null)
            inventory.OnInventoryChanged += RefreshSlots;

        RefreshSlots();

        Debug.Log("InventoryUI iniciado.");
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.iKey.wasPressedThisFrame)
        {
            Debug.Log("Tecla I detectada.");

            if (inventoryPanel != null)
            {
                bool abrir = !inventoryPanel.activeSelf;
                inventoryPanel.SetActive(abrir);

                Debug.Log("Inventário aberto: " + abrir);
            }
            else
            {
                Debug.LogError("Inventory Panel não foi atribuído!");
            }
        }
    }

    private void RefreshSlots()
    {
        if (slots == null) return;

        foreach (InventorySlotUI slot in slots)
        {
            if (slot != null)
                slot.Refresh();
        }
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= RefreshSlots;
    }
}
