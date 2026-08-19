using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using System.Linq;
/// <summary>
/// Server-authoritative sticky explosive. Add a NetworkTransform to the prefab
/// if its thrown and stuck poses should be synchronized continuously.
/// </summary>
public sealed class StickyBombProp : NetworkBehaviour
{
    [Header("Sticking")]
    [SerializeField] private Rigidbody body;
    [SerializeField] private Collider bombCollider;
    [SerializeField] private float normalOffset = 0.01f;

    [Header("Explosion")]
    [SerializeField, Min(0f)] private float damage = 100f;
    [SerializeField, Min(0f)] private float explosionRadius = 5f;
    [SerializeField] float force;
    [SerializeField] private AnimationCurve damageFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private LayerMask clippingLayers = ~0;
    [SerializeField] private LayerMask damageLayers = ~0;
    [SerializeField, Min(0f)] private float armTime = 1f;
    [SerializeField, Min(0f)] private float fxLifetime = 2f;

    [Header("Presentation")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private ParticleSystem[] particles;
    [SerializeField] private Vector2 pitchRange = new(0.95f, 1.05f);
    [SerializeField] private AudioClip[] clips;
    [SerializeField] private GameObject model;
    [SerializeField] private GameObject explosionLight;

    private readonly SyncVar<bool> armed = new(false);
    private readonly SyncVar<bool> detonated = new(false);
    private readonly SyncVar<bool> stuck = new(false);

    private float serverArmAt;
    private float clientArmStartedAt;

    private void Reset()
    {
        body = GetComponent<Rigidbody>();
        bombCollider = GetComponent<Collider>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        armed.Value = false;
        detonated.Value = false;
        stuck.Value = false;
        serverArmAt = Time.time + armTime;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        clientArmStartedAt = Time.time;
        detonated.OnChange += OnDetonatedChanged;
        ApplyDetonatedVisual(detonated.Value);
    }

    public override void OnStopClient()
    {
        detonated.OnChange -= OnDetonatedChanged;
        base.OnStopClient();
    }

    private void Update()
    {
        if (IsServerInitialized && !armed.Value && Time.time >= serverArmAt)
            armed.Value = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServerInitialized || stuck.Value || detonated.Value || collision.contactCount <= 0)
            return;

        ContactPoint contact = collision.GetContact(0);
        Vector3 position = contact.point + contact.normal * normalOffset;
        Quaternion rotation = Quaternion.LookRotation(contact.normal, Vector3.up);

        stuck.Value = true;
        StopBody();
        transform.SetPositionAndRotation(position, rotation);
        ApplyStuckPoseObserversRpc(position, rotation);
    }

    [ObserversRpc]
    private void ApplyStuckPoseObserversRpc(Vector3 position, Quaternion rotation)
    {
        StopBody();
        transform.SetPositionAndRotation(position, rotation);
    }

    [Server]
    public void ServerDetonate(NetworkObject attacker)
    {
        if (!armed.Value || detonated.Value)
            return;

        detonated.Value = true;
        ApplyExplosionDamage(attacker);

        int clipIndex = clips != null && clips.Length > 0 ? Random.Range(0, clips.Length) : -1;
        float pitch = Random.Range(
            Mathf.Min(pitchRange.x, pitchRange.y),
            Mathf.Max(pitchRange.x, pitchRange.y));

        PlayExplosionObserversRpc(clipIndex, pitch);
        StartCoroutine(DespawnAfterFx());
    }

