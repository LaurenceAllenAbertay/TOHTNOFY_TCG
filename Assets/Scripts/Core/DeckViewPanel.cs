using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.Core
{
    public class DeckViewPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private UI.HandCardView deckCardPrefab;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI cardCountText;

        private readonly List<UI.HandCardView> spawnedCards = new List<UI.HandCardView>();
        private readonly List<CardData> shownDeck = new List<CardData>();
        private bool isOpen;
        private NetworkedMatchSync networkSync;

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

        private void Awake()
        {
            if (gameManager != null)
            {
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            }

            if (openButton != null)
            {
                openButton.onClick.AddListener(Open);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        public void Open()
        {
            isOpen = true;

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            ForceRefresh();
        }

        public void Close()
        {
            isOpen = false;

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        public void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Update()
        {
            if (!isOpen || gameManager == null || gameManager.State == null)
            {
                return;
            }

            List<CardData> deck = gameManager.State.GetPlayer(LocalSide).Deck;

            if (DeckMatches(deck))
            {
                return;
            }

            Refresh(deck);
        }

        private void ForceRefresh()
        {
            if (gameManager == null || gameManager.State == null)
            {
                shownDeck.Clear();
                ClearSpawnedCards();
                UpdateCardCountText();
                return;
            }

            Refresh(gameManager.State.GetPlayer(LocalSide).Deck);
        }

        private bool DeckMatches(List<CardData> deck)
        {
            if (deck.Count != shownDeck.Count)
            {
                return false;
            }

            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i] != shownDeck[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void Refresh(List<CardData> deck)
        {
            ClearSpawnedCards();

            shownDeck.Clear();
            shownDeck.AddRange(deck);

            UpdateCardCountText();

            if (cardContainer == null || deckCardPrefab == null)
            {
                return;
            }

            PlayerSide side = LocalSide;

            foreach (CardData card in deck)
            {
                UI.HandCardView view = Instantiate(deckCardPrefab, cardContainer);
                view.Bind(card, side, gameManager.State, useLiveCost: false);
                spawnedCards.Add(view);
            }

            Debug.Log($"[DeckViewPanel] Refreshed with {deck.Count} card(s) remaining in {side}'s deck.");
        }

        private void UpdateCardCountText()
        {
            if (cardCountText != null)
            {
                cardCountText.text = $"{shownDeck.Count} card(s) remaining";
            }
        }

        private void ClearSpawnedCards()
        {
            foreach (UI.HandCardView existing in spawnedCards)
            {
                if (existing != null)
                {
                    Destroy(existing.gameObject);
                }
            }

            spawnedCards.Clear();
        }
    }
}