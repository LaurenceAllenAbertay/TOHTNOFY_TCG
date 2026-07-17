using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.Core
{
    public class HandView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerSide side;
        [SerializeField] private Transform handContainer;
        [SerializeField] private UI.HandCardView faceUpCardPrefab;
        [SerializeField] private UI.HandCardView faceDownCardPrefab;

        private readonly List<UI.HandCardView> spawnedViews = new List<UI.HandCardView>();
        private bool? shownFaceUp;
        private int shownHandCount = -1;
        private CardData shownLastCard;

        private void Update()
        {
            if (gameManager == null || gameManager.State == null || handContainer == null)
            {
                return;
            }

            List<CardData> hand = gameManager.State.GetPlayer(side).Hand;
            bool isFaceUp = gameManager.State.ActivePlayer == side;
            CardData lastCard = hand.Count > 0 ? hand[hand.Count - 1] : null;

            bool handChanged = shownFaceUp != isFaceUp
                || shownHandCount != hand.Count
                || shownLastCard != lastCard;

            if (!handChanged)
            {
                return;
            }

            Rebuild(isFaceUp, hand);

            shownFaceUp = isFaceUp;
            shownHandCount = hand.Count;
            shownLastCard = lastCard;
        }

        private void Rebuild(bool isFaceUp, List<CardData> hand)
        {
            foreach (UI.HandCardView existingView in spawnedViews)
            {
                if (existingView != null)
                {
                    Destroy(existingView.gameObject);
                }
            }

            spawnedViews.Clear();

            UI.HandCardView prefab = isFaceUp ? faceUpCardPrefab : faceDownCardPrefab;

            if (prefab == null)
            {
                return;
            }

            foreach (CardData card in hand)
            {
                UI.HandCardView view = Instantiate(prefab, handContainer);

                if (isFaceUp)
                {
                    view.Bind(card);
                }

                spawnedViews.Add(view);
            }
        }
    }
}