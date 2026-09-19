using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class PhaseActionButton : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Button button;
        [SerializeField] private Image buttonImage;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private string resolvingLabel = "Resolving...";

        private TurnPhase? shownPhase;
        private bool? shownHasPendingTarget;
        private PlayerSide? shownActivePlayer;
        private bool? shownEndTurnRequested;
        private bool endTurnRequested;
        private NetworkedMatchSync networkSync;

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }

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

            TurnPhase currentPhase = gameManager.State.CurrentPhase;
            PlayerSide currentActivePlayer = gameManager.State.ActivePlayer;
            bool hasPendingTarget = gameManager.Phases.HasBlockingPendingTargetedEffect();

            if (endTurnRequested && (currentPhase != TurnPhase.Action || currentActivePlayer != LocalSide))
            {
                Debug.Log($"[PhaseActionButton] {LocalSide}'s requested end turn has gone through (phase={currentPhase}, activePlayer={currentActivePlayer}) - clearing the '{resolvingLabel}' state.");
                endTurnRequested = false;
            }

            if (shownPhase == currentPhase && shownHasPendingTarget == hasPendingTarget && shownActivePlayer == currentActivePlayer && shownEndTurnRequested == endTurnRequested)
            {
                return;
            }

            Refresh(currentPhase, currentActivePlayer, hasPendingTarget, endTurnRequested);
            shownPhase = currentPhase;
            shownHasPendingTarget = hasPendingTarget;
            shownActivePlayer = currentActivePlayer;
            shownEndTurnRequested = endTurnRequested;
        }

        private void Refresh(TurnPhase phase, PlayerSide activePlayer, bool hasPendingTarget, bool isEndTurnRequested)
        {
            bool isRelevantPhase = phase == TurnPhase.Action;
            bool isMyTurn = activePlayer == LocalSide;
            bool isInteractable = isRelevantPhase && isMyTurn && !hasPendingTarget && !isEndTurnRequested;

            if (button != null)
            {
                button.interactable = isInteractable;
            }

            if (buttonImage != null)
            {
                buttonImage.enabled = isRelevantPhase;
            }

            if (label != null)
            {
                label.enabled = isRelevantPhase;
            }

            if (!isRelevantPhase || label == null)
            {
                return;
            }

            if (!isMyTurn)
            {
                label.text = "Opponent's Turn";
                return;
            }

            if (hasPendingTarget)
            {
                label.text = "Choose Target...";
                return;
            }

            label.text = isEndTurnRequested ? resolvingLabel : "End Turn";
        }

        private void HandleClick()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            if (gameManager.State.CurrentPhase != TurnPhase.Action || gameManager.State.ActivePlayer != LocalSide)
            {
                return;
            }

            if (endTurnRequested)
            {
                Debug.Log($"[PhaseActionButton] End Turn clicked again by {LocalSide} while already waiting - ignoring.");
                return;
            }

            endTurnRequested = true;
            Debug.Log($"[PhaseActionButton] End Turn clicked by {LocalSide} - requesting end of action phase. HasUnresolvedActions={gameManager.Phases.HasUnresolvedActions} (only meaningful on the master/offline).");

            if (networkSync != null)
            {
                networkSync.RequestEndActionPhase();
            }
            else
            {
                gameManager.Phases.EndActionPhase();
            }
        }
    }
}