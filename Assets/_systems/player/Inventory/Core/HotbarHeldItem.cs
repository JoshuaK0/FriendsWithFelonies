using UnityEngine;

public abstract class HotbarHeldItem :
	MonoBehaviour,
	IUsableItem,
	IHotbarItemContextReceiver
{
	protected NetHotbarInventory Inventory { get; private set; }
	protected HotbarItemRuntimeStateStore RuntimeState { get; private set; }
	protected ItemServiceLocator ItemServices { get; private set; }
	protected CharControllerServiceLocator CharacterServices { get; private set; }
	protected int ItemId { get; private set; } = -1;
	protected bool IsInitialized { get; private set; }
	protected bool IsEquipped { get; private set; }

	protected bool IsCurrentPlayerItem =>
		Inventory != null &&
		NetHotbarInventory.Instance == Inventory;

	public void InitializeHotbarItem(
		NetHotbarInventory inventory,
		int itemId)
	{
		if (IsInitialized)
			return;

		Inventory = inventory;
		ItemServices = inventory != null ? inventory.ItemServices : null;
		CharacterServices = inventory != null ? inventory.CharacterServices : null;
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
		IsInitialized = true;

		// The item root remains active for its entire inventory lifetime.
		// Item behaviour owns which viewmodels, previews, UI, or effects are
		// hidden while the item starts unequipped.
		OnUnequipped();
	}

	public void OnEquip()
	{
		if (!IsInitialized || IsEquipped)
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
