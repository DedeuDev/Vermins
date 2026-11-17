
using UnityEngine;
using UnityEngine.UI;

public class RingSlotUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private CharacterEquipment characterEquipment;
    [SerializeField] private InventorySystem inventory;
    [SerializeField] private Image ringIcon;
    [SerializeField] private Button slotButton;

    [Header("Ícone padrão")]
    [SerializeField] private Sprite defaultRingIcon;

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

        // Configura o clique para desequipar o anel.
        if (slotButton != null)
        {
            slotButton.onClick.RemoveListener(OnRingSlotClicked);
            slotButton.onClick.AddListener(OnRingSlotClicked);
        }

        // Atualiza o ícone quando o jogo começa.
        UpdateRingIcon();

        // Atualiza o ícone sempre que algum equipamento mudar.
        if (characterEquipment != null)
            characterEquipment.OnEquipmentChanged += UpdateRingIcon;
    }

    private void OnDestroy()
    {
        // Remove a inscrição no evento ao destruir o objeto.
        if (characterEquipment != null)
            characterEquipment.OnEquipmentChanged -= UpdateRingIcon;

        if (slotButton != null)
            slotButton.onClick.RemoveListener(OnRingSlotClicked);
    }

    // Desequipa o anel e tenta guardá-lo na mochila.
    private void OnRingSlotClicked()
    {
        if (inventory == null)
            return;

        inventory.UnequipToInventory(EquipmentSlot.Ring);
    }

    // Atualiza o ícone conforme o anel equipado.
    private void UpdateRingIcon()
    {
        if (ringIcon == null)
            return;

        // Consulta qual anel está equipado.
        EquipmentItem ring = characterEquipment != null
            ? characterEquipment.GetItem(EquipmentSlot.Ring)
            : null;

        // Usa o ícone do anel ou o ícone padrão.
        Sprite iconToShow = ring != null && ring.icon != null
            ? ring.icon
            : (ring != null ? defaultRingIcon : null);

        // Atualiza a imagem e oculta o ícone se não houver anel.
        ringIcon.sprite = iconToShow;
        ringIcon.enabled = iconToShow != null;
    }
}
