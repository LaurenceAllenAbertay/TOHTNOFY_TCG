using UnityEngine;
using UnityEngine.EventSystems;

namespace DDD.TNFY.TCG.UI
{
    public class BoardAreaDropTarget : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null)
            {
                return;
            }

            ItemCardDrag itemDrag = eventData.pointerDrag.GetComponent<ItemCardDrag>();
            if (itemDrag != null && itemDrag.IsDraggingItemCard)
            {
                itemDrag.HandleDroppedOnBoardArea();
            }
        }
    }
}