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
        private NetworkedMatchSync networkSync;

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
            bool hasPendingTarget = gameManager.Phases.HasBlockingPendingTargetedEffect();

            if (shownPhase == currentPhase && shownHasPendingTarget == hasPendingTarget)
            {
                return;
            }

            Refresh(currentPhase, hasPendingTarget);
            shownPhase = currentPhase;
            shownHasPendingTarget = hasPendingTarget;
        }

        private void Refresh(TurnPhase phase, bool hasPendingTarget)
        {
            bool isRelevantPhase = phase == TurnPhase.Action;
            bool isInteractable = isRelevantPhase && !hasPendingTarget;

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

            label.text = hasPendingTarget ? "Choose Target..." : "End Turn";
        }

        private void HandleClick()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            if (gameManager.State.CurrentPhase != TurnPhase.Action)
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