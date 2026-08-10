using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public struct TeamData
{
	public int TeamId;
	public TeamType TeamType;
	public int Score;

	public TeamData(
		int teamId,
		TeamType teamType,
		int score = 0)
	{
		TeamId = teamId;
		TeamType = teamType;
		Score = score;
	}
}

public class TeamScores : NetworkBehaviour
{
	public static TeamScores Instance { get; private set; }

	private readonly SyncDictionary<int, TeamData> teams = new();

	public event Action OnTeamsChanged;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;

		/*
		 * Subscribe before network initialization so initial
		 * synchronization callbacks cannot be missed.
		 */
		teams.OnChange += HandleTeamsChanged;
	}

	public override void OnStartServer()
	{
		base.OnStartServer();

		PlayerTeams.OnTeamDataChanged +=
			HandlePlayerTeamDataChanged;

		/*
		 * PlayerTeams may already have created its default teams
		 * before this component starts.
		 */
		RefreshTeamTypesFromPlayerTeams();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		/*
		 * FishNet has applied initial SyncType data before
		 * OnStartClient, so force the UI to read the final state.
		 */
		OnTeamsChanged?.Invoke();
	}

	public override void OnStopServer()
	{
		PlayerTeams.OnTeamDataChanged -=
			HandlePlayerTeamDataChanged;

		base.OnStopServer();
	}

	private void OnDestroy()
	{
		teams.OnChange -= HandleTeamsChanged;

		PlayerTeams.OnTeamDataChanged -=
			HandlePlayerTeamDataChanged;

		if (Instance == this)
			Instance = null;
	}

	[Server]
	private void HandlePlayerTeamDataChanged()
	{
		RefreshTeamTypesFromPlayerTeams();
	}

	/// <summary>
	/// Creates fresh score entries for a new game.
	/// All existing scores are reset.
	/// </summary>
	[Server]
	public bool InitializeFromPlayerTeams()
	{
		if (PlayerTeams.Instance == null)
		{
			Debug.LogError(
				"[TeamScores] PlayerTeams.Instance is null.");

			return false;
		}

		List<int> teamIds =
			PlayerTeams.Instance.GetTeamIds();

		if (teamIds.Count == 0)
		{
			Debug.LogError(
				"[TeamScores] PlayerTeams contains no teams.");

			return false;
		}

		teams.Clear();

		foreach (int teamId in teamIds)
		{
			if (!PlayerTeams.Instance.TryGetTeam(
					teamId,
					out NetworkTeam networkTeam))
			{
				continue;
			}

			teams[teamId] = new TeamData(
				teamId,
				networkTeam.TeamType);
		}

		Debug.Log(
			$"[TeamScores] Initialized {teams.Count} teams.");

		return teams.Count > 0;
	}

	/// <summary>
	/// Synchronizes the tracked teams with PlayerTeams.
	/// Existing scores are preserved.
	/// </summary>
	[Server]
	public void RefreshTeamTypesFromPlayerTeams()
	{
		if (PlayerTeams.Instance == null)
			return;

		List<int> currentTeamIds =
			PlayerTeams.Instance.GetTeamIds();

		HashSet<int> currentTeamIdSet =
			new HashSet<int>(currentTeamIds);

		/*
		 * Remove score entries for teams that no longer exist.
		 * Copy the keys first because the dictionary cannot be
		 * modified while it is being enumerated.
		 */
		List<int> trackedTeamIds =
			new List<int>(teams.Keys);

		foreach (int trackedTeamId in trackedTeamIds)
		{
			if (!currentTeamIdSet.Contains(trackedTeamId))
				teams.Remove(trackedTeamId);
		}

		foreach (int teamId in currentTeamIds)
		{
			if (!PlayerTeams.Instance.TryGetTeam(
					teamId,
					out NetworkTeam networkTeam))
			{
				continue;
			}

			if (!teams.TryGetValue(
					teamId,
					out TeamData scoreTeam))
			{
				teams[teamId] = new TeamData(
					teamId,
					networkTeam.TeamType);

				continue;
			}

			if (scoreTeam.TeamType ==
				networkTeam.TeamType)
			{
				continue;
			}

			/*
			 * Preserve the score while changing the type.
			 */
			scoreTeam.TeamType =
				networkTeam.TeamType;

			teams[teamId] = scoreTeam;
		}
	}

	[Server]
	public void AddTeam(
		int teamId,
		TeamType teamType)
	{
		if (teams.ContainsKey(teamId))
			return;

		teams.Add(
			teamId,
			new TeamData(teamId, teamType));
	}

	[Server]
	public void RemoveTeam(int teamId)
	{
		teams.Remove(teamId);
	}

	[Server]
	public void SetScore(
		int teamId,
		int score)
	{
		if (!teams.TryGetValue(
				teamId,
				out TeamData team))
		{
			Debug.LogWarning(
				$"[TeamScores] Team {teamId} does not exist.");

			return;
		}

		team.Score = score;
		teams[teamId] = team;
	}

	[Server]
	public void AddScore(
		int teamId,
		int amount)
	{
		if (!teams.TryGetValue(
				teamId,
				out TeamData team))
		{
			Debug.LogWarning(
				$"[TeamScores] Team {teamId} does not exist.");

			return;
		}

		team.Score += amount;
		teams[teamId] = team;
	}

	[Server]
	public void ResetScores()
	{
		List<int> teamIds =
			new List<int>(teams.Keys);

		foreach (int teamId in teamIds)
		{
			TeamData team = teams[teamId];

			if (team.Score == 0)
				continue;

			team.Score = 0;
			teams[teamId] = team;
		}
	}

	public int GetScore(int teamId)
	{
		return teams.TryGetValue(
			teamId,
			out TeamData team)
			? team.Score
			: 0;
	}

	public bool TryGetTeam(
		int teamId,
		out TeamData team)
	{
		return teams.TryGetValue(teamId, out team);
	}

	public List<int> GetTeamIds()
	{
		return new List<int>(teams.Keys);
	}

	public TeamType GetTeamType(int teamId)
	{
		return teams.TryGetValue(
			teamId,
			out TeamData team)
			? team.TeamType
			: TeamType.Spectator;
	}

	public bool TeamExists(int teamId)
	{
		return teams.ContainsKey(teamId);
	}

	private void HandleTeamsChanged(
		SyncDictionaryOperation operation,
		int teamId,
		TeamData team,
		bool asServer)
	{
		OnTeamsChanged?.Invoke();
	}
}