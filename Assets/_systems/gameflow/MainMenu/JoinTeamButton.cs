using UnityEngine;
using FishNet.Connection;
using FishNet;
public class JoinTeamButton : MonoBehaviour
{
	[SerializeField] int teamIndex;

	public void JoinTeam()
	{
		PlayerTeams.Instance.AddPlayerToTeam(MyClient.Instance.OwnerId, teamIndex);
	}
}
