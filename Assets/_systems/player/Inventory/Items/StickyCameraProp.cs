using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// Networked deployable camera. It is owned by the player who placed it, while
/// team ID, head direction and light state are synchronized by the server.
/// </summary>
public sealed class StickyCameraProp : NetworkBehaviour
{
    public static event Action RegistryChanged;

    [SerializeField] private Transform lookPoint;
    [SerializeField] private Transform cameraHead;
    [SerializeField] private GameObject lightParent;
    [SerializeField, Range(0f, 89f)] private float pitchLimit = 85f;

    private readonly SyncVar<int> teamId = new(-1);
    private readonly SyncVar<Vector2> lookDirection = new(Vector2.zero);
    private readonly SyncVar<bool> lightEnabled = new(false);

    public int TeamId => teamId.Value;
    public Vector2 LookDirection => lookDirection.Value;
    public Transform LookPoint => lookPoint != null ? lookPoint : transform;

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

    [Server]
    public void InitializeServer(int newTeamId)
    {
        teamId.Value = newTeamId;
        lookDirection.Value = Vector2.zero;
        lightEnabled.Value = false;
    }

    public void RequestSetLookDirection(Vector2 direction)
    {
        if (!IsOwner)
            return;

        SetLookDirectionServerRpc(direction);
    }

    public void RequestSetLight(bool enabled)
    {
        if (!IsOwner)
            return;

        SetLightServerRpc(enabled);
    }

    [ServerRpc]
    private void SetLookDirectionServerRpc(Vector2 direction)
    {
        direction.x = Mathf.Clamp(direction.x, -pitchLimit, pitchLimit);
        direction.y = Mathf.Repeat(direction.y + 180f, 360f) - 180f;
        lookDirection.Value = direction;
    }

    [ServerRpc]
    private void SetLightServerRpc(bool enabled)
    {
        lightEnabled.Value = enabled;
    }

    private void OnTeamIdChanged(int previous, int next, bool asServer)
    {
        RegistryChanged?.Invoke();
    }

    private void OnLookDirectionChanged(Vector2 previous, Vector2 next, bool asServer)
    {
        ApplyLookDirection(next);
    }

    private void OnLightEnabledChanged(bool previous, bool next, bool asServer)
    {
        ApplyLightState(next);
    }

    private void ApplyLookDirection(Vector2 direction)
    {
        if (cameraHead != null)
            cameraHead.localRotation = Quaternion.Euler(direction.x, direction.y, 0f);
    }

    private void ApplyLightState(bool enabled)
    {
        if (lightParent != null)
            lightParent.SetActive(enabled);
    }

    // Compatibility methods for existing scene references.
    public int GetCameraTeam() => TeamId;
    public Transform GetLookPoint() => LookPoint;
    public Vector2 GetLookDir() => LookDirection;
    public void SetPropLookDir(Vector2 direction) => RequestSetLookDirection(direction);
    public void SetLightEnable(bool enabled) => RequestSetLight(enabled);
}
