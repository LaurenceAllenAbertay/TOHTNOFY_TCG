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

            HandCardDrag handDrag = eventData.pointerDrag.GetComponent<HandCardDrag>();
            if (handDrag != null && handDrag.IsDraggingItemCard)
            {
                handDrag.HandleDroppedOnBoardArea();
            }
        }
    }
}