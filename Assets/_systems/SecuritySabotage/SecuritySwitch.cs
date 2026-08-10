using System.Collections;
using FishNet.Object;
using UnityEngine;

public sealed class SecuritySwitch : NetworkBehaviour, IInteractable
{
	[Header("Interaction")]
	[SerializeField]
	private Transform iconAnchor;

	[Header("Security")]
	[SerializeField]
	private float restartTime = 10f;

	[Header("Audio")]
	[SerializeField]
	private AudioSource audioSource;

	private Coroutine restartRoutine;

	public Transform IconAnchor =>
		iconAnchor != null
			? iconAnchor
			: transform;

	public void Interact(GameObject interactor)
	{
		ToggleSecurityServerRpc();
	}

	[ServerRpc(RequireOwnership = false)]
	private void ToggleSecurityServerRpc()
	{
		if (SecuritySabotageManager.Instance == null)
			return;

		SecuritySabotageManager.Instance.ToggleSecurityServer(false);

		PlaySwitchAudioObserversRpc();

		if (restartRoutine != null)
			StopCoroutine(restartRoutine);

		restartRoutine = StartCoroutine(
			RestartSecurityAfterDelay());
	}

	private IEnumerator RestartSecurityAfterDelay()
	{
		yield return new WaitForSeconds(restartTime);

		if (SecuritySabotageManager.Instance != null)
		{
			SecuritySabotageManager.Instance.ToggleSecurityServer(true);
		}

		restartRoutine = null;
	}

	[ObserversRpc]
	private void PlaySwitchAudioObserversRpc()
	{
		if (audioSource != null)
			audioSource.Play();
	}

	public override void OnStopServer()
	{
		if (restartRoutine != null)
		{
			StopCoroutine(restartRoutine);
			restartRoutine = null;
		}

		base.OnStopServer();
	}

	public bool CanInteract(GameObject interactor, out string reason)
	{
		reason = string.Empty;
		return true;
	}
}