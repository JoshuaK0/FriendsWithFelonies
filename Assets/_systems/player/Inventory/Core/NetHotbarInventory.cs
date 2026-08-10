using FishNet.Object;
using UnityEngine;

public class NetHotbarInventory : NetworkBehaviour
{
    public static NetHotbarInventory Instance;

    [SerializeField] private ItemRegistry registry;
    [SerializeField, Min(1)] private int slotCount = 3;
    [SerializeField] private int selectedIndex;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private NetHeldItemVisual heldItemVisual;
    [SerializeField] private ItemServiceLocator itemServices;

    private HotbarSlot[] slots;
    private GameObject heldInstance;
    private IUsableItem heldUsable;

    public int SlotCount
    {
        get
        {
            EnsureInitialized();
            return slots.Length;
        }
    }

    public int SelectedIndex => selectedIndex;
    public ItemRegistry Registry => registry;
    public ItemServiceLocator ItemServices => itemServices;

    public delegate void HotbarChanged(int selectedIndex);
    public event HotbarChanged OnHotbarChanged;

    private void Awake()
    {
        EnsureInitialized();

        if (itemServices == null)
            itemServices = GetComponent<ItemServiceLocator>();

        if (itemServices == null)
            itemServices = GetComponentInParent<ItemServiceLocator>();
    }

    private void Reset()
    {
        heldItemVisual = GetComponent<NetHeldItemVisual>();
        itemServices = GetComponent<ItemServiceLocator>();

        if (itemServices == null)
            itemServices = GetComponentInParent<ItemServiceLocator>();
    }

    private void EnsureInitialized()
    {
        if (slots != null)
            return;

        slotCount = Mathf.Max(1, slotCount);
        slots = new HotbarSlot[slotCount];

        for (int i = 0; i < slots.Length; i++)
            slots[i] = new HotbarSlot();

        selectedIndex = Mathf.Clamp(selectedIndex, 0, slots.Length - 1);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        EnsureInitialized();

        if (!IsOwner)
            return;

        Instance = this;
        RefreshHeld();
        OnHotbarChanged?.Invoke(selectedIndex);
    }

    public override void OnStopClient()
    {
        if (Instance == this)
            Instance = null;

        ClearHeld();
        base.OnStopClient();
    }

    public HotbarSlot GetSlot(int index)
    {
        EnsureInitialized();
        return index >= 0 && index < slots.Length ? slots[index] : null;
    }

    public HotbarSlot GetSelectedSlot()
    {
        EnsureInitialized();
        return slots[selectedIndex];
    }

    public int GetSelectedItemId()
    {
        HotbarSlot slot = GetSelectedSlot();
        return slot == null || slot.IsEmpty ? -1 : slot.itemId;
    }

    public void SelectSlot(int index)
    {
        if (!IsOwner)
            return;

        EnsureInitialized();
        if (index < 0 || index >= slots.Length || selectedIndex == index)
            return;

        selectedIndex = index;
        RefreshHeld();
        OnHotbarChanged?.Invoke(selectedIndex);
    }

    public void SelectNext(int direction)
    {
        if (!IsOwner)
            return;

        EnsureInitialized();
        if (slots.Length <= 1)
            return;

        selectedIndex = direction > 0
            ? (selectedIndex + 1) % slots.Length
            : (selectedIndex - 1 + slots.Length) % slots.Length;

        RefreshHeld();
        OnHotbarChanged?.Invoke(selectedIndex);
    }

    private int FindStackableSlot(int itemId)
    {
        EnsureInitialized();
        if (registry == null || !registry.IsValidItemId(itemId))
            return -1;

        int maxStack = registry.MaxStackOf(itemId);
        for (int i = 0; i < slots.Length; i++)
        {
            HotbarSlot slot = slots[i];
            if (!slot.IsEmpty && slot.itemId == itemId && slot.count < maxStack)
                return i;
        }

        return -1;
    }

