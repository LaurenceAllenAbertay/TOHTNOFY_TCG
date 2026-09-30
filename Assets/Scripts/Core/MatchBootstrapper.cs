using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.DeckBuilding;

namespace DDD.TNFY.TCG.Core
{
    [RequireComponent(typeof(GameManager))]
    public class MatchBootstrapper : MonoBehaviour
    {
        [Header("Card Pool Setup")]
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private DraftSettings draftSettings = new DraftSettings();

        [Header("Game Mode")]
        [SerializeField] private GameMode fallbackGameMode = GameMode.Draft;

        public IReadOnlyList<CardData> CardPool => cardDatabase.AllCards;
        public IReadOnlyList<LeaderData> LeaderPool => cardDatabase.AllLeaders;

        private GameManager manager;
        private GameState State => manager.State;

        private void Awake()
        {
            manager = GetComponent<GameManager>();
        }

        private void Start()
        {
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            {
                return;
            }

            State.FirstPlayer = Random.Range(0, 2) == 0 ? PlayerSide.PlayerA : PlayerSide.PlayerB;

            switch (ConstructedMatchSync.ReadSelectedMode(fallbackGameMode))
            {
                case GameMode.Constructed:
                    StartConstructedMatch();
                    break;

                case GameMode.RandomDeck:
                    AssignRandomLeaders();
                    State.PlayerA.Deck.AddRange(BuildRandomDeck());
                    State.PlayerB.Deck.AddRange(BuildRandomDeck());
                    ShuffleDecksAndSetFirstPlayer();
                    manager.Phases.StartMatch();
                    break;

                case GameMode.VsAI:
                    StartVsAIMatch();
                    break;

                default:
                    AssignRandomLeaders();
                    manager.Phases.StartDraft(new List<CardData>(CardPool), draftSettings, cardDatabase.MaxCopiesPerCard);
                    break;
            }
        }

        private void StartConstructedMatch()
        {
            bool appliedAnyRemoteDeck = false;

            if (PhotonNetwork.InRoom)
            {
                foreach (Photon.Realtime.Player photonPlayer in PhotonNetwork.PlayerList)
                {
                    Player player = State.GetPlayer(NetworkedMatchSync.SideForActorNumber(photonPlayer.ActorNumber));

                    if (ConstructedMatchSync.TryReadDeck(photonPlayer, cardDatabase, out List<CardData> syncedDeck, out LeaderData syncedLeader))
                    {
                        player.Deck.AddRange(syncedDeck);
                        appliedAnyRemoteDeck = true;
                    }

                    player.Leader = syncedLeader != null ? syncedLeader : PickRandomLeader();
                }
            }

            if (!appliedAnyRemoteDeck)
            {
                List<CardData> localDeck = DeckStorage.LoadActiveDeckCards(cardDatabase);
                State.PlayerA.Deck.AddRange(localDeck);
                State.PlayerB.Deck.AddRange(localDeck);
                State.PlayerA.Leader = LoadActiveLeaderOrRandom();
                State.PlayerB.Leader = PickRandomLeader();
            }

            ShuffleDecksAndSetFirstPlayer();
            manager.Phases.StartMatch();
        }

        private void StartVsAIMatch()
        {
            State.PlayerA.Deck.AddRange(DeckStorage.LoadActiveDeckCards(cardDatabase));
            State.PlayerA.Leader = LoadActiveLeaderOrRandom();
            State.PlayerB.Deck.AddRange(BuildDraftLegalRandomDeck());
            State.PlayerB.Leader = PickRandomLeader();

            ShuffleDecksAndSetFirstPlayer();

            if (TryGetComponent(out AIController aiController))
            {
                aiController.EnableAIControl(PlayerSide.PlayerB);
            }

            if (TryGetComponent(out TurnTimerController turnTimer))
            {
                turnTimer.enabled = false;
            }

            manager.Phases.StartMatch();
        }

        private void ShuffleDecksAndSetFirstPlayer()
        {
            ListShuffler.Shuffle(State.PlayerA.Deck);
            ListShuffler.Shuffle(State.PlayerB.Deck);
            State.ActivePlayer = State.FirstPlayer;
        }

        private List<CardData> BuildRandomDeck()
        {
            List<CardData> pool = new List<CardData>();

            foreach (CardData card in CardPool)
            {
                for (int i = 0; i < cardDatabase.MaxCopiesPerCard; i++)
                {
                    pool.Add(card);
                }
            }

            ListShuffler.Shuffle(pool);
            return pool.GetRange(0, Mathf.Min(cardDatabase.TargetDeckSize, pool.Count));
        }

        private List<CardData> BuildDraftLegalRandomDeck()
        {
            List<CardData> deck = new List<CardData>();

            AddRandomPicks(deck, DraftStage.Common, draftSettings.commonPicks, draftSettings.copiesPerCommonPick);
            AddRandomPicks(deck, DraftStage.Uncommon, draftSettings.uncommonPicks, draftSettings.copiesPerUncommonPick);
            AddRandomPicks(deck, DraftStage.Rare, draftSettings.rarePicks, draftSettings.copiesPerRarePick);
            AddRandomPicks(deck, DraftStage.EpicOrLegendary, draftSettings.epicOrLegendaryPicks, draftSettings.copiesPerEpicOrLegendaryPick);

            return deck;
        }

        private void AddRandomPicks(List<CardData> deck, DraftStage stage, int pickCount, int copiesPerPick)
        {
            for (int pick = 0; pick < pickCount; pick++)
            {
                List<CardData> eligible = new List<CardData>();

                foreach (CardData card in CardPool)
                {
                    if (PhaseManager.MatchesDraftStage(card.Rarity, stage) && deck.FindAll(c => c == card).Count < cardDatabase.MaxCopiesPerCard)
                    {
                        eligible.Add(card);
                    }
                }

                if (eligible.Count == 0)
                {
                    continue;
                }

                CardData chosen = eligible[Random.Range(0, eligible.Count)];

                for (int copy = 0; copy < copiesPerPick; copy++)
                {
                    deck.Add(chosen);
                }
            }
        }

        private void AssignRandomLeaders()
        {
            State.PlayerA.Leader = PickRandomLeader();
            State.PlayerB.Leader = PickRandomLeader();
        }

        private LeaderData LoadActiveLeaderOrRandom()
        {
            return DeckStorage.TryLoadActiveDeckLeader(cardDatabase, out LeaderData leader) ? leader : PickRandomLeader();
        }

        private LeaderData PickRandomLeader()
        {
            return LeaderPool.Count > 0 ? LeaderPool[Random.Range(0, LeaderPool.Count)] : null;
        }
    }
}