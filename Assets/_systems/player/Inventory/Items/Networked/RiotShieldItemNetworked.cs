using FishNet.Object;
using UnityEngine;

public sealed class RiotShieldItemNetworked : NetworkBehaviour
{
    [SerializeField] private RiotShieldNetworkEnabler remoteShield;

    public override void OnStartClient()
    {
        base.OnStartClient();
        remoteShield?.SetShieldEnabled(false);
    }

    public void RequestSetRiotShield(bool enabled)
    {
        if (!IsOwner)
            return;

        SetRiotShieldServerRpc(enabled);
    }

    [ServerRpc]
    private void SetRiotShieldServerRpc(bool enabled)
    {
        SetRiotShieldObserversRpc(enabled);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void SetRiotShieldObserversRpc(bool enabled)
    {
        remoteShield?.SetShieldEnabled(enabled);
    }
}
