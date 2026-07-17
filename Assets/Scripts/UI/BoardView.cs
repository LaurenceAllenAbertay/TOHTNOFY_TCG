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

        public IReadOnlyList<Transform> PlayerASlotContainers => playerASlotContainers;
        public IReadOnlyList<Transform> PlayerBSlotContainers => playerBSlotContainers;

        private readonly BoardUnit[] shownPlayerAUnits = new BoardUnit[Board.SlotsPerSide];
        private readonly BoardUnit[] shownPlayerBUnits = new BoardUnit[Board.SlotsPerSide];

        private readonly BoardCardView[] spawnedPlayerAViews = new BoardCardView[Board.SlotsPerSide];
        private readonly BoardCardView[] spawnedPlayerBViews = new BoardCardView[Board.SlotsPerSide];

        private void Update()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            RefreshSide(PlayerSide.PlayerA, playerASlotContainers, shownPlayerAUnits, spawnedPlayerAViews);
            RefreshSide(PlayerSide.PlayerB, playerBSlotContainers, shownPlayerBUnits, spawnedPlayerBViews);
        }

        private void RefreshSide(PlayerSide side, Transform[] containers, BoardUnit[] shownUnits, BoardCardView[] spawnedViews)
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
                    }

                    continue;
                }

                if (spawnedViews[i] != null)
                {
                    Destroy(spawnedViews[i].gameObject);
                    spawnedViews[i] = null;
                }

                if (currentUnit != null && boardCardViewPrefab != null && containers[i] != null)
                {
                    BoardCardView view = Instantiate(boardCardViewPrefab, containers[i]);
                    view.Bind(currentUnit, gameState);
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
    }
}