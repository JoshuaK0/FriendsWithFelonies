using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HotbarUISlot : MonoBehaviour
{
    [SerializeField] private Graphic selectedGraphic;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;

    public void SetSelected(bool selected)
    {
        if (selectedGraphic != null)
            selectedGraphic.enabled = selected;
    }

    public void SetEmpty()
    {
        if (nameText != null)
            nameText.text = string.Empty;

        if (countText != null)
            countText.text = string.Empty;
    }

    public void SetItem(string displayName, int count)
    {
        if (nameText != null)
            nameText.text = displayName;

        if (countText != null)
            countText.text = count.ToString();
    }
}
