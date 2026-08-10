using FishNet.Object;
using UnityEngine;

public class MapManager : NetworkBehaviour
{
	public static MapManager Instance { get; private set; }

	[SerializeField] private SpawnPoints copSpawnPoints;
	[SerializeField] private SpawnPoints robberSpawnPoints;
	[SerializeField] private SpawnPoints jailSpawnPoints;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
	}

	public Transform GetRandomSpawnPoint(TeamType teamType)
	{
		switch (teamType)
		{
			case TeamType.Cop:
				return copSpawnPoints.GetRandomSpawnPoint();

			case TeamType.Robber:
				return robberSpawnPoints.GetRandomSpawnPoint();

			default:
				return null;
		}
	}

	public Transform GetJailSpawnPoint()
	{
		return jailSpawnPoints.GetRandomSpawnPoint();
	}
}