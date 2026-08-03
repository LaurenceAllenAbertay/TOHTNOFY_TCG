using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Core;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.UI
{
    public class BoardCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IDropHandler
    {
        [System.Serializable]
        private struct KeywordIcon
        {
            public Keyword keyword;
            public GameObject icon;
        }

        [System.Serializable]
        private struct StatusIcon
        {
            public StatusEffectType statusType;
            public GameObject icon;
        }

        private static class AnimState
        {
            public const string Idle = "Idle";
            public const string Attack = "Attack";
            public const string BifurcatedAttack = "BifurcatedAttack";
            public const string Triggered = "Triggered";
            public const string Active = "Active";
        }

        private const float StunnedOverlayAlpha = 0.9f;
        private const float StunnedOverlayHiddenAlpha = 0f;

        [SerializeField] private Image artImage;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Image targetableHighlight;
        [SerializeField] private Image stunnedOverlay;
        [SerializeField] private GameObject attackingIndicator;
        [SerializeField] private Animator animator;
        [SerializeField] private List<KeywordIcon> keywordIcons = new List<KeywordIcon>();
        [SerializeField] private List<StatusIcon> statusIcons = new List<StatusIcon>();

        private CanvasGroup canvasGroup;
        private GameManager gameManager;
        private GameState boundState;
        private Color originalHealthTextColor;
        private NetworkedMatchSync networkSync;

        private BoardUnit lastBoundUnit;
        private int lastKnownSlotIndex = -1;
        private string currentOneShot;
        private BoardUnit lastAnimatedAttacker;

        public BoardUnit Unit { get; private set; }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            gameManager = FindFirstObjectByType<GameManager>();

            if (gameManager != null)
            {
                networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            }

            if (healthText != null)
            {
                originalHealthTextColor = healthText.color;
            }
        }

        private void Update()
        {
            RefreshTargetableHighlight();
            RefreshStunnedOverlay();
            RefreshAttackAnimation();
            UpdateAnimationState();
            RefreshKeywordIcons();
            RefreshStatusIcons();
            RefreshAttackingIndicator();
        }

        public void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
        }

        public void Bind(BoardUnit unit, GameState state)
        {
            bool isNewUnit = unit != lastBoundUnit;

            Unit = unit;
            boundState = state;

            if (artImage != null)
            {
                artImage.sprite = unit.SourceCard.CardArt;
            }

            if (attackText != null)
            {
                string newAttackText = CardDisplayFormatter.GetAttackText(unit, state);
                if (attackText.text != newAttackText)
                {
                    Debug.Log($"[BoardCardView] {unit.SourceCard.CardName} (Slot={unit.SlotIndex}) attack text changed: '{attackText.text}' -> '{newAttackText}'");
                }
                attackText.text = newAttackText;
            }

            if (healthText != null)
            {
                string newHealthText = CardDisplayFormatter.GetHealthText(unit, state);
                Color newHealthColor = CardDisplayFormatter.GetHealthColor(unit.CurrentHealth, unit.GetEffectiveMaxHealth(state), originalHealthTextColor);

                healthText.text = newHealthText;
                healthText.color = newHealthColor;
            }

            if (isNewUnit)
            {
                lastBoundUnit = unit;
                lastKnownSlotIndex = unit.SlotIndex;
                PlayTriggered();
            }
            else if (unit.SlotIndex != lastKnownSlotIndex)
            {
                lastKnownSlotIndex = unit.SlotIndex;
                PlayTriggered();
            }
        }

        public void PlayTriggered()
        {
            PlayOneShot(AnimState.Triggered);
        }

        public void PlayAttack()
        {
            bool isBifurcated = Unit != null
                && boundState != null
                && Unit.HasKeyword(Keyword.BifurcatedAttack, boundState);

            PlayOneShot(isBifurcated ? AnimState.BifurcatedAttack : AnimState.Attack);
        }

        public void OnAttackHitLanded(int hitIndex)
        {
            if (gameManager == null || gameManager.State == null)
            {
                Debug.LogWarning($"[BoardCardView] OnAttackHitLanded({hitIndex}) fired but gameManager/State is null.");
                return;
            }

            Debug.Log($"[BoardCardView] {(Unit != null ? Unit.SourceCard.CardName : "unknown")} OnAttackHitLanded: hitIndex={hitIndex}. Raising AttackHitLanded.");
            gameManager.State.RaiseAttackHitLanded(hitIndex);
        }

        private bool oneShotJustRequested;

        private void PlayOneShot(string stateName)
        {
            if (animator == null)
            {
                return;
            }

            currentOneShot = stateName;
            oneShotJustRequested = true;
            animator.Play(stateName, 0, 0f);
        }

        private void UpdateAnimationState()
        {
            if (animator == null || Unit == null)
            {
                return;
            }

            if (oneShotJustRequested)
            {
                oneShotJustRequested = false;
                return;
            }

            bool justFinishedOneShot = false;

            if (currentOneShot != null)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

                bool stillOnRequestedState = stateInfo.IsName(currentOneShot);
                bool finished = stillOnRequestedState && stateInfo.normalizedTime >= 1f && !stateInfo.loop;

                if (!finished)
                {
                    return;
                }

                bool wasAttackAnimation = currentOneShot == AnimState.Attack || currentOneShot == AnimState.BifurcatedAttack;

                currentOneShot = null;
                justFinishedOneShot = true;

                if (wasAttackAnimation && gameManager != null && gameManager.State != null)
                {
                    Debug.Log($"[BoardCardView] {(Unit != null ? Unit.SourceCard.CardName : "unknown")} attack animation finished playing. Raising AttackAnimationFinished.");
                    gameManager.State.RaiseAttackAnimationFinished(Unit);
                }
            }

            bool isPendingTargetSource = gameManager != null
                && gameManager.State != null
                && gameManager.State.PendingTargetedEffectSource == Unit;

            bool isPendingEnemyMoveGrantTarget = gameManager != null
                && gameManager.State != null
                && gameManager.State.HasPendingEnemyMoveGrantOnPlay
                && gameManager.State.PendingEnemyMoveGrantTarget == Unit;

            bool shouldBeActive = isPendingTargetSource || isPendingEnemyMoveGrantTarget;

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            bool isCurrentlyActive = currentState.IsName(AnimState.Active);

            if (shouldBeActive && !isCurrentlyActive)
            {
                Debug.Log($"[BoardCardView] Playing Active on {Unit?.SourceCard?.CardName} (Owner={Unit?.Owner}, Slot={Unit?.SlotIndex}) - isPendingTargetSource={isPendingTargetSource}, isPendingEnemyMoveGrantTarget={isPendingEnemyMoveGrantTarget}.");
                animator.Play(AnimState.Active, 0, 0f);
            }
            else if (!shouldBeActive && (isCurrentlyActive || justFinishedOneShot))
            {
                animator.Play(AnimState.Idle, 0, 0f);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.dragging || Unit == null)
            {
                return;
            }

            CardHoverPreview.Show(Unit, boundState, transform.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CardHoverPreview.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Unit == null || gameManager == null || gameManager.State == null)
            {
                Debug.Log($"[BoardCardView] Click ignored: Unit={Unit}, gameManager={gameManager}, State={(gameManager != null ? gameManager.State : null)}");
                return;
            }

            Debug.Log($"[BoardCardView] Click on {Unit.SourceCard.CardName} (Owner={Unit.Owner}). PendingTargetedEffect={gameManager.State.PendingTargetedEffect?.action}");

            PlayerSide localSide = networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

            if (gameManager.State.PendingTargetedEffect != null)
            {
                HandleTargetedEffectClick(localSide);
                return;
            }

            HandleAttackClick(localSide);
        }

        private void HandleTargetedEffectClick(PlayerSide localSide)
        {
            BoardUnit effectSource = gameManager.State.PendingTargetedEffectSource;

            if (effectSource == null || effectSource.Owner != localSide)
            {
                return;
            }

            if (networkSync != null)
            {
                networkSync.RequestResolveTargetedEffect(EffectTarget.ForUnit(Unit));
            }
            else
            {
                bool resolved = gameManager.Phases.TryResolvePendingTargetedEffect(EffectTarget.ForUnit(Unit));
                Debug.Log($"[BoardCardView] TryResolvePendingTargetedEffect on {Unit.SourceCard.CardName} returned {resolved}");
            }
        }

        private void HandleAttackClick(PlayerSide localSide)
        {
            if (Unit.Owner != localSide)
            {
                return;
            }

            if (gameManager.State.ActivePlayer != localSide)
            {
                return;
            }

            if (!gameManager.Phases.CanAttackWithUnit(Unit.SlotIndex))
            {
                return;
            }

            bool attacked = networkSync != null
                ? networkSync.RequestAttackWithUnit(Unit.SlotIndex)
                : gameManager.Phases.TryAttackWithUnit(Unit.SlotIndex);

            Debug.Log($"[BoardCardView] Attack click on {Unit.SourceCard.CardName} (Slot={Unit.SlotIndex}) -> attacked={attacked}");
        }

        public void OnDrop(PointerEventData eventData)
        {
            Debug.Log($"[BoardCardView] OnDrop fired on {(Unit != null ? Unit.SourceCard.CardName : "null Unit")}. pointerDrag={(eventData.pointerDrag != null ? eventData.pointerDrag.name : "null")}");

            if (eventData.pointerDrag == null || Unit == null)
            {
                return;
            }

            HandCardDrag handDrag = eventData.pointerDrag.GetComponent<HandCardDrag>();

            if (handDrag != null && (handDrag.IsDraggingItemCard || handDrag.IsDraggingUnitCard))
            {
                handDrag.HandleDroppedOnUnit(Unit);
            }
        }

        private void RefreshAttackAnimation()
        {
            if (gameManager == null || gameManager.State == null || Unit == null)
            {
                return;
            }

            BoardUnit attacker = gameManager.State.CurrentlyAttackingUnit;

            if (attacker == Unit && lastAnimatedAttacker != Unit)
            {
                lastAnimatedAttacker = Unit;
                PlayAttack();
            }
            else if (attacker != Unit && lastAnimatedAttacker == Unit)
            {
                lastAnimatedAttacker = null;
            }
        }

        private void RefreshKeywordIcons()
        {
            if (Unit == null || boundState == null)
            {
                return;
            }

            for (int i = 0; i < keywordIcons.Count; i++)
            {
                KeywordIcon entry = keywordIcons[i];

                if (entry.icon == null)
                {
                    continue;
                }

                bool hasKeyword = Unit.HasKeyword(entry.keyword, boundState);
                entry.icon.SetActive(hasKeyword);
            }
        }

        private void RefreshStatusIcons()
        {
            if (Unit == null)
            {
                return;
            }

            for (int i = 0; i < statusIcons.Count; i++)
            {
                StatusIcon entry = statusIcons[i];

                if (entry.icon == null)
                {
                    continue;
                }

                bool hasStatus = Unit.HasStatus(entry.statusType);
                entry.icon.SetActive(hasStatus);
            }
        }

        private void RefreshAttackingIndicator()
        {
            if (attackingIndicator == null || Unit == null || gameManager == null || gameManager.State == null)
            {
                return;
            }

            bool willAttack = Unit.Owner == gameManager.State.ActivePlayer && gameManager.Phases.CanAttackWithUnit(Unit.SlotIndex);

            attackingIndicator.SetActive(willAttack);
        }

        private bool lastLoggedStunnedState;
        private bool hasLoggedStunnedStateOnce;

        private void RefreshStunnedOverlay()
        {
            if (stunnedOverlay == null || Unit == null)
            {
                return;
            }

            bool isStunned = Unit.HasStatus(StatusEffectType.Stunned);

            if (!hasLoggedStunnedStateOnce || isStunned != lastLoggedStunnedState)
            {
                lastLoggedStunnedState = isStunned;
                hasLoggedStunnedStateOnce = true;
            }

            Color color = stunnedOverlay.color;
            color.a = isStunned ? StunnedOverlayAlpha : StunnedOverlayHiddenAlpha;
            stunnedOverlay.color = color;
        }

        private bool lastLoggedTargetableState;
        private bool hasLoggedTargetableStateOnce;

        private void RefreshTargetableHighlight()
        {
            if (targetableHighlight == null || Unit == null || boundState == null || gameManager == null)
            {
                return;
            }

            CardEffect pending = gameManager.State.PendingTargetedEffect;
            bool isExcludedAsSelf = gameManager.State.IsExcludedAsSelfTarget(Unit);

            bool isTargetable = pending != null
                && !isExcludedAsSelf
                && EffectTargeting.IsValidTarget(pending.targetType, EffectTarget.ForUnit(Unit), boundState);

            if (pending != null && (!hasLoggedTargetableStateOnce || isTargetable != lastLoggedTargetableState))
            {
                Debug.Log($"[BoardCardView] {Unit.SourceCard.CardName} (Slot={Unit.SlotIndex}) targetable highlight -> {isTargetable}, isExcludedAsSelf={isExcludedAsSelf}");
                lastLoggedTargetableState = isTargetable;
                hasLoggedTargetableStateOnce = true;
            }

            targetableHighlight.enabled = isTargetable;
        }
    }
}