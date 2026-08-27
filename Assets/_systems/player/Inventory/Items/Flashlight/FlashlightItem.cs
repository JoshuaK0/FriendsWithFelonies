using UnityEngine;

public sealed class FlashlightItem : HotbarHeldItem
{
	[SerializeField] private GameObject localFlashlightObject;

	[Header("Audio")]
	[SerializeField] private AudioSource audioSource;
	[SerializeField] private AudioClip toggleOnClip;
	[SerializeField] private AudioClip toggleOffClip;

	[Header("Settings")]
	[SerializeField] private bool startsEnabled;

	private FlashlightItemNetworked networkedCounterpart;
	private bool enabledState;

	protected override void OnContextInitialized()
	{
		networkedCounterpart =
			ItemServices != null
				? ItemServices.GetNetworkedFlashlight()
				: null;

		enabledState =
			startsEnabled;
	}

	protected override void OnEquipped()
	{
		ApplyLocalState();
		networkedCounterpart?.RequestSetFlashlight(enabledState);
	}

	protected override void OnEquippedUpdate()
	{
		if (!Input.GetMouseButtonDown(0))
			return;

		enabledState = !enabledState;

		ApplyLocalState();
		PlayToggleSound();

		networkedCounterpart?.RequestSetFlashlight(enabledState);
	}

	protected override void OnUnequipped()
	{
		ApplyLocalState();
		networkedCounterpart?.RequestSetFlashlight(enabledState);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		// Switching slots preserves the flashlight state. Destroying this
		// persistent held-item cache does not, so remote observers cannot be
		// left with a light that no longer has an owner-side item.
		networkedCounterpart?.RequestSetFlashlight(false);
	}

	private void ApplyLocalState()
	{
		if (localFlashlightObject != null)
			localFlashlightObject.SetActive(enabledState);
	}

	private void PlayToggleSound()
	{
		if (audioSource == null)
			return;

		AudioClip clip =
			enabledState
				? toggleOnClip
				: toggleOffClip;

		if (clip != null)
			audioSource.PlayOneShot(clip);
	}
}
