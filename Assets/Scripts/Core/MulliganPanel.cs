using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.Core
{
    public class MulliganPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private UI.HandCardView mulliganCardPrefab;
        [SerializeField] private Button confirmButton;

        private readonly List<UI.MulliganCardSelectable> spawnedCards = new List<UI.MulliganCardSelectable>();
        private readonly List<int> spawnedCardHandIndices = new List<int>();
        private TurnPhase? shownPhase;
        private PlayerSide? shownSide;
        private NetworkedMatchSync networkSync;

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

        private void Awake()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirm);
            }

            if (gameManager != null)
            {
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            }
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            TurnPhase currentPhase = gameManager.State.CurrentPhase;
            PlayerSide currentSide = gameManager.State.ActivePlayer;

            bool stateChanged = shownPhase != currentPhase || shownSide != currentSide;

            if (!stateChanged)
            {
                return;
            }

            Debug.Log($"[MulliganPanel] Update detected state change: ({shownPhase}, {shownSide}) -> ({currentPhase}, {currentSide}). LocalSide={LocalSide}.");

            Refresh(currentPhase, currentSide);

            shownPhase = currentPhase;
            shownSide = currentSide;
        }

        private void Refresh(TurnPhase phase, PlayerSide side)
        {
            bool isMulligan = phase == TurnPhase.Mulligan && side == LocalSide;

            Debug.Log($"[MulliganPanel] Refresh: phase={phase}, side={side}, LocalSide={LocalSide} -> isMulligan={isMulligan}.");

            if (panelRoot != null)
            {
                panelRoot.SetActive(isMulligan);
            }

            ClearSpawnedCards();

            if (!isMulligan || cardContainer == null || mulliganCardPrefab == null)
            {
                return;
            }

            Player player = gameManager.State.GetPlayer(side);
            List<CardData> hand = player.Hand;
            List<CardData> remainingExemptCards = new List<CardData>(player.GameStartBonusCards);

            for (int handIndex = 0; handIndex < hand.Count; handIndex++)
            {
                CardData card = hand[handIndex];

                if (remainingExemptCards.Remove(card))
                {
                    Debug.Log($"[MulliganPanel] {card.CardName} is a GameStartBonusCard for {side} - excluding it from the mulligan.");
                    continue;
                }

                UI.HandCardView view = Instantiate(mulliganCardPrefab, cardContainer);
                view.Bind(card, side, gameManager.State, useLiveCost: false);

                UI.MulliganCardSelectable selectable = view.GetComponent<UI.MulliganCardSelectable>();
                if (selectable != null)
                {
                    selectable.Toggled += HandleCardToggled;
                    spawnedCards.Add(selectable);
                    spawnedCardHandIndices.Add(handIndex);
                }
            }
        }

        private void ClearSpawnedCards()
        {
            foreach (UI.MulliganCardSelectable existing in spawnedCards)
            {
                if (existing != null)
                {
                    existing.Toggled -= HandleCardToggled;
                    Destroy(existing.gameObject);
                }
            }

            spawnedCards.Clear();
            spawnedCardHandIndices.Clear();
        }

        private List<int> ComputeSelectedIndices()
        {
            List<int> selectedIndices = new List<int>();

            for (int i = 0; i < spawnedCards.Count; i++)
            {
                if (spawnedCards[i] != null && spawnedCards[i].IsSelected)
                {
                    selectedIndices.Add(spawnedCardHandIndices[i]);
                }
            }

            return selectedIndices;
        }

        private void HandleCardToggled()
        {
            if (gameManager == null || gameManager.State == null || networkSync == null)
            {
                return;
            }

            networkSync.RequestUpdateMulliganSelection(gameManager.State.ActivePlayer, ComputeSelectedIndices());
        }

        private void HandleConfirm()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            PlayerSide side = gameManager.State.ActivePlayer;
            List<int> selectedIndices = ComputeSelectedIndices();

            if (networkSync != null)
            {
                networkSync.RequestMulliganChoice(side, selectedIndices);
            }
            else
            {
                List<CardData> selectedCards = new List<CardData>();
                foreach (int index in selectedIndices)
                {
                    selectedCards.Add(gameManager.State.GetPlayer(side).Hand[index]);
                }

                gameManager.Phases.ResolveMulliganAndAdvance(side, selectedCards);
            }

            Debug.Log($"[MulliganPanel] Mulligan choice requested for {side}: {selectedIndices.Count} card(s) to swap. LocalSide={LocalSide}, IsMasterClient={Photon.Pun.PhotonNetwork.IsMasterClient}.");
        }
    }
}