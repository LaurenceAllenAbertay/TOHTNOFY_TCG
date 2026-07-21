using System.Collections.Generic;
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

        private readonly List<UI.ChoiceCardSelectable> spawnedCards = new List<UI.ChoiceCardSelectable>();
        private List<CardData> shownOptions;
        private UI.ChoiceCardSelectable selectedCard;

        private void Awake()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirm);
            }
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            List<CardData> currentOptions = gameManager.State.PendingCardChoiceOptions;

            if (currentOptions == shownOptions)
            {
                return;
            }

            Refresh(currentOptions);
            shownOptions = currentOptions;
        }

        private void Refresh(List<CardData> options)
        {
            bool isChoicePending = options != null;

            if (panelRoot != null)
            {
                panelRoot.SetActive(isChoicePending);
            }

            ClearSpawnedCards();
            UpdateConfirmInteractable();

            if (!isChoicePending || cardContainer == null || choiceCardPrefab == null)
            {
                return;
            }

            PlayerSide side = gameManager.State.PendingCardChoiceSource != null
                ? gameManager.State.PendingCardChoiceSource.Owner
                : gameManager.State.ActivePlayer;

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

            Debug.Log($"[CardPoolChoicePanel] Refreshed with {options.Count} offered card(s).");
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

            bool resolved = gameManager.Phases.TryResolvePendingCardChoice(selectedCard.Card);

            Debug.Log($"[CardPoolChoicePanel] HandleConfirm resolved={resolved} for {selectedCard.Card.CardName}.");
        }
    }
}