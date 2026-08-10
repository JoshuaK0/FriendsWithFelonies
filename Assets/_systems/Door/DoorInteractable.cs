using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public sealed class DoorInteractable : NetworkBehaviour, IInteractable
{
	[Header("Interaction")]
	[SerializeField]
	private Transform iconAnchor;

	[Header("Door")]
	[SerializeField]
	private bool direction;

	[SerializeField]
	private Transform doorVisual;

	[SerializeField]
	private Transform doorCollider;

	[SerializeField]
	private float speed = 5f;

	[Header("Hold To Return")]
	[SerializeField]
	private float holdToAutoCloseTime = 0.5f;

	[SerializeField]
	private float autoCloseMaxDist = 3f;

	[Header("Alarm")]
	[SerializeField]
	private AlarmIndicator alarmIndicator;

	[SerializeField]
	private bool isDoorAlarmed;

	[SerializeField]
	private float alarmDelay;

	private readonly SyncVar<float> currentRot = new();

	private float previousRot;

	private GameObject currentInteractor;
	private float interactHoldTime;
	private bool holding;
	private bool isInitialized;

	public Transform IconAnchor =>
		iconAnchor != null
			? iconAnchor
			: transform;

	public override void OnStartServer()
	{
		base.OnStartServer();

		if (doorVisual != null)
			currentRot.Value = doorVisual.localEulerAngles.y;

		previousRot = currentRot.Value;
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		isInitialized = true;
	}

	public void Interact(GameObject interactor)
	{
		if (interactor == null)
			return;

		currentInteractor = interactor;
		interactHoldTime = 0f;
		holding = true;

		InteractDoorServerRpc();
	}

	[ServerRpc(RequireOwnership = false)]
	private void InteractDoorServerRpc(
		NetworkConnection sender = null)
	{
		if (sender == null)
			return;

		previousRot = currentRot.Value;

		if (Mathf.Abs(
				Mathf.DeltaAngle(currentRot.Value, 0f)) < 0.01f)
		{
			currentRot.Value = direction
				? 90f
				: -90f;
		}
		else
		{
			currentRot.Value = 0f;
		}

		if (PlayerTeams.Instance == null)
			return;

		TeamType teamType =
			PlayerTeams.Instance.GetPlayerTeamType(
				sender.ClientId);

		// Robber-specific interaction effects.
		if (teamType == TeamType.Robber)
		{
			/*
			 * Put your new handprint system here.
			 *
			 * For example:
			 * CreateHandprint(sender.ClientId);
			 */
		}

		if (!isDoorAlarmed)
			return;

		if (teamType != TeamType.Robber)
			return;

		if (SecuritySabotageManager.Instance != null && !SecuritySabotageManager.Instance.IsSecurityOn())
			return;


		StartCoroutine(AlarmAfterDelay());
	}

	private IEnumerator AlarmAfterDelay()
	{
		if (alarmDelay > 0f)
			yield return new WaitForSeconds(alarmDelay);

		DoAlarmObserversRpc();
	}

	[ObserversRpc]
	private void DoAlarmObserversRpc()
	{
		if (alarmIndicator != null)
			alarmIndicator.StartAlarm();
	}

	[ServerRpc(RequireOwnership = false)]
	private void GoToPreviousDoorValueServerRpc()
	{
		currentRot.Value = previousRot;
	}

	private void Update()
	{
		if (!isInitialized)
			return;

		UpdateDoorVisual();
		UpdateDoorCollider();
		UpdateHoldInteraction();
	}

	private void UpdateDoorVisual()
	{
		if (doorVisual == null)
			return;

		float currentY =
			doorVisual.localEulerAngles.y;

		if (Mathf.Abs(
				Mathf.DeltaAngle(
					currentY,
					currentRot.Value)) < 0.01f)
		{
			return;
		}

		float newY = Mathf.LerpAngle(
			currentY,
			currentRot.Value,
			speed * Time.deltaTime);

		doorVisual.localRotation =
			Quaternion.Euler(0f, newY, 0f);
	}

	private void UpdateDoorCollider()
	{
		if (doorCollider == null)
			return;

		float currentY =
			doorCollider.localEulerAngles.y;

		if (Mathf.Abs(
				Mathf.DeltaAngle(
					currentY,
					currentRot.Value)) < 0.01f)
		{
			return;
		}

		doorCollider.localRotation =
			Quaternion.Euler(
				0f,
				currentRot.Value,
				0f);
	}

	private void UpdateHoldInteraction()
	{
		if (!holding)
		{
			interactHoldTime = 0f;
			return;
		}

		if (currentInteractor == null)
		{
			StopHolding();
			return;
		}

		interactHoldTime += Time.deltaTime;

		bool releasedInteract =
			Input.GetKeyUp(KeyCode.E);

		bool tooFarAway =
			Vector3.Distance(
				transform.position,
				currentInteractor.transform.position)
			>= autoCloseMaxDist;

		if (!releasedInteract && !tooFarAway)
			return;

		bool shouldReturn =
			interactHoldTime >= holdToAutoCloseTime;

		StopHolding();

		if (shouldReturn)
			GoToPreviousDoorValueServerRpc();
	}

	private void StopHolding()
	{
		holding = false;
		interactHoldTime = 0f;
		currentInteractor = null;
	}

	public bool CanInteract(GameObject interactor, out string reason)
	{
		reason = string.Empty;
		return true;
	}
}