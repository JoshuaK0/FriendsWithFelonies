using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class NetHotbarInventory : NetworkBehaviour
{
	private sealed class HeldItemInstance
	{
		public readonly GameObject GameObject;
		public IUsableItem Usable;

		public HeldItemInstance(GameObject gameObject)
		{
			GameObject = gameObject;
		}
	}

	public static NetHotbarInventory Instance;

	[Header("Item Data")]
	[SerializeField] private ItemRegistry registry;

	[Header("Selection")]
	[SerializeField] private int selectedIndex;

	[Header("Held Item")]
	[SerializeField] private Transform holdPoint;
	[SerializeField] private NetHeldItemVisual heldItemVisual;
	[SerializeField] private ItemServiceLocator itemServices;
	[SerializeField] private CharControllerServiceLocator characterServices;

	private PlayerInventory inventory;
	private GameObject heldInstance;
	private IUsableItem heldUsable;
	private int heldItemId = -1;
	private readonly Dictionary<int, HeldItemInstance> heldItems = new();
	private GameObject heldItemsRoot;
	private bool heldItemsInitialized;

	private bool allowHeldItem = true;
	private bool isPaused;

	public int SlotCount => inventory != null ? inventory.SlotCount : 0;
	public int SelectedIndex => selectedIndex;
	public ItemRegistry Registry => registry;
	public ItemServiceLocator ItemServices => itemServices;
	public CharControllerServiceLocator CharacterServices => characterServices;
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
		ResolveServices();
	}

	private void ResolveServices()
	{
		if (itemServices == null)
			itemServices = GetComponent<ItemServiceLocator>();

		if (itemServices == null)
			itemServices = GetComponentInParent<ItemServiceLocator>();

		if (characterServices == null)
			characterServices = GetComponentInParent<CharControllerServiceLocator>();
	}

	private void Reset()
	{
		heldItemVisual = GetComponent<NetHeldItemVisual>();
		itemServices = GetComponent<ItemServiceLocator>();
		characterServices = GetComponentInParent<CharControllerServiceLocator>();
		ResolveServices();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		if (!IsOwner)
			return;

		Instance = this;
		InitializeHeldItems();

		PlayerInventory.InstanceChanged += HandleInventoryInstanceChanged;
		BindInventory(PlayerInventory.Instance);
	}

	public override void OnStopClient()
	{
		if (IsOwner)
			PlayerInventory.InstanceChanged -= HandleInventoryInstanceChanged;

		DestroyHeldItems();
		ReleaseInventoryRuntime();
		UnbindInventory();

		if (Instance == this)
			Instance = null;

		base.OnStopClient();
	}

	private void OnDestroy()
	{
		PlayerInventory.InstanceChanged -= HandleInventoryInstanceChanged;

		DestroyHeldItems();
		ReleaseInventoryRuntime();
		UnbindInventory();

		if (Instance == this)
			Instance = null;
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
		InitializeHeldItems();

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

	public void ReleaseHeldItemCache()
	{
		if (!IsOwner)
			return;

		allowHeldItem = false;
		DestroyHeldItems();
		ReleaseInventoryRuntime();
		heldItemVisual?.SetHeldItem(-1);
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

		InitializeHeldItems();

		if (!heldItems.TryGetValue(itemId, out HeldItemInstance item) ||
			item.GameObject == null)
		{
			return;
		}

		heldInstance = item.GameObject;

		heldUsable = item.Usable;
		heldUsable?.OnEquip();
	}

	private void InitializeHeldItems()
	{
		if (heldItemsInitialized)
			return;

		if (registry == null || holdPoint == null)
			return;

		heldItemsInitialized = true;
		heldItemsRoot = new GameObject("Held Items");
		heldItemsRoot.SetActive(false);
		heldItemsRoot.transform.SetParent(holdPoint, false);

		for (int itemId = 0; itemId < registry.Count; itemId++)
		{
			GameObject heldPrefab = registry.HeldPrefabOf(itemId);

			if (heldPrefab == null)
				continue;

			GameObject itemInstance =
				Instantiate(
					heldPrefab,
					heldItemsRoot.transform,
					false);

			itemInstance.transform.localPosition = Vector3.zero;
			itemInstance.transform.localRotation = Quaternion.identity;
			itemInstance.SetActive(true);

			heldItems.Add(
				itemId,
				new HeldItemInstance(itemInstance));
		}

		foreach (KeyValuePair<int, HeldItemInstance> pair in heldItems)
			InitializeHeldItem(pair.Key, pair.Value);

		// Items are activated together only after the inventory has supplied
		// context and applied their initial unequipped presentation state.
		heldItemsRoot.SetActive(true);
	}

	private void InitializeHeldItem(
		int itemId,
		HeldItemInstance item)
	{
		MonoBehaviour[] behaviours =
			item.GameObject.GetComponentsInChildren<MonoBehaviour>(true);

		for (int i = 0; i < behaviours.Length; i++)
		{
			if (item.Usable == null && behaviours[i] is IUsableItem usable)
				item.Usable = usable;
		}

		for (int i = 0; i < behaviours.Length; i++)
		{
			if (behaviours[i] is IHotbarItemContextReceiver receiver &&
				behaviours[i] is not IUsableItem)
			{
				receiver.InitializeHotbarItem(this, itemId);
			}
		}

		if (item.Usable is IHotbarItemContextReceiver usableReceiver)
			usableReceiver.InitializeHotbarItem(this, itemId);
	}

	private void ClearHeld()
	{
		if (heldUsable != null)
		{
			heldUsable.OnUnequip();
			heldUsable = null;
		}

		heldInstance = null;

		heldItemId = -1;
	}

	private void DestroyHeldItems()
	{
		ClearHeld();

		if (heldItemsRoot != null)
			Destroy(heldItemsRoot);

		heldItems.Clear();
		heldItemsRoot = null;
		heldItemsInitialized = false;
	}

	private void ReleaseInventoryRuntime()
	{
		MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

		for (int i = 0; i < behaviours.Length; i++)
		{
			if (behaviours[i] is IInventoryReleaseHandler handler)
				handler.OnInventoryReleased();
		}
	}

	public GameObject GetCurrentItemGameObject()
	{
		return heldInstance;
	}
}
