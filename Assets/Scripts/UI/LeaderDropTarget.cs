using UnityEngine;
using UnityEngine.EventSystems;
using DDD.TNFY.TCG.Core;
using DDD.TNFY.TCG.Cards;
using UnityEngine.UI;

namespace DDD.TNFY.TCG.UI
{
    public class LeaderDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private PlayerSide side;
        [SerializeField] private Image artImage;
        [SerializeField] private Image highlightImage;

        private GameManager gameManager;
        private NetworkedMatchSync networkSync;

        public PlayerSide Side => side.ToActualSide(LocalSide);

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

        private void Awake()
        {
            gameManager = FindFirstObjectByType<GameManager>();

            if (gameManager != null)
            {
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            }
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State == null || artImage == null)
            {
                return;
            }

            LeaderData leader = gameManager.State.GetPlayer(Side).Leader;

            if (leader == null)
            {
                return;
            }

            artImage.sprite = leader.Portrait;
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

            LeaderData leader = gameManager.State.GetPlayer(Side).Leader;

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