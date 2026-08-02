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

        private NetworkedMatchSync networkSync;

        public PlayerSide Side => side.ToActualSide(LocalSide);
        public int SlotIndex => slotIndex;

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

        private void Awake()
        {
            GameManager gameManager = FindFirstObjectByType<GameManager>();

            if (gameManager != null)
            {
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            }
        }

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
            Debug.Log($"[BoardSlotDropTarget] OnDrop fired on seat={side}, actualSide={Side}, SlotIndex={slotIndex}. pointerDrag={(eventData.pointerDrag != null ? eventData.pointerDrag.name : "null")}");

            if (eventData.pointerDrag == null)
            {
                return;
            }

            HandCardDrag handDrag = eventData.pointerDrag.GetComponent<HandCardDrag>();
          
            if (handDrag != null && (handDrag.IsDraggingUnitCard || handDrag.IsDraggingItemCard))
            {
                handDrag.HandleDroppedOnSlot(this);
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