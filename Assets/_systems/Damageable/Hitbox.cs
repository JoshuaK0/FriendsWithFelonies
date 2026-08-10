using UnityEngine;

public sealed class Hitbox : MonoBehaviour, IDamageable
{
	[Tooltip("For example, 2 for a headshot.")]
	[SerializeField, Min(0f)]
	private float damageMultiplier = 1f;

	private HealthManager healthManager;
	private int hitboxIndex = -1;

	public int HitboxIndex => hitboxIndex;

	public HealthManager HealthManager =>
		healthManager;

	public void Initialize(
		HealthManager manager,
		int index)
	{
		healthManager = manager;
		hitboxIndex = index;
	}

	public void TakeDamage(
		float amount,
		Vector3 damageDirection,
		Vector3 sourcePos,
		Vector3 targetPos,
		float force,
		MonoBehaviour sourceComponent)
	{
		if (healthManager == null ||
			hitboxIndex < 0 ||
			amount <= 0f)
		{
			return;
		}

		healthManager.TakeDamage(
			amount * damageMultiplier,
			damageDirection,
			sourcePos,
			targetPos,
			force,
			hitboxIndex,
			sourceComponent);
	}

	public HealthManager GetHealthManager()
	{
		return healthManager;
	}
}