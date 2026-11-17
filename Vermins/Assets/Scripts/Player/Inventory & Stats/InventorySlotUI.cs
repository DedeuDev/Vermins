
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("Referências")]
    [SerializeField] private InventorySystem inventory;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Button slotButton;

    private int slotIndex;

    // Guarda o slot de origem durante o arraste.
    private InventorySlotUI draggedSlot;

    // Referência ao Canvas onde a imagem temporária será exibida.
    private Canvas canvas;

    // Imagem temporária que acompanha o mouse durante o arraste.
    private Image dragIcon;

    // Guarda o CanvasGroup para permitir que o slot de destino receba o evento de soltar.
    private CanvasGroup canvasGroup;

    public void Setup(InventorySystem inventorySystem, int index)
    {
        inventory = inventorySystem;
        slotIndex = index;

        if (slotButton == null)
            slotButton = GetComponent<Button>();

        // Encontra o Canvas que contém os espaços do inventário.
        canvas = GetComponentInParent<Canvas>();

        // Cria ou reutiliza o CanvasGroup do slot.
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

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
        if (inventory == null || draggedSlot != null)
            return;

        inventory.EquipItem(slotIndex);
        Refresh();
    }

    // Inicia o arraste sem movimentar o objeto inteiro do slot.
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inventory == null || canvas == null || itemIcon == null)
            return;

        EquipmentItem item = inventory.GetItem(slotIndex);

        if (item == null || item.icon == null)
            return;

        draggedSlot = this;

        // Cria uma imagem temporária independente do slot.
        GameObject dragObject = new GameObject("DragIcon");
        dragObject.transform.SetParent(canvas.transform, false);

        dragIcon = dragObject.AddComponent<Image>();

        // Copia a aparência do ícone original.
        dragIcon.sprite = itemIcon.sprite;
        dragIcon.preserveAspect = itemIcon.preserveAspect;
        dragIcon.raycastTarget = false;

        // Mantém o tamanho original do ícone.
        dragIcon.rectTransform.sizeDelta = itemIcon.rectTransform.rect.size;

        // Coloca a imagem temporária na frente dos elementos do Canvas.
        dragIcon.transform.SetAsLastSibling();

        // Esconde somente o ícone original enquanto ele está sendo arrastado.
        itemIcon.enabled = false;

        // Permite que o evento de soltar alcance o slot de destino.
        canvasGroup.blocksRaycasts = false;

        UpdateDragPosition(eventData);
    }

    // Atualiza a posição somente da imagem temporária.
    public void OnDrag(PointerEventData eventData)
    {
        if (draggedSlot != this || dragIcon == null)
            return;

        UpdateDragPosition(eventData);
    }

    // Converte a posição do mouse para as coordenadas do Canvas.
    private void UpdateDragPosition(PointerEventData eventData)
    {
        RectTransform canvasRect = canvas.transform as RectTransform;

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventCamera,
            out Vector2 localPoint))
        {
            dragIcon.rectTransform.localPosition = localPoint;
        }
    }

    // Finaliza o arraste e restaura a aparência original do slot.
    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggedSlot != this)
            return;

        // Remove a imagem temporária.
        if (dragIcon != null)
            Destroy(dragIcon.gameObject);

        dragIcon = null;

        // Reativa os eventos de raycast do slot.
        canvasGroup.blocksRaycasts = true;

        draggedSlot = null;

        // Atualiza o ícone conforme a posição atual do item.
        Refresh();
    }

    // Recebe o item arrastado de outro espaço do inventário.
    public void OnDrop(PointerEventData eventData)
    {
        InventorySlotUI sourceSlot =
            eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<InventorySlotUI>()
            : null;

        if (sourceSlot == null || sourceSlot == this)
            return;

        if (inventory == null || sourceSlot.inventory != inventory)
            return;

        // Move o item ou troca os equipamentos entre os dois espaços.
        inventory.MoveItem(sourceSlot.slotIndex, slotIndex);
    }

    private void OnDestroy()
    {
        if (slotButton != null)
            slotButton.onClick.RemoveListener(OnSlotClicked);
    }
}
