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
        [SerializeField] private float cardSlotWidth = 160f;
        [SerializeField] private float cardSpacing = 0f;
        [SerializeField] private float cardSlotY = 0f;
        [SerializeField] private int compactHandCardThreshold = 6;
        [SerializeField] private float minCardSlotWidth = 60f;

        private readonly List<UI.HandCardView> spawnedViews = new List<UI.HandCardView>();
        private bool? shownFaceUp;
        private int shownHandCount = -1;
        private CardData shownLastCard;

        public IReadOnlyList<UI.HandCardView> SpawnedViews => spawnedViews;

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

        public void CommitReorder()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            List<CardData> hand = gameManager.State.GetPlayer(side).Hand;

            spawnedViews.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            hand.Clear();
            foreach (UI.HandCardView view in spawnedViews)
            {
                if (view.Card != null)
                {
                    hand.Add(view.Card);
                }
            }

            shownHandCount = hand.Count;
            shownLastCard = hand.Count > 0 ? hand[hand.Count - 1] : null;

            RefreshSlotPositions(snapImmediately: false);
        }

        public void RefreshSlotPositions(bool snapImmediately)
        {
            int count = spawnedViews.Count;

            List<UI.HandCardView> orderedViews = new List<UI.HandCardView>(spawnedViews);
            orderedViews.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            for (int rank = 0; rank < orderedViews.Count; rank++)
            {
                UI.HandCardView view = orderedViews[rank];

                if (view == null)
                {
                    continue;
                }

                Vector2 position = GetSlotAnchoredPosition(rank, count);

                view.SetSlotTarget(position, snapImmediately);
            }
        }

        public Vector2 GetSlotAnchoredPosition(int index, int totalCount)
        {
            float slotWidth = GetEffectiveSlotWidth(totalCount);
            float spacing = GetEffectiveSpacing(slotWidth);
            float slotStride = slotWidth + spacing;
            float totalWidth = totalCount > 0 ? (totalCount * slotStride) - spacing : 0f;
            float firstSlotX = -totalWidth / 2f + slotWidth / 2f;

            return new Vector2(firstSlotX + (index * slotStride), cardSlotY);
        }

        private float GetEffectiveSlotWidth(int totalCount)
        {
            if (totalCount < compactHandCardThreshold)
            {
                return cardSlotWidth;
            }

            float rowWidthBudget = compactHandCardThreshold * cardSlotWidth;
            float compactedWidth = rowWidthBudget / totalCount;

            return Mathf.Max(minCardSlotWidth, compactedWidth);
        }

        private float GetEffectiveSpacing(float effectiveSlotWidth)
        {
            if (cardSlotWidth <= 0f)
            {
                return cardSpacing;
            }

            float scale = effectiveSlotWidth / cardSlotWidth;
            return cardSpacing * scale;
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
                    view.Bind(card, side, gameManager.State);
                }

                spawnedViews.Add(view);
            }

            RefreshSlotPositions(snapImmediately: true);
        }
    }
}