using FishNet.Object;
using UnityEngine;

public sealed class SmokeBombItemNetworked : NetworkBehaviour
{
    [SerializeField] private NetworkObject smokeBombPrefab;
    [SerializeField, Min(0f)] private float throwVelocity = 10f;
    [SerializeField, Min(0f)] private float throwTorque = 3f;

    public void RequestThrowSmokeBomb(Vector3 position, Quaternion rotation, Vector3 direction)
    {
        if (!IsOwner)
            return;

        ThrowSmokeBombServerRpc(position, rotation, direction);
    }

    [ServerRpc]
    private void ThrowSmokeBombServerRpc(Vector3 position, Quaternion rotation, Vector3 direction)
    {
        if (smokeBombPrefab == null)
            return;

        NetworkObject spawned = Instantiate(smokeBombPrefab, position, rotation);
        Spawn(spawned, Owner);

        Rigidbody body = spawned.GetComponent<Rigidbody>();
        if (body == null)
            return;

        body.AddForce(direction.normalized * throwVelocity, ForceMode.VelocityChange);
        body.AddTorque(Random.insideUnitSphere * throwTorque, ForceMode.VelocityChange);
    }
}
