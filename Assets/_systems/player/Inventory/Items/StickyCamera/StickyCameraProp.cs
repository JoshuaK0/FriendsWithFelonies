using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// Networked deployable camera.
/// Team ID, head direction and light state are synchronized by the server.
/// 
/// The light is enabled whenever at least one client is currently
/// viewing this camera, either fullscreen or minimized.
/// </summary>
public sealed class StickyCameraProp : NetworkBehaviour
{
	public static event Action RegistryChanged;

	[Header("Camera Transforms")]
	[Tooltip("Position used by the survey rig. This may be a child of the rotating camera head.")]
	[SerializeField] private Transform lookPoint;

	[Tooltip(
		"Non-rotating reference transform for the camera's mounted orientation. " +
		"Its forward direction should point outward from the surface the camera is mounted to. " +
		"Do not make this a child of cameraHead.")]
	[SerializeField] private Transform surveyBase;

	[Tooltip("The transform that rotates locally from the synchronized pitch/yaw.")]
	[SerializeField] private Transform cameraHead;

	[Header("Light")]
	[Tooltip("Beam/light object enabled whenever at least one client is viewing this camera.")]
	[SerializeField] private GameObject lightParent;

	[Header("Look")]
	[SerializeField, Range(0f, 89f)] private float pitchLimit = 85f;

	private readonly SyncVar<int> teamId = new(-1);
	private readonly SyncVar<Vector2> lookDirection = new(Vector2.zero);
	private readonly SyncVar<bool> lightEnabled = new(false);

	/*
     * Server-only collection of clients currently viewing this camera.
     *
     * This is important because using a single bool directly would mean:
     *
     * Client A opens camera  -> true
     * Client B opens camera  -> true
     * Client A closes camera -> false
     *
     * ...even though Client B is still watching.
     */
	private readonly HashSet<int> activeViewers = new();

	public int TeamId => teamId.Value;
	public Vector2 LookDirection => lookDirection.Value;
	public bool LightEnabled => lightEnabled.Value;

	public Transform LookPoint =>
		lookPoint != null
			? lookPoint
			: transform;

	/// <summary>
	/// World-space rotation representing the camera's neutral mounted direction.
	/// </summary>
	public Quaternion SurveyBaseRotation =>
		surveyBase != null
			? surveyBase.rotation
			: transform.rotation;

	public override void OnStartClient()
	{
		base.OnStartClient();

		teamId.OnChange += OnTeamIdChanged;
		lookDirection.OnChange += OnLookDirectionChanged;
		lightEnabled.OnChange += OnLightEnabledChanged;

		ApplyLookDirection(lookDirection.Value);
		ApplyLightState(lightEnabled.Value);

		RegistryChanged?.Invoke();
	}

	public override void OnStopClient()
	{
		teamId.OnChange -= OnTeamIdChanged;
		lookDirection.OnChange -= OnLookDirectionChanged;
		lightEnabled.OnChange -= OnLightEnabledChanged;

		RegistryChanged?.Invoke();

		base.OnStopClient();
	}

	public override void OnStopServer()
	{
		activeViewers.Clear();

		base.OnStopServer();
	}

	[Server]
	public void InitializeServer(int newTeamId)
	{
		teamId.Value = newTeamId;
		lookDirection.Value = Vector2.zero;

		activeViewers.Clear();
		lightEnabled.Value = false;
	}

	// -------------------------------------------------------------------------
	// LOOK
	// -------------------------------------------------------------------------

	public void RequestSetLookDirection(Vector2 direction)
	{
		if (!IsOwner)
			return;

		NormalizeLookDirection(ref direction);

		// Apply immediately for the owner so the physical head stays locked
		// to the survey view instead of waiting for the network round trip.
		ApplyLookDirection(direction);

		SetLookDirectionServerRpc(direction);
	}

	[ServerRpc]
	private void SetLookDirectionServerRpc(Vector2 direction)
	{
		NormalizeLookDirection(ref direction);
		lookDirection.Value = direction;
	}

	// -------------------------------------------------------------------------
	// LIGHT / VIEWING STATE
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tells the server whether this client is currently viewing this camera.
	///
	/// Unlike look control, this intentionally does NOT require ownership.
	/// Any client capable of viewing the camera needs to be able to register
	/// themselves so the beam is visible to everyone.
	/// </summary>
	public void RequestSetLight(bool enabled)
	{
		SetLightServerRpc(enabled);
	}

	[ServerRpc(RequireOwnership = false)]
	private void SetLightServerRpc(
		bool enabled,
		NetworkConnection sender = null)
	{
		if (sender == null)
			return;

		int clientId = sender.ClientId;

		if (enabled)
		{
			activeViewers.Add(clientId);
		}
		else
		{
			activeViewers.Remove(clientId);
		}

		// Beam stays enabled for EVERY client as long as at least
		// one client is still viewing this camera.
		lightEnabled.Value = activeViewers.Count > 0;
	}

	// -------------------------------------------------------------------------
	// SYNCVAR CALLBACKS
	// -------------------------------------------------------------------------

	private void OnTeamIdChanged(
		int previous,
		int next,
		bool asServer)
	{
		RegistryChanged?.Invoke();
	}

	private void OnLookDirectionChanged(
		Vector2 previous,
		Vector2 next,
		bool asServer)
	{
		ApplyLookDirection(next);
	}

	private void OnLightEnabledChanged(
		bool previous,
		bool next,
		bool asServer)
	{
		ApplyLightState(next);
	}

	// -------------------------------------------------------------------------
	// VISUALS
	// -------------------------------------------------------------------------

	private void ApplyLookDirection(Vector2 direction)
	{
		if (cameraHead == null)
			return;

		Quaternion yaw =
			Quaternion.Euler(
				0f,
				direction.y,
				0f);

		Quaternion pitch =
			Quaternion.Euler(
				direction.x,
				0f,
				0f);

		cameraHead.rotation =
			SurveyBaseRotation *
			yaw *
			pitch;
	}

	private void NormalizeLookDirection(
		ref Vector2 direction)
	{
		direction.x =
			Mathf.Clamp(
				direction.x,
				-pitchLimit,
				pitchLimit);

		direction.y =
			Mathf.Repeat(
				direction.y + 180f,
				360f) - 180f;
	}

	private void ApplyLightState(bool enabled)
	{
		if (lightParent != null)
			lightParent.SetActive(enabled);
	}

	// -------------------------------------------------------------------------
	// COMPATIBILITY
	// -------------------------------------------------------------------------

	public int GetCameraTeam() =>
		TeamId;

	public Transform GetLookPoint() =>
		LookPoint;

	public Vector2 GetLookDir() =>
		LookDirection;

	public void SetPropLookDir(Vector2 direction) =>
		RequestSetLookDirection(direction);

	public void SetLightEnable(bool enabled) =>
		RequestSetLight(enabled);
}
