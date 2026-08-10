using UnityEngine;
using TMPro;
using Photon.Pun;
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

        [Header("Player Nicknames")]
        [SerializeField] private TextMeshProUGUI playerANicknameText;
        [SerializeField] private TextMeshProUGUI playerBNicknameText;

        [Header("Turn Info")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI activePlayerText;

        private Color originalPlayerALeaderHealthColor;
        private Color originalPlayerBLeaderHealthColor;
        private NetworkedMatchSync networkSync;

        private int lastLoggedBottomHealth = int.MinValue;
        private int lastLoggedTopHealth = int.MinValue;

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
                if (bottomSeatPlayer.LeaderHealth != lastLoggedBottomHealth)
                {
                    Debug.Log($"[HudController] Bottom seat (playerALeaderHealthText) now showing side={bottomSeatPlayer.Side}, LeaderHealth={bottomSeatPlayer.LeaderHealth} (LocalSide={LocalSide}).");
                    lastLoggedBottomHealth = bottomSeatPlayer.LeaderHealth;
                }

                playerALeaderHealthText.text = bottomSeatPlayer.LeaderHealth.ToString();
                playerALeaderHealthText.color = CardDisplayFormatter.GetHealthColor(bottomSeatPlayer.LeaderHealth, bottomSeatPlayer.MaxLeaderHealth, originalPlayerALeaderHealthColor);
            }

            if (playerBLeaderHealthText != null)
            {
                if (topSeatPlayer.LeaderHealth != lastLoggedTopHealth)
                {
                    Debug.Log($"[HudController] Top seat (playerBLeaderHealthText) now showing side={topSeatPlayer.Side}, LeaderHealth={topSeatPlayer.LeaderHealth} (LocalSide={LocalSide}).");
                    lastLoggedTopHealth = topSeatPlayer.LeaderHealth;
                }

                playerBLeaderHealthText.text = topSeatPlayer.LeaderHealth.ToString();
                playerBLeaderHealthText.color = CardDisplayFormatter.GetHealthColor(topSeatPlayer.LeaderHealth, topSeatPlayer.MaxLeaderHealth, originalPlayerBLeaderHealthColor);
            }

            if (playerANicknameText != null)
            {
                playerANicknameText.text = GetNicknameForSide(bottomSeatPlayer.Side);
            }

            if (playerBNicknameText != null)
            {
                playerBNicknameText.text = GetNicknameForSide(topSeatPlayer.Side);
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

        private static string GetNicknameForSide(PlayerSide side)
        {
            if (!PhotonNetwork.InRoom)
            {
                return side == PlayerSide.PlayerA ? "Player A" : "Player B";
            }

            foreach (Photon.Realtime.Player photonPlayer in PhotonNetwork.PlayerList)
            {
                if (NetworkedMatchSync.SideForActorNumber(photonPlayer.ActorNumber) == side)
                {
                    return photonPlayer.NickName;
                }
            }

            return side == PlayerSide.PlayerA ? "Player A" : "Player B";
        }
    }
}