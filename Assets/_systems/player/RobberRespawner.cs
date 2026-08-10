using System.Collections;
using UnityEngine;

public sealed class RobberRespawner : MonoBehaviour
{
	[SerializeField, Min(0f)]
	private float setupRespawnDelay = 2f;

	private PlayerManager localPlayerManager;
	private Coroutine respawnCoroutine;

	private IEnumerator Start()
	{
		yield return new WaitUntil(
			() =>
				PlayerManager.Instance != null &&
				GameFlowManager.Instance != null);

		localPlayerManager = PlayerManager.Instance;

		localPlayerManager.OnPlayerStateChanged +=
			HandlePlayerStateChanged;

		GameFlowManager.Instance.OnRoundPhaseChanged +=
			HandleRoundPhaseChanged;

		GameFlowManager.Instance.OnRoundFinished +=
			HandleRoundFinished;
	}

	private void OnDestroy()
	{
		if (localPlayerManager != null)
		{
			localPlayerManager.OnPlayerStateChanged -=
				HandlePlayerStateChanged;
		}

		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.OnRoundPhaseChanged -=
				HandleRoundPhaseChanged;

			GameFlowManager.Instance.OnRoundFinished -=
				HandleRoundFinished;
		}

		CancelRespawn();
	}

	private void HandlePlayerStateChanged(
		PlayerState previous,
		PlayerState current)
	{
		if (current != PlayerState.Dead)
			return;

		TryStartRespawn();
	}

	private void HandleRoundPhaseChanged(
		RoundFlowPhase phase)
	{
		if (phase != RoundFlowPhase.Setup)
		{
			CancelRespawn();
			return;
		}

		// Handles the case where the robber is already dead
		// when a new setup phase begins.
		if (localPlayerManager != null &&
			localPlayerManager.State == PlayerState.Dead)
		{
			TryStartRespawn();
		}
	}

	private void HandleRoundFinished(int round)
	{
		CancelRespawn();
	}

	private void TryStartRespawn()
	{
		if (localPlayerManager == null)
			return;

		if (GameFlowManager.Instance == null)
			return;

		if (GameFlowManager.Instance.RoundPhase !=
			RoundFlowPhase.Setup)
		{
			return;
		}

		if (PlayerTeams.Instance == null)
			return;

		TeamType teamType =
			PlayerTeams.Instance.GetPlayerTeamType(
				localPlayerManager.PlayerId);

		if (teamType != TeamType.Robber)
			return;

		StartRespawn();
	}

	private void StartRespawn()
	{
		if (respawnCoroutine != null)
			return;

		respawnCoroutine = StartCoroutine(
			RespawnAfterDelay());
	}

	private void CancelRespawn()
	{
		if (respawnCoroutine == null)
			return;

		StopCoroutine(respawnCoroutine);
		respawnCoroutine = null;
	}

	private IEnumerator RespawnAfterDelay()
	{
		yield return new WaitForSeconds(setupRespawnDelay);

		respawnCoroutine = null;

		if (localPlayerManager == null)
			yield break;

		if (localPlayerManager.State != PlayerState.Dead)
			yield break;

		if (GameFlowManager.Instance == null)
			yield break;

		// Robbers must NEVER respawn outside setup.
		if (GameFlowManager.Instance.RoundPhase !=
			RoundFlowPhase.Setup)
		{
			yield break;
		}

		if (PlayerTeams.Instance == null)
			yield break;

		TeamType teamType =
			PlayerTeams.Instance.GetPlayerTeamType(
				localPlayerManager.PlayerId);

		if (teamType != TeamType.Robber)
			yield break;

		localPlayerManager.SpawnPlayer();
	}
}