using FishNet.Object;
using UnityEngine;

public sealed class TripWireItemNetworked : NetworkBehaviour
{
    [SerializeField] private NetworkObject tripWirePrefab;
    [Tooltip("Optional component implementing ITeamIdProvider.")]
    [SerializeField] private MonoBehaviour teamIdSource;

    public void RequestPlaceTripWire(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner)
            return;

        PlaceTripWireServerRpc(position, rotation);
    }

    [ServerRpc]
    private void PlaceTripWireServerRpc(Vector3 position, Quaternion rotation)
    {
        if (tripWirePrefab == null)
            return;

        NetworkObject spawned = Instantiate(tripWirePrefab, position, rotation);
        Spawn(spawned, Owner);

        TripWireProp tripWire = spawned.GetComponent<TripWireProp>();
        tripWire?.InitializeServer(GetTeamId());
    }

    private int GetTeamId()
    {
        return teamIdSource is ITeamIdProvider provider ? provider.TeamId : -1;
    }
}
