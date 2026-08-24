using FishNet.Object;
using UnityEngine;
using static Evo.UI.ProgressButton;

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

		if (directions == null ||
			directions.Length == 0)
		{
			return;
		}

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
		 * Send the gunshot to everyone EXCEPT
		 * the shooting client.
		 *
		 * The owner already played the shot locally.
		 */
		PlayFireObserversRpc();


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


			/*
			 * IMPORTANT:
			 *
			 * Use Collide so trigger-based player
			 * hitboxes are included.
			 *
			 * The local GunItem raycast uses the
			 * same trigger behaviour.
			 */
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


			/*
			 * Look for the Hitbox directly first.
			 */
			Hitbox hitbox =
				hit.collider
					.GetComponent<Hitbox>();


			/*
			 * Also support setups where the collider
			 * is below the Hitbox in the hierarchy.
			 */
			if (hitbox == null)
			{
				hitbox =
					hit.collider
						.GetComponentInParent<Hitbox>();
			}


			bool hitBody =
				hitbox != null;


			/*
			 * DAMAGE IS SERVER-ONLY.
			 *
			 * The local GunItem never calls TakeDamage.
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


			/*
			 * Send the server-confirmed impact to
			 * everyone except the shooter.
			 *
			 * The shooter already spawned an
			 * immediate predicted impact locally.
			 */
			SpawnHitObserversRpc(
				hit.point,
				hit.normal,
				hitBody);
		}
	}


	/*
	 * Owner already plays their firing audio locally.
	 *
	 * Excluding owner prevents the shot from being
	 * heard twice by the shooting player.
	 */
	[ObserversRpc(ExcludeOwner = true)]
	private void PlayFireObserversRpc()
	{
		if (networkAudioSource == null)
		{
			return;
		}

		if (fireSound == null)
		{
			return;
		}

		networkAudioSource.PlayOneShot(
			fireSound);
	}


	/*
	 * Owner already spawned an immediate impact locally.
	 *
	 * Other clients receive the server-confirmed impact.
	 */
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
		{
			return;
		}


		Instantiate(
			prefab,
			point,
			Quaternion.LookRotation(
				normal));
	}
}