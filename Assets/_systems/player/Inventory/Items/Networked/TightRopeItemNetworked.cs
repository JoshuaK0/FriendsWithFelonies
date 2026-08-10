using FishNet.Object;
using UnityEngine;

public sealed class TightRopeItemNetworked : NetworkBehaviour
{
    [SerializeField] private NetworkObject tightRopePrefab;

    public void RequestPlaceTightRope(Vector3 firstAnchor, Vector3 secondAnchor)
    {
        if (!IsOwner)
            return;

        PlaceTightRopeServerRpc(firstAnchor, secondAnchor);
    }

    [ServerRpc]
    private void PlaceTightRopeServerRpc(Vector3 firstAnchor, Vector3 secondAnchor)
    {
        if (tightRopePrefab == null)
            return;

        NetworkObject spawned = Instantiate(tightRopePrefab);
        Spawn(spawned, Owner);

        TightRopeProp rope = spawned.GetComponent<TightRopeProp>();
        rope?.InitializeServer(firstAnchor, secondAnchor);
    }
}
