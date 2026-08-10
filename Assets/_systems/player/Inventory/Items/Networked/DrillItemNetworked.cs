using FishNet.Object;
using UnityEngine;

public sealed class DrillItemNetworked : NetworkBehaviour
{
    [SerializeField] private NetworkObject drillPrefab;

    public void RequestPlaceDrill(Vector3 position, Vector3 hitNormal)
    {
        if (!IsOwner)
            return;

        PlaceDrillServerRpc(position, hitNormal);
    }

    [ServerRpc]
    private void PlaceDrillServerRpc(Vector3 position, Vector3 hitNormal)
    {
        if (drillPrefab == null)
            return;

        Quaternion rotation = Quaternion.LookRotation(-hitNormal, Vector3.up);
        NetworkObject spawned = Instantiate(drillPrefab, position, rotation);
        Spawn(spawned, Owner);
    }
}
