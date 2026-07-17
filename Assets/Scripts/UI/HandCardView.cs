using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.UI
{
    public class HandCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image artImage;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private float hoverRiseAmount = 67f;
        [SerializeField] private float hoverLerpSpeed = 12f;

        private CanvasGroup canvasGroup;
        private bool isHovered;
        private Vector2 visualRootRestPosition;

        public CardData Card { get; private set; }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (visualRoot != null)
            {
                visualRootRestPosition = visualRoot.anchoredPosition;
            }
        }

        private void Update()
        {
            if (visualRoot == null)
            {
                return;
            }

            Vector2 targetPosition = visualRootRestPosition + (isHovered ? new Vector2(0f, hoverRiseAmount) : Vector2.zero);
            float t = 1f - Mathf.Exp(-hoverLerpSpeed * Time.deltaTime);
            visualRoot.anchoredPosition = Vector2.Lerp(visualRoot.anchoredPosition, targetPosition, t);
        }

        public void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
        }

        public void Bind(CardData card)
        {
            Card = card;

            if (artImage != null)
            {
                artImage.sprite = card.CardArt;
            }

            if (costText != null)
            {
                costText.text = CardDisplayFormatter.GetCostText(card);
            }

            if (nameText != null)
            {
                nameText.text = CardDisplayFormatter.GetNameText(card);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            CardHoverPreview.Show(Card, transform.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            CardHoverPreview.Hide();
        }
    }
}