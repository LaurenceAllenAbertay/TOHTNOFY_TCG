using UnityEngine;
using UnityEngine.EventSystems;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.UI
{
    public class LeaderPreviewHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private LeaderData leader;

        public void SetLeader(LeaderData newLeader)
        {
            leader = newLeader;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.dragging || leader == null)
            {
                return;
            }

            CardHoverPreview.Show(leader, null, transform.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CardHoverPreview.Hide();
        }
    }
}