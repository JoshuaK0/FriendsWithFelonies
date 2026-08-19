using System;
using UnityEngine;

/// <summary>
/// Persistent inventory belonging to the local player.
///
/// This object survives destruction/respawning of the physical
/// player prefab. Newly spawned NetHotbarInventory instances
/// automatically reconnect to this inventory.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class PlayerInventory : MonoBehaviour
{
	public static PlayerInventory Instance { get; private set; }

	/// <summary>
	/// Fired whenever the persistent inventory singleton changes.
	/// NetHotbarInventory uses this to automatically reconnect.
	/// </summary>
	public static event Action<PlayerInventory> InstanceChanged;

	[Header("Inventory")]
	[SerializeField, Min(1)]
	private int slotCount = 3;

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
		if (Instance != null &&
			Instance != this)
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

		slotCount =
			Mathf.Max(1, slotCount);

		slots =
			new HotbarSlot[slotCount];

		for (int i = 0;
			 i < slots.Length;
			 i++)
		{
			slots[i] =
				new HotbarSlot();
		}
	}

	public bool IsValidSlot(int index)
	{
		EnsureInitialized();

		return index >= 0 &&
			   index < slots.Length;
	}

	public HotbarSlot GetSlot(int index)
	{
		EnsureInitialized();

		if (!IsValidSlot(index))
			return null;

		return slots[index];
	}

	public bool ContainsItem(int itemId)
	{
		EnsureInitialized();

		for (int i = 0;
			 i < slots.Length;
			 i++)
		{
			HotbarSlot slot =
				slots[i];

			if (slot == null)
				continue;

			if (!slot.IsEmpty &&
				slot.itemId == itemId &&
				slot.count > 0)
			{
				return true;
			}
		}

		return false;
	}

	public int GetItemCount(int itemId)
	{
		EnsureInitialized();

		int total = 0;

		for (int i = 0;
			 i < slots.Length;
			 i++)
		{
			HotbarSlot slot =
				slots[i];

			if (slot == null)
				continue;

			if (!slot.IsEmpty &&
				slot.itemId == itemId &&
				slot.count > 0)
			{
				total +=
					slot.count;
			}
		}

		return total;
	}

	private int FindStackableSlot(
		int itemId,
		ItemRegistry registry)
	{
		EnsureInitialized();

		if (registry == null ||
			!registry.IsValidItemId(itemId))
		{
			return -1;
		}

		int maxStack =
			registry.MaxStackOf(itemId);

		for (int i = 0;
			 i < slots.Length;
			 i++)
		{
			HotbarSlot slot =
				slots[i];

			if (slot == null)
				continue;

			if (!slot.IsEmpty &&
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

		for (int i = 0;
			 i < slots.Length;
			 i++)
		{
			HotbarSlot slot =
				slots[i];

			if (slot != null &&
				slot.IsEmpty)
			{
				return i;
			}
		}

		return -1;
	}

	public bool WouldAcceptPickup(
		int itemId,
		ItemRegistry registry,
		int replacementIndex)
	{
		EnsureInitialized();

		if (registry == null ||
			!registry.IsValidItemId(itemId))
		{
			return false;
		}

		if (FindStackableSlot(
				itemId,
				registry) >= 0)
		{
			return true;
		}

		if (FindEmptySlot() >= 0)
			return true;

		if (!IsValidSlot(
				replacementIndex))
		{
			return false;
		}

		HotbarSlot replacement =
			slots[replacementIndex];

		if (replacement == null ||
			replacement.IsEmpty ||
			replacement.itemId == itemId)
		{
			return false;
		}

		return registry.IsDroppable(
			replacement.itemId);
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

		if (registry == null ||
			!registry.IsValidItemId(itemId))
		{
			return false;
		}

		// --------------------------------
		// Stack with an existing item.
		// --------------------------------

		int targetIndex =
			FindStackableSlot(
				itemId,
				registry);

		if (targetIndex >= 0)
		{
			HotbarSlot target =
				slots[targetIndex];

			target.count++;

			NotifySlotChanged(
				targetIndex);

			return true;
		}

		// --------------------------------
		// Use an empty slot.
		// --------------------------------

		targetIndex =
			FindEmptySlot();

		if (targetIndex >= 0)
		{
			HotbarSlot target =
				slots[targetIndex];

			target.itemId =
				itemId;

			target.count =
				1;

			NotifySlotChanged(
				targetIndex);

			return true;
		}

		// --------------------------------
		// Inventory full.
		// Replace selected slot.
		// --------------------------------

		if (!IsValidSlot(
				replacementIndex))
		{
			return false;
		}

		HotbarSlot replacement =
			slots[replacementIndex];

		if (replacement == null ||
			replacement.IsEmpty ||
			replacement.itemId == itemId)
		{
			return false;
		}

		if (!registry.IsDroppable(
				replacement.itemId))
		{
			return false;
		}

		swappedOutItemId =
			replacement.itemId;

		swappedOutCount =
			replacement.count;

		replacement.itemId =
			itemId;

		replacement.count =
			1;

		NotifySlotChanged(
			replacementIndex);

		return true;
	}

	public bool ConsumeOne(
	int itemId,
	ItemRegistry registry,
	int preferredIndex = -1)
	{
		EnsureInitialized();

		int index = -1;

		// Prefer the currently selected slot.
		if (IsValidSlot(preferredIndex))
		{
			HotbarSlot preferred =
				slots[preferredIndex];

			if (preferred != null &&
				!preferred.IsEmpty &&
				preferred.itemId == itemId &&
				preferred.count > 0)
			{
				index = preferredIndex;
			}
		}

		// Otherwise find another stack.
		if (index < 0)
		{
			for (int i = 0;
				 i < slots.Length;
				 i++)
			{
				HotbarSlot candidate =
					slots[i];

				if (candidate == null)
					continue;

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

		HotbarSlot slot =
			slots[index];

		// Consume one.
		slot.count =
			Mathf.Max(
				0,
				slot.count - 1);

		// Only need to consider clearing when
		// this particular stack reaches zero.
		if (slot.count == 0)
		{
			bool consumeOnEmpty =
				registry == null ||
				registry.ConsumeOnEmptyOf(
					itemId);

			// Count remaining usable copies across
			// the ENTIRE inventory after consuming.
			int remainingCount =
				GetItemCount(itemId);

			bool isLastItemInInventory =
				remainingCount <= 0;

			// Normal items are always removed at zero.
			//
			// "Do not consume" items are only preserved
			// when this is genuinely the final copy
			// anywhere in the inventory.
			bool shouldClear =
				consumeOnEmpty ||
				!isLastItemInInventory;

			if (shouldClear)
				slot.Clear();
		}

		NotifySlotChanged(index);

		return true;
	}

	public bool RemoveEmptyItem(
		int itemId)
	{
		EnsureInitialized();

		for (int i = 0;
			 i < slots.Length;
			 i++)
		{
			HotbarSlot slot =
				slots[i];

			if (slot == null)
				continue;

			if (slot.IsEmpty ||
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
		HotbarSlot slot =
			GetSlot(index);

		if (slot == null)
			return;

		slot.Clear();

		NotifySlotChanged(index);
	}

	/// <summary>
	/// Explicitly erase the persistent inventory.
	///
	/// Do NOT call this when the player merely dies unless
	/// death is supposed to remove all of their items.
	/// </summary>
	public void ClearInventory()
	{
		EnsureInitialized();

		for (int i = 0;
			 i < slots.Length;
			 i++)
		{
			slots[i].Clear();
		}

		OnInventoryChanged?.Invoke();
	}

	public void NotifySlotChanged(
		int index)
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