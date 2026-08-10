using FishNet.Object;
using UnityEngine;

public sealed class UVFlashlightItemNetworked : NetworkBehaviour
{
    [SerializeField] private GameObject remoteUVFlashlightGameobject;

    public override void OnStartClient()
    {
        base.OnStartClient();
        SetUVRemoteFlashlight(false);
    }

    public void RequestSetUVFlashlight(bool enabled)
    {
        if (!IsOwner)
            return;

        SetUVFlashlightServerRpc(enabled);
    }

    [ServerRpc]
    private void SetUVFlashlightServerRpc(bool enabled)
    {
        SetUVFlashlightObserversRpc(enabled);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void SetUVFlashlightObserversRpc(bool enabled)
    {
        SetUVRemoteFlashlight(enabled);
    }

    private void SetUVRemoteFlashlight(bool enabled)
    {
        if (remoteUVFlashlightGameobject != null)
            remoteUVFlashlightGameobject.SetActive(enabled);
    }
}
