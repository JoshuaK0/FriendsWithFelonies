using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owner-local list of cameras available to this player.
/// Only cameras belonging to the player's team are included.
/// </summary>
public sealed class StickyCameraManager : MonoBehaviour
{
    public static StickyCameraManager Instance;

    private readonly List<StickyCameraProp> cameraList = new();

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        StickyCameraProp.RegistryChanged += RefreshCameras;
        RefreshCameras();
    }

    private void OnDisable()
    {
        StickyCameraProp.RegistryChanged -= RefreshCameras;
    }

    public void RefreshCameras()
    {
        cameraList.Clear();

        int teamId = ResolveTeamId();

        if (teamId < 0)
            return;

        StickyCameraProp[] cameras =
            FindObjectsOfType<StickyCameraProp>();

        for (int i = 0; i < cameras.Length; i++)
        {
            StickyCameraProp camera =
                cameras[i];

            if (camera == null ||
                camera.NetworkObject == null)
            {
                continue;
            }

            if (camera.TeamId == teamId)
                cameraList.Add(camera);
        }
    }

    public List<StickyCameraProp> GetCameras()
    {
        for (int i = cameraList.Count - 1; i >= 0; i--)
        {
            if (cameraList[i] == null)
                cameraList.RemoveAt(i);
        }

        return cameraList;
    }

    private int ResolveTeamId()
    {
        return MyClient.Instance.TeamId;
    }
}
