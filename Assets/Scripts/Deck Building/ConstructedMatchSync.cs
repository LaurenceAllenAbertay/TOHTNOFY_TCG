using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using DDD.TNFY.TCG.Cards;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using GameMode = DDD.TNFY.TCG.Core.GameMode;

namespace DDD.TNFY.TCG.DeckBuilding
{
    public static class ConstructedMatchSync
    {
        private const string GameModePrefsKey = "SelectedGameMode";
        private const string DeckPropertyKey = "ConstructedDeck";

        public static void PublishSelection(GameMode mode, CardDatabase database)
        {
            PlayerPrefs.SetString(GameModePrefsKey, mode.ToString());
            PlayerPrefs.Save();

            if (mode != GameMode.Constructed)
            {
                return;
            }

            List<SavedDeck> decks = DeckStorage.LoadAll(database);
            int activeIndex = DeckStorage.GetActiveDeckIndex();

            if (activeIndex < 0 || activeIndex >= decks.Count)
            {
                Debug.LogWarning($"[ConstructedMatchSync] No valid active deck (index={activeIndex}, {decks.Count} saved) - not publishing a Constructed deck.");
                return;
            }

            string json = JsonUtility.ToJson(decks[activeIndex]);
            Hashtable props = new Hashtable { { DeckPropertyKey, json } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            Debug.Log($"[ConstructedMatchSync] Published active deck '{decks[activeIndex].deckName}' ({decks[activeIndex].cards.Count} unique card(s)) to local player's custom properties.");
        }

        public static GameMode ReadSelectedMode(GameMode fallback)
        {
            if (!PlayerPrefs.HasKey(GameModePrefsKey))
            {
                return fallback;
            }

            string raw = PlayerPrefs.GetString(GameModePrefsKey);

            if (System.Enum.TryParse(raw, out GameMode parsed))
            {
                return parsed;
            }

            Debug.LogWarning($"[ConstructedMatchSync] Could not parse saved GameMode '{raw}' - falling back to {fallback}.");
            return fallback;
        }

        public static bool TryReadDeck(Player photonPlayer, CardDatabase database, out List<CardData> resolvedCards, out LeaderData resolvedLeader)
        {
            resolvedCards = null;
            resolvedLeader = null;

            if (photonPlayer == null || !photonPlayer.CustomProperties.ContainsKey(DeckPropertyKey))
            {
                return false;
            }

            string json = photonPlayer.CustomProperties[DeckPropertyKey] as string;

            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            SavedDeck deck = JsonUtility.FromJson<SavedDeck>(json);

            if (deck == null)
            {
                return false;
            }

            resolvedCards = DeckStorage.ResolveCards(deck, database);

            if (!string.IsNullOrEmpty(deck.leaderId) && database != null)
            {
                database.TryGetLeader(deck.leaderId, out resolvedLeader);
            }

            return true;
        }
    }
}