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
        private TurnPhase? shownPhase;
        private PlayerSide? shownSide;

        private void Awake()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirm);
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

            Refresh(currentPhase, currentSide);

            shownPhase = currentPhase;
            shownSide = currentSide;
        }

        private void Refresh(TurnPhase phase, PlayerSide side)
        {
            bool isMulligan = phase == TurnPhase.Mulligan;

            if (panelRoot != null)
            {
                panelRoot.SetActive(isMulligan);
            }

            ClearSpawnedCards();

            if (!isMulligan || cardContainer == null || mulliganCardPrefab == null)
            {
                return;
            }

            List<CardData> hand = gameManager.State.GetPlayer(side).Hand;

            foreach (CardData card in hand)
            {
                UI.HandCardView view = Instantiate(mulliganCardPrefab, cardContainer);
                view.Bind(card);

                UI.MulliganCardSelectable selectable = view.GetComponent<UI.MulliganCardSelectable>();
                if (selectable != null)
                {
                    spawnedCards.Add(selectable);
                }
            }
        }

        private void ClearSpawnedCards()
        {
            foreach (UI.MulliganCardSelectable existing in spawnedCards)
            {
                if (existing != null)
                {
                    Destroy(existing.gameObject);
                }
            }

            spawnedCards.Clear();
        }

        private void HandleConfirm()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            PlayerSide side = gameManager.State.ActivePlayer;
            List<CardData> selectedCards = new List<CardData>();

            foreach (UI.MulliganCardSelectable card in spawnedCards)
            {
                if (card != null && card.IsSelected)
                {
                    selectedCards.Add(card.Card);
                }
            }

            gameManager.Phases.ResolveMulliganAndAdvance(side, selectedCards);
        }
    }
}