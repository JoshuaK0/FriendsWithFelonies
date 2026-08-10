using FishNet.Object;
using UnityEngine;

public class CharacterOutline : NetworkBehaviour
{
	[Header("Outline")]
	[SerializeField]
	private GameObject outlineObject;

	[SerializeField]
	private string defaultLayer;

	[SerializeField]
	private string allyOutlineLayer;

	[SerializeField]
	private string enemyOutlineLayer;

	[SerializeField]
	private PlayerCharacter playerCharacter;

	private PlayerCharacter localPlayer;

	public override void OnStartClient()
	{
		base.OnStartClient();

		if (outlineObject == null)
			outlineObject = gameObject;

		// Never change the owner's layer.
		if (IsOwner)
			return;

		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.OnRoundPhaseChanged +=
				HandleRoundPhaseChanged;
		}

		PlayerTeams.OnTeamDataChanged +=
			HandleTeamDataChanged;

		RefreshOutline();
	}

	public override void OnStopClient()
	{
		if (!IsOwner)
		{
			if (GameFlowManager.Instance != null)
			{
				GameFlowManager.Instance.OnRoundPhaseChanged -=
					HandleRoundPhaseChanged;
			}

			PlayerTeams.OnTeamDataChanged -=
				HandleTeamDataChanged;
		}

		base.OnStopClient();
	}

	private void HandleRoundPhaseChanged(
		RoundFlowPhase phase)
	{
		RefreshOutline();
	}

	private void HandleTeamDataChanged()
	{
		// Local player may not have existed when this
		// CharacterOutline first started.
		localPlayer = GetLocalPlayer();

		RefreshOutline();
	}

	private void RefreshOutline()
	{
		if (IsOwner)
			return;

		if (GameFlowManager.Instance == null)
			return;

		if (PlayerTeams.Instance == null)
			return;

		if (playerCharacter == null)
			return;

		if (localPlayer == null)
			localPlayer = GetLocalPlayer();

		if (localPlayer == null)
			return;

		int localTeamId =
			localPlayer.TeamId;

		int playerTeamId =
			playerCharacter.TeamId;

		// Same team is always visible as an ally.
		if (localTeamId == playerTeamId)
		{
			SetLayer(allyOutlineLayer);
			return;
		}

		// Robbers can see enemies during setup.
		if (GameFlowManager.Instance.RoundPhase ==
				RoundFlowPhase.Setup &&
			PlayerTeams.Instance.GetTeamType(localTeamId) ==
				TeamType.Robber)
		{
			SetLayer(enemyOutlineLayer);
			return;
		}

		// Enemy outline disappears when setup finishes.
		SetLayer(defaultLayer);
	}

	private PlayerCharacter GetLocalPlayer()
	{
		if (MyClient.Instance == null)
			return null;

		if (MyClient.Instance.PlayerManager == null)
			return null;

		GameObject localPlayerObject =
			MyClient.Instance.PlayerManager.LocalPlayerController;

		if (localPlayerObject == null)
			return null;

		return localPlayerObject.GetComponent<PlayerCharacter>();
	}

	private void SetLayer(string layerName)
	{
		int layer = LayerMask.NameToLayer(layerName);

		if (layer == -1)
		{
			Debug.LogWarning(
				$"CharacterOutline could not find layer '{layerName}'.",
				this);

			return;
		}

		if (outlineObject == null)
			return;

		outlineObject.layer = layer;
	}
}