using System;
using UnityEngine;

namespace DDD.TNFY.TCG.Core
{
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
    }
}