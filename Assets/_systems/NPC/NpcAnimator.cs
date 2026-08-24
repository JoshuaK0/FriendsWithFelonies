using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

public sealed class NpcAnimator : NetworkBehaviour
{
	[Header("References")]
	[SerializeField] private NavMeshAgent agent;
	[SerializeField] private Animator animator;

	[Header("Animator")]
	[SerializeField] private string speedParameter = "Speed";

	[Header("Smoothing")]
	[SerializeField, Min(0f)] private float smoothing = 10f;

	private int speedHash;
	private float currentSpeed;

	private void Awake()
	{
		if (agent == null)
			agent = GetComponent<NavMeshAgent>();

		if (animator == null)
			animator = GetComponentInChildren<Animator>(true);

		speedHash = Animator.StringToHash(speedParameter);
	}

	private void Update()
	{
		// Only the server is allowed to drive animation parameters.
		if (!IsServerInitialized)
			return;

		if (agent == null || animator == null)
			return;

		float targetSpeed = agent.velocity.magnitude;

		currentSpeed = Mathf.Lerp(
			currentSpeed,
			targetSpeed,
			smoothing * Time.deltaTime);

		animator.SetFloat(speedHash, currentSpeed);
	}
}