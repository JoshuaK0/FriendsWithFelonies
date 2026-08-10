using UnityEngine;

/// <summary>
/// Optional local FX helper. Replicated gun impacts are spawned by GunItemNetworked.
/// </summary>
public sealed class GunHitFX : MonoBehaviour
{
    [SerializeField] private GameObject environmentHitFx;
    [SerializeField] private GameObject bodyHitFx;

    public void Spawn(Vector3 position, Vector3 normal, bool bodyHit)
    {
        GameObject prefab = bodyHit ? bodyHitFx : environmentHitFx;
        if (prefab != null)
            Instantiate(prefab, position, Quaternion.LookRotation(normal));
    }
}
