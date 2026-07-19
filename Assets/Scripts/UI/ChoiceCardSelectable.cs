using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.UI
{
    [RequireComponent(typeof(HandCardView))]
    public class ChoiceCardSelectable : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image selectedOverlay;

        private HandCardView handCardView;

        public CardData Card => handCardView.Card;
        public bool IsSelected { get; private set; }

        public event Action<ChoiceCardSelectable> Clicked;

        private void Awake()
        {
            handCardView = GetComponent<HandCardView>();
            SetSelected(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (selectedOverlay != null)
            {
                selectedOverlay.enabled = selected;
            }
        }
    }
}