	private void ApplyExplosionDamage(NetworkObject attacker)
	{
		Debug.Log(
			$"[Explosion] Applying explosion damage at {transform.position}. " +
			$"Radius: {explosionRadius}, Base Damage: {damage}",
			this);

		Collider[] overlaps = Physics.OverlapSphere(
			transform.position,
			explosionRadius,
			damageLayers,
			QueryTriggerInteraction.Collide);

		Debug.Log(
			$"[Explosion] OverlapSphere found {overlaps.Length} collider(s).",
			this);

		HashSet<IDamageable> damagedTargets = new();

		for (int i = 0; i < overlaps.Length; i++)
		{
			Collider overlap = overlaps[i];

			Debug.Log(
				$"[Explosion] Checking collider: {overlap.name} " +
				$"| Object: {overlap.gameObject.name} " +
				$"| Layer: {LayerMask.LayerToName(overlap.gameObject.layer)}",
				overlap);

			IDamageable damageable =
				overlap.GetComponentInParent<IDamageable>();

			if (damageable == null)
			{
				Debug.LogWarning(
					$"[Explosion] SKIPPED '{overlap.name}' because no IDamageable " +
					$"was found on it or its parents.",
					overlap);

				continue;
			}

			Debug.Log(
				$"[Explosion] Found IDamageable: {damageable}",
				overlap);

			if (!damagedTargets.Add(damageable))
			{
				Debug.Log(
					$"[Explosion] SKIPPED '{overlap.name}' because this " +
					$"IDamageable was already damaged by another collider.",
					overlap);

				continue;
			}

			Vector3 targetPoint =
				overlap.ClosestPoint(transform.position);

			Debug.Log(
				$"[Explosion] Target point for '{overlap.name}': {targetPoint}",
				overlap);

			if (Physics.Linecast(
					transform.position,
					targetPoint,
					out RaycastHit hit,
					clippingLayers,
					QueryTriggerInteraction.Ignore))
			{
				Debug.Log(
					$"[Explosion] Linecast hit '{hit.collider.name}' " +
					$"on layer '{LayerMask.LayerToName(hit.collider.gameObject.layer)}'.",
					hit.collider);

				IDamageable hitDamageable =
					hit.collider.GetComponentInParent<IDamageable>();

				if (hitDamageable != damageable)
				{
					Debug.LogWarning(
						$"[Explosion] SKIPPED '{overlap.name}' because explosion " +
						$"was blocked by '{hit.collider.name}'.",
						overlap);

					continue;
				}

				Debug.Log(
					$"[Explosion] Linecast hit the target itself, so damage is allowed.",
					overlap);
			}
			else
			{
				Debug.Log(
					$"[Explosion] Clear line of sight to '{overlap.name}'.",
					overlap);
			}

			float distance = Vector3.Distance(
				transform.position,
				targetPoint);

			float normalizedDistance = Mathf.InverseLerp(
				0f,
				explosionRadius,
				distance);

			float multiplier =
				damageFalloff != null
					? damageFalloff.Evaluate(normalizedDistance)
					: 1f - normalizedDistance;

			float appliedDamage =
				Mathf.Max(0f, damage * multiplier);

			Debug.Log(
				$"[Explosion] '{overlap.name}' damage calculation:" +
				$"\nDistance: {distance:F2}" +
				$"\nNormalized Distance: {normalizedDistance:F2}" +
				$"\nFalloff Multiplier: {multiplier:F2}" +
				$"\nFinal Damage: {appliedDamage:F2}",
				overlap);

			if (appliedDamage <= 0f)
			{
				Debug.LogWarning(
					$"[Explosion] '{overlap.name}' is receiving 0 damage. " +
					$"Check damageFalloff.",
					overlap);
			}

			Vector3 direction =
				(targetPoint - transform.position).normalized;

			Debug.Log(
				$"[Explosion] Calling TakeDamage({appliedDamage:F2}) " +
				$"on '{damageable}'.",
				overlap);

			damageable.TakeDamage(
				appliedDamage,
				direction,
				targetPoint,
				transform.position,
				force,
				this);

			Debug.Log(
				$"[Explosion] TakeDamage completed for '{overlap.name}'.",
				overlap);
		}

		Debug.Log(
			$"[Explosion] Finished. Damaged {damagedTargets.Count} unique target(s).",
			this);
	}

	[ObserversRpc]
    private void PlayExplosionObserversRpc(int clipIndex, float pitch)
    {
        ApplyDetonatedVisual(true);

        if (particles != null)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null)
                    particles[i].Play();
            }
        }

        if (explosionLight != null)
            explosionLight.SetActive(true);

        if (audioSource != null && clips != null && clipIndex >= 0 && clipIndex < clips.Length)
        {
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clips[clipIndex]);
        }
    }

    private IEnumerator DespawnAfterFx()
    {
        yield return new WaitForSeconds(fxLifetime);
        if (NetworkObject != null)
            NetworkObject.Despawn();
    }

    private void StopBody()
    {
        if (body == null)
            return;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnDetonatedChanged(bool previous, bool next, bool asServer)
    {
        ApplyDetonatedVisual(next);
    }

    private void ApplyDetonatedVisual(bool value)
    {
        if (model != null)
            model.SetActive(!value);

        if (bombCollider != null)
            bombCollider.enabled = !value;

        if (value)
            StopBody();
    }

    public bool IsArmed() => armed.Value;
    public bool IsDetonated() => detonated.Value;

    public float GetArmPercentage()
    {
        if (armed.Value)
            return 1f;

        if (armTime <= 0f)
            return 1f;

        return Mathf.Clamp01((Time.time - clientArmStartedAt) / armTime);
    }
}
