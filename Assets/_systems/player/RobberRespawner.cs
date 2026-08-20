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
		// IMPORTANT:
		// Do not cancel an existing respawn when Setup ends.
		//
		// If the robber died during Setup and a respawn was
		// successfully started, that respawn is allowed to
		// finish even if the round becomes Ready or Active.

		if (phase != RoundFlowPhase.Setup)
			return;

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
		// A round actually finishing should still invalidate
		// any pending respawn.
		CancelRespawn();
	}

	private void TryStartRespawn()
	{
		if (localPlayerManager == null)
			return;

		if (GameFlowManager.Instance == null)
			return;

		// A NEW robber respawn may only be started during Setup.
		//
		// This prevents robbers who die during Active from
		// respawning.
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
		yield return new WaitForSeconds(
			setupRespawnDelay);

		respawnCoroutine = null;

		if (localPlayerManager == null)
			yield break;

		// The player may already have been respawned by
		// something else while we were waiting.
		if (localPlayerManager.State != PlayerState.Dead)
			yield break;

		if (PlayerTeams.Instance == null)
			yield break;

		// Make sure the player is still actually a robber.
		TeamType teamType =
			PlayerTeams.Instance.GetPlayerTeamType(
				localPlayerManager.PlayerId);

		if (teamType != TeamType.Robber)
			yield break;

		// IMPORTANT:
		// We deliberately do NOT check RoundPhase here.
		//
		// The respawn was authorized when the robber died
		// during Setup. If Setup becomes Ready/Active while
		// waiting, the already-authorized respawn still
		// completes.
		localPlayerManager.SpawnPlayer();
	}
}