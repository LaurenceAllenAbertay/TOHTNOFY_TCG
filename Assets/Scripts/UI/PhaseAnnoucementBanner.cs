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
        private bool hasShownFirstActionBanner;
        private NetworkedMatchSync networkSync;

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

        private void OnEnable()
        {
            if (gameManager == null)
            {
                Debug.LogWarning("[PhaseAnnouncementBanner] gameManager reference not set.");
                return;
            }

            gameManager.State.PhaseAnnounced += HandlePhaseAnnounced;
            networkSync = gameManager.GetComponent<NetworkedMatchSync>();
        }

        private void OnDisable()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            gameManager.State.PhaseAnnounced -= HandlePhaseAnnounced;
        }

        private void HandlePhaseAnnounced(TurnPhase newPhase, PlayerSide activePlayerAtAnnouncement)
        {
            string text = GetAnnouncementText(newPhase, activePlayerAtAnnouncement);

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

        private string GetAnnouncementText(TurnPhase newPhase, PlayerSide activePlayerAtAnnouncement)
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

                case TurnPhase.Action:
                    if (!hasShownFirstActionBanner)
                    {
                        hasShownFirstActionBanner = true;
                        return null;
                    }
                    return activePlayerAtAnnouncement == LocalSide ? "YOUR TURN" : "OPPONENT'S TURN";

                default:
                    return null;
            }
        }
    }
}