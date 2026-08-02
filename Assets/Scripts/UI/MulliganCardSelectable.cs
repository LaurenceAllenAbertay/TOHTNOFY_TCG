using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.UI
{
    [RequireComponent(typeof(HandCardView))]
    public class MulliganCardSelectable : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image selectedOverlay;

        private HandCardView handCardView;

        public CardData Card => handCardView.Card;
        public bool IsSelected { get; private set; }

        public event Action Toggled;

        private void Awake()
        {
            handCardView = GetComponent<HandCardView>();

            if (selectedOverlay != null)
            {
                selectedOverlay.enabled = false;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            IsSelected = !IsSelected;

            if (selectedOverlay != null)
            {
                selectedOverlay.enabled = IsSelected;
            }

            Toggled?.Invoke();
        }
    }
}