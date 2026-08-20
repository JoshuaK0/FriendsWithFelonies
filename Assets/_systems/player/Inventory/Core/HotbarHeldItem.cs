using UnityEngine;

public abstract class HotbarHeldItem :
	MonoBehaviour,
	IUsableItem,
	IHotbarItemContextReceiver
{
	protected NetHotbarInventory Inventory { get; private set; }
	protected HotbarItemRuntimeStateStore RuntimeState { get; private set; }
	protected ItemServiceLocator ItemServices { get; private set; }
	protected int ItemId { get; private set; } = -1;
	protected bool IsEquipped { get; private set; }

	protected bool IsCurrentPlayerItem =>
		Inventory != null &&
		NetHotbarInventory.Instance == Inventory;

	public void InitializeHotbarItem(
		NetHotbarInventory inventory,
		int itemId)
	{
		Inventory = inventory;
		ItemServices = inventory != null ? inventory.ItemServices : null;
		ItemId = itemId;

		if (inventory != null)
		{
			RuntimeState =
				inventory.GetComponent<HotbarItemRuntimeStateStore>();

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
		if (!IsEquipped || Inventory == null)
			return;

		// Prevent an old dying body and a new respawned body
		// from both processing the same input.
		if (NetHotbarInventory.Instance != Inventory)
			return;

		// Pause/death blocks player input only.
		if (!Inventory.CanProcessPlayerInput)
			return;

		OnEquippedUpdate();
	}

	protected virtual void OnDestroy()
	{
		OnUnequip();
	}

	protected virtual void OnContextInitialized() { }
	protected virtual void OnEquipped() { }
	protected virtual void OnEquippedUpdate() { }
	protected virtual void OnUnequipped() { }
}
