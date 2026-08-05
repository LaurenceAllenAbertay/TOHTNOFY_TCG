using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.UI
{
    public class DeckListEntryView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private Button removeButton;

        public CardData Card { get; private set; }
        public event Action<DeckListEntryView> RemoveClicked;

        private void Awake()
        {
            if (removeButton != null)
            {
                removeButton.onClick.AddListener(HandleRemoveClicked);
            }
        }

        public void Bind(CardData card, int count)
        {
            Card = card;

            if (costText != null)
            {
                costText.text = CardDisplayFormatter.GetCostText(card);
            }

            if (nameText != null)
            {
                nameText.text = CardDisplayFormatter.GetNameText(card);
            }

            if (countText != null)
            {
                countText.text = $"x{count}";
            }
        }

        private void HandleRemoveClicked()
        {
            RemoveClicked?.Invoke(this);
        }
    }
}