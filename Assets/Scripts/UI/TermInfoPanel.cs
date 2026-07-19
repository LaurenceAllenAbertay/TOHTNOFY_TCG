using UnityEngine;
using TMPro;

namespace DDD.TNFY.TCG.UI
{
    public class TermInfoPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI infoText;

        public void Bind(string term, string description)
        {
            if (nameText != null)
            {
                nameText.text = term;
            }

            if (infoText != null)
            {
                infoText.text = description;
            }
        }
    }
}