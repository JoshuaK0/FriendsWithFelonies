using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;

public sealed class LootSpawner : NetworkBehaviour
{
	[Header("Loot")]
	[SerializeField]
	private NetworkObject lootPrefab;

	[SerializeField, Min(0)]
	private int lootToSpawn = 10;

	[Header("Spawn Points")]
	[SerializeField]
	private List<Transform> spawnPoints = new();

	private readonly List<NetworkObject> spawnedLoot = new();

	public override void OnStartServer()
	{
		base.OnStartServer();

		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.OnRoundSetupStarted +=
				HandleRoundStarted;
		}
		else
		{
			Debug.LogError(
				"LootSpawner could not find GameFlowManager.");
		}
	}

	public override void OnStopServer()
	{
		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.OnRoundSetupStarted -=
				HandleRoundStarted;
		}

		ClearLoot();

		base.OnStopServer();
	}

	[Server]
	private void HandleRoundStarted(int round)
	{
		SpawnRoundLoot();
	}

	[Server]
	public void SpawnRoundLoot()
	{
		ClearLoot();

		if (lootPrefab == null)
		{
			Debug.LogError(
				"LootSpawner has no loot prefab assigned.");
			return;
		}

		if (spawnPoints.Count == 0)
		{
			Debug.LogWarning(
				"LootSpawner has no spawn points.");
			return;
		}

		List<Transform> availablePoints =
			new(spawnPoints);

		for (int i = 0; i < lootToSpawn; i++)
		{
			// Once every spawn point has been used,
			// refill the list so points can be reused.
			if (availablePoints.Count == 0)
			{
				availablePoints.AddRange(spawnPoints);
			}

			int index = Random.Range(
				0,
				availablePoints.Count);

			Transform spawnPoint =
				availablePoints[index];

			availablePoints.RemoveAt(index);

			if (spawnPoint == null)
			{
				i--;
				continue;
			}

			SpawnLoot(spawnPoint);
		}
	}

	[Server]
	private void SpawnLoot(Transform spawnPoint)
	{
		NetworkObject loot = Instantiate(
			lootPrefab,
			spawnPoint.position,
			spawnPoint.rotation);

		InstanceFinder.ServerManager.Spawn(loot);

		spawnedLoot.Add(loot);
	}

	[Server]
	public void ClearLoot()
	{
		for (int i = spawnedLoot.Count - 1; i >= 0; i--)
		{
			NetworkObject loot = spawnedLoot[i];

			if (loot == null)
				continue;

			if (loot.IsSpawned)
				InstanceFinder.ServerManager.Despawn(loot);
			else
				Destroy(loot.gameObject);
		}

		spawnedLoot.Clear();
	}
}