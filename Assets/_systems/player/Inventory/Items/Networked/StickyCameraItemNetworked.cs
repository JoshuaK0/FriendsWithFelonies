using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public sealed class StickyCameraItemNetworked : NetworkBehaviour
{
	[SerializeField]
	private NetworkObject stickyCameraPrefab;

	public void RequestPlaceStickyCamera(
		Vector3 position,
		Quaternion rotation)
	{
		if (!IsOwner)
			return;

		PlaceStickyCameraServerRpc(
			position,
			rotation);
	}

	[ServerRpc]
	private void PlaceStickyCameraServerRpc(
		Vector3 position,
		Quaternion rotation,
		NetworkConnection sender = null)
	{
		if (stickyCameraPrefab == null)
		{
			Debug.LogError(
				$"{nameof(StickyCameraItemNetworked)} has no " +
				$"sticky camera prefab assigned.",
				this);

			return;
		}

		if (sender == null)
		{
			Debug.LogWarning(
				"Sticky camera placement was rejected because " +
				"the sending client could not be resolved.",
				this);

			return;
		}

		if (PlayerTeams.Instance == null)
		{
			Debug.LogWarning(
				"Sticky camera placement was rejected because " +
				"PlayerTeams is not available.",
				this);

			return;
		}

		int placingTeamId =
			PlayerTeams.Instance.GetPlayerTeamId(
				sender.ClientId);

		if (placingTeamId == PlayerTeams.NoTeamId)
		{
			Debug.LogWarning(
				$"Sticky camera placement was rejected because " +
				$"client {sender.ClientId} has no team.",
				this);

			return;
		}

		NetworkObject spawned =
			Instantiate(
				stickyCameraPrefab,
				position,
				rotation);

		// Give ownership to the client who actually placed it.
		Spawn(spawned, sender);

		if (!spawned.TryGetComponent(
				out StickyCameraProp cameraProp))
		{
			Debug.LogError(
				$"The sticky camera prefab does not contain a " +
				$"{nameof(StickyCameraProp)} component.",
				spawned);

			Despawn(spawned);
			return;
		}

		// Use the sending connection's team, not MyClient.Instance.
		cameraProp.InitializeServer(placingTeamId);
	}
}