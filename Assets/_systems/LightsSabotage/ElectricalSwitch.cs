using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public sealed class ElectricalSwitch :
	NetworkBehaviour,
	IInteractable
{
	[Header("Interaction")]
	[SerializeField, Min(0f)]
	private float interactionDuration;

	[SerializeField]
	private bool useDirectRaycast;

	[SerializeField, Min(0f)]
	private float maxInteractionDistance = 3.5f;

	[Header("Switch")]
	[SerializeField]
	private bool canTurnOff = true;

	[SerializeField]
	private bool canTurnOn = true;

	[Header("Audio")]
	[SerializeField]
	private AudioSource audioSource;

	public float InteractionDuration => interactionDuration;
	public bool UseDirectRaycast => useDirectRaycast;

	public void Interact(GameObject interactor)
	{
		if (interactor == null)
			return;

		NetworkObject interactorNetworkObject =
			interactor.GetComponentInParent<NetworkObject>();

		if (interactorNetworkObject == null)
			return;

		if (IsServer)
		{
			HandleInteractionServer(interactorNetworkObject);
			return;
		}

		InteractServerRpc(interactorNetworkObject);
	}

	[ServerRpc(RequireOwnership = false)]
	private void InteractServerRpc(
		NetworkObject interactor,
		NetworkConnection sender = null)
	{
		if (interactor == null || sender == null)
			return;

		// Do not allow a client to submit another player's character.
		if (interactor.Owner != sender)
			return;

		HandleInteractionServer(interactor);
	}

	private void HandleInteractionServer(NetworkObject interactor)
	{
		LightSabotageManager manager = LightSabotageManager.Instance;

		if (manager == null)
			return;

		float distance = Vector3.Distance(
			interactor.transform.position,
			transform.position
		);

		if (distance > maxInteractionDistance)
			return;

		bool changed = false;

		if (manager.IsOn)
		{
			if (canTurnOff)
				changed = manager.SetLightsServer(false);
		}
		else
		{
			if (canTurnOn)
				changed = manager.SetLightsServer(true);
		}

		if (changed)
			PlaySwitchAudioObserversRpc();
	}

	[ObserversRpc]
	private void PlaySwitchAudioObserversRpc()
	{
		if (audioSource != null)
			audioSource.Play();
	}

	public bool CanInteract(GameObject interactor, out string reason)
	{
		reason = string.Empty;
		return true;
	}
}
