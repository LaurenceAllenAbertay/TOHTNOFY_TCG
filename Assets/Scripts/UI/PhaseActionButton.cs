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

        private TurnPhase? shownPhase;
        private bool? shownHasPendingTarget;
        private PlayerSide? shownActivePlayer;
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

            if (shownPhase == currentPhase && shownHasPendingTarget == hasPendingTarget && shownActivePlayer == currentActivePlayer)
            {
                return;
            }

            Refresh(currentPhase, currentActivePlayer, hasPendingTarget);
            shownPhase = currentPhase;
            shownHasPendingTarget = hasPendingTarget;
            shownActivePlayer = currentActivePlayer;
        }

        private void Refresh(TurnPhase phase, PlayerSide activePlayer, bool hasPendingTarget)
        {
            bool isRelevantPhase = phase == TurnPhase.Action;
            bool isMyTurn = activePlayer == LocalSide;
            bool isInteractable = isRelevantPhase && isMyTurn && !hasPendingTarget;

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

            label.text = hasPendingTarget ? "Choose Target..." : "End Turn";
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