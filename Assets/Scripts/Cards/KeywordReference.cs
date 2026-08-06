using System.Collections.Generic;

namespace DDD.TNFY.TCG.Cards
{
    public static class KeywordReference
    {
        private static readonly Dictionary<Keyword, string> Descriptions = new Dictionary<Keyword, string>
        {
            { Keyword.Taunt, "A unit must be placed opposing this one before placing elsewhere. Also blocks Piercing attacks." },
            { Keyword.Rush, "This unit can attack the turn it's played." },
            { Keyword.BifurcatedAttack, "This unit attacks diagonally left and right." },
            { Keyword.Agile, "This unit can move 2 slots instead of 1." },
            { Keyword.Nimble, "This unit can move for free." },
            { Keyword.Piercing, "This unit attacks the leader directly, unless blocked by a Taunt unit opposing it." },
            { Keyword.Unmoving, "This unit can never move." },
            { Keyword.Unstable, "When this unit dies, deal damage equal to its Attack to a random enemy unit." },
            { Keyword.Retaliate, "If this unit survives a hit, it attacks the attacking unit." },
            { Keyword.Teleport, "This unit can move to any open slot." },
            { Keyword.Absorb, "Can be placed on an allied unit, replacing it and gaining its stats." },
            { Keyword.Slippy, "When attacked, this unit tries to move left, then right, before taking the hit." },
            { Keyword.Relentless, "When the opposing unit moves, this unit moves to stay opposite them, if able." },
        };

        public static bool TryGetDescription(Keyword keyword, out string description)
        {
            return Descriptions.TryGetValue(keyword, out description);
        }

        public static IEnumerable<Keyword> GetAllValues()
        {
            return Descriptions.Keys;
        }
    }
}