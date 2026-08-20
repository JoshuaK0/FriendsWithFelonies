using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class NetHotbarDropper : NetworkBehaviour
{
	[SerializeField] private NetHotbarInventory hotbar;
	[SerializeField] private Transform dropOrigin;
	[SerializeField] private float forward = 1.2f;
	[SerializeField] private float up = 0.2f;

	private void Reset()
	{
		hotbar = GetComponent<NetHotbarInventory>();
		dropOrigin = transform;
	}

	/// <summary>
	/// Player-input drop action. Blocked while paused/dead.
	/// </summary>
	public void DropOneSelected()
	{
		if (!IsOwner ||
			hotbar == null ||
			!hotbar.CanProcessPlayerInput)
		{
			return;
		}

		HotbarSlot selected = hotbar.GetSelectedSlot();

		if (selected == null || selected.IsEmpty)
			return;

		int itemId = selected.itemId;
		ItemRegistry registry = hotbar.Registry;

		if (registry == null || !registry.IsDroppable(itemId))
			return;

		if (!hotbar.ConsumeOneConfirmed(itemId))
			return;

		RequestDropOne(itemId);
	}

	/// <summary>
	/// Programmatic world-drop request.
	/// Intentionally NOT pause-blocked so game logic can still
	/// create drops while player input is paused.
	/// </summary>
	public void RequestDropOne(int itemId)
	{
		if (!IsOwner || hotbar == null)
			return;

		ItemRegistry registry = hotbar.Registry;

		if (registry == null || !registry.IsDroppable(itemId))
			return;

		Transform origin = dropOrigin != null ? dropOrigin : transform;

		Vector3 position =
			origin.position +
			origin.forward * forward +
			Vector3.up * up;

		DropRequestServerRpc(
			itemId,
			position,
			Quaternion.identity);
	}

	[ServerRpc]
	private void DropRequestServerRpc(
		int itemId,
		Vector3 position,
		Quaternion rotation,
		NetworkConnection connection = null)
	{
		if (hotbar == null || connection == null)
			return;

		ItemRegistry registry = hotbar.Registry;

		if (registry == null || !registry.IsValidItemId(itemId))
			return;

		if (!registry.IsDroppable(itemId))
			return;

		if (Vector3.Distance(transform.position, position) > 4f)
			return;

		NetworkObject prefab = registry.WorldPrefabOf(itemId);

		if (prefab == null)
			return;

		NetworkObject spawned =
			Instantiate(prefab, position, rotation);

		InstanceFinder.ServerManager.Spawn(spawned.gameObject);
	}
}
