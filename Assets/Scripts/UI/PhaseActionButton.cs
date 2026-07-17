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

            if (shownPhase == currentPhase)
            {
                return;
            }

            Refresh(currentPhase);
            shownPhase = currentPhase;
        }

        private void Refresh(TurnPhase phase)
        {
            bool isRelevantPhase = phase == TurnPhase.Play || phase == TurnPhase.Move || phase == TurnPhase.TurnEnd;

            if (button != null)
            {
                button.interactable = isRelevantPhase;
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

            switch (phase)
            {
                case TurnPhase.Play:
                    label.text = "Attack!";
                    break;
                case TurnPhase.Move:
                    label.text = "Pass";
                    break;
                case TurnPhase.TurnEnd:
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
                    gameManager.Phases.EnterTurnEndPhase();
                    break;
                case TurnPhase.TurnEnd:
                    gameManager.Phases.EndTurn();
                    break;
            }
        }
    }
}