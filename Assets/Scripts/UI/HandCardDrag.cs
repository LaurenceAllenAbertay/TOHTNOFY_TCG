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
    public class HandCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private GameManager gameManager;
        private Canvas dragCanvas;
        private BoardView boardView;
        private HandView handView;
        private BoardAreaDropTarget boardAreaDropTarget;
        private RectTransform boardAreaRect;

        private HandCardView handCardView;
        private GameObject dragGhost;
        private RectTransform dragGhostRect;
        private bool droppedOnLegalTarget;
        private bool isDragging;
        private bool isInsideBoardArea;
        private int originalSiblingIndex;
        private List<BoardSlotDropTarget> cachedSlotDropTargets;
        private List<LeaderDropTarget> cachedLeaderDropTargets;

        public bool IsDraggingUnitCard => handCardView != null && handCardView.Card is UnitCardData;
        public bool IsDraggingItemCard => handCardView != null && handCardView.Card is ItemCardData;

        private void Awake()
        {
            handCardView = GetComponent<HandCardView>();
            gameManager = FindFirstObjectByType<GameManager>();
            boardView = FindFirstObjectByType<BoardView>();
            handView = GetComponentInParent<HandView>();
            boardAreaDropTarget = FindFirstObjectByType<BoardAreaDropTarget>();

            if (boardAreaDropTarget != null)
            {
                boardAreaRect = boardAreaDropTarget.GetComponent<RectTransform>();
            }

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

        private bool CanDragThisCard()
        {
            if (handCardView.Card == null)
            {
                return false;
            }

            if (gameManager == null || gameManager.State == null)
            {
                return false;
            }

            return handCardView.Card is UnitCardData || handCardView.Card is ItemCardData;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            droppedOnLegalTarget = false;
            isDragging = false;

            if (!CanDragThisCard())
            {
                return;
            }

            isDragging = true;
            originalSiblingIndex = transform.GetSiblingIndex();

            dragGhost = Instantiate(gameObject, dragCanvas.transform);
            dragGhostRect = dragGhost.GetComponent<RectTransform>();
            Destroy(dragGhost.GetComponent<HandCardDrag>());

            HandCardView ghostCardView = dragGhost.GetComponent<HandCardView>();
            if (ghostCardView != null)
            {
                ghostCardView.SetSlotLerpEnabled(false);
            }

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

            isInsideBoardArea = IsPointerInsideBoardArea(eventData);
            UpdateHighlights(isInsideBoardArea);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || dragGhostRect == null)
            {
                return;
            }

            RectTransform canvasRect = dragCanvas.transform as RectTransform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                dragGhostRect.anchoredPosition = localPoint;
            }

            bool nowInsideBoardArea = IsPointerInsideBoardArea(eventData);

            if (nowInsideBoardArea != isInsideBoardArea)
            {
                isInsideBoardArea = nowInsideBoardArea;
                UpdateHighlights(isInsideBoardArea);
            }

            if (!isInsideBoardArea)
            {
                UpdateReorderPreview(eventData);
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

            if (isDragging && !droppedOnLegalTarget)
            {
                if (!isInsideBoardArea)
                {
                    if (handView != null)
                    {
                        handView.CommitReorder();
                    }
                }
                else
                {
                    transform.SetSiblingIndex(originalSiblingIndex);
                }

                handCardView.SetVisible(true);
            }

            UpdateHighlights(false);
            isDragging = false;
        }

        private bool IsPointerInsideBoardArea(PointerEventData eventData)
        {
            if (boardAreaRect == null)
            {
                return false;
            }

            bool result = RectTransformUtility.RectangleContainsScreenPoint(boardAreaRect, eventData.position, eventData.pressEventCamera);

            Vector3[] corners = new Vector3[4];
            boardAreaRect.GetWorldCorners(corners);
            
            return result;
        }

        private void UpdateReorderPreview(PointerEventData eventData)
        {
            if (handView == null)
            {
                return;
            }

            IReadOnlyList<HandCardView> siblings = handView.SpawnedViews;

            if (siblings.Count == 0)
            {
                return;
            }

            int targetIndex = ComputeTargetSiblingIndex(siblings, eventData);

            if (transform.GetSiblingIndex() != targetIndex)
            {
                transform.SetSiblingIndex(targetIndex);
                handView.RefreshSlotPositions(snapImmediately: false);
            }
        }

        private int ComputeTargetSiblingIndex(IReadOnlyList<HandCardView> siblings, PointerEventData eventData)
        {
            RectTransform containerRect = transform.parent as RectTransform;

            if (containerRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(containerRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                return transform.GetSiblingIndex();
            }

            int targetIndex = 0;

            for (int i = 0; i < siblings.Count; i++)
            {
                HandCardView sibling = siblings[i];

                if (sibling == null || sibling.transform == transform)
                {
                    continue;
                }

                RectTransform siblingRect = sibling.transform as RectTransform;

                if (siblingRect == null)
                {
                    continue;
                }

                float siblingMidpointX = siblingRect.anchoredPosition.x;

                if (localPoint.x > siblingMidpointX)
                {
                    targetIndex = sibling.transform.GetSiblingIndex() + 1;
                }
            }

            targetIndex = Mathf.Clamp(targetIndex, 0, transform.parent.childCount - 1);

            return targetIndex;
        }

        public void HandleDroppedOnSlot(BoardSlotDropTarget slot)
        {
            if (!isInsideBoardArea)
            {
                return;
            }

            if (handCardView.Card is UnitCardData unitCard)
            {
                if (slot.Side != gameManager.State.ActivePlayer)
                {
                    return;
                }

                if (gameManager.Phases.TryPlayUnit(unitCard, slot.SlotIndex))
                {
                    droppedOnLegalTarget = true;
                }

                return;
            }

            if (handCardView.Card is ItemCardData itemCard)
            {
                EffectTarget target = ResolveDropTarget(itemCard, slot, null);

                if (gameManager.Phases.TryPlayItem(itemCard, target))
                {
                    droppedOnLegalTarget = true;
                }
            }
        }

        public void HandleDroppedOnLeader(LeaderDropTarget leader)
        {
            if (!isInsideBoardArea)
            {
                return;
            }

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
            if (!isInsideBoardArea)
            {
                return;
            }

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
            UnitCardData unitCard = handCardView.Card as UnitCardData;
            ItemCardData itemCard = handCardView.Card as ItemCardData;
            CardEffect itemEffect = itemCard != null ? itemCard.PrimaryEffect : null;
            bool isBoardTargeted = IsBoardTargeted(itemCard);

            foreach (BoardSlotDropTarget slot in GetAllSlotDropTargets())
            {
                bool shouldHighlight = false;

                if (show && unitCard != null)
                {
                    shouldHighlight = slot.Side == gameManager.State.ActivePlayer
                        && gameManager.Phases.CanPlayUnit(unitCard, slot.SlotIndex);
                }
                else if (show && itemCard != null && itemEffect != null && !isBoardTargeted)
                {
                    shouldHighlight = IsSlotHighlightable(itemCard, itemEffect, slot);
                }

                slot.SetHighlighted(shouldHighlight);
            }

            foreach (LeaderDropTarget leader in GetAllLeaderDropTargets())
            {
                bool shouldHighlight = show
                    && itemCard != null
                    && itemEffect != null
                    && !isBoardTargeted
                    && EffectTargeting.IsValidTarget(itemEffect.targetType, EffectTarget.ForLeader(leader.Side), gameManager.State);

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