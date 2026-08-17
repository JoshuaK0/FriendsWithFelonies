using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public sealed class LightController : NetworkBehaviour, ILightFlickerable
{
	private enum TransitionMode
	{
		None,
		FlickerOn,
		FlickerOff
	}

	private enum LightStableState
	{
		On,
		Off
	}

	[SerializeField] private FlickerableLightType lightType;
	[SerializeField] private Light[] flickerLights;
	[SerializeField] private float originalIntensity = 1f;
	[SerializeField] private bool startOn = false;
	[SerializeField] private bool applyDefaultStateOnStart = true;

	[Header("Flicker Settings")]
	[SerializeField] private float minIntensity = 0f;
	[SerializeField] private float flickerSpeed = 0.075f;
	[SerializeField] private float fadeSpeed = 15f;

	[Header("Max Intensity Drift")]
	[SerializeField] private float minMaxLightLerpTime = 2f;
	[SerializeField] private float maxMaxLightLerpTime = 8f;

	[Header("Continuous Flicker Pulse")]
	[SerializeField] private float continuousFlickerTimeout = 0.3f;

	[Header("Audio")]
	[SerializeField] private AudioSource flickerAudioSource;
	[SerializeField] private AudioClip flickerLoopClip;
	[SerializeField] private AudioClip turnOnClip;
	[SerializeField] private AudioClip turnOffClip;
	[SerializeField] private float minPitch = 0.9f;
	[SerializeField] private float maxPitch = 1.1f;

	private Coroutine _routine;

	private float[] originalIntensities;

	private float maxIntensity;
	private float targetMaxIntensity;
	private float maxLightLerpTime;
	private float maxLightLerpTimer;

	private bool isFlickering;
	private float targetIntensity;
	private float flickerTimer;

	private bool continuousFlickerActive;
	private bool continuousFlickerRequested;
	private TransitionMode transitionMode = TransitionMode.None;
	private LightStableState stableState;

	private float lastContinuousFlickerCallTime = float.NegativeInfinity;

	private void Awake()
	{
		if (flickerLights == null || flickerLights.Length == 0)
			flickerLights = GetComponentsInChildren<Light>(true);

		if (flickerAudioSource == null)
			flickerAudioSource = GetComponent<AudioSource>();

		if (flickerAudioSource != null)
			flickerAudioSource.loop = true;

		CacheAuthoredIntensities();

		originalIntensity = flickerLights[0].intensity;

		maxIntensity = 1f;
		targetMaxIntensity = 1f;
		stableState = startOn ? LightStableState.On : LightStableState.Off;
	}

	private void Update()
	{
		if (!continuousFlickerRequested)
			return;

		if (Time.time - lastContinuousFlickerCallTime > continuousFlickerTimeout)
		{
			if (IsServer) StopContinuousFlickerInternalObserversRpc();
			else StopContinuousFlickerInternalServerRpc();
		}
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		if (!applyDefaultStateOnStart)
			return;

		if (startOn)
			ApplyOnLocal(false);
		else
			ApplyOffLocal(false);
	}

	public FlickerableLightType GetLightType()
	{
		return lightType;
	}

	public void TurnOn()
	{
		if (IsServer) TurnOnObserversRpc();
		else TurnOnServerRpc();
	}

	public void TurnOff()
	{
		if (IsServer) TurnOffObserversRpc();
		else TurnOffServerRpc();
	}

	public void FlickerOn(float duration)
	{
		if (IsServer) FlickerOnObserversRpc(duration);
		else FlickerOnServerRpc(duration);
	}

	public void FlickerOff(float duration)
	{
		if (IsServer) FlickerOffObserversRpc(duration);
		else FlickerOffServerRpc(duration);
	}

	public void Toggle()
	{
		if (IsServer) ToggleObserversRpc();
		else ToggleServerRpc();
	}

	public void StartContinuousFlicker()
	{
		lastContinuousFlickerCallTime = Time.time;

		if (continuousFlickerActive)
			return;

		if (Time.time - lastContinuousFlickerCallTime <= continuousFlickerTimeout)
		{
			if (IsServer) StartContinuousFlickerInternalObserversRpc();
			else StartContinuousFlickerInternalServerRpc();
		}
	}

	public void StopContinuousFlicker()
	{
		lastContinuousFlickerCallTime = float.NegativeInfinity;

		if (IsServer) StopContinuousFlickerInternalObserversRpc();
		else StopContinuousFlickerInternalServerRpc();
	}

	[ServerRpc(RequireOwnership = false)]
	private void TurnOnServerRpc()
	{
		TurnOnObserversRpc();
	}

	[ServerRpc(RequireOwnership = false)]
	private void TurnOffServerRpc()
	{
		TurnOffObserversRpc();
	}

	[ServerRpc(RequireOwnership = false)]
	private void FlickerOnServerRpc(float duration)
	{
		FlickerOnObserversRpc(duration);
	}

	[ServerRpc(RequireOwnership = false)]
	private void FlickerOffServerRpc(float duration)
	{
		FlickerOffObserversRpc(duration);
	}

	[ServerRpc(RequireOwnership = false)]
	private void ToggleServerRpc()
	{
		ToggleObserversRpc();
	}

	[ServerRpc(RequireOwnership = false)]
	private void StartContinuousFlickerInternalServerRpc()
	{
		StartContinuousFlickerInternalObserversRpc();
	}

	[ServerRpc(RequireOwnership = false)]
	private void StopContinuousFlickerInternalServerRpc()
	{
		StopContinuousFlickerInternalObserversRpc();
	}

	[ObserversRpc]
	private void TurnOnObserversRpc()
	{
		if (!IsTransitioning() && stableState == LightStableState.On)
			return;

		// Turning power back on must interrupt an active FlickerOff transition.
		continuousFlickerActive = false;
		continuousFlickerRequested = false;
		ApplyOnLocal(true);
	}

	[ObserversRpc]
	private void TurnOffObserversRpc()
	{
		if (IsTransitioning())
			return;

		if (stableState == LightStableState.Off)
			return;

		continuousFlickerActive = false;
		continuousFlickerRequested = false;
		ApplyOffLocal(true);
	}

	[ObserversRpc]
	private void FlickerOnObserversRpc(float duration)
	{
		if (IsTransitioning())
			return;

		if (stableState == LightStableState.On)
			return;

		continuousFlickerActive = false;
		continuousFlickerRequested = false;
		StartFlicker(duration, true, TransitionMode.FlickerOn);
	}

	[ObserversRpc]
	private void FlickerOffObserversRpc(float duration)
	{
		if (IsTransitioning())
			return;

		if (stableState == LightStableState.Off)
			return;

		continuousFlickerActive = false;
		continuousFlickerRequested = false;
		StartFlicker(duration, false, TransitionMode.FlickerOff);
	}

	[ObserversRpc]
	private void ToggleObserversRpc()
	{
		if (IsTransitioning())
			return;

		if (stableState == LightStableState.On)
		{
			continuousFlickerActive = false;
			continuousFlickerRequested = false;
			ApplyOffLocal(true);
		}
		else
		{
			ApplyOnLocal(true);

			if (continuousFlickerRequested)
				TryStartContinuousFlickerLocal();
		}
	}

	[ObserversRpc]
	private void StartContinuousFlickerInternalObserversRpc()
	{
		continuousFlickerRequested = true;
		TryStartContinuousFlickerLocal();
	}

	[ObserversRpc]
	private void StopContinuousFlickerInternalObserversRpc()
	{
		continuousFlickerRequested = false;

		if (IsTransitioning())
			return;

		if (!continuousFlickerActive)
			return;

		continuousFlickerActive = false;

		if (stableState == LightStableState.On)
			ApplyOnLocal(false);
		else
			ApplyOffLocal(false);
	}

	private bool IsTransitioning()
	{
		return transitionMode != TransitionMode.None;
	}

	private void TryStartContinuousFlickerLocal()
	{
		if (IsTransitioning())
			return;

		if (continuousFlickerActive)
			return;

		if (stableState == LightStableState.Off)
			return;

		continuousFlickerActive = true;
		StopRoutine();
		_routine = StartCoroutine(ContinuousFlickerRoutine());
	}

	private void ApplyOnLocal(bool playSound)
	{
		StopRoutine();
		StopFlickerLoopSound();
		transitionMode = TransitionMode.None;
		stableState = LightStableState.On;

		if (flickerLights == null || flickerLights.Length == 0)
			return;

		for (int i = 0; i < flickerLights.Length; i++)
		{
			Light l = flickerLights[i];
			if (l == null)
				continue;

			l.enabled = true;

			float oi = originalIntensities != null && i < originalIntensities.Length ? originalIntensities[i] : originalIntensity;
			l.intensity = oi;
		}

		if (playSound)
			PlayOneShotWithRandomPitch(turnOnClip);
	}

	private void ApplyOffLocal(bool playSound)
	{
		StopRoutine();
		StopFlickerLoopSound();
		transitionMode = TransitionMode.None;
		stableState = LightStableState.Off;

		if (flickerLights == null || flickerLights.Length == 0)
			return;

		for (int i = 0; i < flickerLights.Length; i++)
		{
			Light l = flickerLights[i];
			if (l == null)
				continue;

			l.intensity = 0f;
			l.enabled = false;
		}

		if (playSound)
			PlayOneShotWithRandomPitch(turnOffClip);
	}

	private void StartFlicker(float duration, bool endOn, TransitionMode mode)
	{
		if (IsTransitioning())
			return;

		StopRoutine();
		transitionMode = mode;

		if (mode == TransitionMode.FlickerOff)
			StartFlickerLoopSound();
		else
			StopFlickerLoopSound();

		_routine = StartCoroutine(FlickerRoutine(duration, endOn));
	}

	private void StopRoutine()
	{
		if (_routine != null)
		{
			StopCoroutine(_routine);
			_routine = null;
		}
	}

	private void StartFlickerLoopSound()
	{
		if (flickerAudioSource == null || flickerLoopClip == null)
			return;

		if (flickerAudioSource.clip != flickerLoopClip)
			flickerAudioSource.clip = flickerLoopClip;

		flickerAudioSource.loop = true;
		flickerAudioSource.pitch = Random.Range(minPitch, maxPitch);

		if (!flickerAudioSource.isPlaying)
			flickerAudioSource.Play();
	}

	private void StopFlickerLoopSound()
	{
		if (flickerAudioSource == null)
			return;

		if (flickerAudioSource.isPlaying)
			flickerAudioSource.Stop();
	}

	private void PlayOneShotWithRandomPitch(AudioClip clip)
	{
		if (flickerAudioSource == null || clip == null)
			return;

		flickerAudioSource.pitch = Random.Range(minPitch, maxPitch);
		flickerAudioSource.PlayOneShot(clip);
	}

	private IEnumerator FlickerRoutine(float duration, bool endOn)
	{
		if (flickerLights == null || flickerLights.Length == 0)
		{
			StopFlickerLoopSound();
			transitionMode = TransitionMode.None;
			yield break;
		}

		for (int i = 0; i < flickerLights.Length; i++)
		{
			Light l = flickerLights[i];
			if (l == null)
				continue;

			l.enabled = true;
		}

		isFlickering = true;
		flickerTimer = 0f;
		maxLightLerpTimer = 0f;

		while (maxLightLerpTimer < duration)
		{
			float progress = duration <= 0f ? 1f : maxLightLerpTimer / duration;
			float ceiling = endOn ? Mathf.SmoothStep(0f, 1f, progress) : Mathf.SmoothStep(1f, 0f, progress);
			ceiling = Mathf.Clamp01(ceiling);

			if (flickerTimer <= 0f)
			{
				isFlickering = !isFlickering;
				targetIntensity = isFlickering ? minIntensity : ceiling;
				flickerTimer = Random.Range(0f, flickerSpeed);
			}
			else
			{
				flickerTimer -= Time.deltaTime;
			}

			float t = Time.deltaTime * fadeSpeed;

			for (int i = 0; i < flickerLights.Length; i++)
			{
				Light l = flickerLights[i];
				if (l == null)
					continue;

				float oi = originalIntensities != null && i < originalIntensities.Length ? originalIntensities[i] : originalIntensity;
				float normalizedTarget = Mathf.Clamp01(targetIntensity);
				float desired = normalizedTarget * oi;
				desired = Mathf.Min(desired, oi);
				l.intensity = Mathf.Lerp(l.intensity, desired, t);
			}

			maxLightLerpTimer += Time.deltaTime;
			yield return null;
		}

		_routine = null;
		transitionMode = TransitionMode.None;
		StopFlickerLoopSound();

		if (endOn)
			ApplyOnLocal(false);
		else
			ApplyOffLocal(false);
	}

	private IEnumerator ContinuousFlickerRoutine()
	{
		if (flickerLights == null || flickerLights.Length == 0)
		{
			continuousFlickerActive = false;
			StopFlickerLoopSound();
			_routine = null;
			yield break;
		}

		StartFlickerLoopSound();

		for (int i = 0; i < flickerLights.Length; i++)
		{
			Light l = flickerLights[i];
			if (l == null)
				continue;

			l.enabled = true;
		}

		isFlickering = true;
		targetIntensity = minIntensity;
		flickerTimer = 0f;

		maxLightLerpTime = Random.Range(minMaxLightLerpTime, maxMaxLightLerpTime);
		maxIntensity = 1f;
		targetMaxIntensity = 1f;
		maxLightLerpTimer = 0f;

		while (continuousFlickerActive && continuousFlickerRequested && stableState == LightStableState.On)
		{
			if (flickerTimer <= 0f)
			{
				isFlickering = !isFlickering;
				targetIntensity = isFlickering ? minIntensity : maxIntensity;
				flickerTimer = Random.Range(0f, flickerSpeed);
			}
			else
			{
				flickerTimer -= Time.deltaTime;
			}

			float t = Time.deltaTime * fadeSpeed;

			for (int i = 0; i < flickerLights.Length; i++)
			{
				Light l = flickerLights[i];
				if (l == null)
					continue;

				float oi = originalIntensities != null && i < originalIntensities.Length ? originalIntensities[i] : originalIntensity;
				float desired = Mathf.Lerp(minIntensity * oi, maxIntensity * oi, maxIntensity <= 0f ? 1f : targetIntensity / maxIntensity);
				desired = Mathf.Min(desired, oi);
				l.intensity = Mathf.Lerp(l.intensity, desired, t);
			}

			maxLightLerpTimer += Time.deltaTime;

			if (maxLightLerpTimer >= maxLightLerpTime)
			{
				maxLightLerpTimer = 0f;
				maxLightLerpTime = Random.Range(minMaxLightLerpTime, maxMaxLightLerpTime);
				targetMaxIntensity = Random.Range(minIntensity, 1f);
			}

			maxIntensity = Mathf.Lerp(maxIntensity, targetMaxIntensity, Time.deltaTime);

			yield return null;
		}

		continuousFlickerActive = false;
		StopFlickerLoopSound();
		_routine = null;

		if (stableState == LightStableState.On)
			ApplyOnLocal(false);
		else
			ApplyOffLocal(false);
	}

	private void CacheAuthoredIntensities()
	{
		if (flickerLights == null || flickerLights.Length == 0)
		{
			originalIntensities = new float[0];
			return;
		}

		originalIntensities = new float[flickerLights.Length];

		float sum = 0f;
		int count = 0;

		for (int i = 0; i < flickerLights.Length; i++)
		{
			Light l = flickerLights[i];
			if (l == null)
				continue;

			originalIntensities[i] = l.intensity;
			sum += l.intensity;
			count++;
		}

		if (count > 0)
			originalIntensity = sum / count;
	}

	/// <summary>
	/// Applies a light state only on this client without sending an RPC.
	/// Used to synchronize late-joining clients with the global power state.
	/// </summary>
	public void ApplyImmediateStateLocal(bool on)
	{
		if (on)
			ApplyOnLocal(false);
		else
			ApplyOffLocal(false);
	}

	public bool IsOn()
	{
		return stableState == LightStableState.On;
	}

	public void SetLights(List<Light> newLights)
	{
		if (newLights == null || newLights.Count == 0)
		{
			flickerLights = new Light[0];
			originalIntensities = new float[0];
			continuousFlickerActive = false;
			continuousFlickerRequested = false;
			StopRoutine();
			StopFlickerLoopSound();
			stableState = LightStableState.Off;
			return;
		}

		flickerLights = newLights.ToArray();

		CacheAuthoredIntensities();

		maxIntensity = 1f;
		targetMaxIntensity = 1f;

		continuousFlickerActive = false;
		continuousFlickerRequested = false;

		ApplyOffLocal(false);
	}
}