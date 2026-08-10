using FishNet.Object;
using UnityEngine;

public sealed class LadderItemNetworked : NetworkBehaviour
{
    [SerializeField] private NetworkObject ladderPrefab;

    public void RequestPlaceLadder(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner)
            return;

        PlaceLadderServerRpc(position, rotation);
    }

    [ServerRpc]
    private void PlaceLadderServerRpc(Vector3 position, Quaternion rotation)
    {
        if (ladderPrefab == null)
            return;

        NetworkObject spawned = Instantiate(ladderPrefab, position, rotation);
        Spawn(spawned, Owner);
    }
}
