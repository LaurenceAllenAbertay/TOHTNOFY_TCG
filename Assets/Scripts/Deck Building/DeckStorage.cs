using System;
using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.DeckBuilding
{
    [Serializable]
    public class SavedDeckCardEntry
    {
        public string cardId;
        public int count;
    }

    [Serializable]
    public class SavedDeck
    {
        public string deckName = "New Deck";
        public string leaderId = "";
        public List<SavedDeckCardEntry> cards = new List<SavedDeckCardEntry>();
    }

    [Serializable]
    internal class SavedDeckListWrapper
    {
        public List<SavedDeck> decks = new List<SavedDeck>();
    }

    public static class DeckStorage
    {
        private const string SavedDecksPrefsKey = "SavedDecks";
        private const string ActiveDeckIndexPrefsKey = "ActiveDeckIndex";

        public const int MaxDeckSlots = 5;

        public static List<SavedDeck> LoadAll(CardDatabase database)
        {
            if (!PlayerPrefs.HasKey(SavedDecksPrefsKey))
            {
                return new List<SavedDeck> { CreateDefaultDeck(database) };
            }

            SavedDeckListWrapper wrapper = JsonUtility.FromJson<SavedDeckListWrapper>(PlayerPrefs.GetString(SavedDecksPrefsKey));
            return wrapper?.decks ?? new List<SavedDeck>();
        }

        public static void SaveAll(List<SavedDeck> decks)
        {
            PlayerPrefs.SetString(SavedDecksPrefsKey, JsonUtility.ToJson(new SavedDeckListWrapper { decks = decks }));
            PlayerPrefs.Save();
        }

        public static int GetActiveDeckIndex() => PlayerPrefs.GetInt(ActiveDeckIndexPrefsKey, 0);

        public static void SetActiveDeckIndex(int index)
        {
            PlayerPrefs.SetInt(ActiveDeckIndexPrefsKey, index);
            PlayerPrefs.Save();
        }

        public static SavedDeck GetActiveDeck(CardDatabase database)
        {
            List<SavedDeck> decks = LoadAll(database);
            int index = GetActiveDeckIndex();
            return index >= 0 && index < decks.Count ? decks[index] : null;
        }

        public static List<CardData> LoadActiveDeckCards(CardDatabase database) => ResolveCards(GetActiveDeck(database), database);

        public static bool TryLoadActiveDeckLeader(CardDatabase database, out LeaderData leader)
        {
            leader = null;
            SavedDeck deck = GetActiveDeck(database);
            return deck != null && database != null && database.TryGetLeader(deck.leaderId, out leader);
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
                    continue;
                }

                for (int i = 0; i < entry.count; i++)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        private static SavedDeck CreateDefaultDeck(CardDatabase database)
        {
            SavedDeck deck = new SavedDeck { deckName = "Starter Deck" };

            if (database == null)
            {
                return deck;
            }

            if (database.AllLeaders.Count > 0 && database.AllLeaders[0] != null)
            {
                deck.leaderId = database.AllLeaders[0].LeaderId;
            }

            foreach (CardDatabase.DefaultDeckCardEntry entry in database.DefaultDeck)
            {
                if (entry.card != null && entry.count > 0)
                {
                    deck.cards.Add(new SavedDeckCardEntry { cardId = entry.card.CardId, count = Mathf.Min(entry.count, database.MaxCopiesPerCard) });
                }
            }

            return deck;
        }
    }
}