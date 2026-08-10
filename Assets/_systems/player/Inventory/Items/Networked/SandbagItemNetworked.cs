using FishNet.Object;
using UnityEngine;

public sealed class SandbagItemNetworked : NetworkBehaviour
{
    [SerializeField] private NetworkObject sandbagPrefab;

    public void RequestPlaceSandbag(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner)
            return;

        PlaceSandbagServerRpc(position, rotation);
    }

    [ServerRpc]
    private void PlaceSandbagServerRpc(Vector3 position, Quaternion rotation)
    {
        if (sandbagPrefab == null)
            return;

        NetworkObject spawned = Instantiate(sandbagPrefab, position, rotation);
        Spawn(spawned, Owner);
    }
}
