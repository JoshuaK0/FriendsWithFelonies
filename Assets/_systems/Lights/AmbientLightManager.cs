using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class AmbientLightManager : NetworkBehaviour
{
	public static AmbientLightManager Instance { get; private set; }

	[Header("Normal Ambient")]
	[SerializeField]
	private AmbientMode normalAmbientMode = AmbientMode.Flat;

	[SerializeField]
	private Color normalAmbientColor = Color.white;

	[Header("Lights Off Ambient")]
	[SerializeField]
	private AmbientMode lightsOffAmbientMode = AmbientMode.Flat;

	[SerializeField]
	private Color lightsOffAmbientColor = Color.black;

	[Header("Transition")]
	[SerializeField]
	private Color blackColor = Color.black;

	[SerializeField, Min(0f)]
	private float defaultLerpDuration = 1f;

	private readonly SyncVar<AmbientMode> syncedAmbientMode = new();
	private readonly SyncVar<Color> syncedAmbientColor = new();

	private bool hasLocalOverride;

	private Coroutine ambientLerpRoutine;

	public AmbientMode SyncedAmbientMode =>
		syncedAmbientMode.Value;

	public Color SyncedAmbientColor =>
		syncedAmbientColor.Value;

	public Color BlackColor =>
		blackColor;

	public bool HasLocalOverride =>
		hasLocalOverride;

	private void Awake()
	{
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

		syncedAmbientMode.Value =
			normalAmbientMode;

		syncedAmbientColor.Value =
			normalAmbientColor;
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		ApplySyncedStateInstantLocal();
	}

	// =========================================================
	// Synced World State
	// =========================================================

	public void SetNormalServer()
	{
		SetAmbientServer(
			normalAmbientMode,
			normalAmbientColor
		);
	}

	public void SetNormalServerLerped()
	{
		SetAmbientServerLerped(
			normalAmbientMode,
			normalAmbientColor,
			defaultLerpDuration
		);
	}

	public void SetNormalServerLerped(float duration)
	{
		SetAmbientServerLerped(
			normalAmbientMode,
			normalAmbientColor,
			duration
		);
	}

	public void SetLightsOffServer()
	{
		SetAmbientServer(
			lightsOffAmbientMode,
			lightsOffAmbientColor
		);
	}

	public void SetLightsOffServerLerped()
	{
		SetAmbientServerLerped(
			lightsOffAmbientMode,
			lightsOffAmbientColor,
			defaultLerpDuration
		);
	}

	public void SetLightsOffServerLerped(float duration)
	{
		SetAmbientServerLerped(
			lightsOffAmbientMode,
			lightsOffAmbientColor,
			duration
		);
	}

	/// <summary>
	/// Changes the synchronized ambient state immediately.
	/// </summary>
	public void SetAmbientServer(
		AmbientMode mode,
		Color color)
	{
		if (!IsServer)
		{
			Debug.LogWarning(
				"Ambient state can only be changed by the server."
			);

			return;
		}

		syncedAmbientMode.Value = mode;
		syncedAmbientColor.Value = color;

		ApplyAmbientInstantObserversRpc(
			mode,
			color
		);
	}

	/// <summary>
	/// Changes the synchronized ambient state and tells
	/// clients to lerp toward the new color.
	/// </summary>
	public void SetAmbientServerLerped(
		AmbientMode mode,
		Color color,
		float duration)
	{
		if (!IsServer)
		{
			Debug.LogWarning(
				"Ambient state can only be changed by the server."
			);

			return;
		}

		syncedAmbientMode.Value = mode;
		syncedAmbientColor.Value = color;

		ApplyAmbientLerpedObserversRpc(
			mode,
			color,
			duration
		);
	}

	[ObserversRpc]
	private void ApplyAmbientInstantObserversRpc(
		AmbientMode mode,
		Color color)
	{
		if (hasLocalOverride)
			return;

		ApplyInstantLocal(
			mode,
			color
		);
	}

	[ObserversRpc]
	private void ApplyAmbientLerpedObserversRpc(
		AmbientMode mode,
		Color color,
		float duration)
	{
		if (hasLocalOverride)
			return;

		ApplyLerpedLocal(
			mode,
			color,
			duration
		);
	}

	// =========================================================
	// Local Overrides
	// =========================================================

	/// <summary>
	/// Applies an immediate client-local ambient override.
	/// Does not change the synchronized world state.
	/// </summary>
	public void SetLocalOverride(
		AmbientMode mode,
		Color color)
	{
		if (!IsClient)
			return;

		hasLocalOverride = true;

		ApplyInstantLocal(
			mode,
			color
		);
	}

	/// <summary>
	/// Applies a client-local override beginning at one color
	/// and lerping toward another.
	/// The starting color is applied immediately.
	/// </summary>
	public void SetLocalOverrideLerped(
		AmbientMode mode,
		Color fromColor,
		Color toColor,
		float duration)
	{
		if (!IsClient)
			return;

		hasLocalOverride = true;

		StopAmbientLerp();

		RenderSettings.ambientMode = mode;
		RenderSettings.ambientLight = fromColor;

		ambientLerpRoutine = StartCoroutine(
			LerpAmbientRoutine(
				fromColor,
				toColor,
				duration
			)
		);
	}

	/// <summary>
	/// Convenience method for effects which should begin
	/// completely black before fading into their target color.
	/// </summary>
	public void SetLocalOverrideFromBlack(
		AmbientMode mode,
		Color targetColor,
		float duration)
	{
		SetLocalOverrideLerped(
			mode,
			blackColor,
			targetColor,
			duration
		);
	}

	/// <summary>
	/// Removes the local override and instantly restores
	/// the latest synchronized world ambient state.
	/// </summary>
	public void ClearLocalOverride()
	{
		if (!IsClient)
			return;

		hasLocalOverride = false;

		StopAmbientLerp();

		ApplySyncedStateInstantLocal();
	}

	// =========================================================
	// Local Application
	// =========================================================

	private void ApplySyncedStateInstantLocal()
	{
		if (!IsClient)
			return;

		ApplyInstantLocal(
			syncedAmbientMode.Value,
			syncedAmbientColor.Value
		);
	}

	private void ApplyInstantLocal(
		AmbientMode mode,
		Color color)
	{
		StopAmbientLerp();

		RenderSettings.ambientMode = mode;
		RenderSettings.ambientLight = color;
	}

	private void ApplyLerpedLocal(
		AmbientMode mode,
		Color targetColor,
		float duration)
	{
		StopAmbientLerp();

		RenderSettings.ambientMode = mode;

		Color startColor =
			RenderSettings.ambientLight;

		ambientLerpRoutine = StartCoroutine(
			LerpAmbientRoutine(
				startColor,
				targetColor,
				duration
			)
		);
	}

	private IEnumerator LerpAmbientRoutine(
		Color startColor,
		Color targetColor,
		float duration)
	{
		if (duration <= 0f)
		{
			RenderSettings.ambientLight =
				targetColor;

			ambientLerpRoutine = null;
			yield break;
		}

		float elapsed = 0f;

		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;

			float t =
				Mathf.Clamp01(
					elapsed / duration
				);

			RenderSettings.ambientLight =
				Color.Lerp(
					startColor,
					targetColor,
					t
				);

			yield return null;
		}

		RenderSettings.ambientLight =
			targetColor;

		ambientLerpRoutine = null;
	}

	private void StopAmbientLerp()
	{
		if (ambientLerpRoutine == null)
			return;

		StopCoroutine(
			ambientLerpRoutine
		);

		ambientLerpRoutine = null;
	}
}