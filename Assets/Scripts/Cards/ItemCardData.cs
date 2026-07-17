using UnityEngine;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Cards
{
    [CreateAssetMenu(fileName = "NewItemCard", menuName = "TNFY TCG/Cards/Item Card")]
    public class ItemCardData : CardData
    {
        public CardEffect PrimaryEffect => Effects.Count > 0 ? Effects[0] : null;
    }
}