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
        public bool IsLocked { get; private set; }

        public event Action Toggled;

        private void Awake()
        {
            handCardView = GetComponent<HandCardView>();

            if (selectedOverlay != null)
            {
                selectedOverlay.enabled = false;
            }
        }

        public void SetLocked(bool locked)
        {
            IsLocked = locked;

            if (locked && IsSelected)
            {
                IsSelected = false;

                if (selectedOverlay != null)
                {
                    selectedOverlay.enabled = false;
                }
            }

            handCardView.SetDimmed(locked);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsLocked)
            {
                return;
            }

            IsSelected = !IsSelected;

            if (selectedOverlay != null)
            {
                selectedOverlay.enabled = IsSelected;
            }

            Toggled?.Invoke();
        }
    }
}