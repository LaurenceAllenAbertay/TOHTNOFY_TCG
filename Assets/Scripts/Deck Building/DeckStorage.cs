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

            if (database.AllLeaders.Count > 0 && database.AllLeaders[0] != null)
            {
                deck.leaderId = database.AllLeaders[0].LeaderId;
                Debug.Log($"[DeckStorage] CreateDefaultDeck() assigned default leader '{database.AllLeaders[0].LeaderName}' (leaderId='{deck.leaderId}').");
            }
            else
            {
                Debug.LogWarning("[DeckStorage] CreateDefaultDeck() - CardDatabase has no leaders configured, starter deck will have no leaderId.");
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

            Debug.Log($"[DeckStorage] TryLoadActiveDeckLeader: activeIndex={activeIndex}, decks.Count={decks.Count}, database={(database != null ? database.name : "NULL")}.");

            if (activeIndex < 0 || activeIndex >= decks.Count || database == null)
            {
                Debug.LogWarning($"[DeckStorage] TryLoadActiveDeckLeader FAIL: activeIndex out of range or database is null.");
                return false;
            }

            string leaderId = decks[activeIndex].leaderId;

            Debug.Log($"[DeckStorage] TryLoadActiveDeckLeader: deck '{decks[activeIndex].deckName}' (slot {activeIndex}) has leaderId='{leaderId}'.");

            if (string.IsNullOrEmpty(leaderId))
            {
                Debug.LogWarning($"[DeckStorage] TryLoadActiveDeckLeader FAIL: deck '{decks[activeIndex].deckName}' has no leaderId saved.");
                return false;
            }

            bool found = database.TryGetLeader(leaderId, out leader);
            Debug.Log($"[DeckStorage] TryLoadActiveDeckLeader: database.TryGetLeader('{leaderId}') found={found}, leader={(leader != null ? leader.LeaderName : "NULL")} (database asset='{database.name}').");

            return found;
        }
    }
}