using UnityEngine;
using UnityEngine.UI;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class SurrenderButton : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Button surrenderButton;

        private NetworkedMatchSync networkSync;
        private MatchLifecycleController lifecycle;

        private void Awake()
        {
            if (surrenderButton != null)
            {
                surrenderButton.onClick.AddListener(HandleSurrenderClicked);
            }

            if (gameManager != null)
            {
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
                lifecycle = gameManager.GetComponent<MatchLifecycleController>();
            }
        }

        private void HandleSurrenderClicked()
        {
            if (gameManager == null || gameManager.State == null || gameManager.State.IsGameOver)
            {
                return;
            }

            if (networkSync != null)
            {
                networkSync.RequestSurrender();
            }

            if (lifecycle != null)
            {
                lifecycle.ReturnToMainMenu();
            }
        }
    }
}