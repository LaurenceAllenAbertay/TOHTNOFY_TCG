using UnityEngine;
using UnityEngine.EventSystems;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class LeaderDropTarget : MonoBehaviour, IDropHandler
    {
        [SerializeField] private PlayerSide side;
        [SerializeField] private UnityEngine.UI.Image highlightImage;

        public PlayerSide Side => side;

        public void SetHighlighted(bool highlighted)
        {
            if (highlightImage != null)
            {
                highlightImage.enabled = highlighted;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null)
            {
                return;
            }

            ItemCardDrag itemDrag = eventData.pointerDrag.GetComponent<ItemCardDrag>();
            if (itemDrag != null)
            {
                itemDrag.HandleDroppedOnLeader(this);
            }
        }
    }
}