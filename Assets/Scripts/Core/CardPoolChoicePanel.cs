using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.Core
{
    public class CardPoolChoicePanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private UI.HandCardView choiceCardPrefab;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI stageLabel;
        [SerializeField] private Button toggleVisibilityButton;
        [SerializeField] private GameObject toggleVisibilityButtonRoot;
        [SerializeField] private Image toggleVisibilityIcon;
        [SerializeField] private Sprite showPanelSprite;
        [SerializeField] private Sprite hidePanelSprite;

        private readonly List<UI.ChoiceCardSelectable> spawnedCards = new List<UI.ChoiceCardSelectable>();
        private List<CardData> shownOptions;
        private UI.ChoiceCardSelectable selectedCard;
        private bool isDraftChoice;
        private bool isManuallyHidden;

        private void Awake()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirm);
            }

            if (toggleVisibilityButton != null)
            {
                toggleVisibilityButton.onClick.AddListener(HandleToggleVisibility);
            }
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            bool draftPending = gameManager.State.PendingDraftOptions != null;
            List<CardData> currentOptions = draftPending
                ? gameManager.State.PendingDraftOptions
                : gameManager.State.PendingCardChoiceOptions;

            if (currentOptions == shownOptions)
            {
                return;
            }

            Refresh(currentOptions, draftPending);
            shownOptions = currentOptions;
        }

        private void Refresh(List<CardData> options, bool draftPending)
        {
            bool isChoicePending = options != null;
            isDraftChoice = draftPending;

            if (!isChoicePending)
            {
                isManuallyHidden = false;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(isChoicePending && !isManuallyHidden);
            }

            if (toggleVisibilityButtonRoot != null)
            {
                toggleVisibilityButtonRoot.SetActive(isChoicePending);
            }

            UpdateToggleIcon();

            ClearSpawnedCards();
            UpdateConfirmInteractable();

            if (stageLabel != null)
            {
                stageLabel.text = draftPending
                    ? $"{gameManager.State.ActivePlayer}: Choose a {gameManager.State.CurrentDraftStage} Card"
                    : string.Empty;
            }

            if (!isChoicePending || cardContainer == null || choiceCardPrefab == null)
            {
                return;
            }

            PlayerSide side = draftPending
                ? gameManager.State.ActivePlayer
                : (gameManager.State.PendingCardChoiceSource != null
                    ? gameManager.State.PendingCardChoiceSource.Owner
                    : gameManager.State.ActivePlayer);

            foreach (CardData card in options)
            {
                UI.HandCardView view = Instantiate(choiceCardPrefab, cardContainer);
                view.Bind(card, side, gameManager.State, useLiveCost: false);

                UI.ChoiceCardSelectable selectable = view.GetComponent<UI.ChoiceCardSelectable>();
                if (selectable != null)
                {
                    selectable.Clicked += HandleCardClicked;
                    spawnedCards.Add(selectable);
                }
            }

            Debug.Log($"[CardPoolChoicePanel] Refreshed with {options.Count} offered card(s), isDraftChoice={isDraftChoice}.");
        }

        private void HandleCardClicked(UI.ChoiceCardSelectable clicked)
        {
            if (selectedCard == clicked)
            {
                return;
            }

            if (selectedCard != null)
            {
                selectedCard.SetSelected(false);
            }

            selectedCard = clicked;
            selectedCard.SetSelected(true);

            Debug.Log($"[CardPoolChoicePanel] Selected {clicked.Card.CardName}.");

            UpdateConfirmInteractable();
        }

        private void UpdateConfirmInteractable()
        {
            if (confirmButton != null)
            {
                confirmButton.interactable = selectedCard != null;
            }
        }

        private void HandleToggleVisibility()
        {
            isManuallyHidden = !isManuallyHidden;

            if (panelRoot != null)
            {
                bool isChoicePending = shownOptions != null;
                panelRoot.SetActive(isChoicePending && !isManuallyHidden);
            }

            UpdateToggleIcon();

            Debug.Log($"[CardPoolChoicePanel] Toggle pressed, isManuallyHidden={isManuallyHidden}.");
        }

        private void UpdateToggleIcon()
        {
            if (toggleVisibilityIcon == null)
            {
                return;
            }

            toggleVisibilityIcon.sprite = isManuallyHidden ? showPanelSprite : hidePanelSprite;
        }

        private void ClearSpawnedCards()
        {
            foreach (UI.ChoiceCardSelectable existing in spawnedCards)
            {
                if (existing != null)
                {
                    existing.Clicked -= HandleCardClicked;
                    Destroy(existing.gameObject);
                }
            }

            spawnedCards.Clear();
            selectedCard = null;
        }

        private void HandleConfirm()
        {
            if (gameManager == null || gameManager.State == null || selectedCard == null)
            {
                Debug.Log("[CardPoolChoicePanel] HandleConfirm FAIL: no card selected.");
                return;
            }

            bool resolved = isDraftChoice
                ? gameManager.Phases.TryResolvePendingDraftChoice(selectedCard.Card)
                : gameManager.Phases.TryResolvePendingCardChoice(selectedCard.Card);

            Debug.Log($"[CardPoolChoicePanel] HandleConfirm resolved={resolved} for {selectedCard.Card.CardName}, isDraftChoice={isDraftChoice}.");
        }
    }
}