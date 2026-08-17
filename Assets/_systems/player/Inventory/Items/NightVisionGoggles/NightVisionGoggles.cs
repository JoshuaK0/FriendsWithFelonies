using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Owner-local night vision with rechargeable runtime duration.
///
/// Battery state is retained when the held prefab is rebuilt
/// after changing hotbar slots.
///
/// Night vision is entirely local and does not modify the
/// synchronized world ambient state.
/// </summary>
public sealed class NightVisionGoggles : HotbarHeldItem
{
	private const string DurationKey =
		"nvg_duration";

	private const string LastStoredTimeKey =
		"nvg_last_stored_time";

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
	private float nightVisionReflectionIntensity = 0f;

	[SerializeField]
	private GameObject screenFx;

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

	[SerializeField]
	private Slider slider;

	private float currentDuration;
	private bool isOn;

	private float previousReflectionIntensity;

	protected override void OnContextInitialized()
	{
		currentDuration = duration;

		if (RuntimeState == null)
			return;

		currentDuration =
			RuntimeState.GetFloat(
				ItemId,
				DurationKey,
				duration
			);

		float storedAt =
			RuntimeState.GetFloat(
				ItemId,
				LastStoredTimeKey,
				Time.time
			);

		float elapsedUnequipped =
			Mathf.Max(
				0f,
				Time.time - storedAt
			);

		currentDuration =
			Mathf.Min(
				duration,
				currentDuration +
				elapsedUnequipped *
				consumeRate
			);
	}

	protected override void OnEquipped()
	{
		isOn = false;

		SetVisualState(false);
		UpdateSlider();
	}

	protected override void OnEquippedUpdate()
	{
		HandleInput();
		UpdateBattery();
		UpdateSlider();
	}

	protected override void OnUnequipped()
	{
		TurnOff();

		if (RuntimeState != null)
		{
			RuntimeState.SetFloat(
				ItemId,
				DurationKey,
				currentDuration
			);

			RuntimeState.SetFloat(
				ItemId,
				LastStoredTimeKey,
				Time.time
			);
		}

		if (slider != null)
			slider.gameObject.SetActive(false);
	}

	private void HandleInput()
	{
		if (!Input.GetMouseButtonDown(0))
			return;

		if (!isOn &&
			currentDuration <= 0f)
		{
			return;
		}

		if (audioSource != null &&
			toggleClip != null)
		{
			audioSource.PlayOneShot(
				toggleClip
			);
		}

		if (isOn)
			TurnOff();
		else
			TurnOn();
	}

	private void UpdateBattery()
	{
		if (isOn)
		{
			currentDuration =
				Mathf.Max(
					0f,
					currentDuration -
					Time.deltaTime *
					consumeRate
				);

			if (currentDuration <= 0f)
				TurnOff();

			return;
		}

		currentDuration =
			Mathf.Min(
				duration,
				currentDuration +
					Time.deltaTime *
					consumeRate
			);
	}

	private void TurnOn()
	{
		if (isOn ||
			currentDuration <= 0f)
		{
			return;
		}

		if (AmbientLightManager.Instance == null)
			return;

		isOn = true;

		/*
		 * Store the current reflection intensity
		 * before applying the local night-vision
		 * override.
		 */
		previousReflectionIntensity =
			RenderSettings.reflectionIntensity;

		RenderSettings.reflectionIntensity =
			nightVisionReflectionIntensity;

		/*
		 * Immediately make the client's ambient
		 * lighting black, then fade into the
		 * night-vision color.
		 *
		 * This remains completely local.
		 */
		AmbientLightManager.Instance
			.SetLocalOverrideFromBlack(
				nightVisionAmbientMode,
				nightVisionAmbientLight,
				nightVisionLerpDuration
			);

		SetVisualState(true);
	}

	private void TurnOff()
	{
		if (!isOn)
		{
			SetVisualState(false);
			return;
		}

		isOn = false;

		if (AmbientLightManager.Instance != null)
		{
			/*
			 * No lerp.
			 *
			 * Immediately return to whatever the
			 * current synchronized world state is.
			 */
			AmbientLightManager.Instance
				.ClearLocalOverride();
		}

		/*
		 * Restore the reflection intensity that
		 * was active before night vision was
		 * enabled.
		 */
		RenderSettings.reflectionIntensity =
			previousReflectionIntensity;

		SetVisualState(false);
	}

	private void SetVisualState(bool enabled)
	{
		if (screenFx != null)
			screenFx.SetActive(enabled);
	}

	private void UpdateSlider()
	{
		if (slider == null)
			return;

		slider.gameObject.SetActive(
			currentDuration < duration
		);

		slider.value =
			duration > 0f
				? currentDuration / duration
				: 0f;
	}
}