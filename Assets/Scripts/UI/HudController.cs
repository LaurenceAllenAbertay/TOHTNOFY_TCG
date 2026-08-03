using UnityEngine;
using TMPro;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class HudController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        [Header("Mana")]
        [SerializeField] private TextMeshProUGUI manaText;

        [Header("Leader Health")]
        [SerializeField] private TextMeshProUGUI playerALeaderHealthText;
        [SerializeField] private TextMeshProUGUI playerBLeaderHealthText;

        [Header("Turn Info")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI activePlayerText;

        private Color originalPlayerALeaderHealthColor;
        private Color originalPlayerBLeaderHealthColor;
        private NetworkedMatchSync networkSync;

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

        private void Awake()
        {
            if (gameManager != null)
            {
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            }

            if (playerALeaderHealthText != null)
            {
                originalPlayerALeaderHealthColor = playerALeaderHealthText.color;
            }

            if (playerBLeaderHealthText != null)
            {
                originalPlayerBLeaderHealthColor = playerBLeaderHealthText.color;
            }
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            Refresh(gameManager.State);
        }

        private void Refresh(GameState state)
        {
            Player active = state.GetActivePlayerData();
            Player bottomSeatPlayer = state.GetPlayer(PlayerSide.PlayerA.ToActualSide(LocalSide));
            Player topSeatPlayer = state.GetPlayer(PlayerSide.PlayerB.ToActualSide(LocalSide));

            if (manaText != null)
            {
                bool manaIsRelevant = state.CurrentPhase != TurnPhase.Draft && state.CurrentPhase != TurnPhase.Mulligan;
                manaText.enabled = manaIsRelevant;

                if (manaIsRelevant)
                {
                    manaText.text = $"Mana: {active.CurrentMana} / {active.MaxManaThisGame}";
                }
            }

            if (playerALeaderHealthText != null)
            {
                playerALeaderHealthText.text = bottomSeatPlayer.LeaderHealth.ToString();
                playerALeaderHealthText.color = CardDisplayFormatter.GetHealthColor(bottomSeatPlayer.LeaderHealth, bottomSeatPlayer.MaxLeaderHealth, originalPlayerALeaderHealthColor);
            }

            if (playerBLeaderHealthText != null)
            {
                playerBLeaderHealthText.text = topSeatPlayer.LeaderHealth.ToString();
                playerBLeaderHealthText.color = CardDisplayFormatter.GetHealthColor(topSeatPlayer.LeaderHealth, topSeatPlayer.MaxLeaderHealth, originalPlayerBLeaderHealthColor);
            }

            if (turnText != null)
            {
                turnText.text = $"Turn {state.TurnNumber}";
            }

            if (phaseText != null)
            {
                phaseText.text = $"Phase: {state.CurrentPhase}";
            }

            if (activePlayerText != null)
            {
                activePlayerText.text = state.ActivePlayer == LocalSide ? "Active: You" : "Active: Opponent";
            }
        }
    }
}