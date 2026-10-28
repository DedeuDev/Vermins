using UnityEngine;

[RequireComponent(typeof(EquipmentPickup))]
public sealed class SandboxLootItem : MonoBehaviour
{
    [SerializeField] private EquipmentItem item;

    private void Awake()
    {
        // Preparo o item antes do Start do Pickup montar o rotulo.
        GetComponent<EquipmentPickup>().SetItem(item);
    }
}
