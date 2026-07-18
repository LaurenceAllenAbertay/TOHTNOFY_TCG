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
        [SerializeField] private Image artImage;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Image targetableHighlight;

        private CanvasGroup canvasGroup;
        private GameManager gameManager;
        private GameState boundState;

        public BoardUnit Unit { get; private set; }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            gameManager = FindFirstObjectByType<GameManager>();
        }

        private void Update()
        {
            RefreshTargetableHighlight();
        }

        public void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
        }

        public void Bind(BoardUnit unit, GameState state)
        {
            Unit = unit;
            boundState = state;

            if (artImage != null)
            {
                artImage.sprite = unit.SourceCard.CardArt;
            }

            if (attackText != null)
            {
                attackText.text = CardDisplayFormatter.GetAttackText(unit, state);
            }

            if (healthText != null)
            {
                healthText.text = CardDisplayFormatter.GetHealthText(unit, state);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.dragging || Unit == null)
            {
                return;
            }

            CardHoverPreview.Show(Unit.SourceCard, transform.position);
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

        private void RefreshTargetableHighlight()
        {
            if (targetableHighlight == null || Unit == null || boundState == null || gameManager == null)
            {
                return;
            }

            CardEffect pending = gameManager.State.PendingTargetedEffect;
            BoardUnit pendingSource = gameManager.State.PendingTargetedEffectSource;

            bool isTargetable = pending != null
                && Unit != pendingSource
                && EffectTargeting.IsValidTarget(pending.targetType, EffectTarget.ForUnit(Unit), boundState);

            targetableHighlight.enabled = isTargetable;
        }
    }
}