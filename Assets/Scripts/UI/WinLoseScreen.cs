using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class WinLoseScreen : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button rematchButton;
        [SerializeField] private Button mainMenuButton;

        private MatchLifecycleController lifecycle;
        private NetworkedMatchSync networkSync;
        private bool shownGameOver;

        private void Awake()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            if (rematchButton != null)
            {
                rematchButton.onClick.AddListener(HandleRematchClicked);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(HandleMainMenuClicked);
            }

            if (gameManager != null)
            {
                lifecycle = gameManager.GetComponent<MatchLifecycleController>();
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            }
        }

        private void Update()
        {
            if (shownGameOver || gameManager == null || gameManager.State == null || !gameManager.State.IsGameOver)
            {
                return;
            }

            shownGameOver = true;
            Show();
        }

        private void Show()
        {
            PlayerSide localSide = networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;
            bool localPlayerWon = gameManager.State.Winner == localSide;

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (resultText != null)
            {
                resultText.text = localPlayerWon ? "You Win!" : "You Lose!";
            }

            Debug.Log($"[WinLoseScreen] Showing result - winner={gameManager.State.Winner}, localSide={localSide}, localPlayerWon={localPlayerWon}.");
        }

        private void HandleRematchClicked()
        {
            if (lifecycle != null)
            {
                lifecycle.RequestRematch();
            }
        }

        private void HandleMainMenuClicked()
        {
            if (lifecycle != null)
            {
                lifecycle.ReturnToMainMenu();
            }
        }
    }
}