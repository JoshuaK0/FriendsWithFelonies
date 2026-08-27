using FishNet.Object;
using UnityEngine;

public sealed class GunItemNetworked : NetworkBehaviour
{
	[Header("Network Effects")]
	[SerializeField]
	private AudioSource networkAudioSource;

	[SerializeField]
	private AudioClip fireSound;

	[SerializeField]
	private GameObject bodyHitFx;

	[SerializeField]
	private GameObject environmentHitFx;

	[Header("Projectile Visual")]
	[SerializeField]
	private GameObject bulletProjectilePrefab;

	[Tooltip(
		"Where visual projectiles are spawned. " +
		"If empty, the supplied firing origin is used.")]
	[SerializeField]
	private Transform projectileSpawnPoint;

	/*
	 * Prevents an accidentally configured shotgun from sending
	 * an excessively large array in one RPC.
	 *
	 * This is not a weapon stat.
	 */
	private const int MaximumPelletsPerRequest = 64;

	private float nextAllowedFireTime;

	public void RequestFire(
		Vector3 origin,
		Vector3[] directions,
		float roundsPerMinute,
		float damage,
		float range,
		int hitMask,
		float ragdollForce,
		int teamId)
	{
		if (!IsOwner)
			return;

		if (directions == null ||
			directions.Length == 0)
		{
			return;
		}

		/*
		 * Spawn immediately for the owner so the projectile
		 * does not wait for a server round trip.
		 */
		SpawnProjectiles(
			origin,
			directions);

		FireServerRpc(
			origin,
			directions,
			roundsPerMinute,
			damage,
			range,
			hitMask,
			ragdollForce,
			teamId);
	}

	[ServerRpc]
	private void FireServerRpc(
		Vector3 origin,
		Vector3[] directions,
		float roundsPerMinute,
		float damage,
		float range,
		int hitMask,
		float ragdollForce,
		int teamId)
	{
		if (directions == null ||
			directions.Length == 0)
		{
			return;
		}

		roundsPerMinute =
			Mathf.Max(
				1f,
				roundsPerMinute);

		if (Time.time <
			nextAllowedFireTime)
		{
			return;
		}

		nextAllowedFireTime =
			Time.time +
			60f /
			roundsPerMinute;

		damage =
			Mathf.Max(
				0f,
				damage);

		range =
			Mathf.Max(
				0f,
				range);

		ragdollForce =
			Mathf.Max(
				0f,
				ragdollForce);

		/*
		 * Send the firing audio and visual projectiles to
		 * everyone except the shooting client.
		 *
		 * The owner already played/spawned them locally.
		 */
		PlayFireEffectsObserversRpc(
			origin,
			directions);

		int directionCount =
			Mathf.Min(
				directions.Length,
				MaximumPelletsPerRequest);

		for (int i = 0;
			i < directionCount;
			i++)
		{
			Vector3 direction =
				directions[i];

			if (direction.sqrMagnitude <=
				0.001f)
			{
				continue;
			}

			direction.Normalize();

			if (!Physics.Raycast(
					origin,
					direction,
					out RaycastHit hit,
					range,
					hitMask,
					QueryTriggerInteraction.Collide))
			{
				continue;
			}

			Hitbox hitbox =
				hit.collider
					.GetComponent<Hitbox>();

			if (hitbox == null)
			{
				hitbox =
					hit.collider
						.GetComponentInParent<Hitbox>();
			}

			bool hitBody =
				hitbox != null;

			/*
			 * Damage remains server-only and hitscan.
			 * BulletProjectile is only a visual object.
			 */
			if (hitbox != null)
			{
				HealthManager healthManager =
					hitbox.GetHealthManager();

				if (healthManager != null &&
					healthManager.TeamId != teamId)
				{
					hitbox.TakeDamage(
						damage,
						direction,
						transform.position,
						hit.point,
						ragdollForce,
						this);
				}
			}

			SpawnHitObserversRpc(
				hit.point,
				hit.normal,
				hitBody);
		}
	}

	[ObserversRpc(ExcludeOwner = true)]
	private void PlayFireEffectsObserversRpc(
		Vector3 origin,
		Vector3[] directions)
	{
		if (networkAudioSource != null &&
			fireSound != null)
		{
			networkAudioSource.PlayOneShot(
				fireSound);
		}

		SpawnProjectiles(
			origin,
			directions);
	}

	private void SpawnProjectiles(
		Vector3 fallbackOrigin,
		Vector3[] directions)
	{
		if (bulletProjectilePrefab == null ||
			directions == null)
		{
			return;
		}

		Vector3 spawnPosition =
			projectileSpawnPoint != null
				? projectileSpawnPoint.position
				: fallbackOrigin;

		int directionCount =
			Mathf.Min(
				directions.Length,
				MaximumPelletsPerRequest);

		for (int i = 0;
			i < directionCount;
			i++)
		{
			Vector3 direction =
				directions[i];

			if (direction.sqrMagnitude <=
				0.001f)
			{
				continue;
			}

			Instantiate(
				bulletProjectilePrefab,
				spawnPosition,
				Quaternion.LookRotation(
					direction.normalized));
		}
	}

	[ObserversRpc(ExcludeOwner = true)]
	private void SpawnHitObserversRpc(
		Vector3 point,
		Vector3 normal,
		bool bodyHit)
	{
		GameObject prefab =
			bodyHit
				? bodyHitFx
				: environmentHitFx;

		if (prefab == null)
			return;

		Instantiate(
			prefab,
			point,
			Quaternion.LookRotation(
				normal));
	}
}
