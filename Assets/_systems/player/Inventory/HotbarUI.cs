using UnityEngine;

public class HotbarUI : MonoBehaviour
{
    [SerializeField] private NetHotbarInventory hotbar;
    [SerializeField] private HotbarUISlot slotPrefab;
    [SerializeField] private Transform slotParent;

    private HotbarUISlot[] uiSlots;

    private void Awake()
    {
        if (slotParent == null)
            slotParent = transform;

        if (hotbar == null)
        {
            Debug.LogWarning("HotbarUI: No hotbar assigned.", this);
            return;
        }

        BuildSlots();
        hotbar.OnHotbarChanged += OnHotbarChanged;
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (hotbar != null)
            hotbar.OnHotbarChanged -= OnHotbarChanged;
    }

    private void OnHotbarChanged(int selectedIndex)
    {
        RefreshAll();
    }

    private void BuildSlots()
    {
        for (int i = slotParent.childCount - 1; i >= 0; i--)
            Destroy(slotParent.GetChild(i).gameObject);

        uiSlots = new HotbarUISlot[hotbar.SlotCount];
        for (int i = 0; i < hotbar.SlotCount; i++)
            uiSlots[i] = Instantiate(slotPrefab, slotParent);
    }

    private void RefreshAll()
    {
        if (hotbar == null || uiSlots == null)
            return;

        ItemRegistry registry = hotbar.Registry;
        for (int i = 0; i < uiSlots.Length; i++)
        {
            HotbarUISlot uiSlot = uiSlots[i];
            HotbarSlot slot = hotbar.GetSlot(i);
            uiSlot.SetSelected(i == hotbar.SelectedIndex);

            if (slot == null || slot.IsEmpty)
            {
                uiSlot.SetEmpty();
                continue;
            }

            string displayName = registry != null
                ? registry.NameOf(slot.itemId)
                : $"Item {slot.itemId}";

            uiSlot.SetItem(displayName, slot.count);
        }
    }
}
