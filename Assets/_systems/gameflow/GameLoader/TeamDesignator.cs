using UnityEngine;

/// <summary>
/// Changes team roles. PlayerTeams remains the source of truth.
/// </summary>
public class TeamDesignator : MonoBehaviour
{
	public static TeamDesignator Instance { get; private set; }

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

	public bool AssignInitialTeams()
	{
		if (!TryGetTeamCount(out int teamCount))
			return false;

		SetAllTeamsToSpectator(teamCount);
		PlayerTeams.Instance.SetTeamType(0, TeamType.Cop);
		PlayerTeams.Instance.SetTeamType(1, TeamType.Robber);
		return true;
	}

	public bool RotateTeams()
	{
		if (!TryGetTeamCount(out int teamCount))
			return false;

		int copTeamId =
			PlayerTeams.Instance.GetFirstTeamIdOfType(TeamType.Cop);

		int robberTeamId =
			PlayerTeams.Instance.GetFirstTeamIdOfType(TeamType.Robber);

		if (copTeamId < 0 || robberTeamId < 0)
			return AssignInitialTeams();

		int nextCopTeamId = (copTeamId + 1) % teamCount;
		int nextRobberTeamId = (robberTeamId + 1) % teamCount;

		SetAllTeamsToSpectator(teamCount);
		PlayerTeams.Instance.SetTeamType(nextCopTeamId, TeamType.Cop);
		PlayerTeams.Instance.SetTeamType(
			nextRobberTeamId,
			TeamType.Robber);

		return true;
	}

	private bool TryGetTeamCount(out int teamCount)
	{
		teamCount = 0;

		if (PlayerTeams.Instance == null)
		{
			Debug.LogError("PlayerTeams.Instance is null.");
			return false;
		}

		teamCount = PlayerTeams.Instance.GetTotalTeamCount();

		if (teamCount < 2)
		{
			Debug.LogError(
				"At least two teams are required for Cop and Robber roles.");
			return false;
		}

		return true;
	}

	private static void SetAllTeamsToSpectator(int teamCount)
	{
		for (int teamId = 0; teamId < teamCount; teamId++)
		{
			PlayerTeams.Instance.SetTeamType(
				teamId,
				TeamType.Spectator);
		}
	}
}
