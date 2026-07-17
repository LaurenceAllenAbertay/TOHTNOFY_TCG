using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class BoardCardView : MonoBehaviour
    {
        [SerializeField] private Image artImage;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;

        private CanvasGroup canvasGroup;

        public BoardUnit Unit { get; private set; }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
        }

        public void Bind(BoardUnit unit)
        {
            Unit = unit;

            if (artImage != null)
            {
                artImage.sprite = unit.SourceCard.CardArt;
            }

            if (attackText != null)
            {
                attackText.text = CardDisplayFormatter.GetAttackText(unit);
            }

            if (healthText != null)
            {
                healthText.text = CardDisplayFormatter.GetHealthText(unit);
            }
        }
    }
}