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

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
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
            bool isRelevantPhase = phase == TurnPhase.Play || phase == TurnPhase.Move;
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

            if (hasPendingTarget)
            {
                label.text = "Choose Target...";
                return;
            }

            switch (phase)
            {
                case TurnPhase.Play:
                    label.text = "Attack!";
                    break;
                case TurnPhase.Move:
                    label.text = "End Turn";
                    break;
            }
        }

        private void HandleClick()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            switch (gameManager.State.CurrentPhase)
            {
                case TurnPhase.Play:
                    gameManager.Phases.EnterAttackPhase();
                    break;
                case TurnPhase.Move:
                    gameManager.Phases.PassMoveToEndTurn();
                    break;
            }
        }
    }
}