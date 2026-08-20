using FishNet.Object;
using UnityEngine;

public class NetHotbarInventory : NetworkBehaviour
{
	public static NetHotbarInventory Instance;

	[Header("Item Data")]
	[SerializeField] private ItemRegistry registry;

	[Header("Selection")]
	[SerializeField] private int selectedIndex;

	[Header("Held Item")]
	[SerializeField] private Transform holdPoint;
	[SerializeField] private NetHeldItemVisual heldItemVisual;
	[SerializeField] private ItemServiceLocator itemServices;

	private PlayerInventory inventory;
	private GameObject heldInstance;
	private IUsableItem heldUsable;
	private int heldItemId = -1;

	private bool allowHeldItem = true;
	private bool isPaused;

	public int SlotCount => inventory != null ? inventory.SlotCount : 0;
	public int SelectedIndex => selectedIndex;
	public ItemRegistry Registry => registry;
	public ItemServiceLocator ItemServices => itemServices;
	public PlayerInventory Inventory => inventory;
	public bool IsPaused => isPaused;

	public bool CanProcessPlayerInput =>
		IsOwner &&
		!isPaused &&
		allowHeldItem;

	public delegate void HotbarChanged(int selectedIndex);
	public event HotbarChanged OnHotbarChanged;

	private void Awake()
	{
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

	public override void OnStartClient()
	{
		base.OnStartClient();

		if (!IsOwner)
			return;

		Instance = this;

		PlayerInventory.InstanceChanged += HandleInventoryInstanceChanged;
		BindInventory(PlayerInventory.Instance);
	}

	public override void OnStopClient()
	{
		if (IsOwner)
			PlayerInventory.InstanceChanged -= HandleInventoryInstanceChanged;

		UnbindInventory();

		if (Instance == this)
			Instance = null;

		ClearHeld();

		base.OnStopClient();
	}

	private void OnDestroy()
	{
		PlayerInventory.InstanceChanged -= HandleInventoryInstanceChanged;

		UnbindInventory();

		if (Instance == this)
			Instance = null;

		ClearHeld();
	}

	private void HandleInventoryInstanceChanged(PlayerInventory newInventory)
	{
		if (!IsOwner)
			return;

		BindInventory(newInventory);
	}

	private void BindInventory(PlayerInventory newInventory)
	{
		if (!IsOwner)
			return;

		if (inventory == newInventory)
			return;

		if (inventory != null)
			inventory.OnInventoryChanged -= HandleInventoryChanged;

		inventory = newInventory;

		if (inventory == null)
		{
			selectedIndex = 0;

			ClearHeld();
			heldItemVisual?.SetHeldItem(-1);

			OnHotbarChanged?.Invoke(selectedIndex);
			return;
		}

		inventory.OnInventoryChanged += HandleInventoryChanged;

		selectedIndex =
			Mathf.Clamp(
				selectedIndex,
				0,
				inventory.SlotCount - 1);

		RefreshHeld(true);
		OnHotbarChanged?.Invoke(selectedIndex);
	}

	private void UnbindInventory()
	{
		if (inventory == null)
			return;

		inventory.OnInventoryChanged -= HandleInventoryChanged;
		inventory = null;
	}

	private void HandleInventoryChanged()
	{
		if (!IsOwner)
			return;

		RefreshHeld(false);
		OnHotbarChanged?.Invoke(selectedIndex);
	}

	public void SetPaused(bool paused)
	{
		if (!IsOwner)
			return;

		isPaused = paused;
	}

	public void Pause() => SetPaused(true);
	public void Resume() => SetPaused(false);

	public HotbarSlot GetSlot(int index)
	{
		return inventory != null ? inventory.GetSlot(index) : null;
	}

	public HotbarSlot GetSelectedSlot()
	{
		if (inventory == null ||
			selectedIndex < 0 ||
			selectedIndex >= inventory.SlotCount)
		{
			return null;
		}

		return inventory.GetSlot(selectedIndex);
	}

	public int GetSelectedItemId()
	{
		HotbarSlot slot = GetSelectedSlot();
		return slot == null || slot.IsEmpty ? -1 : slot.itemId;
	}

	public void SelectSlot(int index)
	{
		if (!IsOwner || inventory == null)
			return;

		if (index < 0 ||
			index >= inventory.SlotCount ||
			selectedIndex == index)
		{
			return;
		}

		selectedIndex = index;
		RefreshHeld(true);
		OnHotbarChanged?.Invoke(selectedIndex);
	}

	public void SelectNext(int direction)
	{
		if (!IsOwner ||
			inventory == null ||
			inventory.SlotCount <= 1)
		{
			return;
		}

		int count = inventory.SlotCount;

		selectedIndex =
			direction > 0
				? (selectedIndex + 1) % count
				: (selectedIndex - 1 + count) % count;

		RefreshHeld(true);
		OnHotbarChanged?.Invoke(selectedIndex);
	}

	// These facade methods are intentionally NOT pause-blocked.
	// Pause only blocks player input entry points.

	public bool WouldAcceptPickup(int itemId)
	{
		if (inventory == null)
			return false;

		return inventory.WouldAcceptPickup(
			itemId,
			registry,
			selectedIndex);
	}

	public bool TryAddPickup(
		int itemId,
		out int swappedOutItemId,
		out int swappedOutCount)
	{
		swappedOutItemId = -1;
		swappedOutCount = 0;

		if (inventory == null)
			return false;

		return inventory.TryAddPickup(
			itemId,
			registry,
			selectedIndex,
			out swappedOutItemId,
			out swappedOutCount);
	}

	public bool ConsumeOneConfirmed(int itemId)
	{
		if (!IsOwner || inventory == null)
			return false;

		return inventory.ConsumeOne(
			itemId,
			registry,
			selectedIndex);
	}

	public bool RemoveOneOfType(int itemId)
	{
		return ConsumeOneConfirmed(itemId);
	}

	public bool ContainsItem(int itemId)
	{
		return inventory != null && inventory.ContainsItem(itemId);
	}

	public int GetItemCount(int itemId)
	{
		return inventory != null ? inventory.GetItemCount(itemId) : 0;
	}

	public bool RemoveEmptyItem(int itemId)
	{
		if (!IsOwner || inventory == null)
			return false;

		return inventory.RemoveEmptyItem(itemId);
	}

	public void NotifyChanged()
	{
		inventory?.NotifyInventoryChanged();
	}

	public void UnequipCurrentItem()
	{
		if (!IsOwner)
			return;

		allowHeldItem = false;

		ClearHeld();
		heldItemVisual?.SetHeldItem(-1);
	}

	public void ReequipCurrentItem()
	{
		if (!IsOwner)
			return;

		allowHeldItem = true;
		RefreshHeld(true);
	}

	private void RefreshHeld(bool force)
	{
		if (!IsOwner || !allowHeldItem)
			return;

		if (inventory == null)
		{
			ClearHeld();
			heldItemVisual?.SetHeldItem(-1);
			return;
		}

		int itemId = GetSelectedItemId();

		if (!force && itemId == heldItemId)
			return;

		ClearHeld();

		heldItemId = itemId;
		heldItemVisual?.SetHeldItem(itemId);

		if (itemId < 0 || holdPoint == null || registry == null)
			return;

		GameObject heldPrefab = registry.HeldPrefabOf(itemId);

		if (heldPrefab == null)
			return;

		heldInstance = Instantiate(heldPrefab, holdPoint);
		heldInstance.transform.localPosition = Vector3.zero;
		heldInstance.transform.localRotation = Quaternion.identity;

		MonoBehaviour[] behaviours =
			heldInstance.GetComponentsInChildren<MonoBehaviour>(true);

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
		if (heldUsable != null)
		{
			heldUsable.OnUnequip();
			heldUsable = null;
		}

		if (heldInstance != null)
		{
			Destroy(heldInstance);
			heldInstance = null;
		}

		heldItemId = -1;
	}

	public GameObject GetCurrentItemGameObject()
	{
		return heldInstance;
	}
}
