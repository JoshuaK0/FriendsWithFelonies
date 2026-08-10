using System.Collections;
using FishNet.Object;
using UnityEngine;

public sealed class StickyBombItemNetworked : NetworkBehaviour
{
    [SerializeField] private NetworkObject stickyBombPrefab;
    [SerializeField, Min(0f)] private float throwVelocity = 10f;
    [SerializeField, Min(0f)] private float throwTorque = 3f;
    [SerializeField] private Vector2 detonationDelayRange = new(0.05f, 0.2f);

    public void RequestThrowStickyBomb(Vector3 position, Quaternion rotation, Vector3 direction)
    {
        if (!IsOwner)
            return;

        ThrowStickyBombServerRpc(position, rotation, direction);
    }

    public void RequestDetonateAllStickyBombs()
    {
        if (!IsOwner)
            return;

        DetonateAllStickyBombsServerRpc();
    }

    public void RequestDetonateStickyBomb(NetworkObject bombObject)
    {
        if (!IsOwner || bombObject == null)
            return;

        DetonateStickyBombServerRpc(bombObject);
    }

    [ServerRpc]
    private void ThrowStickyBombServerRpc(Vector3 position, Quaternion rotation, Vector3 direction)
    {
        if (stickyBombPrefab == null)
            return;

        NetworkObject spawned = Instantiate(stickyBombPrefab, position, rotation);
        Spawn(spawned, Owner);

        Rigidbody body = spawned.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.AddForce(direction.normalized * throwVelocity, ForceMode.VelocityChange);
            body.AddTorque(Random.insideUnitSphere * throwTorque, ForceMode.VelocityChange);
        }
    }

    [ServerRpc]
    private void DetonateAllStickyBombsServerRpc()
    {
        StickyBombProp[] bombs = FindObjectsOfType<StickyBombProp>();
        float delay = 0f;
        float minimumDelay = Mathf.Min(detonationDelayRange.x, detonationDelayRange.y);
        float maximumDelay = Mathf.Max(detonationDelayRange.x, detonationDelayRange.y);

        for (int i = 0; i < bombs.Length; i++)
        {
            StickyBombProp bomb = bombs[i];
            if (!IsOwnedArmedBomb(bomb))
                continue;

            StartCoroutine(DetonateAfterDelay(bomb, delay));
            delay += Random.Range(minimumDelay, maximumDelay);
        }
    }

    [ServerRpc]
    private void DetonateStickyBombServerRpc(NetworkObject bombObject)
    {
        if (bombObject == null)
            return;

        StickyBombProp bomb = bombObject.GetComponent<StickyBombProp>();
        if (IsOwnedArmedBomb(bomb))
            bomb.ServerDetonate(NetworkObject);
    }

    private bool IsOwnedArmedBomb(StickyBombProp bomb)
    {
        return bomb != null
            && bomb.NetworkObject != null
            && bomb.NetworkObject.Owner == Owner
            && bomb.IsArmed()
            && !bomb.IsDetonated();
    }

    private IEnumerator DetonateAfterDelay(StickyBombProp bomb, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (bomb != null)
            bomb.ServerDetonate(NetworkObject);
    }
}
