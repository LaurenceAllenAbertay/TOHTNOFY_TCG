using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.UI
{
    public class CardHoverPreview : MonoBehaviour
    {
        private static CardHoverPreview instance;

        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private Image artImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI abilityText;
        [SerializeField] private float leftSideX = 300f;
        [SerializeField] private float rightSideX = -300f;
        [SerializeField] private float hoverDelaySeconds = 1f;
        [SerializeField] private float hideGracePeriodSeconds = 0.15f;

        private Coroutine pendingShowCoroutine;
        private Coroutine pendingHideCoroutine;

        private void Awake()
        {
            instance = this;

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public static void Show(CardData card, Vector3 screenPosition)
        {
            if (instance == null || card == null)
            {
                return;
            }

            instance.BeginShow(card, screenPosition);
        }

        public static void Hide()
        {
            if (instance == null)
            {
                return;
            }

            instance.BeginHide();
        }

        private void BeginShow(CardData card, Vector3 screenPosition)
        {
            CancelPendingHide();
            CancelPendingShow();

            bool alreadyVisible = root != null && root.activeSelf;

            if (alreadyVisible)
            {
                DisplayCard(card, screenPosition);
                return;
            }

            pendingShowCoroutine = StartCoroutine(ShowAfterDelay(card, screenPosition));
        }

        private void BeginHide()
        {
            CancelPendingShow();
            CancelPendingHide();
            pendingHideCoroutine = StartCoroutine(HideAfterGracePeriod());
        }

        private void CancelPendingShow()
        {
            if (pendingShowCoroutine != null)
            {
                StopCoroutine(pendingShowCoroutine);
                pendingShowCoroutine = null;
            }
        }

        private void CancelPendingHide()
        {
            if (pendingHideCoroutine != null)
            {
                StopCoroutine(pendingHideCoroutine);
                pendingHideCoroutine = null;
            }
        }

        private System.Collections.IEnumerator HideAfterGracePeriod()
        {
            yield return new WaitForSeconds(hideGracePeriodSeconds);
            pendingHideCoroutine = null;

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private System.Collections.IEnumerator ShowAfterDelay(CardData card, Vector3 screenPosition)
        {
            yield return new WaitForSeconds(hoverDelaySeconds);
            pendingShowCoroutine = null;
            DisplayCard(card, screenPosition);
        }

        private void DisplayCard(CardData card, Vector3 screenPosition)
        {
            if (root != null)
            {
                root.SetActive(true);
            }

            if (rootRect != null)
            {
                bool cardIsOnLeftHalf = screenPosition.x < Screen.width / 2f;
                float targetX = cardIsOnLeftHalf ? leftSideX : rightSideX;

                Vector2 anchoredPosition = rootRect.anchoredPosition;
                anchoredPosition.x = targetX;
                rootRect.anchoredPosition = anchoredPosition;
            }

            if (artImage != null)
            {
                artImage.sprite = card.CardArt;
            }

            if (nameText != null)
            {
                nameText.text = CardDisplayFormatter.GetNameText(card);
            }

            if (costText != null)
            {
                costText.text = CardDisplayFormatter.GetCostText(card);
            }

            if (abilityText != null)
            {
                abilityText.text = CardDisplayFormatter.GetAbilityText(card);
            }

            UnitCardData unitCard = card as UnitCardData;
            bool isUnit = unitCard != null;

            if (attackText != null)
            {
                attackText.gameObject.SetActive(isUnit);
                if (isUnit)
                {
                    attackText.text = CardDisplayFormatter.GetAttackText(unitCard);
                }
            }

            if (healthText != null)
            {
                healthText.gameObject.SetActive(isUnit);
                if (isUnit)
                {
                    healthText.text = CardDisplayFormatter.GetHealthText(unitCard);
                }
            }
        }
    }
}