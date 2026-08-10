using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class SpectatorCameraTrack : MonoBehaviour
{
	[Header("Cycling")]
	[Tooltip("0 = left, 1 = right, 2 = middle mouse button.")]
	[SerializeField, Range(0, 6)]
	private int cycleMouseButton;

	[SerializeField]
	private bool excludeLocalPlayer = true;

	[Header("Following")]
	[SerializeField, Min(0f)]
	private float positionLerpSpeed = 10f;

	private readonly List<MyClient> teamPlayers = new();

	private MyClient target;
	private int targetIndex = -1;

	private void OnEnable()
	{
		PlayerTeams.OnTeamDataChanged +=
			HandleTargetsChanged;

		PickRandomTeamPlayer();
	}

	private void OnDisable()
	{
		PlayerTeams.OnTeamDataChanged -=
			HandleTargetsChanged;

		ClearTarget();
	}

	private void Update()
	{
		if (Input.GetMouseButtonDown(cycleMouseButton))
			CycleTeamPlayer();

		if (!IsValidTarget(target))
			PickRandomTeamPlayer();
	}

	private void LateUpdate()
	{
		FollowTarget();
	}

	private void HandleTargetsChanged()
	{
		RefreshTeamPlayers();

		if (target == null && teamPlayers.Count > 0)
		{
			targetIndex = 0;
			target = teamPlayers[targetIndex];
		}
	}

	private void RefreshTeamPlayers()
	{
		MyClient previousTarget = target;

		teamPlayers.Clear();

		if (PlayerTeams.Instance == null ||
			MyClient.Instance == null)
		{
			ClearTarget();
			return;
		}

		int localConnectionId =
			MyClient.Instance.OwnerId;

		int localTeamId =
			PlayerTeams.Instance.GetPlayerTeamId(
				localConnectionId);

		if (localTeamId == PlayerTeams.NoTeamId)
		{
			ClearTarget();
			return;
		}

		MyClient[] clients =
			FindObjectsByType<MyClient>(
				FindObjectsInactive.Exclude,
				FindObjectsSortMode.None);

		foreach (MyClient client in clients)
		{
			if (!IsValidTarget(
					client,
					localConnectionId,
					localTeamId))
			{
				continue;
			}

			teamPlayers.Add(client);
		}

		teamPlayers.Sort(
			(a, b) => a.OwnerId.CompareTo(b.OwnerId));

		targetIndex =
			previousTarget != null
				? teamPlayers.IndexOf(previousTarget)
				: -1;

		if (targetIndex >= 0)
		{
			target = previousTarget;
		}
		else
		{
			target = null;
			targetIndex = -1;
		}
	}

	private void PickRandomTeamPlayer()
	{
		RefreshTeamPlayers();

		if (teamPlayers.Count == 0)
		{
			ClearTarget();
			return;
		}

		targetIndex = Random.Range(
			0,
			teamPlayers.Count);

		target = teamPlayers[targetIndex];
	}

	private void CycleTeamPlayer()
	{
		RefreshTeamPlayers();

		if (teamPlayers.Count == 0)
		{
			ClearTarget();
			return;
		}

		if (targetIndex < 0 ||
			targetIndex >= teamPlayers.Count)
		{
			targetIndex = 0;
		}
		else
		{
			targetIndex =
				(targetIndex + 1) %
				teamPlayers.Count;
		}

		target = teamPlayers[targetIndex];
	}

	private bool IsValidTarget(MyClient client)
	{
		if (client == null ||
			PlayerTeams.Instance == null ||
			MyClient.Instance == null)
		{
			return false;
		}

		int localConnectionId =
			MyClient.Instance.OwnerId;

		int localTeamId =
			PlayerTeams.Instance.GetPlayerTeamId(
				localConnectionId);

		return IsValidTarget(
			client,
			localConnectionId,
			localTeamId);
	}

	private bool IsValidTarget(
		MyClient client,
		int localConnectionId,
		int localTeamId)
	{
		if (client == null ||
			localTeamId == PlayerTeams.NoTeamId)
		{
			return false;
		}

		if (excludeLocalPlayer &&
			client.OwnerId == localConnectionId)
		{
			return false;
		}

		if (!PlayerTeams.Instance.IsPlayerInTeam(
				client.OwnerId,
				localTeamId))
		{
			return false;
		}

		PlayerManager playerManager =
			client.PlayerManager;

		if (playerManager == null ||
			playerManager.State != PlayerState.Alive ||
			playerManager.PlayerControllerNetworkObject == null)
		{
			return false;
		}

		HealthManager healthManager =
			playerManager.PlayerControllerNetworkObject
				.GetComponentInChildren<HealthManager>(true);

		return healthManager == null ||
			!healthManager.IsDead;
	}

	private void FollowTarget()
	{
		NetworkObject controllerObject =
			target?.PlayerManager?.PlayerControllerNetworkObject;

		if (controllerObject == null)
			return;

		Transform viewTransform =
			FindViewTransform(controllerObject);

		if (viewTransform == null)
			return;

		transform.position = Vector3.Lerp(
			transform.position,
			viewTransform.position,
			positionLerpSpeed * Time.deltaTime);
	}

	private static Transform FindViewTransform(
		NetworkObject controllerObject)
	{
		Camera[] cameras =
			controllerObject.GetComponentsInChildren<Camera>(true);

		foreach (Camera targetCamera in cameras)
		{
			if (targetCamera != null &&
				targetCamera.CompareTag("MainCamera"))
			{
				return targetCamera.transform;
			}
		}

		if (cameras.Length > 0 && cameras[0] != null)
			return cameras[0].transform;

		CharacterController characterController =
			controllerObject.GetComponentInChildren<
				CharacterController>(true);

		return characterController != null
			? characterController.transform
			: controllerObject.transform;
	}

	private void ClearTarget()
	{
		target = null;
		targetIndex = -1;
	}
}