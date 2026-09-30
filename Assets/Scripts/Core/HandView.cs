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
        [SerializeField] private RectTransform drawOrigin;
        [SerializeField] private float cardSlotWidth = 160f;
        [SerializeField] private float cardSpacing = 0f;
        [SerializeField] private float cardSlotY = 0f;
        [SerializeField] private int compactHandCardThreshold = 6;
        [SerializeField] private float minCardSlotWidth = 60f;

        private readonly List<UI.HandCardView> spawnedViews = new List<UI.HandCardView>();
        private readonly List<CardData> previousHand = new List<CardData>();
        private readonly List<CardData> previousDeck = new List<CardData>();
        private bool? shownFaceUp;
        private int shownHandCount = -1;
        private CardData shownLastCard;
        private NetworkedMatchSync networkSync;

        public IReadOnlyList<UI.HandCardView> SpawnedViews => spawnedViews;

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;
        private PlayerSide ActualSide => side.ToActualSide(LocalSide);

        private void Awake()
        {
            if (gameManager != null)
            {
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            }
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State == null || handContainer == null)
            {
                return;
            }

            Player player = gameManager.State.GetPlayer(ActualSide);
            List<CardData> hand = player.Hand;
            List<CardData> deck = player.Deck;
            bool isFaceUp = side == PlayerSide.PlayerA;
            CardData lastCard = hand.Count > 0 ? hand[hand.Count - 1] : null;

            bool handChanged = shownFaceUp != isFaceUp
                || shownHandCount != hand.Count
                || shownLastCard != lastCard;

            if (!handChanged)
            {
                if (deck.Count != previousDeck.Count)
                {
                    SnapshotCards(previousDeck, deck);
                }

                return;
            }

            HashSet<int> drawnHandIndices = FindDrawnHandIndices(hand, deck);

            Rebuild(isFaceUp, hand, drawnHandIndices);

            shownFaceUp = isFaceUp;
            shownHandCount = hand.Count;
            shownLastCard = lastCard;

            SnapshotCards(previousHand, hand);
            SnapshotCards(previousDeck, deck);
        }

        public void CommitReorder()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            List<CardData> hand = gameManager.State.GetPlayer(ActualSide).Hand;

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

        private HashSet<int> FindDrawnHandIndices(List<CardData> hand, List<CardData> deck)
        {
            HashSet<int> drawnIndices = new HashSet<int>();

            Dictionary<CardData, int> previousHandCounts = CountCards(previousHand);
            Dictionary<CardData, int> currentHandCounts = CountCards(hand);
            Dictionary<CardData, int> previousDeckCounts = CountCards(previousDeck);
            Dictionary<CardData, int> currentDeckCounts = CountCards(deck);

            Dictionary<CardData, int> remainingDrawnCopies = new Dictionary<CardData, int>();

            foreach (KeyValuePair<CardData, int> entry in currentHandCounts)
            {
                int addedToHand = entry.Value - CountOf(previousHandCounts, entry.Key);
                int leftDeck = CountOf(previousDeckCounts, entry.Key) - CountOf(currentDeckCounts, entry.Key);
                int drawnCopies = Mathf.Min(addedToHand, leftDeck);

                if (drawnCopies > 0)
                {
                    remainingDrawnCopies[entry.Key] = drawnCopies;
                }
            }

            for (int i = hand.Count - 1; i >= 0; i--)
            {
                CardData card = hand[i];

                if (card != null && remainingDrawnCopies.TryGetValue(card, out int remaining) && remaining > 0)
                {
                    drawnIndices.Add(i);
                    remainingDrawnCopies[card] = remaining - 1;
                }
            }

            if (drawnIndices.Count > 0)
            {
                Debug.Log($"[HandView] {ActualSide} drew {drawnIndices.Count} card(s) at hand index(es) [{string.Join(", ", drawnIndices)}]. Hand {previousHand.Count} -> {hand.Count}, deck {previousDeck.Count} -> {deck.Count}. drawOrigin={(drawOrigin != null ? drawOrigin.name : "NULL (cards will snap into place)")}.");
            }

            return drawnIndices;
        }

        private static Dictionary<CardData, int> CountCards(List<CardData> cards)
        {
            Dictionary<CardData, int> counts = new Dictionary<CardData, int>();

            foreach (CardData card in cards)
            {
                if (card == null)
                {
                    continue;
                }

                counts[card] = CountOf(counts, card) + 1;
            }

            return counts;
        }

        private static int CountOf(Dictionary<CardData, int> counts, CardData card)
        {
            return counts.TryGetValue(card, out int count) ? count : 0;
        }

        private static void SnapshotCards(List<CardData> snapshot, List<CardData> source)
        {
            snapshot.Clear();
            snapshot.AddRange(source);
        }

        private void Rebuild(bool isFaceUp, List<CardData> hand, HashSet<int> drawnHandIndices)
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
                    view.Bind(card, ActualSide, gameManager.State);
                }

                spawnedViews.Add(view);
            }

            RefreshSlotPositions(snapImmediately: true);

            if (drawOrigin == null)
            {
                return;
            }

            foreach (int handIndex in drawnHandIndices)
            {
                if (handIndex >= 0 && handIndex < spawnedViews.Count && spawnedViews[handIndex] != null)
                {
                    spawnedViews[handIndex].BeginDrawFrom(drawOrigin);
                }
            }
        }
    }
}