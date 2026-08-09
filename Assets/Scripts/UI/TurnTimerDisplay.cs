using Photon.Pun;
using TMPro;
using UnityEngine;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class TurnTimerDisplay : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI timerText;

        private TurnTimerController turnTimer;
        private NetworkedMatchSync networkSync;

        private void Awake()
        {
            if (gameManager != null)
            {
                turnTimer = gameManager.GetComponent<TurnTimerController>();
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            }
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State == null || turnTimer == null || timerText == null)
            {
                return;
            }

            float? remaining = GetRelevantRemainingSeconds();

            if (!remaining.HasValue)
            {
                if (timerText.enabled)
                {
                    Debug.Log($"[TurnTimerDisplay] Hiding timer - no remaining seconds for phase={gameManager.State.CurrentPhase} (IsMasterClient={PhotonNetwork.IsMasterClient}).");
                }

                timerText.enabled = false;
                return;
            }

            timerText.enabled = true;
            timerText.text = Mathf.CeilToInt(remaining.Value).ToString();
        }

        private float? GetRelevantRemainingSeconds()
        {
            TurnPhase phase = gameManager.State.CurrentPhase;

            if (phase == TurnPhase.Draft)
            {
                PlayerSide localSide = networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;
                return turnTimer.GetDraftRemainingSeconds(localSide);
            }

            if (phase == TurnPhase.Mulligan)
            {
                PlayerSide localSide = networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;
                return turnTimer.GetMulliganRemainingSeconds(localSide);
            }

            if (phase == TurnPhase.Action)
            {
                return turnTimer.GetTurnRemainingSeconds();
            }

            return null;
        }
    }
}