using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class HandCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image artImage;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI abilityText;
        [SerializeField] private Image rarityIndicator;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private RectTransform selfRect;
        [SerializeField] private float hoverRiseAmount = 67f;
        [SerializeField] private float hoverLerpSpeed = 12f;
        [SerializeField] private float slotLerpSpeed = 14f;

        private const float DrawArrivalThreshold = 0.001f;

        private CanvasGroup canvasGroup;
        private bool isHovered;
        private Vector2 visualRootRestPosition;
        private Vector2 targetSlotPosition;
        private bool hasTargetSlotPosition;
        private bool slotLerpEnabled = true;
        private PlayerSide ownerSide;
        private GameState boundState;
        private bool useLiveCost;
        private Vector3 restScale = Vector3.one;
        private bool isArrivingFromDeck;

        public CardData Card { get; private set; }
        public bool IsArrivingFromDeck => isArrivingFromDeck;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (selfRect == null)
            {
                selfRect = transform as RectTransform;
            }

            if (selfRect != null)
            {
                restScale = selfRect.localScale;
            }

            if (visualRoot != null)
            {
                visualRootRestPosition = visualRoot.anchoredPosition;
            }
        }

        private void Update()
        {
            UpdateSlotPosition();
            UpdateDrawGrowth();
            UpdateHoverOffset();
        }

        private void UpdateSlotPosition()
        {
            if (selfRect == null || !hasTargetSlotPosition)
            {
                return;
            }

            if (!slotLerpEnabled)
            {
                selfRect.anchoredPosition = targetSlotPosition;
                return;
            }

            float t = 1f - Mathf.Exp(-slotLerpSpeed * Time.deltaTime);
            selfRect.anchoredPosition = Vector2.Lerp(selfRect.anchoredPosition, targetSlotPosition, t);
        }

        private void UpdateDrawGrowth()
        {
            if (!isArrivingFromDeck || selfRect == null)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-slotLerpSpeed * Time.deltaTime);
            selfRect.localScale = Vector3.Lerp(selfRect.localScale, restScale, t);

            if ((selfRect.localScale - restScale).sqrMagnitude <= DrawArrivalThreshold * DrawArrivalThreshold)
            {
                selfRect.localScale = restScale;
                isArrivingFromDeck = false;
                Debug.Log($"[HandCardView] {(Card != null ? Card.CardName : "Face-down card")} finished arriving from the deck at {selfRect.anchoredPosition} (target {targetSlotPosition}).");
            }
        }

        private void UpdateHoverOffset()
        {
            if (visualRoot == null)
            {
                return;
            }

            Vector2 targetPosition = visualRootRestPosition + (isHovered ? new Vector2(0f, hoverRiseAmount) : Vector2.zero);
            float t = 1f - Mathf.Exp(-hoverLerpSpeed * Time.deltaTime);
            visualRoot.anchoredPosition = Vector2.Lerp(visualRoot.anchoredPosition, targetPosition, t);
        }

        public void SetSlotTarget(Vector2 position, bool snapImmediately = false)
        {
            targetSlotPosition = position;
            hasTargetSlotPosition = true;

            if (snapImmediately && selfRect != null)
            {
                selfRect.anchoredPosition = position;
            }
        }

        public void BeginDrawFrom(RectTransform origin)
        {
            if (selfRect == null || origin == null)
            {
                return;
            }

            selfRect.position = origin.position;
            selfRect.localScale = Vector3.zero;
            isArrivingFromDeck = true;

            Debug.Log($"[HandCardView] {(Card != null ? Card.CardName : "Face-down card")} starting draw from '{origin.name}' at anchored {selfRect.anchoredPosition} -> slot {targetSlotPosition}, rest scale {restScale}.");
        }

        public void SetSlotLerpEnabled(bool enabled)
        {
            slotLerpEnabled = enabled;
        }

        public void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
        }

        public void SetDimmed(bool dimmed)
        {
            canvasGroup.alpha = dimmed ? 0.6f : 1f;
        }

        public void Bind(CardData card, PlayerSide side, GameState state, bool useLiveCost = true)
        {
            Card = card;
            ownerSide = side;
            boundState = state;
            this.useLiveCost = useLiveCost;

            if (artImage != null)
            {
                artImage.sprite = card.CardArt;
            }

            if (costText != null)
            {
                costText.text = useLiveCost
                    ? CardDisplayFormatter.GetCurrentCostText(card, side, state)
                    : CardDisplayFormatter.GetCostText(card);
            }

            if (nameText != null)
            {
                nameText.text = CardDisplayFormatter.GetNameText(card);
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
                    attackText.text = useLiveCost
                        ? CardDisplayFormatter.GetAttackText(unitCard, side, state)
                        : CardDisplayFormatter.GetAttackText(unitCard);
                }
            }

            if (healthText != null)
            {
                healthText.gameObject.SetActive(isUnit);
                if (isUnit)
                {
                    healthText.text = useLiveCost
                        ? CardDisplayFormatter.GetHealthText(unitCard, side, state)
                        : CardDisplayFormatter.GetHealthText(unitCard);
                }
            }

            if (rarityIndicator != null)
            {
                if (CardRarityReference.TryGetColor(card.Rarity, out Color rarityColor))
                {
                    rarityIndicator.color = rarityColor;
                }
                else
                {
                    Debug.LogWarning($"[HandCardView] No rarity color defined for {card.Rarity} on card {card.CardName}.");
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.dragging)
            {
                return;
            }

            isHovered = true;
            CardHoverPreview.Show(Card, ownerSide, boundState, transform.position, useLiveCost);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            CardHoverPreview.Hide();
        }
    }
}