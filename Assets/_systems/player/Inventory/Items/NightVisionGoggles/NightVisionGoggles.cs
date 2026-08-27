using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Provides input and configuration for the persistent
/// night-vision runtime controller.
/// </summary>
public sealed class NightVisionGoggles : HotbarHeldItem
{
	[Header("Night Vision")]
	[SerializeField]
	private Color nightVisionAmbientLight =
		Color.green;

	[SerializeField]
	private AmbientMode nightVisionAmbientMode =
		AmbientMode.Flat;

	[SerializeField, Min(0f)]
	private float nightVisionLerpDuration = 0.5f;

	[SerializeField, Range(0f, 1f)]
	private float nightVisionReflectionIntensity;

	[Header("Audio")]
	[SerializeField]
	private AudioSource audioSource;

	[SerializeField]
	private AudioClip toggleClip;

	[Header("Battery")]
	[SerializeField, Min(0.01f)]
	private float duration = 10f;

	[SerializeField, Min(0f)]
	private float consumeRate = 1f;

	private NightVisionRuntimeController runtimeController;

	private GameObject screenFx;
	private Slider slider;

	protected override void OnContextInitialized()
	{
		if (Inventory == null)
			return;

		runtimeController =
			Inventory.GetComponent
				<NightVisionRuntimeController>();

		if (runtimeController == null)
		{
			runtimeController =
				Inventory.gameObject.AddComponent
					<NightVisionRuntimeController>();
		}

		ResolvePlayerOutputs();

		runtimeController.Configure(
			ItemId,
			RuntimeState,
			duration,
			consumeRate,
			nightVisionAmbientLight,
			nightVisionAmbientMode,
			nightVisionLerpDuration,
			nightVisionReflectionIntensity,
			screenFx,
			slider
		);
	}

	protected override void OnEquipped()
	{
		ResolvePlayerOutputs();

		/*
		 * Equipping does not change whether night
		 * vision is currently on.
		 */
		runtimeController?.SetOutputReferences(
			screenFx,
			slider
		);
	}

	protected override void OnEquippedUpdate()
	{
		if (!Input.GetMouseButtonDown(0))
			return;

		if (runtimeController == null)
			return;

		bool stateChanged =
			runtimeController.TryToggle();

		if (!stateChanged)
			return;

		if (audioSource != null &&
			toggleClip != null)
		{
			audioSource.PlayOneShot(toggleClip);
		}
	}

	protected override void OnUnequipped()
	{
		/*
		 * Do not turn off night vision or hide the
		 * slider when unequipping.
		 */
		runtimeController?.SaveState();
	}

	private bool ResolvePlayerOutputs()
	{
		PlayerCharacter playerCharacter =
			Inventory != null
				? Inventory.GetComponentInParent<PlayerCharacter>()
				: null;

		if (playerCharacter == null)
			return false;

		slider =
			playerCharacter.GetNVGSlider();

		screenFx =
			playerCharacter.GetNVGFX();

		runtimeController?.SetOutputReferences(
			screenFx,
			slider
		);

		return slider != null ||
			screenFx != null;
	}
}
