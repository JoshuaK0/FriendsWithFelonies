using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[Serializable]
public struct NetworkTeam
{
	public int TeamId;
	public TeamType TeamType;

	public NetworkTeam(int teamId, TeamType teamType)
	{
		TeamId = teamId;
		TeamType = teamType;
	}
}

public class PlayerTeams : NetworkBehaviour
{
	public static PlayerTeams Instance { get; private set; }

	/// <summary>
	/// Invoked locally whenever team information changes.
	/// MyClient instances subscribe to this to refresh their cached team data.
	/// </summary>
	public static event Action OnTeamDataChanged;

	public const int NoTeamId = -1;

	[Header("Debugging")]
	[SerializeField]
	private bool enableDebugLogs = true;

	[Tooltip("Show logs when an RPC request is received.")]
	[SerializeField]
	private bool enableRequestLogs = false;

	// TeamId -> Team information.
	private readonly SyncDictionary<int, NetworkTeam> teams = new();

	// ConnectionId -> TeamId.
	private readonly SyncDictionary<int, int> playerTeamIds = new();

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			LogWarning(
				$"Duplicate PlayerTeams object detected on " +
				$"'{gameObject.name}'. Destroying duplicate."
			);

			Destroy(gameObject);
			return;
		}

		Instance = this;

		Log($"Instance assigned on '{gameObject.name}'.");
	}

	public override void OnStartNetwork()
	{
		base.OnStartNetwork();

		teams.OnChange += OnTeamsChanged;
		playerTeamIds.OnChange += OnPlayerTeamIdsChanged;

		Log(
			$"Network started. " +
			$"IsServerInitialized: {IsServerInitialized}, " +
			$"IsClientInitialized: {IsClientInitialized}."
		);

		OnTeamDataChanged?.Invoke();
	}

	public override void OnStartServer()
	{
		base.OnStartServer();

		Log("Creating default teams.");

		AddTeamServer(0, TeamType.Cop);
		AddTeamServer(1, TeamType.Robber);

		Log(
			$"Finished creating default teams. " +
			$"Total teams: {teams.Count}."
		);
	}

	public override void OnStopNetwork()
	{
		Log(
			$"Network stopping. Current teams: {teams.Count}, " +
			$"assigned players: {playerTeamIds.Count}."
		);

		teams.OnChange -= OnTeamsChanged;
		playerTeamIds.OnChange -= OnPlayerTeamIdsChanged;

		base.OnStopNetwork();
	}

	private void OnDestroy()
	{
		if (Instance != this)
			return;

		Log("PlayerTeams instance destroyed.");

		Instance = null;

		OnTeamDataChanged?.Invoke();
	}

	private void OnTeamsChanged(
		SyncDictionaryOperation operation,
		int teamId,
		NetworkTeam team,
		bool asServer)
	{
		OnTeamDataChanged?.Invoke();
	}

	private void OnPlayerTeamIdsChanged(
		SyncDictionaryOperation operation,
		int connectionId,
		int teamId,
		bool asServer)
	{
		OnTeamDataChanged?.Invoke();
	}

	#region Team Management

	/// <summary>
	/// Allows a client to request that the server creates a team.
	/// </summary>
	[ServerRpc(RequireOwnership = false)]
	public void AddTeam(int teamId, TeamType teamType)
	{
		LogRequest(
			$"AddTeam requested. " +
			$"TeamId: {teamId}, TeamType: {teamType}."
		);

		AddTeamServer(teamId, teamType);
	}

	/// <summary>
	/// Performs the actual team creation on the server.
	/// </summary>
	private void AddTeamServer(int teamId, TeamType teamType)
	{
		if (teams.ContainsKey(teamId))
		{
			LogWarning(
				$"Could not add team {teamId}. " +
				$"A team with that ID already exists."
			);

			return;
		}

		NetworkTeam team = new(teamId, teamType);

		teams.Add(teamId, team);

		Log(
			$"Team added successfully. " +
			$"TeamId: {teamId}, " +
			$"TeamType: {teamType}, " +
			$"Total teams: {teams.Count}."
		);
	}

	[ServerRpc(RequireOwnership = false)]
	public void SetTeamType(int teamId, TeamType teamType)
	{
		LogRequest(
			$"SetTeamType requested. " +
			$"TeamId: {teamId}, NewType: {teamType}."
		);

		SetTeamTypeServer(teamId, teamType);
	}

	private void SetTeamTypeServer(
		int teamId,
		TeamType teamType)
	{
		if (!teams.TryGetValue(teamId, out NetworkTeam team))
		{
			LogWarning(
				$"Could not change team type. " +
				$"Team {teamId} does not exist."
			);

			return;
		}

		TeamType previousTeamType = team.TeamType;

		if (previousTeamType == teamType)
		{
			Log(
				$"Team {teamId} is already type {teamType}. " +
				$"No change was made."
			);

			return;
		}

		team.TeamType = teamType;

		// Reassign the struct so FishNet synchronizes the change.
		teams[teamId] = team;

		Log(
			$"Team type changed. " +
			$"TeamId: {teamId}, " +
			$"PreviousType: {previousTeamType}, " +
			$"NewType: {teamType}."
		);
	}

	[ServerRpc(RequireOwnership = false)]
	public void RemoveTeam(int teamId)
	{
		LogRequest(
			$"RemoveTeam requested. TeamId: {teamId}."
		);

		RemoveTeamServer(teamId);
	}

	private void RemoveTeamServer(int teamId)
	{
		if (!teams.ContainsKey(teamId))
		{
			LogWarning(
				$"Could not remove team {teamId}. " +
				$"The team does not exist."
			);

			return;
		}

		List<int> playersToRemove = new();

		foreach (KeyValuePair<int, int> playerTeam in playerTeamIds)
		{
			if (playerTeam.Value == teamId)
				playersToRemove.Add(playerTeam.Key);
		}

		Log(
			$"Removing team {teamId}. " +
			$"{playersToRemove.Count} player assignment(s) " +
			$"will be removed."
		);

		foreach (int connectionId in playersToRemove)
		{
			playerTeamIds.Remove(connectionId);

			Log(
				$"Removed connection {connectionId} " +
				$"from deleted team {teamId}."
			);
		}

		teams.Remove(teamId);

		Log(
			$"Team {teamId} removed successfully. " +
			$"Remaining teams: {teams.Count}."
		);
	}

	public bool TryGetTeam(
		int teamId,
		out NetworkTeam team)
	{
		return teams.TryGetValue(teamId, out team);
	}

	public TeamType GetTeamType(int teamId)
	{
		if (teams.TryGetValue(teamId, out NetworkTeam team))
			return team.TeamType;

		return TeamType.Spectator;
	}

	public bool TeamExists(int teamId)
	{
		return teams.ContainsKey(teamId);
	}

	#endregion

	#region Player Membership

	[ServerRpc(RequireOwnership = false)]
	public void AddPlayerToTeam(
		int connectionId,
		int teamId)
	{
		LogRequest(
			$"AddPlayerToTeam requested. " +
			$"ConnectionId: {connectionId}, TeamId: {teamId}."
		);

		AddPlayerToTeamServer(connectionId, teamId);
	}

	private void AddPlayerToTeamServer(
		int connectionId,
		int teamId)
	{
		if (!teams.ContainsKey(teamId))
		{
			LogWarning(
				$"Team {teamId} does not exist. " +
				$"Creating it as a Spectator team."
			);

			AddTeamServer(teamId, TeamType.Spectator);
		}

		if (playerTeamIds.TryGetValue(
				connectionId,
				out int previousTeamId))
		{
			if (previousTeamId == teamId)
			{
				Log(
					$"Connection {connectionId} is already " +
					$"assigned to team {teamId}. " +
					$"No change was made."
				);

				return;
			}

			playerTeamIds[connectionId] = teamId;

			Log(
				$"Connection {connectionId} moved from " +
				$"team {previousTeamId} to team {teamId}."
			);

			return;
		}

		playerTeamIds.Add(connectionId, teamId);

		Log(
			$"Connection {connectionId} added to team {teamId}. " +
			$"Team player count: {GetPlayerCount(teamId)}."
		);
	}

	[ServerRpc(RequireOwnership = false)]
	public void MovePlayerToTeam(
		int connectionId,
		int newTeamId)
	{
		int currentTeamId = GetPlayerTeamId(connectionId);

		LogRequest(
			$"MovePlayerToTeam requested. " +
			$"ConnectionId: {connectionId}, " +
			$"CurrentTeamId: {currentTeamId}, " +
			$"NewTeamId: {newTeamId}."
		);

		AddPlayerToTeamServer(connectionId, newTeamId);
	}

	[ServerRpc(RequireOwnership = false)]
	public void RemovePlayer(int connectionId)
	{
		LogRequest(
			$"RemovePlayer requested. " +
			$"ConnectionId: {connectionId}."
		);

		RemovePlayerServer(connectionId);
	}

	private void RemovePlayerServer(int connectionId)
	{
		if (!playerTeamIds.TryGetValue(
				connectionId,
				out int previousTeamId))
		{
			LogWarning(
				$"Could not remove connection {connectionId}. " +
				$"The player has no team assignment."
			);

			return;
		}

		playerTeamIds.Remove(connectionId);

		Log(
			$"Connection {connectionId} removed from " +
			$"team {previousTeamId}."
		);
	}

	public int GetPlayerTeamId(int connectionId)
	{
		if (playerTeamIds.TryGetValue(
				connectionId,
				out int teamId))
		{
			return teamId;
		}

		return NoTeamId;
	}

	public TeamType GetPlayerTeamType(int connectionId)
	{
		if (!playerTeamIds.TryGetValue(
				connectionId,
				out int teamId))
		{
			return TeamType.Spectator;
		}

		return GetTeamType(teamId);
	}

	public bool TryGetPlayerTeam(
		int connectionId,
		out NetworkTeam team)
	{
		team = default;

		if (!playerTeamIds.TryGetValue(
				connectionId,
				out int teamId))
		{
			return false;
		}

		return teams.TryGetValue(teamId, out team);
	}

	public bool IsPlayerInTeam(
		int connectionId,
		int teamId)
	{
		return playerTeamIds.TryGetValue(
			connectionId,
			out int assignedTeamId
		) && assignedTeamId == teamId;
	}

	public List<int> GetPlayersInTeam(int teamId)
	{
		List<int> connectionIds = new();

		foreach (KeyValuePair<int, int> playerTeam in playerTeamIds)
		{
			if (playerTeam.Value == teamId)
				connectionIds.Add(playerTeam.Key);
		}

		return connectionIds;
	}

	public int GetPlayerCount(int teamId)
	{
		int count = 0;

		foreach (KeyValuePair<int, int> playerTeam in playerTeamIds)
		{
			if (playerTeam.Value == teamId)
				count++;
		}

		return count;
	}

	public int GetTotalTeamCount()
	{
		return teams.Count;
	}

	public int GetFirstTeamIdOfType(TeamType teamType)
	{
		foreach (
			KeyValuePair<int, NetworkTeam> teamEntry in teams)
		{
			if (teamEntry.Value.TeamType == teamType)
				return teamEntry.Key;
		}

		LogWarning(
			$"No team with type {teamType} could be found."
		);

		return NoTeamId;
	}

	#endregion

	#region Debugging

	private void Log(string message)
	{
		if (!enableDebugLogs)
			return;

		Debug.Log(
			$"[PlayerTeams] {message}",
			this
		);
	}

	private void LogRequest(string message)
	{
		if (!enableDebugLogs || !enableRequestLogs)
			return;

		Debug.Log(
			$"[PlayerTeams Request] {message}",
			this
		);
	}

	private void LogWarning(string message)
	{
		if (!enableDebugLogs)
			return;

		Debug.LogWarning(
			$"[PlayerTeams] {message}",
			this
		);
	}

	public List<int> GetTeamIds()
	{
		return new List<int>(teams.Keys);
	}

	#endregion
}