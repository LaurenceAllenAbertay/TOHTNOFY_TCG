using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Core;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.UI
{
    public class CardHoverPreview : MonoBehaviour
    {
        private static CardHoverPreview instance;

        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private Image artImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI abilityText;
        [SerializeField] private RectTransform extraInfoContainer;
        [SerializeField] private TermInfoPanel infoPanelPrefab;
        [SerializeField] private float extraInfoGapX = 20f;
        [SerializeField] private float leftSideX = 300f;
        [SerializeField] private float rightSideX = -300f;
        [SerializeField] private float hoverDelaySeconds = 1f;
        [SerializeField] private float hideGracePeriodSeconds = 0.15f;

        private Coroutine pendingShowCoroutine;
        private Coroutine pendingHideCoroutine;
        private readonly List<TermInfoPanel> spawnedInfoPanels = new List<TermInfoPanel>();

        private void Awake()
        {
            instance = this;

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public static void Show(CardData card, Vector3 screenPosition)
        {
            if (instance == null || card == null)
            {
                return;
            }

            instance.BeginShow(card, null, null, null, screenPosition, useLiveCost: true);
        }

        public static void Show(CardData card, PlayerSide side, GameState state, Vector3 screenPosition, bool useLiveCost = true)
        {
            if (instance == null || card == null)
            {
                return;
            }

            instance.BeginShow(card, side, state, null, screenPosition, useLiveCost);
        }

        public static void Show(BoardUnit unit, GameState state, Vector3 screenPosition)
        {
            if (instance == null || unit == null)
            {
                return;
            }

            instance.BeginShow(unit.SourceCard, unit.Owner, state, unit, screenPosition, useLiveCost: true);
        }

        public static void Show(LeaderData leader, Vector3 screenPosition)
        {
            if (instance == null || leader == null)
            {
                return;
            }

            instance.BeginShowLeader(leader, screenPosition);
        }

        public static void Hide()
        {
            if (instance == null)
            {
                return;
            }

            instance.BeginHide();
        }

        private void BeginShow(CardData card, PlayerSide? side, GameState state, BoardUnit liveUnit, Vector3 screenPosition, bool useLiveCost)
        {
            CancelPendingHide();
            CancelPendingShow();

            bool alreadyVisible = root != null && root.activeSelf;

            if (alreadyVisible)
            {
                DisplayCard(card, side, state, liveUnit, screenPosition, useLiveCost);
                return;
            }

            pendingShowCoroutine = StartCoroutine(ShowAfterDelay(card, side, state, liveUnit, screenPosition, useLiveCost));
        }

        private void BeginShowLeader(LeaderData leader, Vector3 screenPosition)
        {
            CancelPendingHide();
            CancelPendingShow();

            bool alreadyVisible = root != null && root.activeSelf;

            if (alreadyVisible)
            {
                DisplayLeader(leader, screenPosition);
                return;
            }

            pendingShowCoroutine = StartCoroutine(ShowLeaderAfterDelay(leader, screenPosition));
        }

        private void BeginHide()
        {
            CancelPendingShow();
            CancelPendingHide();
            pendingHideCoroutine = StartCoroutine(HideAfterGracePeriod());
        }

        private void CancelPendingShow()
        {
            if (pendingShowCoroutine != null)
            {
                StopCoroutine(pendingShowCoroutine);
                pendingShowCoroutine = null;
            }
        }

        private void CancelPendingHide()
        {
            if (pendingHideCoroutine != null)
            {
                StopCoroutine(pendingHideCoroutine);
                pendingHideCoroutine = null;
            }
        }

        private System.Collections.IEnumerator HideAfterGracePeriod()
        {
            yield return new WaitForSeconds(hideGracePeriodSeconds);
            pendingHideCoroutine = null;

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private System.Collections.IEnumerator ShowAfterDelay(CardData card, PlayerSide? side, GameState state, BoardUnit liveUnit, Vector3 screenPosition, bool useLiveCost)
        {
            yield return new WaitForSeconds(hoverDelaySeconds);
            pendingShowCoroutine = null;
            DisplayCard(card, side, state, liveUnit, screenPosition, useLiveCost);
        }

        private System.Collections.IEnumerator ShowLeaderAfterDelay(LeaderData leader, Vector3 screenPosition)
        {
            yield return new WaitForSeconds(hoverDelaySeconds);
            pendingShowCoroutine = null;
            DisplayLeader(leader, screenPosition);
        }

        private void DisplayCard(CardData card, PlayerSide? side, GameState state, BoardUnit liveUnit, Vector3 screenPosition, bool useLiveCost)
        {
            if (root != null)
            {
                root.SetActive(true);
            }

            PositionPreview(screenPosition);

            if (artImage != null)
            {
                artImage.sprite = card.CardArt;
            }

            if (nameText != null)
            {
                nameText.text = CardDisplayFormatter.GetNameText(card);
            }

            if (costText != null)
            {
                costText.gameObject.SetActive(true);
                costText.text = side.HasValue && useLiveCost
                    ? CardDisplayFormatter.GetCurrentCostText(card, side.Value, state)
                    : CardDisplayFormatter.GetCostText(card);
            }

            if (abilityText != null)
            {
                abilityText.text = liveUnit != null
                    ? CardDisplayFormatter.GetAbilityText(liveUnit)
                    : CardDisplayFormatter.GetAbilityText(card);
            }

            UnitCardData unitCard = card as UnitCardData;
            bool isUnit = unitCard != null;

            if (attackText != null)
            {
                attackText.gameObject.SetActive(isUnit);
                if (isUnit)
                {
                    attackText.text = GetAttackTextFor(unitCard, side, state, liveUnit);
                }
            }

            if (healthText != null)
            {
                healthText.gameObject.SetActive(isUnit);
                if (isUnit)
                {
                    healthText.text = GetHealthTextFor(unitCard, side, state, liveUnit);
                }
            }

            PopulateExtraInfo(card, liveUnit);
        }

        private void PopulateExtraInfo(CardData card, BoardUnit liveUnit = null)
        {
            ClearExtraInfo();

            if (extraInfoContainer == null || infoPanelPrefab == null)
            {
                Debug.Log($"[CardHoverPreview] PopulateExtraInfo skipped for {card.CardName} — extraInfoContainer or infoPanelPrefab not assigned in the Inspector.");
                return;
            }

            if (liveUnit != null && liveUnit.IsSilenced)
            {
                Debug.Log($"[CardHoverPreview] PopulateExtraInfo skipped for {card.CardName} — unit is Silenced.");
                return;
            }

            int keywordCount = 0;

            if (card is UnitCardData unitCard)
            {
                foreach (Keyword keyword in KeywordReference.GetAllValues())
                {
                    if (!unitCard.HasKeyword(keyword))
                    {
                        continue;
                    }

                    if (KeywordReference.TryGetDescription(keyword, out string description))
                    {
                        SpawnInfoPanel(keyword.ToString(), description);
                        keywordCount++;
                    }
                }
            }

            int referencedCardCount = 0;

            foreach (CardEffect effect in card.Effects)
            {
                if (effect.relevantCard == null)
                {
                    continue;
                }

                string referencedDescription = GetReferencedCardDescription(effect.relevantCard);
                SpawnInfoPanel(effect.relevantCard.CardName, referencedDescription);
                referencedCardCount++;
            }

            int grantedKeywordCount = 0;
            Keyword alreadyShownGrantedKeywords = Keyword.None;

            foreach (CardEffect effect in card.Effects)
            {
                if (effect.action != EffectActionType.GrantKeyword && effect.action != EffectActionType.GrantRush)
                {
                    continue;
                }

                Keyword grantedKeyword = effect.action == EffectActionType.GrantRush ? Keyword.Rush : effect.keyword;

                if (grantedKeyword == Keyword.None)
                {
                    continue;
                }

                if ((alreadyShownGrantedKeywords & grantedKeyword) != 0)
                {
                    continue;
                }

                if (card is UnitCardData grantingUnitCard && grantingUnitCard.HasKeyword(grantedKeyword))
                {
                    continue;
                }

                if (!KeywordReference.TryGetDescription(grantedKeyword, out string grantedDescription))
                {
                    continue;
                }

                SpawnInfoPanel(grantedKeyword.ToString(), grantedDescription);
                alreadyShownGrantedKeywords |= grantedKeyword;
                grantedKeywordCount++;
            }

            if (liveUnit != null)
            {
                Debug.Log($"[CardHoverPreview] Checking liveUnit.GrantedKeywords for {liveUnit.SourceCard.CardName}: {liveUnit.GrantedKeywords}");

                foreach (Keyword keyword in KeywordReference.GetAllValues())
                {
                    if ((liveUnit.GrantedKeywords & keyword) == 0)
                    {
                        continue;
                    }

                    if ((alreadyShownGrantedKeywords & keyword) != 0)
                    {
                        continue;
                    }

                    if (liveUnit.SourceCard.HasKeyword(keyword))
                    {
                        continue;
                    }

                    if (!KeywordReference.TryGetDescription(keyword, out string description))
                    {
                        continue;
                    }

                    SpawnInfoPanel(keyword.ToString(), description);
                    alreadyShownGrantedKeywords |= keyword;
                    grantedKeywordCount++;
                }
            }
        }

        private static string GetReferencedCardDescription(CardData referencedCard)
        {
            if (!string.IsNullOrEmpty(referencedCard.AbilityText))
            {
                return referencedCard.AbilityText;
            }

            if (referencedCard is UnitCardData referencedUnit)
            {
                return $"{referencedUnit.ManaCost} cost, {referencedUnit.Attack} attack, {referencedUnit.Health} health.";
            }

            return string.Empty;
        }

        private void SpawnInfoPanel(string term, string description)
        {
            TermInfoPanel instance = Instantiate(infoPanelPrefab, extraInfoContainer);
            instance.Bind(term, description);
            spawnedInfoPanels.Add(instance);
        }

        private void ClearExtraInfo()
        {
            foreach (TermInfoPanel existing in spawnedInfoPanels)
            {
                if (existing != null)
                {
                    Destroy(existing.gameObject);
                }
            }

            spawnedInfoPanels.Clear();
        }

        private static string GetAttackTextFor(UnitCardData unitCard, PlayerSide? side, GameState state, BoardUnit liveUnit)
        {
            if (liveUnit != null && state != null)
            {
                return CardDisplayFormatter.GetAttackText(liveUnit, state);
            }

            if (side.HasValue && state != null)
            {
                return CardDisplayFormatter.GetAttackText(unitCard, side.Value, state);
            }

            return CardDisplayFormatter.GetAttackText(unitCard);
        }

        private static string GetHealthTextFor(UnitCardData unitCard, PlayerSide? side, GameState state, BoardUnit liveUnit)
        {
            if (liveUnit != null && state != null)
            {
                return CardDisplayFormatter.GetHealthText(liveUnit, state);
            }

            if (side.HasValue && state != null)
            {
                return CardDisplayFormatter.GetHealthText(unitCard, side.Value, state);
            }

            return CardDisplayFormatter.GetHealthText(unitCard);
        }

        private void DisplayLeader(LeaderData leader, Vector3 screenPosition)
        {
            if (root != null)
            {
                root.SetActive(true);
            }

            PositionPreview(screenPosition);

            if (artImage != null)
            {
                artImage.sprite = leader.Portrait;
            }

            if (nameText != null)
            {
                nameText.text = leader.LeaderName;
            }

            if (costText != null)
            {
                costText.gameObject.SetActive(false);
            }

            if (abilityText != null)
            {
                abilityText.text = leader.AbilityText;
            }

            if (attackText != null)
            {
                attackText.gameObject.SetActive(false);
            }

            if (healthText != null)
            {
                healthText.gameObject.SetActive(true);
                healthText.text = leader.MaxHealth.ToString();
            }

            ClearExtraInfo();
        }

        private void PositionPreview(Vector3 screenPosition)
        {
            if (rootRect == null)
            {
                return;
            }

            bool isOnLeftHalf = screenPosition.x < Screen.width / 2f;
            float targetX = isOnLeftHalf ? leftSideX : rightSideX;

            Vector2 anchoredPosition = rootRect.anchoredPosition;
            anchoredPosition.x = targetX;
            rootRect.anchoredPosition = anchoredPosition;

            MirrorExtraInfoSide(isOnLeftHalf);
        }

        private void MirrorExtraInfoSide(bool isOnLeftHalf)
        {
            if (extraInfoContainer == null)
            {
                return;
            }

            Vector2 anchoredPosition = extraInfoContainer.anchoredPosition;
            anchoredPosition.x = isOnLeftHalf ? extraInfoGapX : -extraInfoGapX;
            extraInfoContainer.anchoredPosition = anchoredPosition;
        }
    }
}