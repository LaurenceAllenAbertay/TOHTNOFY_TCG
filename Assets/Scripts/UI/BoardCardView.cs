using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DDD.TNFY.TCG.Core;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.UI
{
    public class BoardCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private static class AnimState
        {
            public const string Idle = "Idle";
            public const string Attack = "Attack";
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
        [SerializeField] private Animator animator;

        private CanvasGroup canvasGroup;
        private GameManager gameManager;
        private GameState boundState;

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
        }

        private void Update()
        {
            RefreshTargetableHighlight();
            RefreshStunnedOverlay();
            RefreshAttackAnimation();
            UpdateAnimationState();
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
                healthText.text = CardDisplayFormatter.GetHealthText(unit, state);
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
            PlayOneShot(AnimState.Attack);
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

                currentOneShot = null;
                justFinishedOneShot = true;
            }

            bool shouldBeActive = gameManager != null
                && gameManager.State != null
                && gameManager.State.PendingTargetedEffectSource == Unit;

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            bool isCurrentlyActive = currentState.IsName(AnimState.Active);

            if (shouldBeActive && !isCurrentlyActive)
            {
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

            if (gameManager.State.PendingTargetedEffect == null)
            {
                return;
            }

            bool resolved = gameManager.Phases.TryResolvePendingTargetedEffect(EffectTarget.ForUnit(Unit));
            Debug.Log($"[BoardCardView] TryResolvePendingTargetedEffect on {Unit.SourceCard.CardName} returned {resolved}");
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