
using UnityEngine;
using UnityEngine.UI;

public class ArmorSlotUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private CharacterEquipment characterEquipment;
    [SerializeField] private InventorySystem inventory;
    [SerializeField] private Image armorIcon;
    [SerializeField] private Button slotButton;

    [Header("Ícone padrão")]
    [SerializeField] private Sprite defaultArmorIcon;

    private void Start()
    {
        // Procura os sistemas de equipamento e inventário no jogador.
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            if (characterEquipment == null)
                characterEquipment = player.GetComponent<CharacterEquipment>();

            if (inventory == null)
                inventory = player.GetComponent<InventorySystem>();
        }

        // Procura o botão do próprio espaço, caso não esteja configurado.
        if (slotButton == null)
            slotButton = GetComponent<Button>();

        // Configura o clique para desequipar a armadura.
        if (slotButton != null)
        {
            slotButton.onClick.RemoveListener(OnArmorSlotClicked);
            slotButton.onClick.AddListener(OnArmorSlotClicked);
        }

        // Atualiza o ícone quando o jogo começa.
        UpdateArmorIcon();

        // Atualiza o ícone sempre que algum equipamento mudar.
        if (characterEquipment != null)
            characterEquipment.OnEquipmentChanged += UpdateArmorIcon;
    }

    private void OnDestroy()
    {
        // Remove a inscrição no evento ao destruir o objeto.
        if (characterEquipment != null)
            characterEquipment.OnEquipmentChanged -= UpdateArmorIcon;

        if (slotButton != null)
            slotButton.onClick.RemoveListener(OnArmorSlotClicked);
    }

    // Desequipa a armadura e tenta guardá-la na mochila.
    private void OnArmorSlotClicked()
    {
        if (inventory == null)
            return;

        inventory.UnequipToInventory(EquipmentSlot.Armor);
    }

    // Atualiza o ícone conforme a armadura equipada.
    private void UpdateArmorIcon()
    {
        if (armorIcon == null)
            return;

        // Consulta qual armadura está equipada.
        EquipmentItem armor = characterEquipment != null
            ? characterEquipment.GetItem(EquipmentSlot.Armor)
            : null;

        // Usa o ícone da armadura ou o ícone padrão.
        Sprite iconToShow = armor != null && armor.icon != null
            ? armor.icon
            : (armor != null ? defaultArmorIcon : null);

        // Atualiza a imagem e oculta o ícone se não houver armadura.
        armorIcon.sprite = iconToShow;
        armorIcon.enabled = iconToShow != null;
    }
}
