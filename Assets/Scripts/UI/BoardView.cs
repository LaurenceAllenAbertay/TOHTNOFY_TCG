using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BoardCardView boardCardViewPrefab;

        [Header("Player A Slot Containers (index 0-6)")]
        [SerializeField] private Transform[] playerASlotContainers = new Transform[Board.SlotsPerSide];

        [Header("Player B Slot Containers (index 0-6)")]
        [SerializeField] private Transform[] playerBSlotContainers = new Transform[Board.SlotsPerSide];

        [Header("Card Layers (siblings of the slot rows, rendered on top)")]
        [SerializeField] private RectTransform playerACardLayer;
        [SerializeField] private RectTransform playerBCardLayer;

        public IReadOnlyList<Transform> PlayerASlotContainers => playerASlotContainers;
        public IReadOnlyList<Transform> PlayerBSlotContainers => playerBSlotContainers;

        public Transform GetSlotContainer(PlayerSide actualSide, int slotIndex)
        {
            PlayerSide seat = actualSide.ToActualSide(LocalSide);
            Transform[] containers = seat == PlayerSide.PlayerA ? playerASlotContainers : playerBSlotContainers;

            if (slotIndex < 0 || slotIndex >= containers.Length)
            {
                Debug.LogWarning($"[BoardView] GetSlotContainer: slotIndex {slotIndex} out of range for {actualSide}.");
                return null;
            }

            return containers[slotIndex];
        }

        private readonly BoardUnit[] shownPlayerAUnits = new BoardUnit[Board.SlotsPerSide];
        private readonly BoardUnit[] shownPlayerBUnits = new BoardUnit[Board.SlotsPerSide];

        private readonly BoardCardView[] spawnedPlayerAViews = new BoardCardView[Board.SlotsPerSide];
        private readonly BoardCardView[] spawnedPlayerBViews = new BoardCardView[Board.SlotsPerSide];

        private NetworkedMatchSync networkSync;

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

        private void Awake()
        {
            if (gameManager != null)
            {
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            }
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            RefreshSide(PlayerSide.PlayerA.ToActualSide(LocalSide), playerASlotContainers, playerACardLayer, shownPlayerAUnits, spawnedPlayerAViews);
            RefreshSide(PlayerSide.PlayerB.ToActualSide(LocalSide), playerBSlotContainers, playerBCardLayer, shownPlayerBUnits, spawnedPlayerBViews);
        }

        private void RefreshSide(PlayerSide side, Transform[] containers, RectTransform cardLayer, BoardUnit[] shownUnits, BoardCardView[] spawnedViews)
        {
            GameState gameState = gameManager.State;
            Board board = gameState.Board;

            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit currentUnit = board.GetUnit(side, i);

                if (currentUnit == shownUnits[i])
                {
                    if (currentUnit != null && spawnedViews[i] != null)
                    {
                        spawnedViews[i].Bind(currentUnit, gameState);
                        SyncCardPositionToSlot(spawnedViews[i], containers[i]);
                    }

                    continue;
                }

                if (spawnedViews[i] != null)
                {
                    Destroy(spawnedViews[i].gameObject);
                    spawnedViews[i] = null;
                }

                if (currentUnit != null && boardCardViewPrefab != null && containers[i] != null && cardLayer != null)
                {
                    BoardCardView view = Instantiate(boardCardViewPrefab, cardLayer);
                    view.Bind(currentUnit, gameState);
                    SyncCardPositionToSlot(view, containers[i]);
                    spawnedViews[i] = view;
                }

                if (containers[i] != null)
                {
                    BoardSlotDropTarget dropTarget = containers[i].GetComponent<BoardSlotDropTarget>();
                    if (dropTarget != null)
                    {
                        dropTarget.SetOccupied(currentUnit != null);
                    }
                }

                shownUnits[i] = currentUnit;
            }
        }

        private static void SyncCardPositionToSlot(BoardCardView view, Transform slotContainer)
        {
            if (view == null || slotContainer == null)
            {
                return;
            }

            RectTransform cardRect = view.transform as RectTransform;
            RectTransform slotRect = slotContainer as RectTransform;

            if (cardRect == null || slotRect == null)
            {
                return;
            }

            Debug.Log($"[BoardView] SyncCardPositionToSlot: slot='{slotContainer.name}' slotWorldPos={slotRect.position} cardWorldPosBefore={cardRect.position}");

            cardRect.position = slotRect.position;

            Debug.Log($"[BoardView] SyncCardPositionToSlot: cardWorldPosAfter={cardRect.position}");
        }
    }
}