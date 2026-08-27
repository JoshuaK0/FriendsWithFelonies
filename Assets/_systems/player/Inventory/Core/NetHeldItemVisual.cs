using FishNet.Object;
using UnityEngine;

/// <summary>
/// Replicates a presentation-only held prefab to non-owning clients.
/// The owner continues to use the local held prefab created by NetHotbarInventory.
/// </summary>
public sealed class NetHeldItemVisual : NetworkBehaviour
{
    [SerializeField] private ItemRegistry registry;
    [SerializeField] private Transform remoteHoldPoint;

    private GameObject remoteInstance;

    public void SetHeldItem(int itemId)
    {
        if (!IsOwner)
            return;

        SetHeldItemServerRpc(itemId);
    }

    [ServerRpc]
    private void SetHeldItemServerRpc(int itemId)
    {
        SetHeldItemObserversRpc(itemId);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void SetHeldItemObserversRpc(int itemId)
    {
        RefreshRemoteVisual(itemId);
    }

    private void RefreshRemoteVisual(int itemId)
    {
        if (remoteInstance != null)
        {
            Destroy(remoteInstance);
            remoteInstance = null;
        }

        if (registry == null || remoteHoldPoint == null || !registry.IsValidItemId(itemId))
            return;

        GameObject prefab = registry.RemoteHeldPrefabOf(itemId);
        if (prefab == null)
            return;

        remoteInstance = Instantiate(prefab, remoteHoldPoint);
        remoteInstance.transform.localPosition = Vector3.zero;
        remoteInstance.transform.localRotation = Quaternion.identity;
    }
}
