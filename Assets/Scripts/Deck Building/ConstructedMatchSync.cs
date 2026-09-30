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

            SavedDeck deck = mode == GameMode.Constructed ? DeckStorage.GetActiveDeck(database) : null;

            if (deck != null)
            {
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { DeckPropertyKey, JsonUtility.ToJson(deck) } });
            }
        }

        public static GameMode ReadSelectedMode(GameMode fallback)
        {
            return System.Enum.TryParse(PlayerPrefs.GetString(GameModePrefsKey, ""), out GameMode parsed) ? parsed : fallback;
        }

        public static bool TryReadDeck(Player photonPlayer, CardDatabase database, out List<CardData> resolvedCards, out LeaderData resolvedLeader)
        {
            resolvedCards = null;
            resolvedLeader = null;

            if (photonPlayer == null || !photonPlayer.CustomProperties.TryGetValue(DeckPropertyKey, out object raw) || !(raw is string json) || string.IsNullOrEmpty(json))
            {
                return false;
            }

            SavedDeck deck = JsonUtility.FromJson<SavedDeck>(json);

            if (deck == null)
            {
                return false;
            }

            resolvedCards = DeckStorage.ResolveCards(deck, database);

            if (database != null)
            {
                database.TryGetLeader(deck.leaderId, out resolvedLeader);
            }

            return true;
        }
    }
}