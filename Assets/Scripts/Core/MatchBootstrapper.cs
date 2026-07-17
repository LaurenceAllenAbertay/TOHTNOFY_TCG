using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.Core
{
    [RequireComponent(typeof(GameManager))]
    public class MatchBootstrapper : MonoBehaviour
    {
        [Header("Test Deck Setup")]
        [SerializeField] private List<CardData> testCardPool = new List<CardData>();
        [SerializeField] private int copiesOfEachCard = 3;

        private GameManager manager;

        private void Awake()
        {
            manager = GetComponent<GameManager>();
        }

        private void Start()
        {
            BuildTestDecks();
            manager.Phases.StartMatch();
        }

        private void BuildTestDecks()
        {
            BuildDeckFor(manager.State.PlayerA);
            BuildDeckFor(manager.State.PlayerB);
        }

        private void BuildDeckFor(Player player)
        {
            player.Deck.Clear();

            foreach (CardData card in testCardPool)
            {
                for (int i = 0; i < copiesOfEachCard; i++)
                {
                    player.Deck.Add(card);
                }
            }

            ListShuffler.Shuffle(player.Deck);
        }
    }
}