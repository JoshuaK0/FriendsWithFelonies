using FishNet.Object;
using UnityEngine;

public sealed class CannonItemNetworked : NetworkBehaviour
{
    [SerializeField] private NetworkObject cannonPrefab;

    public void RequestPlaceCannon(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner)
            return;

        PlaceCannonServerRpc(position, rotation);
    }

    [ServerRpc]
    private void PlaceCannonServerRpc(Vector3 position, Quaternion rotation)
    {
        if (cannonPrefab == null)
            return;

        NetworkObject spawned = Instantiate(cannonPrefab, position, rotation);
        Spawn(spawned, Owner);
    }
}
