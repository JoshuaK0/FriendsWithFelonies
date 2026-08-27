using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

/// <summary>
/// Revives every player currently in the Dead state.
/// This object must be a spawned FishNet NetworkObject.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class JailInteractable :
	NetworkBehaviour,
	IInteractable
{
	[Header("Interaction")]
	[SerializeField, Min(0f)]
	private float interactionDuration;

	[SerializeField]
	private bool useDirectRaycast;

	[Header("Server Validation")]
	[Tooltip(
		"Maximum distance from which the server permits interaction. " +
		"This should be equal to or slightly greater than the " +
		"PlayerInteractor interaction distance.")]
	[SerializeField, Min(0f)]
	private float serverInteractionDistance = 3.5f;

	public float InteractionDuration => interactionDuration;
	public bool UseDirectRaycast => useDirectRaycast;

	/// <summary>
	/// Called locally by PlayerInteractor.
	/// </summary>
	public void Interact(GameObject interactor)
	{
		if (interactor == null || !IsSpawned)
			return;

		ReviveAllDeadPlayersServerRpc();
	}

	/// <summary>
	/// Sends the interaction request to the server.
	/// The jail does not need to be owned by the interacting player.
	/// </summary>
	[ServerRpc(RequireOwnership = false)]
	private void ReviveAllDeadPlayersServerRpc(
		NetworkConnection sender = null)
	{
		if (!CanInteract(sender))
			return;

		ReviveAllDeadPlayersServer();
	}

	/// <summary>
	/// Ensures the player requesting the interaction is actually
	/// close enough to the jail on the server.
	/// </summary>
	[Server]
	private bool CanInteract(NetworkConnection sender)
	{
		if (sender == null)
			return false;

		if (!PlayerManager.TryGetPlayerManager(
				sender.ClientId,
				out PlayerManager playerManager))
		{
			return false;
		}

		GameObject playerController =
			playerManager.PlayerController;

		if (playerController == null)
			return false;

		float maximumDistanceSquared =
			serverInteractionDistance *
			serverInteractionDistance;

		float distanceSquared =
			(
				playerController.transform.position -
				transform.position
			).sqrMagnitude;

		if (distanceSquared > maximumDistanceSquared)
		{
			Debug.LogWarning(
				$"Player {sender.ClientId} attempted to use " +
				$"{nameof(JailInteractable)} from too far away.",
				this);

			return false;
		}

		return true;
	}

	/// <summary>
	/// Finds and revives every dead player.
	/// Can also be called directly by another server-side system.
	/// </summary>
	[Server]
	public void ReviveAllDeadPlayersServer()
	{
		PlayerManager[] playerManagers =
			FindObjectsByType<PlayerManager>(
				FindObjectsSortMode.None);

		int revivedPlayers = 0;

		foreach (PlayerManager playerManager in playerManagers)
		{
			if (playerManager == null)
				continue;

			if (playerManager.State != PlayerState.Dead)
				continue;

			playerManager.RevivePlayerServer();
			revivedPlayers++;
		}

		Debug.Log(
			$"{nameof(JailInteractable)} revived " +
			$"{revivedPlayers} dead player(s).",
			this);
	}

	public bool CanInteract(GameObject interactor, out string reason)
	{
		reason = string.Empty;
		return true;
	}
}
