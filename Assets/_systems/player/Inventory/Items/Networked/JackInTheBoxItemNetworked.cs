using System.Collections;
using FishNet.Object;
using UnityEngine;

public sealed class JackInTheBoxItemNetworked : NetworkBehaviour
{
    [SerializeField] private JackInTheBoxDefinition definition;

    [Header("Remote presentation")]
    [SerializeField] private GameObject remoteBoxVisual;
    [SerializeField] private AudioSource networkAudioSource;
    [SerializeField] private AudioClip boxOpenClip;
    [SerializeField] private AudioClip scareClip;
    [SerializeField] private AudioClip ambientClip;

    public void RequestSetBoxState(bool enabled)
    {
        if (!IsOwner)
            return;

        SetBoxStateServerRpc(enabled);
    }

    public void RequestPlayScare()
    {
        if (!IsOwner)
            return;

        PlayScareServerRpc();
    }

    public void RequestAttack(NetworkObject targetObject, Vector3 hitPoint)
    {
        if (!IsOwner || targetObject == null)
            return;

        AttackServerRpc(targetObject, hitPoint);
    }

    [ServerRpc]
    private void SetBoxStateServerRpc(bool enabled)
    {
        SetBoxStateObserversRpc(enabled);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void SetBoxStateObserversRpc(bool enabled)
    {
        if (remoteBoxVisual != null)
            remoteBoxVisual.SetActive(enabled);

        if (networkAudioSource == null)
            return;

        if (boxOpenClip != null)
            networkAudioSource.PlayOneShot(boxOpenClip);

        if (enabled && ambientClip != null)
        {
            networkAudioSource.clip = ambientClip;
            networkAudioSource.loop = true;
            networkAudioSource.Play();
        }
        else if (!enabled && networkAudioSource.clip == ambientClip)
        {
            networkAudioSource.Stop();
            networkAudioSource.loop = false;
            networkAudioSource.clip = null;
        }
    }

    [ServerRpc]
    private void PlayScareServerRpc()
    {
        PlayScareObserversRpc();
    }

    [ObserversRpc]
    private void PlayScareObserversRpc()
    {
        if (networkAudioSource != null && scareClip != null)
            networkAudioSource.PlayOneShot(scareClip);
    }

    [ServerRpc]
    private void AttackServerRpc(NetworkObject targetObject, Vector3 hitPoint)
    {
        if (definition == null || targetObject == null || targetObject == NetworkObject)
            return;

/*        INetworkDamageable damageable = ComponentInterfaceUtility.FindInParents<INetworkDamageable>(targetObject);
        if (damageable != null)
            StartCoroutine(ApplyDelayedDamage(targetObject, hitPoint, damageable));*/
    }

    private IEnumerator ApplyDelayedDamage(
        NetworkObject targetObject,
        Vector3 hitPoint
        )
    {
        yield return new WaitForSeconds(definition.DamageDelay);

        if (targetObject == null)
            yield break;

/*        Vector3 direction = (targetObject.transform.position - transform.position).normalized;
        damageable.ApplyNetworkDamage(
            definition.Damage,
            hitPoint,
            direction,
            definition.RagdollForce,
            NetworkObject);*/
    }
}
