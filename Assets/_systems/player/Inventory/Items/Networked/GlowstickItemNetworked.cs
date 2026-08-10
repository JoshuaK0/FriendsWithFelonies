using FishNet.Object;
using UnityEngine;

public sealed class GlowstickItemNetworked : NetworkBehaviour
{
    [SerializeField] private NetworkObject glowstickPrefab;
    [SerializeField, Min(0f)] private float throwVelocity = 10f;
    [SerializeField, Min(0f)] private float throwTorque = 3f;

    public void RequestThrowGlowstick(Vector3 position, Quaternion rotation, Vector3 direction)
    {
        if (!IsOwner)
            return;

        ThrowGlowstickServerRpc(position, rotation, direction);
    }

    [ServerRpc]
    private void ThrowGlowstickServerRpc(Vector3 position, Quaternion rotation, Vector3 direction)
    {
        if (glowstickPrefab == null)
            return;

        NetworkObject spawned = Instantiate(glowstickPrefab, position, rotation);
        Spawn(spawned, Owner);

        Rigidbody body = spawned.GetComponent<Rigidbody>();
        if (body == null)
            return;

        body.AddForce(direction.normalized * throwVelocity, ForceMode.VelocityChange);
        body.AddTorque(Random.insideUnitSphere * throwTorque, ForceMode.VelocityChange);
    }
}
