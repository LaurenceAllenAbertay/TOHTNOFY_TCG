using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.Core
{
    [RequireComponent(typeof(GameManager))]
    public class MatchBootstrapper : MonoBehaviour
    {
        [Header("Card Pool Setup")]
        [SerializeField] private List<CardData> cardPool = new List<CardData>();
        [SerializeField] private int copiesOfEachCard = 3;
        [SerializeField] private int deckSize = 33;

        [Header("Leader Setup")]
        [SerializeField] private List<LeaderData> leaderPool = new List<LeaderData>();

        private GameManager manager;

        private void Awake()
        {
            manager = GetComponent<GameManager>();
        }

        private void Start()
        {
            BuildDecks();
            AssignRandomLeaders();
            manager.Phases.StartMatch();
        }

        private void BuildDecks()
        {
            BuildDeckFor(manager.State.PlayerA);
            BuildDeckFor(manager.State.PlayerB);
        }

        private void BuildDeckFor(Player player)
        {
            player.Deck.Clear();

            List<CardData> availableCopies = new List<CardData>();

            foreach (CardData card in cardPool)
            {
                for (int i = 0; i < copiesOfEachCard; i++)
                {
                    availableCopies.Add(card);
                }
            }

            ListShuffler.Shuffle(availableCopies);

            int cardsToTake = Mathf.Min(deckSize, availableCopies.Count);

            for (int i = 0; i < cardsToTake; i++)
            {
                player.Deck.Add(availableCopies[i]);
            }

            Debug.Log($"[MatchBootstrapper] Built {player.Side}'s deck: {player.Deck.Count} cards drawn independently from a pool of {cardPool.Count} distinct cards.");
        }

        private void AssignRandomLeaders()
        {
            if (leaderPool.Count == 0)
            {
                return;
            }

            manager.State.PlayerA.Leader = leaderPool[Random.Range(0, leaderPool.Count)];
            manager.State.PlayerB.Leader = leaderPool[Random.Range(0, leaderPool.Count)];
        }
    }
}