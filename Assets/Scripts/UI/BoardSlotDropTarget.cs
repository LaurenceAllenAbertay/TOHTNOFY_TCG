using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class BoardSlotDropTarget : MonoBehaviour, IDropHandler
    {
        [SerializeField] private PlayerSide side;
        [SerializeField] private int slotIndex;
        [SerializeField] private Image highlightImage;
        [SerializeField] private Image emptySlotImage;

        public PlayerSide Side => side;
        public int SlotIndex => slotIndex;

        public void SetHighlighted(bool highlighted)
        {
            if (highlightImage != null)
            {
                highlightImage.enabled = highlighted;
            }
        }

        public void SetOccupied(bool occupied)
        {
            if (emptySlotImage != null)
            {
                emptySlotImage.enabled = !occupied;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null)
            {
                return;
            }

            ItemCardDrag itemDrag = eventData.pointerDrag.GetComponent<ItemCardDrag>();
            if (itemDrag != null && itemDrag.IsDraggingItemCard)
            {
                itemDrag.HandleDroppedOnSlot(this);
                return;
            }

            HandCardDrag handDrag = eventData.pointerDrag.GetComponent<HandCardDrag>();
            if (handDrag != null && handDrag.IsDraggingUnitCard)
            {
                handDrag.HandleDroppedOn(this);
                return;
            }

            BoardCardDrag boardDrag = eventData.pointerDrag.GetComponent<BoardCardDrag>();
            if (boardDrag != null)
            {
                boardDrag.HandleDroppedOn(this);
            }
        }
    }
}