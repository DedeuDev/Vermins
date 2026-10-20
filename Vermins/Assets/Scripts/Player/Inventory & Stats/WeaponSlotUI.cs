
using UnityEngine;
using UnityEngine.UI;

public class WeaponSlotUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private CharacterEquipment characterEquipment;
    [SerializeField] private InventorySystem inventory;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private Button slotButton;

    [Header("Ícone padrão")]
    [SerializeField] private Sprite defaultWeaponIcon;

    private void Start()
    {
        // Procura o sistema de equipamentos no jogador, se necessário.
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            if (characterEquipment == null)
                characterEquipment = player.GetComponent<CharacterEquipment>();

            if (inventory == null)
                inventory = player.GetComponent<InventorySystem>();
        }

        // Procura o botão do próprio slot, se necessário.
        if (slotButton == null)
            slotButton = GetComponent<Button>();

        // Configura o clique para desequipar a arma.
        if (slotButton != null)
            slotButton.onClick.AddListener(OnWeaponSlotClicked);

        // Atualiza o ícone ao iniciar.
        UpdateWeaponIcon();

        // Atualiza o ícone sempre que o equipamento mudar.
        if (characterEquipment != null)
            characterEquipment.OnEquipmentChanged += UpdateWeaponIcon;
    }

    private void OnDestroy()
    {
        // Remove a inscrição no evento ao destruir o objeto.
        if (characterEquipment != null)
            characterEquipment.OnEquipmentChanged -= UpdateWeaponIcon;

        if (slotButton != null)
            slotButton.onClick.RemoveListener(OnWeaponSlotClicked);
    }

    private void OnWeaponSlotClicked()
    {
        if (inventory == null)
            return;

        inventory.UnequipToInventory(EquipmentSlot.Weapon);
    }

    private void UpdateWeaponIcon()
    {
        if (weaponIcon == null)
            return;

        // Consulta qual arma está equipada.
        EquipmentItem weapon = characterEquipment != null
            ? characterEquipment.GetItem(EquipmentSlot.Weapon)
            : null;

        // Usa o ícone da arma ou o ícone padrão, se houver.
        Sprite iconToShow = weapon != null && weapon.icon != null
            ? weapon.icon
            : (weapon != null ? defaultWeaponIcon : null);

        // Atualiza a imagem e oculta o ícone se não houver arma.
        weaponIcon.sprite = iconToShow;
        weaponIcon.enabled = iconToShow != null;
    }
}
