using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owner-local list of cameras available to this player.
/// Only cameras belonging to the player's team are included.
/// </summary>
public sealed class StickyCameraManager : MonoBehaviour
{
	public static StickyCameraManager Instance { get; private set; }

	[SerializeField, Min(0.05f)]
	private float emptyListRetryInterval = 0.25f;

	private readonly List<StickyCameraProp> cameraList = new();

	private int cachedTeamId = int.MinValue;
	private float nextRetryTime;

	private void Awake()
	{
		Instance = this;
	}

	private void OnEnable()
	{
		StickyCameraProp.RegistryChanged += RefreshCameras;

		cachedTeamId = int.MinValue;
		nextRetryTime = 0f;

		RefreshCameras();
	}

	private void OnDisable()
	{
		StickyCameraProp.RegistryChanged -= RefreshCameras;
	}

	private void Update()
	{
		int currentTeamId = ResolveTeamId();

		// Remote clients may receive their team ID after this component
		// and the camera props have already started.
		if (currentTeamId != cachedTeamId)
		{
			RefreshCameras();
			return;
		}

		// Retry if the original registry notification happened before
		// the client's team data was ready.
		if (currentTeamId >= 0 &&
			cameraList.Count == 0 &&
			Time.unscaledTime >= nextRetryTime)
		{
			RefreshCameras();
		}
	}

	public void RefreshCameras()
	{
		cameraList.Clear();

		cachedTeamId = ResolveTeamId();
		nextRetryTime =
			Time.unscaledTime + emptyListRetryInterval;

		if (cachedTeamId < 0)
			return;

		StickyCameraProp[] cameras =
			FindObjectsOfType<StickyCameraProp>();

		for (int i = 0; i < cameras.Length; i++)
		{
			StickyCameraProp camera = cameras[i];

			if (camera == null ||
				camera.NetworkObject == null ||
				!camera.IsClientStarted)
			{
				continue;
			}

			if (camera.TeamId == cachedTeamId)
				cameraList.Add(camera);
		}
	}

	public List<StickyCameraProp> GetCameras()
	{
		RemoveDestroyedCameras();

		int currentTeamId = ResolveTeamId();

		if (currentTeamId != cachedTeamId ||
			(currentTeamId >= 0 &&
			 cameraList.Count == 0 &&
			 Time.unscaledTime >= nextRetryTime))
		{
			RefreshCameras();
		}

		return cameraList;
	}

	private void RemoveDestroyedCameras()
	{
		for (int i = cameraList.Count - 1; i >= 0; i--)
		{
			if (cameraList[i] == null)
				cameraList.RemoveAt(i);
		}
	}

	private int ResolveTeamId()
	{
		if (MyClient.Instance == null)
			return -1;

		return MyClient.Instance.TeamId;
	}

	private void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}
}