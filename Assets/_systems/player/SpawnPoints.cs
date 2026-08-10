using System.Collections.Generic;
using UnityEngine;

public class SpawnPoints : MonoBehaviour
{
	[SerializeField] private List<Transform> spawnPoints;

	public Transform GetSpawnPoint(int index)
	{
		if (spawnPoints == null || spawnPoints.Count == 0)
			return null;
		return spawnPoints[index % spawnPoints.Count];
	}

	public Transform GetRandomSpawnPoint()
	{
		if (spawnPoints == null || spawnPoints.Count == 0)
			return null;
		int randomIndex = Random.Range(0, spawnPoints.Count);
		return spawnPoints[randomIndex];
	}

	private void OnDrawGizmosSelected()
	{
		if (spawnPoints == null)
			return;

		Gizmos.color = Color.yellow;

		foreach (Transform spawnPoint in spawnPoints)
		{
			if (spawnPoint != null)
				Gizmos.DrawSphere(
					spawnPoint.position,
					0.25f);
		}
	}
}