using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.DeckBuilding
{
    public static class DeckStorage
    {
        private const string SavedDecksPrefsKey = "SavedDecks";
        private const string ActiveDeckIndexPrefsKey = "ActiveDeckIndex";

        public const int MaxDeckSlots = 5;

        public static List<SavedDeck> LoadAll(CardDatabase database)
        {
            if (!PlayerPrefs.HasKey(SavedDecksPrefsKey))
            {
                Debug.Log("[DeckStorage] No saved decks found for this player - seeding a default starter deck.");
                return new List<SavedDeck> { CreateDefaultDeck(database) };
            }

            string json = PlayerPrefs.GetString(SavedDecksPrefsKey);
            SavedDeckListWrapper wrapper = JsonUtility.FromJson<SavedDeckListWrapper>(json);

            return wrapper != null && wrapper.decks != null ? wrapper.decks : new List<SavedDeck>();
        }

        private static SavedDeck CreateDefaultDeck(CardDatabase database)
        {
            SavedDeck deck = new SavedDeck { deckName = "Starter Deck" };

            if (database == null)
            {
                Debug.LogWarning("[DeckStorage] Cannot build a default deck without a CardDatabase reference - returning an empty deck.");
                return deck;
            }

            Debug.Log($"[DeckStorage] CreateDefaultDeck() start. database.DefaultDeck.Count={database.DefaultDeck.Count}.");

            int index = 0;
            foreach (CardDatabase.DefaultDeckCardEntry entry in database.DefaultDeck)
            {
                Debug.Log($"[DeckStorage] CreateDefaultDeck() entry {index}: card={(entry.card != null ? entry.card.CardName : "NULL")}, count={entry.count}.");
                index++;

                if (entry.card == null || entry.count <= 0)
                {
                    continue;
                }

                int clampedCount = Mathf.Min(entry.count, database.MaxCopiesPerCard);
                deck.cards.Add(new SavedDeckCardEntry { cardId = entry.card.CardId, count = clampedCount });
            }

            Debug.Log($"[DeckStorage] CreateDefaultDeck() complete. deck.cards.Count={deck.cards.Count}.");

            return deck;
        }

        public static void SaveAll(List<SavedDeck> decks)
        {
            SavedDeckListWrapper wrapper = new SavedDeckListWrapper { decks = decks };
            string json = JsonUtility.ToJson(wrapper);

            PlayerPrefs.SetString(SavedDecksPrefsKey, json);
            PlayerPrefs.Save();

            Debug.Log($"[DeckStorage] Saved {decks.Count} deck(s) to PlayerPrefs.");
        }

        public static int GetActiveDeckIndex()
        {
            return PlayerPrefs.GetInt(ActiveDeckIndexPrefsKey, 0);
        }

        public static void SetActiveDeckIndex(int index)
        {
            PlayerPrefs.SetInt(ActiveDeckIndexPrefsKey, index);
            PlayerPrefs.Save();
        }

        public static List<CardData> ResolveCards(SavedDeck deck, CardDatabase database)
        {
            List<CardData> result = new List<CardData>();

            if (deck == null || database == null)
            {
                return result;
            }

            foreach (SavedDeckCardEntry entry in deck.cards)
            {
                if (!database.TryGetCard(entry.cardId, out CardData card))
                {
                    Debug.LogWarning($"[DeckStorage] Saved deck '{deck.deckName}' references unknown cardId '{entry.cardId}' - skipping. It may have been removed from the Card Database.");
                    continue;
                }

                for (int i = 0; i < entry.count; i++)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        public static List<CardData> LoadActiveDeckCards(CardDatabase database)
        {
            List<SavedDeck> decks = LoadAll(database);
            int activeIndex = GetActiveDeckIndex();

            if (activeIndex < 0 || activeIndex >= decks.Count)
            {
                Debug.LogWarning($"[DeckStorage] Active deck index {activeIndex} is out of range ({decks.Count} saved deck(s)) - returning an empty deck.");
                return new List<CardData>();
            }

            return ResolveCards(decks[activeIndex], database);
        }

        public static bool TryLoadActiveDeckLeader(CardDatabase database, out LeaderData leader)
        {
            leader = null;

            List<SavedDeck> decks = LoadAll(database);
            int activeIndex = GetActiveDeckIndex();

            if (activeIndex < 0 || activeIndex >= decks.Count || database == null)
            {
                return false;
            }

            string leaderId = decks[activeIndex].leaderId;

            if (string.IsNullOrEmpty(leaderId))
            {
                return false;
            }

            return database.TryGetLeader(leaderId, out leader);
        }
    }
}