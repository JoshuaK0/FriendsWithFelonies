using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public sealed class LootItemNetworked : NetworkBehaviour
{
	[Header("Loot")]
	[SerializeField] private int itemId;

	[Header("Throw Validation")]
	[SerializeField, Min(0f)] private float maxSpawnDistance = 4f;
	[SerializeField, Min(0f)] private float maxThrowSpeed = 20f;

	private NetHotbarInventory inventory;

	private void Awake()
	{
		inventory = GetComponent<NetHotbarInventory>();

		if (inventory == null)
			inventory = GetComponentInParent<NetHotbarInventory>();
	}

	public void RequestThrowLoot(
		Vector3 position,
		Quaternion rotation,
		Vector3 velocity)
	{
		if (!IsOwner)
			return;

		ThrowLootServerRpc(
			position,
			rotation,
			velocity);
	}

	[ServerRpc]
	private void ThrowLootServerRpc(
		Vector3 position,
		Quaternion rotation,
		Vector3 velocity,
		NetworkConnection connection = null)
	{
		if (connection == null ||
			inventory == null)
		{
			return;
		}

		ItemRegistry registry = inventory.Registry;

		if (registry == null ||
			!registry.IsValidItemId(itemId))
		{
			return;
		}

		// Prevent the client from spawning loot too far away.
		if (Vector3.Distance(transform.position, position) > maxSpawnDistance)
			return;

		// Prevent unreasonable client-supplied throw speeds.
		velocity = Vector3.ClampMagnitude(
			velocity,
			maxThrowSpeed);

		NetworkObject prefab =
			registry.WorldPrefabOf(itemId);

		if (prefab == null)
			return;

		NetworkObject spawned =
			Instantiate(
				prefab,
				position,
				rotation);

		InstanceFinder.ServerManager.Spawn(
			spawned.gameObject);

		Rigidbody rb =
			spawned.GetComponent<Rigidbody>();

		if (rb != null)
			rb.linearVelocity = velocity;
	}
}