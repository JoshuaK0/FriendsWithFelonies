using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Persistent night-vision state attached to the player's
/// inventory object.
///
/// This continues updating while the held-item prefab is unequipped.
/// </summary>
public sealed class NightVisionRuntimeController :
	MonoBehaviour,
	IInventoryReleaseHandler
{
	private const string DurationKey =
		"nvg_duration";

	private const string IsOnKey =
		"nvg_is_on";

	private const string LastStoredTimeKey =
		"nvg_last_stored_time";

	private HotbarItemRuntimeStateStore runtimeState;

	private int itemId = -1;

	private float maximumDuration;
	private float consumeRate;
	private float currentDuration;

	private Color ambientLight;
	private AmbientMode ambientMode;
	private float lerpDuration;
	private float reflectionIntensity;

	private GameObject screenFx;
	private Slider slider;

	private bool isInitialized;
	private bool isOn;
	private bool localOverrideApplied;
	private bool isReleased;

	private float previousReflectionIntensity;

	public bool IsOn => isOn;

	public float CurrentDuration => currentDuration;

	public void Configure(
		int configuredItemId,
		HotbarItemRuntimeStateStore configuredRuntimeState,
		float configuredDuration,
		float configuredConsumeRate,
		Color configuredAmbientLight,
		AmbientMode configuredAmbientMode,
		float configuredLerpDuration,
		float configuredReflectionIntensity,
		GameObject configuredScreenFx,
		Slider configuredSlider)
	{
		isReleased = false;
		itemId = configuredItemId;
		runtimeState = configuredRuntimeState;

		maximumDuration =
			Mathf.Max(0.01f, configuredDuration);

		consumeRate =
			Mathf.Max(0f, configuredConsumeRate);

		ambientLight =
			configuredAmbientLight;

		ambientMode =
			configuredAmbientMode;

		lerpDuration =
			Mathf.Max(0f, configuredLerpDuration);

		reflectionIntensity =
			Mathf.Clamp01(
				configuredReflectionIntensity
			);

		SetOutputReferences(
			configuredScreenFx,
			configuredSlider
		);

		if (!isInitialized)
			LoadState();

		currentDuration =
			Mathf.Clamp(
				currentDuration,
				0f,
				maximumDuration
			);

		RefreshOutputs();
	}

	public void SetOutputReferences(
		GameObject configuredScreenFx,
		Slider configuredSlider)
	{
		if (configuredScreenFx != null)
			screenFx = configuredScreenFx;

		if (configuredSlider != null)
			slider = configuredSlider;

		RefreshOutputs();
	}

	private void LoadState()
	{
		currentDuration = maximumDuration;
		isOn = false;

		if (runtimeState != null)
		{
			currentDuration =
				runtimeState.GetFloat(
					itemId,
					DurationKey,
					maximumDuration
				);

			isOn =
				runtimeState.GetFloat(
					itemId,
					IsOnKey,
					0f
				) > 0.5f;

			float storedAt =
				runtimeState.GetFloat(
					itemId,
					LastStoredTimeKey,
					Time.time
				);

			float elapsed =
				Mathf.Max(
					0f,
					Time.time - storedAt
				);

			if (isOn)
			{
				currentDuration -=
					elapsed * consumeRate;
			}
			else
			{
				currentDuration +=
					elapsed * consumeRate;
			}
		}

		currentDuration =
			Mathf.Clamp(
				currentDuration,
				0f,
				maximumDuration
			);

		if (isOn && currentDuration <= 0f)
			isOn = false;

		isInitialized = true;

		if (isOn)
			ApplyNightVision();

		SaveState();
	}

	private void Update()
	{
		if (!isInitialized)
			return;

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
		}
		else
		{
			currentDuration =
				Mathf.Min(
					maximumDuration,
					currentDuration +
						Time.deltaTime *
						consumeRate
				);
		}

		UpdateSlider();
	}

	public bool TryToggle()
	{
		if (!isInitialized)
			return false;

		if (isOn)
		{
			TurnOff();
			return true;
		}

		if (currentDuration <= 0f)
			return false;

		if (AmbientLightManager.Instance == null)
			return false;

		TurnOn();
		return true;
	}

	private void TurnOn()
	{
		if (isOn || currentDuration <= 0f)
			return;

		isOn = true;

		ApplyNightVision();
		RefreshOutputs();
		SaveState();
	}

	private void ApplyNightVision()
	{
		if (AmbientLightManager.Instance == null)
			return;

		if (!localOverrideApplied)
		{
			previousReflectionIntensity =
				RenderSettings.reflectionIntensity;
		}

		RenderSettings.reflectionIntensity =
			reflectionIntensity;

		AmbientLightManager.Instance
			.SetLocalOverrideFromBlack(
				ambientMode,
				ambientLight,
				lerpDuration
			);

		localOverrideApplied = true;
	}

	private void TurnOff()
	{
		if (!isOn)
			return;

		isOn = false;

		ClearNightVisionOutput();
		RefreshOutputs();
		SaveState();
	}

	private void ClearNightVisionOutput()
	{
		if (localOverrideApplied &&
			AmbientLightManager.Instance != null)
		{
			AmbientLightManager.Instance
				.ClearLocalOverride();
		}

		if (localOverrideApplied)
		{
			RenderSettings.reflectionIntensity =
				previousReflectionIntensity;
		}

		localOverrideApplied = false;
	}

	private void RefreshOutputs()
	{
		if (screenFx != null)
			screenFx.SetActive(isOn);

		UpdateSlider();
	}

	private void UpdateSlider()
	{
		if (slider == null)
			return;

		bool isFull =
			currentDuration >=
			maximumDuration - Mathf.Epsilon;

		/*
		 * The slider stays visible while charging or
		 * draining, even when the goggles are not the
		 * currently equipped item.
		 */
		slider.gameObject.SetActive(!isFull);

		slider.value =
			maximumDuration > 0f
				? currentDuration / maximumDuration
				: 0f;
	}

	public void SaveState()
	{
		if (runtimeState == null ||
			itemId < 0)
		{
			return;
		}

		runtimeState.SetFloat(
			itemId,
			DurationKey,
			currentDuration
		);

		runtimeState.SetFloat(
			itemId,
			IsOnKey,
			isOn ? 1f : 0f
		);

		runtimeState.SetFloat(
			itemId,
			LastStoredTimeKey,
			Time.time
		);
	}

	public void OnInventoryReleased()
	{
		if (isReleased)
			return;

		isReleased = true;
		isOn = false;

		ClearNightVisionOutput();

		if (screenFx != null)
			screenFx.SetActive(false);

		if (slider != null)
			slider.gameObject.SetActive(false);

		SaveState();
	}

	private void OnDestroy()
	{
		OnInventoryReleased();
	}
}
