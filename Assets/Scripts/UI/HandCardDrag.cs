using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DDD.TNFY.TCG.Core;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.UI
{
    [RequireComponent(typeof(HandCardView))]
    public class HandCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private GameManager gameManager;
        private Canvas dragCanvas;
        private BoardView boardView;

        private HandCardView handCardView;
        private GameObject dragGhost;
        private RectTransform dragGhostRect;
        private bool droppedOnLegalSlot;
        private List<BoardSlotDropTarget> cachedSlotDropTargets;

        public bool IsDraggingUnitCard => handCardView != null && handCardView.Card is UnitCardData;

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

        public void OnBeginDrag(PointerEventData eventData)
        {
            droppedOnLegalSlot = false;

            if (!(handCardView.Card is UnitCardData))
            {
                return;
            }

            dragGhost = Instantiate(gameObject, dragCanvas.transform);
            dragGhostRect = dragGhost.GetComponent<RectTransform>();
            Destroy(dragGhost.GetComponent<HandCardDrag>());

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

            if (!droppedOnLegalSlot)
            {
                handCardView.SetVisible(true);
            }

            UpdateHighlights(false);
        }

        public void HandleDroppedOn(BoardSlotDropTarget slot)
        {
            if (!(handCardView.Card is UnitCardData unitCard))
            {
                return;
            }

            if (slot.Side != gameManager.State.ActivePlayer)
            {
                return;
            }

            if (gameManager.Phases.TryPlayUnit(unitCard, slot.SlotIndex))
            {
                droppedOnLegalSlot = true;
            }
        }

        private void UpdateHighlights(bool show)
        {
            UnitCardData unitCard = handCardView.Card as UnitCardData;

            foreach (BoardSlotDropTarget slot in GetAllSlotDropTargets())
            {
                bool shouldHighlight = show
                    && unitCard != null
                    && slot.Side == gameManager.State.ActivePlayer
                    && gameManager.Phases.CanPlayUnit(unitCard, slot.SlotIndex);

                slot.SetHighlighted(shouldHighlight);
            }
        }
    }
}