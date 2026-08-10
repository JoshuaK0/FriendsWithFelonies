using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owner-local list of cameras available to this player. When a team provider
/// is assigned, all cameras for that team are shown; otherwise it falls back to
/// cameras owned by the local hotbar connection.
/// </summary>
public sealed class StickyCameraManager : MonoBehaviour
{
    [SerializeField] private NetHotbarInventory hotbar;
    [SerializeField] private MonoBehaviour teamIdSource;

    private readonly List<StickyCameraProp> cameraList = new();

    private void Reset()
    {
        hotbar = GetComponentInParent<NetHotbarInventory>();
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
        StickyCameraProp[] cameras = FindObjectsOfType<StickyCameraProp>();
        int teamId = ResolveTeamId();

        for (int i = 0; i < cameras.Length; i++)
        {
            StickyCameraProp camera = cameras[i];
            if (camera == null || camera.NetworkObject == null)
                continue;

            bool matches = teamId >= 0
                ? camera.TeamId == teamId
                : hotbar != null && camera.NetworkObject.Owner == hotbar.Owner;

            if (matches)
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
        if (teamIdSource is ITeamIdProvider configuredProvider)
            return configuredProvider.TeamId;

        ITeamIdProvider provider = ComponentInterfaceUtility.FindInChildren<ITeamIdProvider>(gameObject);
        return provider != null ? provider.TeamId : -1;
    }
}
