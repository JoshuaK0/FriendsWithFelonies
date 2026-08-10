using FishNet;
using FishNet.Object;
using UnityEngine;

public sealed class LootDespawner : NetworkBehaviour
{
	public override void OnStartServer()
	{
		base.OnStartServer();

		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.OnRoundFinished +=
				HandleRoundFinished;
		}
	}

	public override void OnStopServer()
	{
		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.OnRoundFinished -=
				HandleRoundFinished;
		}

		base.OnStopServer();
	}

	[Server]
	private void HandleRoundFinished(int round)
	{
		DespawnLoot();
	}

	[Server]
	private void DespawnLoot()
	{
		if (!NetworkObject.IsSpawned)
			return;

		InstanceFinder.ServerManager.Despawn(NetworkObject);
	}
}