using FishNet.Object;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Contains scoring and role-rotation rules. It does not control sequencing.
/// </summary>
public class MatchRules : NetworkBehaviour
{
	[Header("Round Result Points")]
	[SerializeField, Min(0)]
	[FormerlySerializedAs("copScorePerRound")]
	private int copPointsOnTimeout = 1;

	[SerializeField, Min(0)]
	private int copPointsOnAllRobbersCaptured = 3;

	[SerializeField, Min(0)]
	[FormerlySerializedAs("robberPointsOnLootStolen")]
	private int robberPointsPerLootCaptured = 3;

	/// <summary>
	/// Awards points according to the condition which ended the round.
	/// </summary>
	[Server]
	public void AwardRoundResult(
		RoundEndReason reason,
		int resultScoreMultiplier = 1)
	{
		resultScoreMultiplier = Mathf.Max(1, resultScoreMultiplier);
		switch (reason)
		{
			case RoundEndReason.TimeExpired:
				AwardTeamsOfType(
					TeamType.Cop,
					copPointsOnTimeout);
				break;

			case RoundEndReason.AllRobbersCaptured:
				AwardTeamsOfType(
					TeamType.Cop,
					copPointsOnAllRobbersCaptured);
				break;

			case RoundEndReason.LootStolen:
				AwardTeamsOfType(
					TeamType.Robber,
					robberPointsPerLootCaptured *
					resultScoreMultiplier);
				break;

			default:
				Debug.LogWarning(
					$"No scoring rule exists for round result {reason}.");
				break;
		}
	}

	/// <summary>
	/// Kept for compatibility with older callers. This awards timeout points.
	/// </summary>
	[Server]
	public void AwardCopTeams()
	{
		AwardTeamsOfType(TeamType.Cop, copPointsOnTimeout);
	}

	[Server]
	private void AwardTeamsOfType(
		TeamType teamType,
		int amount)
	{
		if (amount <= 0)
			return;

		if (TeamScores.Instance == null || PlayerTeams.Instance == null)
		{
			Debug.LogError(
				"MatchRules requires TeamScores and PlayerTeams.");
			return;
		}

		foreach (int teamId in TeamScores.Instance.GetTeamIds())
		{
			// PlayerTeams remains the source of truth for current team roles.
			if (PlayerTeams.Instance.GetTeamType(teamId) != teamType)
				continue;

			TeamScores.Instance.AddScore(teamId, amount);
		}
	}

	[Server]
	public bool RotateTeams(out string failureReason)
	{
		if (TeamDesignator.Instance == null)
		{
			failureReason = "TeamDesignator.Instance is null.";
			return false;
		}

		if (!TeamDesignator.Instance.RotateTeams())
		{
			failureReason = "Team roles could not be rotated.";
			return false;
		}

		failureReason = null;
		return true;
	}
}
