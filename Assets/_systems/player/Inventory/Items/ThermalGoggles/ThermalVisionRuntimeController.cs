using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Persistent owner-local thermal-vision state.
///
/// This component is added to the player's inventory at runtime. It continues
/// updating while the thermal goggles are unequipped or their held prefab has
/// been destroyed and rebuilt.
/// </summary>
public sealed class ThermalVisionRuntimeController : MonoBehaviour
{
	private const string DurationKey =
		"thermal_duration";

	private const string IsOnKey =
		"thermal_is_on";

	private const string LastStoredTimeKey =
		"thermal_last_stored_time";

	private HotbarItemRuntimeStateStore runtimeState;
	private int itemId = -1;

	private UniversalRendererData rendererData;
	private string featureName;
	private ScriptableRendererFeature renderFeature;

	private GameObject screenFx;
	private Slider slider;

	private float maximumDuration;
	private float consumeRate;
	private float currentDuration;

	private bool isInitialized;
	private bool isOn;
	private bool initialFeatureState;
	private bool initialFeatureStateCaptured;
	private bool warnedAboutMissingFeature;

	public bool IsOn => isOn;
	public float CurrentDuration => currentDuration;

	public void Configure(
		int configuredItemId,
		HotbarItemRuntimeStateStore configuredRuntimeState,
		UniversalRendererData configuredRendererData,
		string configuredFeatureName,
		float configuredDuration,
		float configuredConsumeRate,
		GameObject configuredScreenFx,
		Slider configuredSlider)
	{
		itemId = configuredItemId;
		runtimeState = configuredRuntimeState;
		rendererData = configuredRendererData;
		featureName = configuredFeatureName;

		maximumDuration =
			Mathf.Max(0.01f, configuredDuration);

		consumeRate =
			Mathf.Max(0f, configuredConsumeRate);

		ResolveFeature();

		if (!initialFeatureStateCaptured &&
			renderFeature != null)
		{
			initialFeatureState =
				renderFeature.isActive;

			initialFeatureStateCaptured = true;
		}

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

		if (isOn && !ApplyFeatureState(true))
			isOn = false;
		else if (!isOn)
			ApplyFeatureState(false);

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
		isOn = renderFeature != null &&
			renderFeature.isActive;

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
					isOn ? 1f : 0f
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

		if (isOn && !ApplyFeatureState(true))
			isOn = false;
		else if (!isOn)
			ApplyFeatureState(false);

		RefreshOutputs();
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
			// Recharge whenever thermal vision is off,
			// whether the goggles are equipped or unequipped.
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

		return TurnOn();
	}

	private bool TurnOn()
	{
		if (isOn || currentDuration <= 0f)
			return false;

		if (!ApplyFeatureState(true))
			return false;

		isOn = true;

		RefreshOutputs();
		SaveState();

		return true;
	}

	private void TurnOff()
	{
		if (!isOn)
			return;

		isOn = false;

		ApplyFeatureState(false);
		RefreshOutputs();
		SaveState();
	}

	private bool ApplyFeatureState(bool enabled)
	{
		if (renderFeature == null)
			ResolveFeature();

		if (renderFeature == null)
			return false;

		renderFeature.SetActive(enabled);
		return true;
	}

	private void ResolveFeature()
	{
		if (renderFeature != null)
			return;

		if (rendererData == null ||
			rendererData.rendererFeatures == null)
		{
			WarnAboutMissingFeature();
			return;
		}

		for (int i = 0;
			i < rendererData.rendererFeatures.Count;
			i++)
		{
			ScriptableRendererFeature feature =
				rendererData.rendererFeatures[i];

			if (feature == null ||
				feature.name != featureName)
			{
				continue;
			}

			renderFeature = feature;
			return;
		}

		WarnAboutMissingFeature();
	}

	private void WarnAboutMissingFeature()
	{
		if (warnedAboutMissingFeature)
			return;

		warnedAboutMissingFeature = true;

		Debug.LogWarning(
			$"Thermal vision could not find renderer feature " +
			$"'{featureName}'.",
			this
		);
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

		// Remain visible while draining or charging, even when unequipped.
		slider.gameObject.SetActive(!isFull);

		slider.value =
			maximumDuration > 0f
				? currentDuration / maximumDuration
				: 0f;
	}

	public void SaveState()
	{
		if (runtimeState == null || itemId < 0)
			return;

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

	private void OnDestroy()
	{
		SaveState();

		if (renderFeature != null &&
			initialFeatureStateCaptured)
		{
			renderFeature.SetActive(
				initialFeatureState
			);
		}

		if (screenFx != null)
			screenFx.SetActive(false);
	}
}
