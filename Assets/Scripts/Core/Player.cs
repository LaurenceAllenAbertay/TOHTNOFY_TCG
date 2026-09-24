using System.Collections.Generic;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public class Player
    {
        public const int StartingLeaderHealth = 30;
        public const int MaxMana = 10;
        public const int AbsoluteMaxHandSize = 10;

        public PlayerSide Side { get; }
        public List<CardData> Deck { get; } = new List<CardData>();
        public List<CardData> Hand { get; } = new List<CardData>();
        public List<CardData> GameStartBonusCards { get; } = new List<CardData>();
        public LeaderData Leader { get; set; }

        public List<CardData> PendingDraftOptions { get; set; }
        public DraftStage? CurrentDraftStage { get; set; }
        public bool HasCompletedMulligan { get; set; }

        public int CurrentMana { get; set; }
        public int MaxManaThisGame { get; set; }
        public int MaxLeaderHealth { get; set; } = StartingLeaderHealth;
        public int LeaderHealth { get; set; } = StartingLeaderHealth;
        public int PendingManaReduction { get; set; }
        public bool HasReachedMaxMana { get; set; }
        public bool HasNextItemDoubled { get; set; }
        public int OwnTurnCount { get; set; }
        public int FatigueDamageTaken { get; set; }
        public int AlliedUnitsDied { get; set; }

        public List<ActiveStatusEffect> Statuses { get; } = new List<ActiveStatusEffect>();
        public HashSet<CardEffect> TriggeredOncePerTurnEffects { get; } = new HashSet<CardEffect>();

        public Player(PlayerSide side)
        {
            Side = side;
        }

        public bool CanAfford(CardData card)
        {
            return CurrentMana >= card.ManaCost;
        }

        public bool HasStatus(StatusEffectType type)
        {
            foreach (ActiveStatusEffect status in Statuses)
            {
                if (status.Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        public CardData DrawCard(out bool addedToHand)
        {
            addedToHand = false;

            if (Deck.Count == 0)
            {
                return null;
            }

            CardData drawn = Deck[0];
            Deck.RemoveAt(0);

            addedToHand = TryAddCardToHand(drawn);

            return drawn;
        }

        public CardData DrawRandomItemCard(out bool addedToHand)
        {
            addedToHand = false;

            List<CardData> itemCards = Deck.FindAll(card => card is ItemCardData);

            if (itemCards.Count == 0)
            {
                return null;
            }

            System.Random rng = new System.Random();
            CardData drawn = itemCards[rng.Next(itemCards.Count)];

            Deck.Remove(drawn);

            addedToHand = TryAddCardToHand(drawn);

            return drawn;
        }

        public bool TryAddCardToHand(CardData card)
        {
            if (Hand.Count >= AbsoluteMaxHandSize)
            {
                return false;
            }

            Hand.Add(card);
            return true;
        }

        public void CopyFrom(Player source)
        {
            Deck.Clear();
            Deck.AddRange(source.Deck);

            Hand.Clear();
            Hand.AddRange(source.Hand);

            GameStartBonusCards.Clear();
            GameStartBonusCards.AddRange(source.GameStartBonusCards);

            Leader = source.Leader;
            PendingDraftOptions = source.PendingDraftOptions != null ? new List<CardData>(source.PendingDraftOptions) : null;
            CurrentDraftStage = source.CurrentDraftStage;
            HasCompletedMulligan = source.HasCompletedMulligan;

            CurrentMana = source.CurrentMana;
            MaxManaThisGame = source.MaxManaThisGame;
            MaxLeaderHealth = source.MaxLeaderHealth;
            LeaderHealth = source.LeaderHealth;
            PendingManaReduction = source.PendingManaReduction;
            HasReachedMaxMana = source.HasReachedMaxMana;
            HasNextItemDoubled = source.HasNextItemDoubled;
            OwnTurnCount = source.OwnTurnCount;
            FatigueDamageTaken = source.FatigueDamageTaken;
            AlliedUnitsDied = source.AlliedUnitsDied;

            Statuses.Clear();
            foreach (ActiveStatusEffect status in source.Statuses)
            {
                Statuses.Add(status.Clone());
            }

            TriggeredOncePerTurnEffects.Clear();
            foreach (CardEffect effect in source.TriggeredOncePerTurnEffects)
            {
                TriggeredOncePerTurnEffects.Add(effect);
            }
        }
    }
}