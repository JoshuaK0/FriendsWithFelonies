using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public sealed class LightSabotageManager : NetworkBehaviour
{
	public static LightSabotageManager Instance { get; private set; }

	[Header("Lights")]
	[SerializeField]
	private List<LightController> lightControllers = new();

	[SerializeField, Min(0f)]
	private float flickerOffDuration = 2f;

	[Header("Ambient")]
	[SerializeField, Min(0f)]
	private float ambientOffLerpDuration = 2f;

	[SerializeField, Min(0f)]
	private float ambientRestoreLerpDuration = 2f;

	[Header("Environment")]
	[SerializeField]
	private GameObject reverbZones;

	[Header("Audio")]
	[SerializeField]
	private AudioSource lightOffSound;

	[SerializeField]
	private AudioSource lightOnSound;

	private readonly SyncVar<bool> isOn =
		new(true);

	public bool IsOn =>
		isOn.Value;

	/// <summary>
	/// Server-side event raised whenever the powered-light
	/// state changes.
	///
	/// true  = lights restored.
	/// false = lights sabotaged.
	///
	/// SoundtrackController can subscribe to this event
	/// to change the global soundtrack.
	/// </summary>
	public event Action<bool> OnLightsChanged;


	private void Awake()
	{
		if (Instance != null &&
			Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
	}


	private void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}


	public override void OnStartServer()
	{
		base.OnStartServer();

		isOn.Value = true;

		if (AmbientLightManager.Instance != null)
		{
			AmbientLightManager.Instance
				.SetNormalServer();
		}
	}


	public override void OnStartClient()
	{
		base.OnStartClient();

		StartCoroutine(
			ApplyInitialStateNextFrame()
		);
	}


	/// <summary>
	/// Changes the global powered-light state.
	///
	/// Must be called by the server.
	///
	/// false:
	/// - Lights flicker off.
	/// - Ambient lighting fades to the lights-off state.
	/// - Reverb zones are enabled.
	/// - Lights-off sound plays.
	/// - OnLightsChanged(false) is raised on the server.
	///
	/// true:
	/// - Lights restore immediately.
	/// - Ambient lighting fades back to normal.
	/// - Reverb zones are disabled.
	/// - Lights-on sound plays.
	/// - OnLightsChanged(true) is raised on the server.
	/// </summary>
	[Server]
	public bool SetLightsServer(
		bool lightsOn)
	{
		if (isOn.Value == lightsOn)
			return false;

		isOn.Value =
			lightsOn;

		SetLightControllers(
			lightsOn
		);

		SetAmbientState(
			lightsOn
		);

		ApplyEnvironmentObserversRpc(
			lightsOn
		);

		/*
		 * Raised only on the server.
		 *
		 * SoundtrackController subscribes to this
		 * from OnStartServer(), so the server remains
		 * responsible for selecting soundtrack tracks.
		 */
		OnLightsChanged?.Invoke(
			lightsOn
		);

		return true;
	}


	private void SetLightControllers(
		bool lightsOn)
	{
		for (int i = 0;
			 i < lightControllers.Count;
			 i++)
		{
			LightController light =
				lightControllers[i];

			if (light == null)
				continue;


			if (lightsOn)
			{
				/*
				 * Power restored.
				 * Restore immediately.
				 */
				light.TurnOn();
			}
			else
			{
				/*
				 * Power sabotaged.
				 * Flicker into the off state.
				 */
				light.FlickerOff(
					flickerOffDuration
				);
			}
		}
	}


	private void SetAmbientState(
		bool lightsOn)
	{
		if (AmbientLightManager.Instance == null)
			return;


		if (lightsOn)
		{
			/*
			 * Power restored:
			 * smoothly return to normal
			 * ambient lighting.
			 */
			AmbientLightManager.Instance
				.SetNormalServerLerped(
					ambientRestoreLerpDuration
				);
		}
		else
		{
			/*
			 * Power sabotaged:
			 * smoothly transition to the
			 * lights-off ambient state.
			 */
			AmbientLightManager.Instance
				.SetLightsOffServerLerped(
					ambientOffLerpDuration
				);
		}
	}


	[ObserversRpc]
	private void ApplyEnvironmentObserversRpc(
		bool lightsOn)
	{
		ApplyEnvironmentLocal(
			lightsOn,
			true
		);
	}


	private void ApplyEnvironmentLocal(
		bool lightsOn,
		bool playSound)
	{
		if (reverbZones != null)
		{
			reverbZones.SetActive(
				!lightsOn
			);
		}


		if (!playSound)
			return;


		if (lightsOn)
		{
			if (lightOnSound != null)
			{
				lightOnSound.Play();
			}
		}
		else
		{
			if (lightOffSound != null)
			{
				lightOffSound.Play();
			}
		}
	}


	private IEnumerator ApplyInitialStateNextFrame()
	{
		/*
		 * Give SyncVars one frame to apply before
		 * forcing the initial local presentation.
		 */
		yield return null;


		ApplyEnvironmentLocal(
			isOn.Value,
			false
		);


		for (int i = 0;
			 i < lightControllers.Count;
			 i++)
		{
			LightController light =
				lightControllers[i];

			if (light == null)
				continue;


			/*
			 * Joining clients should immediately
			 * match the current networked state,
			 * without playing flicker animations
			 * or power sounds.
			 */
			light.ApplyImmediateStateLocal(
				isOn.Value
			);
		}
	}
}