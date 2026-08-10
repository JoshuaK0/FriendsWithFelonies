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

		if (directions == null || directions.Length == 0)
			return;

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
		if (directions == null || directions.Length == 0)
			return;

		roundsPerMinute = Mathf.Max(1f, roundsPerMinute);

		if (Time.time < nextAllowedFireTime)
			return;

		nextAllowedFireTime =
			Time.time + 60f / roundsPerMinute;

		damage = Mathf.Max(0f, damage);
		range = Mathf.Max(0f, range);
		ragdollForce = Mathf.Max(0f, ragdollForce);

		PlayFireObserversRpc();

		int directionCount = Mathf.Min(
			directions.Length,
			MaximumPelletsPerRequest);

		for (int i = 0; i < directionCount; i++)
		{
			Vector3 direction = directions[i];

			if (direction.sqrMagnitude <= 0.001f)
				continue;

			direction.Normalize();

			if (!Physics.Raycast(
					origin,
					direction,
					out RaycastHit hit,
					range,
					hitMask,
					QueryTriggerInteraction.Ignore))
			{
				continue;
			}

			Hitbox hitbox =
				hit.collider.GetComponent<Hitbox>();

			bool hitBody = hitbox != null;

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

	[ObserversRpc]
	private void PlayFireObserversRpc()
	{
		if (networkAudioSource == null)
			return;

		if (fireSound == null)
			return;

		networkAudioSource.PlayOneShot(fireSound);
	}

	[ObserversRpc]
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
			Quaternion.LookRotation(normal));
	}
}