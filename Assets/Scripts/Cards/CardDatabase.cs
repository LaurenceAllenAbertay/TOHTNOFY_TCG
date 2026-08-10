using System.Collections.Generic;
using UnityEngine;

namespace DDD.TNFY.TCG.Cards
{
    [CreateAssetMenu(fileName = "NewCardDatabase", menuName = "TNFY TCG/Card Database")]
    public class CardDatabase : ScriptableObject
    {
        [System.Serializable]
        public struct DefaultDeckCardEntry
        {
            public CardData card;
            public int count;
        }

        [SerializeField] private List<CardData> allCards = new List<CardData>();
        [SerializeField] private int maxCopiesPerCard = 2;
        [SerializeField] private int targetDeckSize = 33;
        [SerializeField] private List<DefaultDeckCardEntry> defaultDeck = new List<DefaultDeckCardEntry>();
        [SerializeField] private List<LeaderData> allLeaders = new List<LeaderData>();

        private Dictionary<string, CardData> lookup;
        private Dictionary<string, LeaderData> leaderLookup;

        public IReadOnlyList<CardData> AllCards => allCards;
        public int MaxCopiesPerCard => maxCopiesPerCard;
        public int TargetDeckSize => targetDeckSize;
        public IReadOnlyList<DefaultDeckCardEntry> DefaultDeck => defaultDeck;
        public IReadOnlyList<LeaderData> AllLeaders => allLeaders;

        public bool TryGetCard(string cardId, out CardData card)
        {
            if (lookup == null)
            {
                BuildLookup();
            }

            return lookup.TryGetValue(cardId, out card);
        }

        public bool TryGetLeader(string leaderId, out LeaderData leader)
        {
            if (leaderLookup == null)
            {
                BuildLeaderLookup();
            }

            if (string.IsNullOrEmpty(leaderId))
            {
                leader = null;
                return false;
            }

            return leaderLookup.TryGetValue(leaderId, out leader);
        }

        private void BuildLookup()
        {
            lookup = new Dictionary<string, CardData>();

            foreach (CardData card in allCards)
            {
                if (card == null)
                {
                    continue;
                }

                lookup[card.CardId] = card;
            }
        }

        private void BuildLeaderLookup()
        {
            leaderLookup = new Dictionary<string, LeaderData>();

            foreach (LeaderData leader in allLeaders)
            {
                if (leader == null)
                {
                    continue;
                }

                leaderLookup[leader.LeaderId] = leader;
            }
        }

        private void OnValidate()
        {
            lookup = null;
            leaderLookup = null;
        }
    }
}