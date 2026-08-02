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

        private readonly List<UI.ChoiceCardSelectable> spawnedCards = new List<UI.ChoiceCardSelectable>();
        private List<CardData> shownOptions;
        private UI.ChoiceCardSelectable selectedCard;
        private bool isDraftChoice;
        private NetworkedMatchSync networkSync;

        private void Awake()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirm);
            }

            if (gameManager != null)
            {
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            }
        }

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

        private void Update()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            Player localPlayer = gameManager.State.GetPlayer(LocalSide);
            bool draftPending = localPlayer.PendingDraftOptions != null;

            List<CardData> currentOptions;

            if (draftPending)
            {
                currentOptions = localPlayer.PendingDraftOptions;
            }
            else
            {
                bool cardChoiceIsMine = gameManager.State.PendingCardChoiceOptions != null
                    && gameManager.State.PendingCardChoiceSource != null
                    && gameManager.State.PendingCardChoiceSource.Owner == LocalSide;

                currentOptions = cardChoiceIsMine ? gameManager.State.PendingCardChoiceOptions : null;
            }

            if (OptionsMatch(currentOptions, shownOptions))
            {
                return;
            }

            Refresh(currentOptions, draftPending);
            shownOptions = currentOptions;
        }

        private static bool OptionsMatch(List<CardData> a, List<CardData> b)
        {
            if (a == b)
            {
                return true;
            }

            if (a == null || b == null || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void Refresh(List<CardData> options, bool draftPending)
        {
            bool isChoicePending = options != null;
            isDraftChoice = draftPending;

            if (panelRoot != null)
            {
                panelRoot.SetActive(isChoicePending);
            }

            ClearSpawnedCards();
            UpdateConfirmInteractable();

            if (stageLabel != null)
            {
                stageLabel.text = draftPending
                    ? $"Choose a {gameManager.State.GetPlayer(LocalSide).CurrentDraftStage} Card"
                    : string.Empty;
            }

            if (!isChoicePending || cardContainer == null || choiceCardPrefab == null)
            {
                return;
            }

            PlayerSide side = LocalSide;

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

            if (isDraftChoice)
            {
                if (networkSync != null)
                {
                    networkSync.RequestDraftChoice(LocalSide, selectedCard.Card);
                }
                else
                {
                    gameManager.Phases.TryResolvePendingDraftChoice(LocalSide, selectedCard.Card);
                }

                Debug.Log($"[CardPoolChoicePanel] Draft choice requested for {selectedCard.Card.CardName} ({LocalSide}).");
                return;
            }

            bool resolved;

            if (networkSync != null)
            {
                int optionIndex = spawnedCards.IndexOf(selectedCard);
                networkSync.RequestResolveCardChoice(optionIndex);
                Debug.Log($"[CardPoolChoicePanel] Card choice requested for {selectedCard.Card.CardName} (index={optionIndex}).");
                return;
            }

            resolved = gameManager.Phases.TryResolvePendingCardChoice(selectedCard.Card);
            Debug.Log($"[CardPoolChoicePanel] HandleConfirm resolved={resolved} for {selectedCard.Card.CardName}, isDraftChoice={isDraftChoice}.");
        }
    }
}