using System;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class PlayerInventory : MonoBehaviour
{
	[Serializable]
	private sealed class StartingInventorySlot
	{
		[SerializeField] private ItemDefinition item;
		[SerializeField, Min(1)] private int count = 1;

		public ItemDefinition Item => item;
		public int Count => Mathf.Max(1, count);
	}

	public static PlayerInventory Instance { get; private set; }
	public static event Action<PlayerInventory> InstanceChanged;

	[Header("Inventory")]
	[SerializeField, Min(1)] private int slotCount = 3;

	[Header("Starting Inventory")]
	[Tooltip("Registry used to convert starting Item Definitions into inventory item IDs.")]
	[SerializeField] private ItemRegistry registry;

	[Tooltip(
		"Starting hotbar contents. Element 0 fills slot 0, element 1 fills " +
		"slot 1, and so on. Leave an element's Item empty for an empty slot.")]
	[SerializeField] private StartingInventorySlot[] startingSlots;

	private HotbarSlot[] slots;

	public int SlotCount
	{
		get
		{
			EnsureInitialized();
			return slots.Length;
		}
	}

	public event Action<int> OnSlotChanged;
	public event Action OnInventoryChanged;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
		DontDestroyOnLoad(gameObject);
		EnsureInitialized();
		InstanceChanged?.Invoke(this);
	}

	private void OnDestroy()
	{
		if (Instance != this)
			return;

		Instance = null;
		InstanceChanged?.Invoke(null);
	}

	private void EnsureInitialized()
	{
		if (slots != null)
			return;

		slotCount = Mathf.Max(1, slotCount);
		slots = new HotbarSlot[slotCount];

		for (int i = 0; i < slots.Length; i++)
			slots[i] = new HotbarSlot();

		ApplyStartingInventory();
	}

	private void ApplyStartingInventory()
	{
		if (startingSlots == null || startingSlots.Length == 0)
			return;

		if (registry == null)
		{
			Debug.LogWarning(
				"PlayerInventory: Starting inventory could not be applied " +
				"because no Item Registry is assigned.",
				this);
			return;
		}

		int entriesToApply = Mathf.Min(slots.Length, startingSlots.Length);

		for (int i = 0; i < entriesToApply; i++)
		{
			StartingInventorySlot startingSlot = startingSlots[i];

			if (startingSlot == null || startingSlot.Item == null)
				continue;

			int itemId = registry.GetItemId(startingSlot.Item);

			if (!registry.IsValidItemId(itemId))
			{
				Debug.LogWarning(
					$"PlayerInventory: Starting item " +
					$"'{startingSlot.Item.name}' in slot {i} is not in " +
					"the assigned Item Registry.",
					this);
				continue;
			}

			int maxStack = registry.MaxStackOf(itemId);
			int count = Mathf.Clamp(startingSlot.Count, 1, maxStack);

			if (startingSlot.Count > maxStack)
			{
				Debug.LogWarning(
					$"PlayerInventory: Starting count for " +
					$"'{registry.NameOf(itemId)}' in slot {i} was " +
					$"clamped from {startingSlot.Count} to its max stack " +
					$"size of {maxStack}.",
					this);
			}

			slots[i].itemId = itemId;
			slots[i].count = count;
		}
	}

	public bool IsValidSlot(int index)
	{
		EnsureInitialized();
		return index >= 0 && index < slots.Length;
	}

	public HotbarSlot GetSlot(int index)
	{
		EnsureInitialized();
		return IsValidSlot(index) ? slots[index] : null;
	}

	public bool ContainsItem(int itemId)
	{
		return GetItemCount(itemId) > 0;
	}

	public int GetItemCount(int itemId)
	{
		EnsureInitialized();

		int total = 0;
		for (int i = 0; i < slots.Length; i++)
		{
			HotbarSlot slot = slots[i];

			if (slot != null &&
				!slot.IsEmpty &&
				slot.itemId == itemId &&
				slot.count > 0)
			{
				total += slot.count;
			}
		}

		return total;
	}

	private int FindStackableSlot(int itemId, ItemRegistry registry)
	{
		EnsureInitialized();

		if (registry == null || !registry.IsValidItemId(itemId))
			return -1;

		int maxStack = registry.MaxStackOf(itemId);

		for (int i = 0; i < slots.Length; i++)
		{
			HotbarSlot slot = slots[i];

			if (slot != null &&
				!slot.IsEmpty &&
				slot.itemId == itemId &&
				slot.count < maxStack)
			{
				return i;
			}
		}

		return -1;
	}

	private int FindEmptySlot()
	{
		EnsureInitialized();

		for (int i = 0; i < slots.Length; i++)
		{
			if (slots[i] != null && slots[i].IsEmpty)
				return i;
		}

		return -1;
	}

	public bool WouldAcceptPickup(int itemId, ItemRegistry registry, int replacementIndex)
	{
		EnsureInitialized();

		if (registry == null || !registry.IsValidItemId(itemId))
			return false;

		if (FindStackableSlot(itemId, registry) >= 0 || FindEmptySlot() >= 0)
			return true;

		if (!IsValidSlot(replacementIndex))
			return false;

		HotbarSlot replacement = slots[replacementIndex];

		if (replacement == null ||
			replacement.IsEmpty ||
			replacement.itemId == itemId)
		{
			return false;
		}

		return registry.IsDroppable(replacement.itemId);
	}

	public bool TryAddPickup(
		int itemId,
		ItemRegistry registry,
		int replacementIndex,
		out int swappedOutItemId,
		out int swappedOutCount)
	{
		swappedOutItemId = -1;
		swappedOutCount = 0;

		EnsureInitialized();

		if (registry == null || !registry.IsValidItemId(itemId))
			return false;

		int targetIndex = FindStackableSlot(itemId, registry);

		if (targetIndex >= 0)
		{
			slots[targetIndex].count++;
			NotifySlotChanged(targetIndex);
			return true;
		}

		targetIndex = FindEmptySlot();

		if (targetIndex >= 0)
		{
			slots[targetIndex].itemId = itemId;
			slots[targetIndex].count = 1;
			NotifySlotChanged(targetIndex);
			return true;
		}

		if (!IsValidSlot(replacementIndex))
			return false;

		HotbarSlot replacement = slots[replacementIndex];

		if (replacement == null ||
			replacement.IsEmpty ||
			replacement.itemId == itemId ||
			!registry.IsDroppable(replacement.itemId))
		{
			return false;
		}

		swappedOutItemId = replacement.itemId;
		swappedOutCount = replacement.count;

		replacement.itemId = itemId;
		replacement.count = 1;

		NotifySlotChanged(replacementIndex);
		return true;
	}

	public bool ConsumeOne(int itemId, ItemRegistry registry, int preferredIndex = -1)
	{
		EnsureInitialized();

		int index = -1;

		if (IsValidSlot(preferredIndex))
		{
			HotbarSlot preferred = slots[preferredIndex];

			if (preferred != null &&
				!preferred.IsEmpty &&
				preferred.itemId == itemId &&
				preferred.count > 0)
			{
				index = preferredIndex;
			}
		}

		if (index < 0)
		{
			for (int i = 0; i < slots.Length; i++)
			{
				HotbarSlot candidate = slots[i];

				if (candidate != null &&
					!candidate.IsEmpty &&
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

		if (slot.count == 0)
		{
			bool consumeOnEmpty =
				registry == null ||
				registry.ConsumeOnEmptyOf(itemId);

			int remainingCount = GetItemCount(itemId);
			bool isTrulyLastItem = remainingCount <= 0;

			if (consumeOnEmpty || !isTrulyLastItem)
				slot.Clear();
		}

		NotifySlotChanged(index);
		return true;
	}

	public bool RemoveEmptyItem(int itemId)
	{
		EnsureInitialized();

		for (int i = 0; i < slots.Length; i++)
		{
			HotbarSlot slot = slots[i];

			if (slot == null ||
				slot.IsEmpty ||
				slot.itemId != itemId ||
				slot.count > 0)
			{
				continue;
			}

			slot.Clear();
			NotifySlotChanged(i);
			return true;
		}

		return false;
	}

	public void ClearSlot(int index)
	{
		HotbarSlot slot = GetSlot(index);

		if (slot == null)
			return;

		slot.Clear();
		NotifySlotChanged(index);
	}

	public void ClearInventory()
	{
		EnsureInitialized();

		for (int i = 0; i < slots.Length; i++)
			slots[i].Clear();

		OnInventoryChanged?.Invoke();
	}

	public void NotifySlotChanged(int index)
	{
		if (!IsValidSlot(index))
			return;

		OnSlotChanged?.Invoke(index);
		OnInventoryChanged?.Invoke();
	}

	public void NotifyInventoryChanged()
	{
		OnInventoryChanged?.Invoke();
	}
}