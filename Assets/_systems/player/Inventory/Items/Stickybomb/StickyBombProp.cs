using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

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
	[SerializeField] private float force;

	[SerializeField]
	private AnimationCurve damageFalloff =
		AnimationCurve.Linear(0f, 1f, 1f, 0f);

	[SerializeField] private LayerMask clippingLayers = ~0;
	[SerializeField] private LayerMask damageLayers = ~0;

	[SerializeField, Min(0f)] private float armTime = 1f;
	[SerializeField, Min(0f)] private float fxLifetime = 2f;

	[Header("Aesthetic Explosion Delay")]
	[Tooltip("Random delay before sound, particles, light, and visual disappearance.")]
	[SerializeField] private Vector2 aestheticDelayRange = new(0f, 0.2f);

	[Header("Presentation")]
	[SerializeField] private AudioSource audioSource;
	[SerializeField] private ParticleSystem[] particles;
	[SerializeField] private Vector2 pitchRange = new(0.95f, 1.05f);
	[SerializeField] private AudioClip[] clips;
	[SerializeField] private GameObject model;

	[Header("Explosion Light")]
	[SerializeField] private Light explosionLight;

	[Tooltip("Controls light intensity over the normalized explosion light lifetime.")]
	[SerializeField]
	private AnimationCurve lightCurve =
		AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

	[SerializeField, Min(0f)]
	private float lightDuration = 0.25f;

	[SerializeField, Min(0f)]
	private float lightIntensityMultiplier = 10f;

	private readonly SyncVar<bool> armed = new(false);
	private readonly SyncVar<bool> detonated = new(false);
	private readonly SyncVar<bool> stuck = new(false);

	private float serverArmAt;
	private float clientArmStartedAt;

	private Coroutine lightCoroutine;

	private void Reset()
	{
		body = GetComponent<Rigidbody>();
		bombCollider = GetComponent<Collider>();
	}

	private void Awake()
	{
		if (explosionLight != null)
		{
			explosionLight.intensity = 0f;
			explosionLight.enabled = false;
		}
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

		if (!detonated.Value)
			ApplyDetonatedVisual(false);
	}

	public override void OnStopClient()
	{
		detonated.OnChange -= OnDetonatedChanged;

		base.OnStopClient();
	}

	private void Update()
	{
		if (IsServerInitialized &&
			!armed.Value &&
			Time.time >= serverArmAt)
		{
			armed.Value = true;
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (!IsServerInitialized ||
			stuck.Value ||
			detonated.Value ||
			collision.contactCount <= 0)
		{
			return;
		}

		ContactPoint contact = collision.GetContact(0);

		Vector3 position =
			contact.point +
			contact.normal * normalOffset;

		Quaternion rotation =
			Quaternion.LookRotation(
				contact.normal,
				Vector3.up);

		stuck.Value = true;

		StopBody();

		transform.SetPositionAndRotation(
			position,
			rotation);

		ApplyStuckPoseObserversRpc(
			position,
			rotation);
	}

	[ObserversRpc]
	private void ApplyStuckPoseObserversRpc(
		Vector3 position,
		Quaternion rotation)
	{
		StopBody();

		transform.SetPositionAndRotation(
			position,
			rotation);
	}

	[Server]
	public void ServerDetonate(NetworkObject attacker)
	{
		if (!armed.Value || detonated.Value)
			return;

		detonated.Value = true;

		// Damage is applied immediately.
		ApplyExplosionDamage(attacker);

		float minDelay =
			Mathf.Min(
				aestheticDelayRange.x,
				aestheticDelayRange.y);

		float maxDelay =
			Mathf.Max(
				aestheticDelayRange.x,
				aestheticDelayRange.y);

		float aestheticDelay =
			Random.Range(
				Mathf.Max(0f, minDelay),
				Mathf.Max(0f, maxDelay));

		int clipIndex =
			clips != null && clips.Length > 0
				? Random.Range(0, clips.Length)
				: -1;

		float pitch =
			Random.Range(
				Mathf.Min(pitchRange.x, pitchRange.y),
				Mathf.Max(pitchRange.x, pitchRange.y));

		PlayExplosionObserversRpc(
			clipIndex,
			pitch,
			aestheticDelay);

		StartCoroutine(
			DespawnAfterFx(aestheticDelay));
	}

	private void ApplyExplosionDamage(NetworkObject attacker)
	{
		Debug.Log(
			$"[Explosion] Applying explosion damage at {transform.position}. " +
			$"Radius: {explosionRadius}, Base Damage: {damage}",
			this);

		Collider[] overlaps =
			Physics.OverlapSphere(
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

			if (Physics.Linecast(
					transform.position,
					targetPoint,
					out RaycastHit hit,
					clippingLayers,
					QueryTriggerInteraction.Ignore))
			{
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
			}

			float distance =
				Vector3.Distance(
					transform.position,
					targetPoint);

			float normalizedDistance =
				Mathf.InverseLerp(
					0f,
					explosionRadius,
					distance);

			float multiplier =
				damageFalloff != null
					? damageFalloff.Evaluate(normalizedDistance)
					: 1f - normalizedDistance;

			float appliedDamage =
				Mathf.Max(
					0f,
					damage * multiplier);

			Vector3 direction =
				(targetPoint - transform.position).normalized;

			damageable.TakeDamage(
				appliedDamage,
				direction,
				targetPoint,
				transform.position,
				force,
				this);
		}

		Debug.Log(
			$"[Explosion] Finished. Damaged {damagedTargets.Count} unique target(s).",
			this);
	}

	[ObserversRpc]
	private void PlayExplosionObserversRpc(
		int clipIndex,
		float pitch,
		float delay)
	{
		StartCoroutine(
			PlayExplosionEffectsAfterDelay(
				clipIndex,
				pitch,
				delay));
	}

	private IEnumerator PlayExplosionEffectsAfterDelay(
		int clipIndex,
		float pitch,
		float delay)
	{
		if (delay > 0f)
			yield return new WaitForSeconds(delay);

		// Hide the bomb when the visible explosion actually occurs.
		ApplyDetonatedVisual(true);

		// Particles.
		if (particles != null)
		{
			for (int i = 0; i < particles.Length; i++)
			{
				if (particles[i] != null)
					particles[i].Play();
			}
		}

		// Sound.
		if (audioSource != null &&
			clips != null &&
			clipIndex >= 0 &&
			clipIndex < clips.Length)
		{
			audioSource.pitch = pitch;
			audioSource.PlayOneShot(clips[clipIndex]);
		}

		// Animated explosion light.
		if (explosionLight != null)
		{
			if (lightCoroutine != null)
				StopCoroutine(lightCoroutine);

			lightCoroutine = StartCoroutine(
				AnimateExplosionLight());
		}
	}

	private IEnumerator AnimateExplosionLight()
	{
		if (explosionLight == null)
			yield break;

		explosionLight.enabled = true;

		if (lightDuration <= 0f)
		{
			explosionLight.intensity = 0f;
			explosionLight.enabled = false;
			lightCoroutine = null;
			yield break;
		}

		float elapsed = 0f;

		while (elapsed < lightDuration)
		{
			elapsed += Time.deltaTime;

			float normalizedTime =
				Mathf.Clamp01(
					elapsed / lightDuration);

			float curveValue =
				lightCurve != null
					? lightCurve.Evaluate(normalizedTime)
					: 1f - normalizedTime;

			explosionLight.intensity =
				Mathf.Max(
					0f,
					curveValue * lightIntensityMultiplier);

			yield return null;
		}

		explosionLight.intensity = 0f;
		explosionLight.enabled = false;

		lightCoroutine = null;
	}

	private IEnumerator DespawnAfterFx(float aestheticDelay)
	{
		// Make sure the object stays alive long enough for either
		// the general FX lifetime or the light animation.
		float effectDuration =
			Mathf.Max(
				fxLifetime,
				lightDuration);

		yield return new WaitForSeconds(
			aestheticDelay + effectDuration);

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

	private void OnDetonatedChanged(
		bool previous,
		bool next,
		bool asServer)
	{
		// Detonation gameplay state changes immediately, but
		// visuals are intentionally delayed.
		if (!next)
			ApplyDetonatedVisual(false);
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

	public bool IsArmed()
	{
		return armed.Value;
	}

	public bool IsDetonated()
	{
		return detonated.Value;
	}

	public float GetArmPercentage()
	{
		if (armed.Value)
			return 1f;

		if (armTime <= 0f)
			return 1f;

		return Mathf.Clamp01(
			(Time.time - clientArmStartedAt) /
			armTime);
	}
}