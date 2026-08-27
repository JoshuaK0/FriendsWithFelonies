using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BulletProjectile : MonoBehaviour
{
	[SerializeField]
	private Rigidbody projectileRigidbody;

	[SerializeField, Min(0f)]
	private float forwardForce = 30f;

	[SerializeField, Min(0f)]
	private float lifetime = 3f;

	[SerializeField] bool colliderWithTriggers = false;

	private void Awake()
	{
		if (projectileRigidbody == null)
		{
			projectileRigidbody =
				GetComponent<Rigidbody>();
		}
	}

	private void Start()
	{
		projectileRigidbody.AddForce(
			transform.forward * forwardForce,
			ForceMode.VelocityChange);

		Destroy(
			gameObject,
			lifetime);
	}

	private void OnCollisionEnter(
		Collision collision)
	{
		projectileRigidbody.isKinematic = true;
		projectileRigidbody.angularVelocity = Vector3.zero;
		projectileRigidbody.linearVelocity = Vector3.zero;
	}

	private void OnTriggerEnter(
		Collider other)
	{
		if(colliderWithTriggers || !other.isTrigger)
		{
            projectileRigidbody.isKinematic = true;
            projectileRigidbody.angularVelocity = Vector3.zero;
            projectileRigidbody.linearVelocity = Vector3.zero;
        }
    }
}
