using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Cards
{
    [CreateAssetMenu(fileName = "NewUnitCard", menuName = "TNFY TCG/Cards/Unit Card")]
    public class UnitCardData : CardData
    {
        [SerializeField] private int attack;
        [SerializeField] private int health;
        [SerializeField] private Keyword keywords;
        [SerializeField] private bool grantsEnemyUnitMove;

        [SerializeField]
        private List<LeaderAura> auras = new List<LeaderAura>();

        public int Attack => attack;
        public int Health => health;
        public Keyword Keywords => keywords;
        public bool GrantsEnemyUnitMove => grantsEnemyUnitMove;
        public IReadOnlyList<LeaderAura> Auras => auras;

        public bool HasKeyword(Keyword keyword)
        {
            return (keywords & keyword) != 0;
        }

        public UnitCardData CreateRandomizedClone(int minInclusive, int maxInclusive, System.Random rng)
        {
            UnitCardData clone = Instantiate(this);

            clone.SetManaCost(rng.Next(minInclusive, maxInclusive + 1));
            clone.attack = rng.Next(minInclusive, maxInclusive + 1);
            clone.health = rng.Next(minInclusive, maxInclusive + 1);

            return clone;
        }
    }
}