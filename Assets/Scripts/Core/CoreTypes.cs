using System;
using System.Collections.Generic;
using UnityEngine;

namespace DDD.TNFY.TCG.Core
{
    public enum PlayerSide
    {
        PlayerA,
        PlayerB
    }

    public enum TurnPhase
    {
        None,
        Draft,
        Mulligan,
        Draw,
        Action,
        TurnEnd
    }

    public enum GameMode
    {
        Draft,
        RandomDeck,
        Constructed,
        VsAI
    }

    public enum DraftStage
    {
        Common,
        Uncommon,
        Rare,
        EpicOrLegendary
    }

    public static class PlayerSideExtensions
    {
        public static PlayerSide Opposite(this PlayerSide side) => side == PlayerSide.PlayerA ? PlayerSide.PlayerB : PlayerSide.PlayerA;

        public static PlayerSide ToActualSide(this PlayerSide seat, PlayerSide localSide) => localSide == PlayerSide.PlayerA ? seat : seat.Opposite();
    }

    [Serializable]
    public class DraftSettings
    {
        [Header("Picks Per Stage")]
        public int commonPicks = 8;
        public int uncommonPicks = 6;
        public int rarePicks = 4;
        public int epicOrLegendaryPicks = 1;

        [Header("Copies Added Per Pick")]
        public int copiesPerCommonPick = 2;
        public int copiesPerUncommonPick = 2;
        public int copiesPerRarePick = 1;
        public int copiesPerEpicOrLegendaryPick = 1;

        [Header("Options Offered Per Choice")]
        public int optionsPerChoice = 3;

        [Header("Deck Building Rules")]
        public int maxCopiesPerCard = 2;
    }

    public static class ListShuffler
    {
        public static void Shuffle<T>(List<T> list)
        {
            System.Random rng = new System.Random();

            for (int n = list.Count - 1; n > 0; n--)
            {
                int k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}