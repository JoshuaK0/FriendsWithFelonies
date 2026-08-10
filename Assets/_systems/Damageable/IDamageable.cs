using UnityEngine;

public interface IDamageable
{
	void TakeDamage(
		float amount,
		Vector3 damageDirection,
		Vector3 sourcePos,
		Vector3 targetPos,
		float force,
		MonoBehaviour sourceComponent);
}