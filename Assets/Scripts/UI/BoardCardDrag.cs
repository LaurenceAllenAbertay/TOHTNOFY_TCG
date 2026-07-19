using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    [RequireComponent(typeof(BoardCardView))]
    public class BoardCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private GameManager gameManager;
        private Canvas dragCanvas;
        private BoardView boardView;

        private BoardCardView boardCardView;
        private GameObject dragGhost;
        private RectTransform dragGhostRect;
        private bool droppedOnLegalSlot;
        private List<BoardSlotDropTarget> cachedSlotDropTargets;

        private void Awake()
        {
            boardCardView = GetComponent<BoardCardView>();
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

        private bool IsPendingFreeMoveEligible()
        {
            GameState state = gameManager.State;

            if (!state.HasPendingFreeMove)
            {
                return false;
            }

            if (boardCardView.Unit == state.PendingFreeMoveExcludedUnit)
            {
                return false;
            }

            return true;
        }

        private bool IsEnemyMoveGrantEligible()
        {
            if (gameManager.State.CurrentPhase != TurnPhase.Move)
            {
                return false;
            }

            return gameManager.Phases.HasAvailableGrantedEnemyMove(gameManager.State.ActivePlayer);
        }

        private bool CanDragThisUnit()
        {
            if (boardCardView.Unit == null)
            {
                return false;
            }

            if (gameManager == null || gameManager.State == null)
            {
                return false;
            }

            bool isMovePhase = gameManager.State.CurrentPhase == TurnPhase.Move;
            bool isOwnUnit = boardCardView.Unit.Owner == gameManager.State.ActivePlayer;

            if (isOwnUnit)
            {
                return isMovePhase || IsPendingFreeMoveEligible();
            }

            return IsEnemyMoveGrantEligible();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            droppedOnLegalSlot = false;

            if (!CanDragThisUnit())
            {
                return;
            }

            dragGhost = Instantiate(gameObject, dragCanvas.transform);
            dragGhostRect = dragGhost.GetComponent<RectTransform>();
            Destroy(dragGhost.GetComponent<BoardCardDrag>());

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

            boardCardView.SetVisible(false);

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

            if (!droppedOnLegalSlot)
            {
                boardCardView.SetVisible(true);
            }

            UpdateHighlights(false);
        }

        public void HandleDroppedOn(BoardSlotDropTarget slot)
        {
            if (!CanDragThisUnit())
            {
                return;
            }

            if (slot.Side != boardCardView.Unit.Owner)
            {
                return;
            }

            int fromSlot = boardCardView.Unit.SlotIndex;
            GameState state = gameManager.State;
            bool isOwnUnit = boardCardView.Unit.Owner == state.ActivePlayer;

            if (!isOwnUnit)
            {
                if (gameManager.Phases.MoveEnemyUnitViaGrantedAbility(state.ActivePlayer, fromSlot, slot.SlotIndex))
                {
                    droppedOnLegalSlot = true;
                    Debug.Log($"[BoardCardDrag] Granted enemy move used: {boardCardView.Unit.SourceCard.CardName} {fromSlot} -> {slot.SlotIndex}");
                }

                return;
            }

            if (state.HasPendingFreeMove && IsPendingFreeMoveEligible())
            {
                if (gameManager.Phases.MoveUnitFree(boardCardView.Unit.Owner, fromSlot, slot.SlotIndex))
                {
                    state.HasPendingFreeMove = false;
                    state.PendingFreeMoveExcludedUnit = null;
                    droppedOnLegalSlot = true;
                    Debug.Log($"[BoardCardDrag] Pending free move consumed: {boardCardView.Unit.SourceCard.CardName} {fromSlot} -> {slot.SlotIndex}");
                }

                return;
            }

            if (gameManager.Phases.TryMoveUnit(fromSlot, slot.SlotIndex))
            {
                droppedOnLegalSlot = true;
            }
        }

        private void UpdateHighlights(bool show)
        {
            if (show && !CanDragThisUnit())
            {
                return;
            }

            if (boardCardView.Unit == null)
            {
                return;
            }

            int fromSlot = boardCardView.Unit.SlotIndex;
            PlayerSide owner = boardCardView.Unit.Owner;
            bool isOwnUnit = owner == gameManager.State.ActivePlayer;
            bool useFreeMoveRules = gameManager.State.HasPendingFreeMove && IsPendingFreeMoveEligible();

            foreach (BoardSlotDropTarget slot in GetAllSlotDropTargets())
            {
                bool shouldHighlight;

                if (isOwnUnit)
                {
                    shouldHighlight = show
                        && slot.Side == owner
                        && gameManager.Phases.CanMoveUnit(fromSlot, slot.SlotIndex, ignoreMoveLimitAndCost: useFreeMoveRules);
                }
                else
                {
                    shouldHighlight = show
                        && slot.Side == owner
                        && gameManager.Phases.CanMoveEnemyUnitViaGrantedAbility(gameManager.State.ActivePlayer, fromSlot, slot.SlotIndex);
                }

                slot.SetHighlighted(shouldHighlight);
            }
        }
    }
}