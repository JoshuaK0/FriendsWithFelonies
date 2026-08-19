using FishNet.Object;
using UnityEngine;

public sealed class StickyCameraItemNetworked : NetworkBehaviour
{
    [SerializeField] private NetworkObject stickyCameraPrefab;

    public void RequestPlaceStickyCamera(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner)
            return;

        PlaceStickyCameraServerRpc(position, rotation);
    }

    [ServerRpc]
    private void PlaceStickyCameraServerRpc(Vector3 position, Quaternion rotation)
    {
        if (stickyCameraPrefab == null)
            return;

        NetworkObject spawned = Instantiate(stickyCameraPrefab, position, rotation);
        Spawn(spawned, Owner);

        StickyCameraProp cameraProp = spawned.GetComponent<StickyCameraProp>();
        cameraProp?.InitializeServer(GetTeamId());
    }

    private int GetTeamId()
    {
        return MyClient.Instance.TeamId;
    }
}
