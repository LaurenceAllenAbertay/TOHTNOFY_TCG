using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class DeckBuilderCardEntry : MonoBehaviour
    {
        [SerializeField] private HandCardView cardView;
        [SerializeField] private Button addButton;
        [SerializeField] private TextMeshProUGUI ownedCountText;

        public CardData Card { get; private set; }
        public event Action<DeckBuilderCardEntry> AddClicked;

        private void Awake()
        {
            if (cardView == null)
            {
                cardView = GetComponent<HandCardView>();
            }

            if (addButton != null)
            {
                addButton.onClick.AddListener(HandleAddClicked);
            }
        }

        public void Bind(CardData card)
        {
            Card = card;

            if (cardView != null)
            {
                cardView.Bind(card, PlayerSide.PlayerA, null, useLiveCost: false);
            }
        }

        public void SetOwnedCount(int count, int maxCopies)
        {
            if (ownedCountText != null)
            {
                ownedCountText.gameObject.SetActive(count > 0);
                ownedCountText.text = $"x{count}";
            }

            if (addButton != null)
            {
                addButton.interactable = count < maxCopies;
            }
        }

        private void HandleAddClicked()
        {
            AddClicked?.Invoke(this);
        }
    }
}