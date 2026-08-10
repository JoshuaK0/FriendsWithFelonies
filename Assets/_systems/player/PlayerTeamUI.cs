using TMPro;
using UnityEngine;

public class PlayerTeamUI : MonoBehaviour
{
	[SerializeField]
	private TMP_Text teamText;

	private void OnEnable()
	{
		MyClient.OnStartClient += OnLocalClientChanged;
		PlayerTeams.OnTeamDataChanged += RefreshUI;

		RefreshUI();
	}

	private void OnDisable()
	{
		MyClient.OnStartClient -= OnLocalClientChanged;
		PlayerTeams.OnTeamDataChanged -= RefreshUI;
	}

	private void OnLocalClientChanged(MyClient client)
	{
		RefreshUI();
	}

	private void RefreshUI()
	{
		if (teamText == null)
			return;

		if (MyClient.Instance == null || PlayerTeams.Instance == null)
		{
			DisplayNoTeam();
			return;
		}

		int connectionId = MyClient.Instance.Owner.ClientId;

		int teamId = PlayerTeams.Instance.GetPlayerTeamId(connectionId);
		TeamType teamType =
			PlayerTeams.Instance.GetPlayerTeamType(connectionId);

		if (teamId == PlayerTeams.NoTeamId)
		{
			DisplayNoTeam();
			return;
		}

		teamText.text = $"Team: {teamType} ({teamId})";
	}

	private void DisplayNoTeam()
	{
		if (teamText != null)
			teamText.text = "Team: Spectator";
	}
}