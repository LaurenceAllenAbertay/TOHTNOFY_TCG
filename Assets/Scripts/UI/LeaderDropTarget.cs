using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DDD.TNFY.TCG.Core;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;
using UnityEngine.UI;

namespace DDD.TNFY.TCG.UI
{
    public class LeaderDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [System.Serializable]
        private struct StatusIcon
        {
            public StatusEffectType statusType;
            public GameObject icon;
        }

        [SerializeField] private PlayerSide side;
        [SerializeField] private Image artImage;
        [SerializeField] private Image highlightImage;
        [SerializeField] private List<StatusIcon> statusIcons = new List<StatusIcon>();

        private GameManager gameManager;
        private NetworkedMatchSync networkSync;

        public PlayerSide Side => side.ToActualSide(LocalSide);

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

        private void Awake()
        {
            gameManager = FindFirstObjectByType<GameManager>();

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

            Player player = gameManager.State.GetPlayer(Side);

            if (player == null)
            {
                return;
            }

            if (artImage != null && player.Leader != null)
            {
                artImage.sprite = player.Leader.Portrait;
            }

            RefreshStatusIcons(player);
        }

        private void RefreshStatusIcons(Player player)
        {
            for (int i = 0; i < statusIcons.Count; i++)
            {
                StatusIcon entry = statusIcons[i];

                if (entry.icon == null)
                {
                    continue;
                }

                bool hasStatus = player.HasStatus(entry.statusType);
                entry.icon.SetActive(hasStatus);
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlightImage != null)
            {
                highlightImage.enabled = highlighted;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null)
            {
                return;
            }

            HandCardDrag handDrag = eventData.pointerDrag.GetComponent<HandCardDrag>();
            if (handDrag != null)
            {
                handDrag.HandleDroppedOnLeader(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.dragging || gameManager == null || gameManager.State == null)
            {
                return;
            }

            Player player = gameManager.State.GetPlayer(Side);

            if (player == null || player.Leader == null)
            {
                return;
            }

            CardHoverPreview.Show(player.Leader, player, transform.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CardHoverPreview.Hide();
        }
    }
}