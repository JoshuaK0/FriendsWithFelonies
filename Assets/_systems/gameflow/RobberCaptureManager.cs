using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class RobberCaptureManager : NetworkBehaviour
{
	public static RobberCaptureManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
	}

	private void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}

	/// <summary>
	/// Called on the server whenever a player dies.
	/// </summary>
	[Server]
	public void ReportPlayerKilled(PlayerManager playerManager)
	{
		if (playerManager == null)
			return;

		if (GameFlowManager.Instance == null)
			return;

		// We only care about deaths during active gameplay.
		if (GameFlowManager.Instance.RoundPhase !=
			RoundFlowPhase.Active)
		{
			return;
		}

		if (PlayerTeams.Instance == null)
			return;

		// Cop deaths do not affect the robber capture condition.
		if (PlayerTeams.Instance.GetPlayerTeamType(
				playerManager.PlayerId) != TeamType.Robber)
		{
			return;
		}

		CheckAllRobbersCaptured();
	}

	[Server]
	private void CheckAllRobbersCaptured()
	{
		if (PlayerTeams.Instance == null)
			return;

		if (InstanceFinder.ServerManager == null)
			return;

		bool foundRobber = false;

		foreach (KeyValuePair<int, NetworkConnection> entry in
				 InstanceFinder.ServerManager.Clients)
		{
			NetworkConnection connection = entry.Value;

			if (connection == null ||
				!connection.IsActive ||
				!connection.IsAuthenticated)
			{
				continue;
			}

			int playerId = connection.ClientId;

			if (PlayerTeams.Instance.GetPlayerTeamType(playerId) !=
				TeamType.Robber)
			{
				continue;
			}

			foundRobber = true;

			if (!PlayerManager.TryGetPlayerManager(
					playerId,
					out PlayerManager playerManager))
			{
				// If the manager isn't available yet,
				// don't accidentally declare victory.
				return;
			}

			if (playerManager.State != PlayerState.Dead)
				return;
		}

		// Prevent a round with zero robbers from
		// immediately counting as a cop victory.
		if (!foundRobber)
			return;

		GameFlowManager.Instance.ReportAllRobbersCaptured();
	}
}