using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CharacterAnimator : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Animator animator;
	[SerializeField] private PlayerMovement playerMovement;

	[Tooltip("The transform moved by PlayerMovement.")]
	[SerializeField] private Transform trackedTransform;

	[Tooltip("The visual character mesh that receives positional offsets.")]
	[SerializeField] private Transform characterMesh;

	[Header("Velocity Normalisation")]
	[SerializeField] private float walkSpeed = 2.5f;
	[SerializeField] private float runSpeed = 5f;
	[SerializeField] private float sprintSpeed = 8f;
	[SerializeField] private float crouchSpeed = 2.5f;

	[Header("Animation")]
	[SerializeField] private float velocitySmoothingSpeed = 10f;

	[Tooltip("Minimum horizontal speed before the character is considered moving.")]
	[SerializeField] private float movementThreshold = 0.05f;

	[Header("Mesh Position Offsets")]
	[SerializeField] private Vector3 idlePositionOffset;
	[SerializeField] private Vector3 walkPositionOffset;
	[SerializeField] private Vector3 runPositionOffset;
	[SerializeField] private Vector3 sprintPositionOffset;
	[SerializeField] private Vector3 crouchPositionOffset;

	[SerializeField] private float positionSmoothingSpeed = 10f;

	private static readonly int VelXHash =
		Animator.StringToHash("VelX");

	private static readonly int VelYHash =
		Animator.StringToHash("VelY");

	private static readonly int IsWalkingHash =
		Animator.StringToHash("IsWalking");

	private static readonly int IsSprintingHash =
		Animator.StringToHash("IsSprinting");

	private static readonly int IsCrouchingHash =
		Animator.StringToHash("IsCrouching");

	private Vector3 previousPosition;
	private Vector3 baseMeshLocalPosition;

	private Vector2 currentAnimationVelocity;

	private bool hasMovementVelocity;

	public Vector2 AnimationVelocity =>
		currentAnimationVelocity;

	private void Reset()
	{
		animator =
			GetComponent<Animator>();

		playerMovement =
			GetComponent<PlayerMovement>();

		if (playerMovement == null)
		{
			playerMovement =
				GetComponentInParent<PlayerMovement>();
		}

		trackedTransform =
			playerMovement != null
				? playerMovement.transform
				: transform;

		characterMesh =
			animator != null
				? animator.transform
				: transform;
	}

	private void Awake()
	{
		FindReferences();

		if (animator == null)
		{
			Debug.LogError(
				$"{nameof(CharacterAnimator)} on {name} " +
				"could not find an Animator.",
				this);

			enabled = false;
			return;
		}

		if (playerMovement == null)
		{
			Debug.LogError(
				$"{nameof(CharacterAnimator)} on {name} " +
				$"could not find a {nameof(PlayerMovement)} component.",
				this);

			enabled = false;
			return;
		}

		if (trackedTransform == null)
		{
			Debug.LogError(
				$"{nameof(CharacterAnimator)} on {name} " +
				"has no tracked transform.",
				this);

			enabled = false;
			return;
		}

		if (characterMesh == null)
		{
			Debug.LogError(
				$"{nameof(CharacterAnimator)} on {name} " +
				"has no character mesh assigned.",
				this);

			enabled = false;
			return;
		}

		if (characterMesh == trackedTransform)
		{
			Debug.LogWarning(
				$"{nameof(CharacterAnimator)} on {name} has the same " +
				"transform assigned as both the tracked transform and " +
				"the character mesh. Assign a visual model child as " +
				"the character mesh to avoid moving the player root.",
				this);
		}

		previousPosition =
			trackedTransform.position;

		baseMeshLocalPosition =
			characterMesh.localPosition;
	}

	private void OnEnable()
	{
		if (trackedTransform != null)
		{
			previousPosition =
				trackedTransform.position;
		}
	}

	private void LateUpdate()
	{
		if (animator == null ||
			playerMovement == null ||
			trackedTransform == null ||
			characterMesh == null)
		{
			return;
		}

		UpdateMovementVelocity();
		UpdateMovementFlags();
		UpdateMeshPosition();
	}

	private void UpdateMovementVelocity()
	{
		float deltaTime =
			Mathf.Max(
				Time.deltaTime,
				0.0001f);

		Vector3 worldVelocity =
			(trackedTransform.position - previousPosition) /
			deltaTime;

		previousPosition =
			trackedTransform.position;

		Vector3 localVelocity =
			trackedTransform.InverseTransformDirection(
				worldVelocity);

		Vector2 horizontalVelocity =
			new Vector2(
				localVelocity.x,
				localVelocity.z);

		hasMovementVelocity =
			horizontalVelocity.sqrMagnitude >
			movementThreshold * movementThreshold;

		float normalisationSpeed =
			GetCurrentMovementSpeed();

		Vector2 targetVelocity =
			new Vector2(
				Mathf.Clamp(
					localVelocity.x / normalisationSpeed,
					-1f,
					1f),
				Mathf.Clamp(
					localVelocity.z / normalisationSpeed,
					-1f,
					1f));

		float smoothingAmount =
			1f - Mathf.Exp(
				-velocitySmoothingSpeed *
				deltaTime);

		currentAnimationVelocity =
			Vector2.Lerp(
				currentAnimationVelocity,
				targetVelocity,
				smoothingAmount);

		animator.SetFloat(
			VelXHash,
			currentAnimationVelocity.x);

		animator.SetFloat(
			VelYHash,
			currentAnimationVelocity.y);
	}

	private void UpdateMovementFlags()
	{
		animator.SetBool(
			IsWalkingHash,
			playerMovement.IsWalking);

		animator.SetBool(
			IsSprintingHash,
			playerMovement.IsSprinting);

		animator.SetBool(
			IsCrouchingHash,
			playerMovement.IsCrouching);
	}

	private void UpdateMeshPosition()
	{
		Vector3 targetPosition =
			baseMeshLocalPosition +
			GetCurrentPositionOffset();

		float smoothingAmount =
			1f - Mathf.Exp(
				-positionSmoothingSpeed *
				Time.deltaTime);

		characterMesh.localPosition =
			Vector3.Lerp(
				characterMesh.localPosition,
				targetPosition,
				smoothingAmount);
	}

	private Vector3 GetCurrentPositionOffset()
	{
		if (playerMovement.IsCrouching)
		{
			return crouchPositionOffset;
		}

		if (!hasMovementVelocity)
		{
			return idlePositionOffset;
		}

		if (playerMovement.IsSprinting)
		{
			return sprintPositionOffset;
		}

		if (playerMovement.IsWalking)
		{
			return walkPositionOffset;
		}

		return runPositionOffset;
	}

	private float GetCurrentMovementSpeed()
	{
		if (playerMovement.IsCrouching)
		{
			return Mathf.Max(
				crouchSpeed,
				0.01f);
		}

		if (playerMovement.IsSprinting)
		{
			return Mathf.Max(
				sprintSpeed,
				0.01f);
		}

		if (playerMovement.IsWalking)
		{
			return Mathf.Max(
				walkSpeed,
				0.01f);
		}

		return Mathf.Max(
			runSpeed,
			0.01f);
	}

	private void FindReferences()
	{
		if (animator == null)
		{
			animator =
				GetComponent<Animator>();
		}

		if (playerMovement == null)
		{
			playerMovement =
				GetComponent<PlayerMovement>();
		}

		if (playerMovement == null)
		{
			playerMovement =
				GetComponentInParent<PlayerMovement>();
		}

		if (trackedTransform == null)
		{
			trackedTransform =
				playerMovement != null
					? playerMovement.transform
					: transform;
		}

		if (characterMesh == null)
		{
			characterMesh =
				animator != null
					? animator.transform
					: transform;
		}
	}
}