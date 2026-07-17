using UnityEngine;
using UnityEngine.EventSystems;
using DDD.TNFY.TCG.Core;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.UI
{
    public class LeaderDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private PlayerSide side;
        [SerializeField] private UnityEngine.UI.Image highlightImage;

        private GameManager gameManager;

        public PlayerSide Side => side;

        private void Awake()
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

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

            HandCardDrag handDrag = eventData.pointerDrag.GetComponent<HandCardDrag>();
            if (handDrag != null)
            {
                handDrag.HandleDroppedOnLeader(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.dragging || gameManager == null || gameManager.State == null)
            {
                return;
            }

            LeaderData leader = gameManager.State.GetPlayer(side).Leader;

            if (leader == null)
            {
                return;
            }

            CardHoverPreview.Show(leader, transform.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CardHoverPreview.Hide();
        }
    }
}