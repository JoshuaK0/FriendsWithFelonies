using System.Collections;
using UnityEngine;

public sealed class CopRespawner : MonoBehaviour
{
	[Header("Respawn Times")]
	[SerializeField, Min(0f)]
	private float respawnDelay = 5f;

	[SerializeField, Min(0f)]
	private float setupRespawnDelay = 1f;

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

		if (PlayerTeams.Instance == null)
			return;

		TeamType teamType =
			PlayerTeams.Instance.GetPlayerTeamType(
				localPlayerManager.PlayerId);

		if (teamType != TeamType.Cop)
			return;

		StartRespawn();
	}

	private void HandleRoundFinished(int round)
	{
		CancelRespawn();
	}

	private void StartRespawn()
	{
		if (respawnCoroutine != null)
			return;

		float delay = GetCurrentRespawnDelay();

		respawnCoroutine = StartCoroutine(
			RespawnAfterDelay(delay));
	}

	private float GetCurrentRespawnDelay()
	{
		if (GameFlowManager.Instance != null &&
			GameFlowManager.Instance.RoundPhase ==
			RoundFlowPhase.Setup)
		{
			return setupRespawnDelay;
		}

		return respawnDelay;
	}

	private void CancelRespawn()
	{
		if (respawnCoroutine == null)
			return;

		StopCoroutine(respawnCoroutine);
		respawnCoroutine = null;
	}

	private IEnumerator RespawnAfterDelay(float delay)
	{
		yield return new WaitForSeconds(delay);

		respawnCoroutine = null;

		if (localPlayerManager == null)
			yield break;

		if (localPlayerManager.State != PlayerState.Dead)
			yield break;

		if (PlayerTeams.Instance == null)
			yield break;

		TeamType teamType =
			PlayerTeams.Instance.GetPlayerTeamType(
				localPlayerManager.PlayerId);

		if (teamType != TeamType.Cop)
			yield break;

		localPlayerManager.SpawnPlayer();
	}
}