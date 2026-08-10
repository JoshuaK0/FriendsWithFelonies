using System.Collections;
using FishNet.Object;
using UnityEngine;

public sealed class PeriodicAreaDamage : NetworkBehaviour
{
	[Header("Damage")]
	[SerializeField, Min(0f)]
	private float damagePerTick = 5f;

	[SerializeField, Min(0.1f)]
	private float damageInterval = 1f;

	[SerializeField, Min(0f)]
	private float initialDelay;

	[Header("Area")]
	[SerializeField, Min(0f)]
	private float radius = 5f;

	[SerializeField]
	private bool damageSelf;

	private Coroutine damageCoroutine;

	public override void OnStartServer()
	{
		base.OnStartServer();

		damageCoroutine =
			StartCoroutine(DamagePeriodically());
	}

	public override void OnStopServer()
	{
		if (damageCoroutine != null)
		{
			StopCoroutine(damageCoroutine);
			damageCoroutine = null;
		}

		base.OnStopServer();
	}

	private IEnumerator DamagePeriodically()
	{
		if (initialDelay > 0f)
			yield return new WaitForSeconds(initialDelay);

		WaitForSeconds interval =
			new(damageInterval);

		while (true)
		{
			DamageNearbyHealthManagers();

			yield return interval;
		}
	}

	[Server]
	private void DamageNearbyHealthManagers()
	{
		if (damagePerTick <= 0f || radius <= 0f)
			return;

		Vector3 sourcePosition =
			transform.position;

		float radiusSquared =
			radius * radius;

		HealthManager[] healthManagers =
			FindObjectsByType<HealthManager>(
				FindObjectsSortMode.None);

		foreach (HealthManager healthManager in healthManagers)
		{
			if (healthManager == null ||
				healthManager.IsDead ||
				!healthManager.IsSpawned)
			{
				continue;
			}

			if (!damageSelf &&
				healthManager.transform.root == transform.root)
			{
				continue;
			}

			Vector3 targetPosition =
				healthManager.transform.position;

			Vector3 difference =
				targetPosition - sourcePosition;

			if (difference.sqrMagnitude > radiusSquared)
				continue;

			healthManager.TakeDamage(
				damagePerTick,
				difference,
				sourcePosition,
				targetPosition,
				200,
				this);
		}
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.DrawWireSphere(
			transform.position,
			radius);
	}
}