using UnityEngine;
using TMPro;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class TargetPromptDisplay : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private string promptMessage = "Choose a target...";

        private bool? shownIsTargeting;
        private NetworkedMatchSync networkSync;

        private void Awake()
        {
            networkSync = gameManager != null ? gameManager.GetComponent<NetworkedMatchSync>() : null;

            if (promptRoot != null)
            {
                promptRoot.SetActive(false);
            }
            else if (promptText != null)
            {
                promptText.enabled = false;
            }
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            PlayerSide localSide = networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;
            BoardUnit pendingSource = gameManager.State.PendingTargetedEffectSource;

            bool isTargeting = gameManager.State.PendingTargetedEffect != null
                && pendingSource != null
                && pendingSource.Owner == localSide;

            if (shownIsTargeting == isTargeting)
            {
                return;
            }

            Refresh(isTargeting);
            shownIsTargeting = isTargeting;
        }

        private void Refresh(bool isTargeting)
        {
            if (promptRoot != null)
            {
                promptRoot.SetActive(isTargeting);
            }

            if (promptText != null)
            {
                promptText.enabled = isTargeting;

                if (isTargeting)
                {
                    promptText.text = promptMessage;
                }
            }
        }
    }
}