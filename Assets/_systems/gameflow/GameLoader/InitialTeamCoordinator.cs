using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

/// <summary>
/// Owns the initial team assignment acknowledgement handshake.
/// GameFlowManager only waits for this component to finish.
/// </summary>
public class InitialTeamCoordinator : NetworkBehaviour
{
	[SerializeField, Min(0f)]
	[Tooltip("Set to 0 to wait indefinitely.")]
	private float confirmationTimeout = 15f;

	public bool Succeeded { get; private set; }
	public string FailureReason { get; private set; }

	private readonly HashSet<int> pendingConfirmations = new();

	private Coroutine clientConfirmationCoroutine;
	private int activeConfirmationId;
	private int clientConfirmationId;
	private bool cancelRequested;

	public override void OnStartServer()
	{
		base.OnStartServer();

		if (InstanceFinder.ServerManager != null)
		{
			InstanceFinder.ServerManager.OnRemoteConnectionState +=
				HandleRemoteConnectionState;
		}
	}

	public override void OnStopServer()
	{
		if (InstanceFinder.ServerManager != null)
		{
			InstanceFinder.ServerManager.OnRemoteConnectionState -=
				HandleRemoteConnectionState;
		}

		Cancel();
		base.OnStopServer();
	}

	public override void OnStopNetwork()
	{
		StopClientConfirmationCoroutine();
		base.OnStopNetwork();
	}

	[Server]
	public IEnumerator InitializeTeams()
	{
		Succeeded = false;
		FailureReason = null;
		cancelRequested = false;
		pendingConfirmations.Clear();

		if (TeamDesignator.Instance == null)
		{
			Fail("TeamDesignator.Instance is null.");
			yield break;
		}

		if (PlayerTeams.Instance == null)
		{
			Fail("PlayerTeams.Instance is null.");
			yield break;
		}

		if (InstanceFinder.ServerManager == null)
		{
			Fail("FishNet ServerManager is unavailable.");
			yield break;
		}

		if (!TeamDesignator.Instance.AssignInitialTeams())
		{
			Fail("The initial team layout could not be created.");
			yield break;
		}

		if (TeamScores.Instance == null)
		{
			Fail("TeamScores.Instance is null.");
			yield break;
		}

		if (!TeamScores.Instance.InitializeFromPlayerTeams())
		{
			Fail("TeamScores could not be initialized.");
			yield break;
		}

		activeConfirmationId++;

		if (activeConfirmationId <= 0)
			activeConfirmationId = 1;

		if (!RequestConfirmations())
			yield break;

		float startedAt = Time.realtimeSinceStartup;

		while (pendingConfirmations.Count > 0 && !cancelRequested)
		{
			bool timedOut = confirmationTimeout > 0f &&
				Time.realtimeSinceStartup - startedAt >=
				confirmationTimeout;

			if (timedOut)
			{
				Fail(
					"Timed out waiting for team confirmations. " +
					"Missing connection IDs: " +
					string.Join(", ", pendingConfirmations) + ".");

				yield break;
			}

			yield return null;
		}

		if (cancelRequested)
		{
			Fail("Initial team synchronisation was cancelled.");
			yield break;
		}

		Succeeded = true;
	}

	[Server]
	public void Cancel()
	{
		cancelRequested = true;
		pendingConfirmations.Clear();
	}

	[Server]
	private bool RequestConfirmations()
	{
		List<NetworkConnection> clients = GetReadyClientConnections();

		if (clients.Count == 0)
		{
			Fail(
				"There are no authenticated clients with loaded " +
				"start scenes.");
			return false;
		}

		foreach (NetworkConnection connection in clients)
		{
			int expectedTeamId =
				PlayerTeams.Instance.GetPlayerTeamId(
					connection.ClientId);

			if (expectedTeamId == PlayerTeams.NoTeamId)
			{
				Fail(
					$"Connection {connection.ClientId} has not been " +
					"assigned to a team.");
				return false;
			}

			if (!PlayerTeams.Instance.TeamExists(expectedTeamId))
			{
				Fail(
					$"Connection {connection.ClientId} references " +
					$"missing team {expectedTeamId}.");
				return false;
			}
		}

		foreach (NetworkConnection connection in clients)
		{
			int expectedTeamId =
				PlayerTeams.Instance.GetPlayerTeamId(
					connection.ClientId);

			pendingConfirmations.Add(connection.ClientId);

			RequestConfirmationTargetRpc(
				connection,
				activeConfirmationId,
				expectedTeamId);
		}

		return true;
	}

