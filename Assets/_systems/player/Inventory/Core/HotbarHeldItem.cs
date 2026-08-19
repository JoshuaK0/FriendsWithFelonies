using UnityEngine;

/// <summary>
/// Base class for owner-local held-item prefabs.
///
/// Only the held item belonging to the current active
/// NetHotbarInventory.Instance is allowed to process input.
/// This prevents old player bodies from using items during
/// death/respawn overlap.
/// </summary>
public abstract class HotbarHeldItem :
	MonoBehaviour,
	IUsableItem,
	IHotbarItemContextReceiver
{
	protected NetHotbarInventory Inventory
	{
		get;
		private set;
	}

	protected HotbarItemRuntimeStateStore RuntimeState
	{
		get;
		private set;
	}

	protected ItemServiceLocator ItemServices
	{
		get;
		private set;
	}

	protected int ItemId
	{
		get;
		private set;
	} = -1;

	protected bool IsEquipped
	{
		get;
		private set;
	}

	/// <summary>
	/// True only when this held item belongs to the
	/// currently active local player body.
	/// </summary>
	protected bool IsCurrentPlayerItem =>
		Inventory != null &&
		NetHotbarInventory.Instance == Inventory;

	public void InitializeHotbarItem(
		NetHotbarInventory inventory,
		int itemId)
	{
		Inventory = inventory;

		ItemServices =
			inventory != null
				? inventory.ItemServices
				: null;

		ItemId = itemId;

		if (inventory != null)
		{
			RuntimeState =
				inventory.GetComponent
				<HotbarItemRuntimeStateStore>();

			if (RuntimeState == null)
			{
				RuntimeState =
					inventory.gameObject.AddComponent
					<HotbarItemRuntimeStateStore>();
			}
		}

		OnContextInitialized();
	}

	public void OnEquip()
	{
		// Prevent accidental double-equipping of
		// the same held prefab instance.
		if (IsEquipped)
			return;

		IsEquipped = true;

		OnEquipped();
	}

	public void OnUnequip()
	{
		if (!IsEquipped)
			return;

		IsEquipped = false;

		OnUnequipped();
	}

	protected virtual void Update()
	{
		if (!IsEquipped)
			return;

		if (Inventory == null)
			return;

		// Extremely important for respawning:
		//
		// An old owned player body may still exist briefly
		// when the new owned body has already spawned.
		//
		// Only the newest/current local hotbar is allowed
		// to process held-item input.
		if (NetHotbarInventory.Instance != Inventory)
			return;

		// Covers pause and death/disabled-item states.
		if (!Inventory.CanUseHeldItem)
			return;

		OnEquippedUpdate();
	}

	protected virtual void OnDestroy()
	{
		// Guarantees item-specific cleanup even if the
		// player object is destroyed unexpectedly.
		OnUnequip();
	}

	protected virtual void OnContextInitialized()
	{
	}

	protected virtual void OnEquipped()
	{
	}

	protected virtual void OnEquippedUpdate()
	{
	}

	protected virtual void OnUnequipped()
	{
	}
}