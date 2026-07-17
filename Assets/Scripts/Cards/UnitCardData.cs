using UnityEngine;

namespace DDD.TNFY.TCG.Cards
{
    [CreateAssetMenu(fileName = "NewUnitCard", menuName = "TNFY TCG/Cards/Unit Card")]
    public class UnitCardData : CardData
    {
        [SerializeField] private int attack;
        [SerializeField] private int health;
        [SerializeField] private Keyword keywords;

        public int Attack => attack;
        public int Health => health;
        public Keyword Keywords => keywords;

        public bool HasKeyword(Keyword keyword)
        {
            return (keywords & keyword) != 0;
        }
    }
}