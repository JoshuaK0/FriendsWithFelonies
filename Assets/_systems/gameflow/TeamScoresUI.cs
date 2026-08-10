using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public sealed class TeamScoresUI : MonoBehaviour
{
	[Header("Team Scores")]
	[Tooltip("Optional. TeamScores.Instance is used when left empty.")]
	[SerializeField]
	private TeamScores teamScores;

	[Header("UI")]
	[SerializeField]
	private TMP_Text scoresText;

	[Header("Display")]
	[SerializeField]
	private string header = "SCORES";

	[SerializeField]
	private string emptyText = "No teams available";

	[SerializeField]
	private bool showTeamId = true;

	[SerializeField]
	private bool showSpectatorTeams;

	private TeamScores subscribedTeamScores;

	private readonly StringBuilder textBuilder = new();

	private void OnEnable()
	{
		TryBind();
	}

	private void OnDisable()
	{
		Unbind();
	}

	private void Update()
	{
		/*
		 * The networked TeamScores object may spawn after
		 * the scene UI has already been enabled.
		 */
		if (subscribedTeamScores == null)
			TryBind();
	}

	private void TryBind()
	{
		TeamScores scoresToBind = teamScores;

		if (scoresToBind == null)
			scoresToBind = TeamScores.Instance;

		if (scoresToBind == null)
		{
			SetUnavailableState();
			return;
		}

		if (subscribedTeamScores == scoresToBind)
			return;

		Unbind();

		subscribedTeamScores = scoresToBind;
		teamScores = scoresToBind;

		subscribedTeamScores.OnTeamsChanged += Refresh;

		Refresh();
	}

	private void Unbind()
	{
		if (subscribedTeamScores == null)
			return;

		subscribedTeamScores.OnTeamsChanged -= Refresh;
		subscribedTeamScores = null;
	}

	private void Refresh()
	{
		if (scoresText == null)
			return;

		if (subscribedTeamScores == null)
		{
			SetUnavailableState();
			return;
		}

		List<int> teamIds =
			subscribedTeamScores.GetTeamIds();

		teamIds.Sort();

		textBuilder.Clear();

		if (!string.IsNullOrWhiteSpace(header))
		{
			textBuilder.AppendLine(header);
			textBuilder.AppendLine();
		}

		int displayedTeams = 0;

		foreach (int teamId in teamIds)
		{
			if (!subscribedTeamScores.TryGetTeam(
					teamId,
					out TeamData team))
			{
				continue;
			}

			if (!showSpectatorTeams &&
				team.TeamType == TeamType.Spectator)
			{
				continue;
			}

			if (displayedTeams > 0)
				textBuilder.AppendLine();

			AppendTeamLine(team);
			displayedTeams++;
		}

		if (displayedTeams == 0)
		{
			textBuilder.Append(emptyText);
		}

		scoresText.text = textBuilder.ToString();
	}

	private void AppendTeamLine(TeamData team)
	{
		textBuilder.Append(team.TeamType);

		if (showTeamId)
		{
			textBuilder
				.Append(" ")
				.Append(team.TeamId);
		}

		textBuilder
			.Append(": ")
			.Append(team.Score);
	}

	private void SetUnavailableState()
	{
		if (scoresText == null)
			return;

		scoresText.text = string.IsNullOrWhiteSpace(header)
			? emptyText
			: $"{header}\n\n{emptyText}";
	}
}