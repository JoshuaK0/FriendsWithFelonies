using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

/// <summary>
/// Interactable used to capture every loot object currently inside a
/// LootCaptureZone.
///
/// The client only requests the interaction. The server validates the sender,
/// the sender-owned player object, distance, team, zone contents, loot count,
/// round state, scoring, and despawning.
/// </summary>
public sealed class CaptureLootInteractable : NetworkBehaviour, IInteractable
{
	[SerializeField]
	private LootCaptureZone lootCaptureZone;

	[Header("Interaction")]
	[SerializeField]
	private Transform iconAnchor;

	[SerializeField, Min(0f)]
	private float serverInteractionDistance = 3.5f;

	[SerializeField]
	private bool requireRobberTeam = true;

	public Transform IconAnchor =>
		iconAnchor != null ? iconAnchor : transform;


	/// <summary>
	/// Called locally by PlayerInteractor.
	/// </summary>
	public void Interact(GameObject interactor)
	{
		if (interactor == null || !IsClientStarted)
			return;

		NetworkObject interactorObject =
			interactor.GetComponentInParent<NetworkObject>();

		if (interactorObject == null)
		{
			Debug.LogWarning(
				"The interacting player requires a parent NetworkObject.");
			return;
		}

		RequestCaptureServerRpc(interactorObject);
	}

	[ServerRpc(RequireOwnership = false)]
	private void RequestCaptureServerRpc(
		NetworkObject interactorObject,
		NetworkConnection sender = null)
	{
		if (!ValidateInteractorServer(interactorObject, sender))
			return;

		if (lootCaptureZone == null)
		{
			Debug.LogError(
				"CaptureLootInteractable requires a LootCaptureZone.");
			return;
		}

		if (!lootCaptureZone.TryCaptureAllLootServer(
				out int capturedLootCount))
		{
			return;
		}

		Debug.Log(
			$"Connection {sender.ClientId} captured " +
			$"{capturedLootCount} loot object(s).");
	}

	[Server]
	private bool ValidateInteractorServer(
	NetworkObject interactorObject,
	NetworkConnection sender)
	{
		if (sender == null)
		{
			Debug.LogWarning("Capture rejected: sender is null.");
			return false;
		}

		if (!sender.IsActive)
		{
			Debug.LogWarning("Capture rejected: sender is not active.");
			return false;
		}

		if (interactorObject == null)
		{
			Debug.LogWarning("Capture rejected: interactor NetworkObject is null.");
			return false;
		}

		if (!interactorObject.IsSpawned)
		{
			Debug.LogWarning("Capture rejected: interactor is not spawned.");
			return false;
		}

		if (interactorObject.Owner != sender)
		{
			Debug.LogWarning(
				$"Capture rejected: interactor owner " +
				$"{interactorObject.Owner?.ClientId} != sender {sender.ClientId}.");

			return false;
		}

		float maximumDistanceSquared =
			serverInteractionDistance * serverInteractionDistance;

		float distanceSquared =
			(interactorObject.transform.position - transform.position)
			.sqrMagnitude;

		if (distanceSquared > maximumDistanceSquared)
		{
			Debug.LogWarning(
				$"Capture rejected: too far away. " +
				$"Distance = {Mathf.Sqrt(distanceSquared):F2}, " +
				$"maximum = {serverInteractionDistance:F2}");

			return false;
		}

		if (!requireRobberTeam)
			return true;

		if (PlayerTeams.Instance == null)
		{
			Debug.LogError(
				"Capture rejected: PlayerTeams.Instance is null.");

			return false;
		}

		int teamId =
			PlayerTeams.Instance.GetPlayerTeamId(sender.ClientId);

		if (teamId == PlayerTeams.NoTeamId)
		{
			Debug.LogWarning(
				$"Capture rejected: connection {sender.ClientId} has no team.");

			return false;
		}

		TeamType teamType =
			PlayerTeams.Instance.GetTeamType(teamId);

		if (teamType != TeamType.Robber)
		{
			Debug.LogWarning(
				$"Capture rejected: player is {teamType}, not Robber.");

			return false;
		}

		Debug.Log(
			$"Capture validation passed for connection {sender.ClientId}.");

		return true;
	}

	public bool CanInteract(GameObject interactor, out string reason)
	{
		reason = string.Empty;
		return true;
	}
}
