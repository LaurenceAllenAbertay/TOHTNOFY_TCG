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

        private void Awake()
        {
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

            bool isTargeting = gameManager.State.PendingTargetedEffect != null;

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