    private int FindEmptySlot()
    {
        EnsureInitialized();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsEmpty)
                return i;
        }

        return -1;
    }

    public bool WouldAcceptPickup(int itemId)
    {
        EnsureInitialized();
        if (registry == null || !registry.IsValidItemId(itemId))
            return false;

        if (FindStackableSlot(itemId) >= 0 || FindEmptySlot() >= 0)
            return true;

        HotbarSlot selected = slots[selectedIndex];

        if (selected.IsEmpty || selected.itemId == itemId)
            return false;

        return registry.IsDroppable(selected.itemId);
    }

    public bool TryAddPickup(int itemId, out int swappedOutItemId, out int swappedOutCount)
    {
        swappedOutItemId = -1;
        swappedOutCount = 0;

        EnsureInitialized();
        if (registry == null || !registry.IsValidItemId(itemId))
            return false;

        int targetIndex = FindStackableSlot(itemId);
        if (targetIndex >= 0)
        {
            slots[targetIndex].count++;
            NotifySlotMutation(targetIndex, false);
            return true;
        }

        targetIndex = FindEmptySlot();
        if (targetIndex >= 0)
        {
            slots[targetIndex].itemId = itemId;
            slots[targetIndex].count = 1;
            NotifySlotMutation(targetIndex, true);
            return true;
        }

        HotbarSlot selected = slots[selectedIndex];
        if (selected.IsEmpty ||
            selected.itemId == itemId ||
            !registry.IsDroppable(selected.itemId))
        {
            return false;
        }

        swappedOutItemId = selected.itemId;
        swappedOutCount = selected.count;
        selected.itemId = itemId;
        selected.count = 1;
        NotifySlotMutation(selectedIndex, true);
        return true;
    }

    /// <summary>
    /// Called by an item-specific network counterpart after the server accepts a consumable action.
    /// </summary>
    public bool ConsumeOneConfirmed(int itemId)
    {
        if (!IsOwner)
            return false;

        EnsureInitialized();

        int index = selectedIndex;
        if (slots[index].IsEmpty ||
            slots[index].itemId != itemId ||
            slots[index].count <= 0)
        {
            index = -1;

            for (int i = 0; i < slots.Length; i++)
            {
                HotbarSlot candidate = slots[i];

                if (!candidate.IsEmpty &&
                    candidate.itemId == itemId &&
                    candidate.count > 0)
                {
                    index = i;
                    break;
                }
            }
        }

        if (index < 0)
            return false;

        HotbarSlot slot = slots[index];
        slot.count = Mathf.Max(0, slot.count - 1);

        bool removeWhenEmpty =
            slot.count == 0 &&
            (registry == null || registry.ConsumeOnEmptyOf(itemId));

        if (removeWhenEmpty)
            slot.Clear();

        NotifySlotMutation(
            index,
            removeWhenEmpty && index == selectedIndex);

        return true;
    }

    public bool RemoveOneOfType(int itemId)
    {
        return ConsumeOneConfirmed(itemId);
    }

    public bool ContainsItem(int itemId)
    {
        EnsureInitialized();

        for (int i = 0; i < slots.Length; i++)
        {
            HotbarSlot slot = slots[i];

            if (!slot.IsEmpty &&
                slot.itemId == itemId &&
                slot.count > 0)
            {
                return true;
            }
        }

        return false;
    }

    public bool RemoveEmptyItem(int itemId)
    {
        if (!IsOwner)
            return false;

        EnsureInitialized();

        for (int i = 0; i < slots.Length; i++)
        {
            HotbarSlot slot = slots[i];

            if (slot.IsEmpty ||
                slot.itemId != itemId ||
                slot.count > 0)
            {
                continue;
            }

            slot.Clear();
            NotifySlotMutation(i, i == selectedIndex);
            return true;
        }

        return false;
    }

    public void NotifyChanged()
    {
        EnsureInitialized();
        RefreshHeld();
        OnHotbarChanged?.Invoke(selectedIndex);
    }

    private void NotifySlotMutation(int changedIndex, bool refreshHeld)
    {
        if (refreshHeld && changedIndex == selectedIndex)
            RefreshHeld();

        OnHotbarChanged?.Invoke(selectedIndex);
    }

    private void RefreshHeld()
    {
        if (!IsOwner)
            return;

        EnsureInitialized();
        ClearHeld();

        HotbarSlot selected = slots[selectedIndex];
        int itemId = selected.IsEmpty ? -1 : selected.itemId;
        heldItemVisual?.SetHeldItem(itemId);

        if (itemId < 0 || holdPoint == null || registry == null)
            return;

        GameObject heldPrefab = registry.HeldPrefabOf(itemId);
        if (heldPrefab == null)
            return;

        heldInstance = Instantiate(heldPrefab, holdPoint);
        heldInstance.transform.localPosition = Vector3.zero;
        heldInstance.transform.localRotation = Quaternion.identity;

        MonoBehaviour[] behaviours = heldInstance.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IHotbarItemContextReceiver receiver)
                receiver.InitializeHotbarItem(this, itemId);

            if (heldUsable == null && behaviours[i] is IUsableItem usable)
                heldUsable = usable;
        }

        heldUsable?.OnEquip();
    }

    private void ClearHeld()
    {
        heldUsable?.OnUnequip();
        heldUsable = null;

        if (heldInstance != null)
        {
            Destroy(heldInstance);
            heldInstance = null;
        }
    }

    public GameObject GetCurrentItemGameObject()
    {
        return heldInstance;
    }
}
