using FishNet.Object;
using UnityEngine;

public sealed class BugItemNetworked : NetworkBehaviour
{
    [SerializeField] private NetworkObject bugPrefab;

    public void RequestPlaceBug(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner)
            return;

        PlaceBugServerRpc(position, rotation);
    }

    public void RequestActivateBug(NetworkObject bugObject)
    {
        if (!IsOwner || bugObject == null)
            return;

        ActivateBugServerRpc(bugObject);
    }

    [ServerRpc]
    private void PlaceBugServerRpc(Vector3 position, Quaternion rotation)
    {
        if (bugPrefab == null)
            return;

        NetworkObject spawned = Instantiate(bugPrefab, position, rotation);
        Spawn(spawned, Owner);
    }

    [ServerRpc]
    private void ActivateBugServerRpc(NetworkObject bugObject)
    {
        if (bugObject == null)
            return;

        BugProp bug = bugObject.GetComponent<BugProp>();
        bug?.ActivateServer();
    }
}
