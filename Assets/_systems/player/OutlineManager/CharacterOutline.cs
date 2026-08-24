using System.Collections;
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
	private Coroutine initializationCoroutine;

	public override void OnStartClient()
	{
		base.OnStartClient();

		if (outlineObject == null)
			outlineObject = gameObject;

		// The local player's own character does not need an outline.
		if (IsOwner)
			return;

		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.OnRoundPhaseChanged +=
				HandleRoundPhaseChanged;
		}

		PlayerTeams.OnTeamDataChanged +=
			HandleTeamDataChanged;

		initializationCoroutine =
			StartCoroutine(InitializeWhenReady());
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

			if (initializationCoroutine != null)
			{
				StopCoroutine(initializationCoroutine);
				initializationCoroutine = null;
			}
		}

		base.OnStopClient();
	}

	private IEnumerator InitializeWhenReady()
	{
		while (true)
		{
			if (GameFlowManager.Instance != null &&
				PlayerTeams.Instance != null &&
				playerCharacter != null)
			{
				localPlayer = GetLocalPlayer();

				if (localPlayer != null)
					break;
			}

			yield return null;
		}

		initializationCoroutine = null;

		RefreshOutline();
	}

	private void HandleRoundPhaseChanged(
		RoundFlowPhase phase)
	{
		RefreshOutline();
	}

	private void HandleTeamDataChanged()
	{
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

		// Don't evaluate team relationships before team assignment.
		if (localTeamId == PlayerTeams.NoTeamId ||
			playerTeamId == PlayerTeams.NoTeamId)
		{
			SetLayer(defaultLayer);
			return;
		}

		// Same team is always visible.
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

		// Enemy outlines disappear once setup finishes.
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
		int layer =
			LayerMask.NameToLayer(layerName);

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