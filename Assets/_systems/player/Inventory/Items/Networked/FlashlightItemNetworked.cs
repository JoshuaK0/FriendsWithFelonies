using FishNet.Object;
using UnityEngine;

public sealed class FlashlightItemNetworked : NetworkBehaviour
{
    [SerializeField] private GameObject remoteFlashlightObject;

    public override void OnStartClient()
    {
        base.OnStartClient();
        SetRemoteFlashlight(false);
    }

    public void RequestSetFlashlight(bool enabled)
    {
        if (!IsOwner)
            return;

        SetFlashlightServerRpc(enabled);
    }

    [ServerRpc]
    private void SetFlashlightServerRpc(bool enabled)
    {
        SetFlashlightObserversRpc(enabled);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void SetFlashlightObserversRpc(bool enabled)
    {
        SetRemoteFlashlight(enabled);
    }

    private void SetRemoteFlashlight(bool enabled)
    {
        if (remoteFlashlightObject != null)
            remoteFlashlightObject.SetActive(enabled);
    }
}
