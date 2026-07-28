using UnityEngine;
using TMPro;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class PhaseAnnouncementBanner : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Animator animator;
        [SerializeField] private TextMeshProUGUI label;

        private static readonly int NewPhaseTrigger = Animator.StringToHash("NewPhase");

        private bool hasGameStarted;
        private bool hasShownFirstPlayBanner;

        private void OnEnable()
        {
            if (gameManager == null)
            {
                Debug.LogWarning("[PhaseAnnouncementBanner] gameManager reference not set.");
                return;
            }

            gameManager.State.PhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            gameManager.State.PhaseChanged -= HandlePhaseChanged;
        }

        private void HandlePhaseChanged(TurnPhase newPhase)
        {
            string text = GetAnnouncementText(newPhase);

            if (text == null)
            {
                return;
            }

            if (label != null)
            {
                label.text = text;
            }

            if (animator != null)
            {
                animator.SetTrigger(NewPhaseTrigger);
            }
        }

        public void OnBannerAnimationComplete()
        {
            if (gameManager == null || gameManager.State == null)
            {
                Debug.LogWarning("[PhaseAnnouncementBanner] OnBannerAnimationComplete fired but gameManager/State is null.");
                return;
            }

            gameManager.State.RaiseBannerAnimationFinished();
        }

        private string GetAnnouncementText(TurnPhase newPhase)
        {
            switch (newPhase)
            {
                case TurnPhase.Draw:
                    if (!hasGameStarted)
                    {
                        hasGameStarted = true;
                        return "GAME START!";
                    }
                    return null;

                case TurnPhase.Play:
                    if (!hasShownFirstPlayBanner)
                    {
                        hasShownFirstPlayBanner = true;
                        return null;
                    }
                    return gameManager.State.ActivePlayer == PlayerSide.PlayerA ? "PLAY PHASE" : "OPPONENT'S TURN";

                case TurnPhase.Attack:
                    return "ATTACK PHASE";

                case TurnPhase.Move:
                    return "MOVE PHASE";

                default:
                    return null;
            }
        }
    }
}