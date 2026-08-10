using FishNet.Object;
using UnityEngine;

public sealed class GeigerCounterItemNetworked : NetworkBehaviour
{
    [SerializeField] private AudioSource networkAudioSource;
    [SerializeField] private AudioClip clickClip;

    public void RequestGeigerPing(float pitch)
    {
        if (!IsOwner)
            return;

        GeigerPingServerRpc(pitch);
    }

    [ServerRpc]
    private void GeigerPingServerRpc(float pitch)
    {
        GeigerPingObserversRpc(pitch);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void GeigerPingObserversRpc(float pitch)
    {
        if (networkAudioSource == null || clickClip == null)
            return;

        networkAudioSource.pitch = pitch;
        networkAudioSource.PlayOneShot(clickClip);
    }
}
