using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DDD.TNFY.TCG.Core;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.UI
{
    [RequireComponent(typeof(HandCardView))]
    public class ItemCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private GameManager gameManager;
        private Canvas dragCanvas;
        private BoardView boardView;

        private HandCardView handCardView;
        private GameObject dragGhost;
        private RectTransform dragGhostRect;
        private bool droppedOnLegalTarget;
        private List<BoardSlotDropTarget> cachedSlotDropTargets;
        private List<LeaderDropTarget> cachedLeaderDropTargets;

        public bool IsDraggingItemCard => handCardView != null && handCardView.Card is ItemCardData;

        private void Awake()
        {
            handCardView = GetComponent<HandCardView>();
            gameManager = FindFirstObjectByType<GameManager>();
            boardView = FindFirstObjectByType<BoardView>();

            if (boardView != null)
            {
                dragCanvas = boardView.GetComponentInParent<Canvas>();
            }
        }

        private List<BoardSlotDropTarget> GetAllSlotDropTargets()
        {
            if (cachedSlotDropTargets != null)
            {
                return cachedSlotDropTargets;
            }

            cachedSlotDropTargets = new List<BoardSlotDropTarget>();

            if (boardView == null)
            {
                return cachedSlotDropTargets;
            }

            AddDropTargetsFrom(boardView.PlayerASlotContainers, cachedSlotDropTargets);
            AddDropTargetsFrom(boardView.PlayerBSlotContainers, cachedSlotDropTargets);

            return cachedSlotDropTargets;
        }

        private static void AddDropTargetsFrom(IReadOnlyList<Transform> containers, List<BoardSlotDropTarget> results)
        {
            for (int i = 0; i < containers.Count; i++)
            {
                if (containers[i] == null)
                {
                    continue;
                }

                BoardSlotDropTarget dropTarget = containers[i].GetComponent<BoardSlotDropTarget>();
                if (dropTarget != null)
                {
                    results.Add(dropTarget);
                }
            }
        }

        private List<LeaderDropTarget> GetAllLeaderDropTargets()
        {
            if (cachedLeaderDropTargets != null)
            {
                return cachedLeaderDropTargets;
            }

            cachedLeaderDropTargets = new List<LeaderDropTarget>(FindObjectsByType<LeaderDropTarget>(FindObjectsSortMode.None));
            return cachedLeaderDropTargets;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            droppedOnLegalTarget = false;

            if (!(handCardView.Card is ItemCardData))
            {
                return;
            }

            dragGhost = Instantiate(gameObject, dragCanvas.transform);
            dragGhostRect = dragGhost.GetComponent<RectTransform>();
            Destroy(dragGhost.GetComponent<ItemCardDrag>());

            dragGhostRect.anchorMin = new Vector2(0.5f, 0.5f);
            dragGhostRect.anchorMax = new Vector2(0.5f, 0.5f);
            dragGhostRect.pivot = new Vector2(0.5f, 0.5f);

            CanvasGroup ghostCanvasGroup = dragGhost.GetComponent<CanvasGroup>();
            if (ghostCanvasGroup == null)
            {
                ghostCanvasGroup = dragGhost.AddComponent<CanvasGroup>();
            }
            ghostCanvasGroup.blocksRaycasts = false;

            RectTransform canvasRect = dragCanvas.transform as RectTransform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 startLocalPoint))
            {
                dragGhostRect.anchoredPosition = startLocalPoint;
            }

            handCardView.SetVisible(false);

            UpdateHighlights(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragGhostRect == null)
            {
                return;
            }

            RectTransform canvasRect = dragCanvas.transform as RectTransform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                dragGhostRect.anchoredPosition = localPoint;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragGhost != null)
            {
                Destroy(dragGhost);
                dragGhost = null;
                dragGhostRect = null;
            }

            if (!droppedOnLegalTarget)
            {
                handCardView.SetVisible(true);
            }

            UpdateHighlights(false);
        }

        public void HandleDroppedOnSlot(BoardSlotDropTarget slot)
        {
            if (!(handCardView.Card is ItemCardData itemCard))
            {
                Debug.Log("[ItemCardDrag] HandleDroppedOnSlot: card is not an ItemCardData, ignoring.");
                return;
            }

            EffectTarget target = ResolveDropTarget(itemCard, slot, null);
            Debug.Log($"[ItemCardDrag] HandleDroppedOnSlot: item={itemCard.CardName}, slot side={slot.Side} index={slot.SlotIndex}, resolved target kind={target.Kind}");

            bool played = gameManager.Phases.TryPlayItem(itemCard, target);
            Debug.Log($"[ItemCardDrag] TryPlayItem returned {played}");

            if (played)
            {
                droppedOnLegalTarget = true;
            }
        }

        public void HandleDroppedOnLeader(LeaderDropTarget leader)
        {
            if (!(handCardView.Card is ItemCardData itemCard))
            {
                return;
            }

            EffectTarget target = ResolveDropTarget(itemCard, null, leader);

            if (gameManager.Phases.TryPlayItem(itemCard, target))
            {
                droppedOnLegalTarget = true;
            }
        }

        public void HandleDroppedOnBoardArea()
        {
            if (!(handCardView.Card is ItemCardData itemCard))
            {
                return;
            }

            if (gameManager.Phases.TryPlayItem(itemCard, EffectTarget.None))
            {
                droppedOnLegalTarget = true;
            }
        }

        private bool IsBoardTargeted(ItemCardData itemCard)
        {
            CardEffect effect = itemCard != null ? itemCard.PrimaryEffect : null;
            return effect != null && effect.targetType == TargetType.Board;
        }

        private EffectTarget ResolveDropTarget(ItemCardData itemCard, BoardSlotDropTarget slot, LeaderDropTarget leader)
        {
            if (IsBoardTargeted(itemCard))
            {
                return EffectTarget.None;
            }

            if (leader != null)
            {
                return EffectTarget.ForLeader(leader.Side);
            }

            BoardUnit unit = gameManager.State.Board.GetUnit(slot.Side, slot.SlotIndex);

            if (unit != null)
            {
                return EffectTarget.ForUnit(unit);
            }

            return EffectTarget.ForSlot(slot.Side, slot.SlotIndex);
        }

        private void UpdateHighlights(bool show)
        {
            ItemCardData itemCard = handCardView.Card as ItemCardData;
            CardEffect effect = itemCard != null ? itemCard.PrimaryEffect : null;
            bool isBoardTargeted = IsBoardTargeted(itemCard);

            foreach (BoardSlotDropTarget slot in GetAllSlotDropTargets())
            {
                bool shouldHighlight = show && effect != null && !isBoardTargeted && IsSlotHighlightable(itemCard, effect, slot);
                slot.SetHighlighted(shouldHighlight);
            }

            foreach (LeaderDropTarget leader in GetAllLeaderDropTargets())
            {
                bool shouldHighlight = show
                    && effect != null
                    && !isBoardTargeted
                    && EffectTargeting.IsValidTarget(effect.targetType, EffectTarget.ForLeader(leader.Side), gameManager.State);

                leader.SetHighlighted(shouldHighlight);
            }
        }

        private bool IsSlotHighlightable(ItemCardData itemCard, CardEffect effect, BoardSlotDropTarget slot)
        {
            EffectTarget target = ResolveDropTarget(itemCard, slot, null);
            return EffectTargeting.IsValidTarget(effect.targetType, target, gameManager.State);
        }
    }
}