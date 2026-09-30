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

        public IReadOnlyList<CardData> AllCards => allCards;
        public int MaxCopiesPerCard => maxCopiesPerCard;
        public int TargetDeckSize => targetDeckSize;
        public IReadOnlyList<DefaultDeckCardEntry> DefaultDeck => defaultDeck;
        public IReadOnlyList<LeaderData> AllLeaders => allLeaders;

        public bool TryGetCard(string cardId, out CardData card)
        {
            card = allCards.Find(c => c != null && c.CardId == cardId);
            return card != null;
        }

        public bool TryGetLeader(string leaderId, out LeaderData leader)
        {
            leader = string.IsNullOrEmpty(leaderId) ? null : allLeaders.Find(l => l != null && l.LeaderId == leaderId);
            return leader != null;
        }
    }
}