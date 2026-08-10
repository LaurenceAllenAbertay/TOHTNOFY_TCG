using System;
using System.Collections.Generic;

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
}