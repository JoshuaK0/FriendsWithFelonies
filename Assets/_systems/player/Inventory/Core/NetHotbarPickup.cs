using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class NetHotbarPickup : NetworkBehaviour
{
	[SerializeField] private NetHotbarInventory hotbar;
	[SerializeField] private NetHotbarDropper dropper;
	[SerializeField] private float maxPickupDistance = 3f;

	private void Reset()
	{
		hotbar = GetComponent<NetHotbarInventory>();
		dropper = GetComponent<NetHotbarDropper>();
	}

	/// <summary>
	/// Player-input pickup action. Blocked while paused/dead.
	/// </summary>
	public bool RequestPickup(NetWorldItem worldItem)
	{
		if (!IsOwner ||
			hotbar == null ||
			!hotbar.CanProcessPlayerInput ||
			worldItem == null)
		{
			return false;
		}

		ItemRegistry registry = hotbar.Registry;

		if (registry == null)
			return false;

		int itemId = worldItem.GetItemId(registry);

		if (!registry.IsValidItemId(itemId))
			return false;

		if (Vector3.Distance(
				transform.position,
				worldItem.transform.position) > maxPickupDistance)
		{
			return false;
		}

		if (!hotbar.WouldAcceptPickup(itemId))
			return false;

		if (worldItem.NetworkObject == null)
			return false;

		PickupRequestServerRpc(worldItem.NetworkObject);
		return true;
	}

	/// <summary>
	/// Gives the owning player a specific amount of an item.
	/// Intended for shops, rewards, etc.
	/// Intentionally NOT pause-blocked.
	/// </summary>
	public void RequestGiveItem(int itemId, int amount = 1)
	{
		if (!IsOwner || hotbar == null)
			return;

		if (amount <= 0)
			return;

		ItemRegistry registry = hotbar.Registry;

		if (registry == null || !registry.IsValidItemId(itemId))
			return;

		GiveItemServerRpc(itemId, amount);
	}

	[ServerRpc]
	private void PickupRequestServerRpc(
		NetworkObject itemObject,
		NetworkConnection connection = null)
	{
		if (hotbar == null ||
			itemObject == null ||
			connection == null)
		{
			return;
		}

		ItemRegistry registry = hotbar.Registry;

		if (registry == null)
			return;

		NetWorldItem worldItem =
			itemObject.GetComponent<NetWorldItem>();

		if (worldItem == null)
			return;

		if (Vector3.Distance(
				transform.position,
				worldItem.transform.position) >
			maxPickupDistance + 0.5f)
		{
			return;
		}

		int itemId = worldItem.GetItemId(registry);

		if (!registry.IsValidItemId(itemId))
			return;

		itemObject.Despawn();

		PickupConfirmedTargetRpc(connection, itemId);
	}

	[ServerRpc]
	private void GiveItemServerRpc(
		int itemId,
		int amount,
		NetworkConnection connection = null)
	{
		if (hotbar == null || connection == null)
			return;

		if (amount <= 0)
			return;

		ItemRegistry registry = hotbar.Registry;

		if (registry == null || !registry.IsValidItemId(itemId))
			return;

		GiveItemTargetRpc(connection, itemId, amount);
	}

	[TargetRpc]
	private void GiveItemTargetRpc(
		NetworkConnection connection,
		int itemId,
		int amount)
	{
		if (!IsOwner || hotbar == null)
			return;

		for (int i = 0; i < amount; i++)
			AddItemLocally(itemId);
	}

	[TargetRpc]
	private void PickupConfirmedTargetRpc(
		NetworkConnection connection,
		int itemId)
	{
		if (!IsOwner || hotbar == null)
			return;

		AddItemLocally(itemId);
	}

	private void AddItemLocally(int itemId)
	{
		if (!hotbar.TryAddPickup(
				itemId,
				out int swappedItemId,
				out int swappedCount))
		{
			return;
		}

		if (dropper == null ||
			swappedItemId < 0 ||
			swappedCount <= 0)
		{
			return;
		}

		for (int i = 0; i < swappedCount; i++)
			dropper.RequestDropOne(swappedItemId);
	}
}
