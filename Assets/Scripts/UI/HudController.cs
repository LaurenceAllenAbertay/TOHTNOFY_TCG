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

        private void Awake()
        {
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

            if (manaText != null)
            {
                manaText.text = $"Mana: {active.CurrentMana} / {active.MaxManaThisGame}";
            }

            if (playerALeaderHealthText != null)
            {
                playerALeaderHealthText.text = state.PlayerA.LeaderHealth.ToString();
                playerALeaderHealthText.color = CardDisplayFormatter.GetHealthColor(state.PlayerA.LeaderHealth, state.PlayerA.MaxLeaderHealth, originalPlayerALeaderHealthColor);
            }

            if (playerBLeaderHealthText != null)
            {
                playerBLeaderHealthText.text = state.PlayerB.LeaderHealth.ToString();
                playerBLeaderHealthText.color = CardDisplayFormatter.GetHealthColor(state.PlayerB.LeaderHealth, state.PlayerB.MaxLeaderHealth, originalPlayerBLeaderHealthColor);
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
                activePlayerText.text = $"Active: {state.ActivePlayer}";
            }
        }
    }
}