	[TargetRpc]
	private void RequestConfirmationTargetRpc(
		NetworkConnection target,
		int confirmationId,
		int expectedTeamId)
	{
		StopClientConfirmationCoroutine();

		clientConfirmationId = confirmationId;
		clientConfirmationCoroutine = StartCoroutine(
			WaitForLocalAssignmentCoroutine(
				confirmationId,
				expectedTeamId));
	}

	private IEnumerator WaitForLocalAssignmentCoroutine(
		int confirmationId,
		int expectedTeamId)
	{
		while (IsClientInitialized &&
		       clientConfirmationId == confirmationId)
		{
			NetworkConnection localConnection =
				InstanceFinder.ClientManager?.Connection;

			bool hasExpectedTeam =
				localConnection != null &&
				localConnection.IsValid &&
				PlayerTeams.Instance != null &&
				PlayerTeams.Instance.GetPlayerTeamId(
					localConnection.ClientId) == expectedTeamId &&
				PlayerTeams.Instance.TeamExists(expectedTeamId);

			if (hasExpectedTeam)
			{
				// Allow other team-data subscribers to refresh first.
				yield return null;

				if (IsClientInitialized &&
				    clientConfirmationId == confirmationId)
				{
					ConfirmAssignmentServerRpc(
						confirmationId,
						expectedTeamId);
				}

				clientConfirmationCoroutine = null;
				yield break;
			}

			yield return null;
		}

		clientConfirmationCoroutine = null;
	}

	[ServerRpc(RequireOwnership = false)]
	private void ConfirmAssignmentServerRpc(
		int confirmationId,
		int observedTeamId,
		NetworkConnection sender = null)
	{
		if (sender == null || !sender.IsActive)
			return;

		if (confirmationId != activeConfirmationId)
			return;

		if (!pendingConfirmations.Contains(sender.ClientId))
			return;

		if (PlayerTeams.Instance == null)
			return;

		int serverTeamId =
			PlayerTeams.Instance.GetPlayerTeamId(sender.ClientId);

		if (serverTeamId == PlayerTeams.NoTeamId ||
		    observedTeamId != serverTeamId)
		{
			return;
		}

		pendingConfirmations.Remove(sender.ClientId);
	}

	private void StopClientConfirmationCoroutine()
	{
		if (clientConfirmationCoroutine == null)
			return;

		StopCoroutine(clientConfirmationCoroutine);
		clientConfirmationCoroutine = null;
	}

	private void HandleRemoteConnectionState(
		NetworkConnection connection,
		RemoteConnectionStateArgs args)
	{
		if (args.ConnectionState == RemoteConnectionState.Stopped)
			pendingConfirmations.Remove(args.ConnectionId);
	}

	[Server]
	private List<NetworkConnection> GetReadyClientConnections()
	{
		List<NetworkConnection> result = new();

		if (InstanceFinder.ServerManager == null)
			return result;

		foreach (KeyValuePair<int, NetworkConnection> entry in
		         InstanceFinder.ServerManager.Clients)
		{
			NetworkConnection connection = entry.Value;

			if (connection == null ||
			    !connection.IsActive ||
			    !connection.IsAuthenticated ||
			    !connection.LoadedStartScenes())
			{
				continue;
			}

			result.Add(connection);
		}

		result.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));
		return result;
	}

	private void Fail(string reason)
	{
		Succeeded = false;
		FailureReason = reason;
		pendingConfirmations.Clear();
	}
}
