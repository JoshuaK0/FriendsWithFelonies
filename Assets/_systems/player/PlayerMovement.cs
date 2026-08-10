using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private float walkSpeed = 2.5f;
	[SerializeField] private float runSpeed = 5f;
	[SerializeField] private float sprintSpeed = 8f;
	[SerializeField] private float crouchSpeed = 2.5f;

	[Header("Walking")]
	[SerializeField] private KeyCode walkKey = KeyCode.LeftAlt;
	[SerializeField] private bool toggleWalk;

	[Header("Sprint")]
	[SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
	[SerializeField] private bool toggleSprint;

	[Header("Jump and Gravity")]
	[SerializeField] private KeyCode jumpKey = KeyCode.Space;
	[SerializeField] private float jumpHeight = 1.5f;
	[SerializeField] private float gravity = -20f;
	[SerializeField] private float groundedForce = -2f;
	[SerializeField] private float terminalVelocity = -50f;

	[Header("Crouching")]
	[SerializeField] private Transform cameraHolder;
	[SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
	[SerializeField] private bool toggleCrouch;
	[SerializeField] private float standingHeight = 2f;
	[SerializeField] private float crouchingHeight = 1f;
	[SerializeField] private float crouchingCameraOffset = -0.5f;
	[SerializeField] private float crouchTransitionSpeed = 10f;

	private CharacterController characterController;

	private Vector3 verticalVelocity;

	private Vector3 standingCenter;
	private Vector3 crouchingCenter;

	private Vector3 standingCameraPosition;
	private Vector3 crouchingCameraPosition;

	private bool walkRequested;
	private bool sprintRequested;

	private bool isWalking;
	private bool isSprinting;
	private bool isCrouching;

	/*
	 * Used for hold-to-crouch.
	 *
	 * When sprint or walk cancels crouching while the crouch key
	 * is still held, crouching remains blocked until that key is
	 * released.
	 */
	private bool crouchBlockedUntilRelease;

	public bool IsWalking => isWalking;
	public bool IsSprinting => isSprinting;
	public bool IsCrouching => isCrouching;

	public bool IsGrounded =>
		characterController != null &&
		characterController.isGrounded;

	private void Awake()
	{
		characterController =
			GetComponent<CharacterController>();

		standingCenter =
			characterController.center;

		float controllerBottom =
			standingCenter.y -
			standingHeight * 0.5f;

		crouchingCenter =
			standingCenter;

		crouchingCenter.y =
			controllerBottom +
			crouchingHeight * 0.5f;

		if (cameraHolder != null)
		{
			standingCameraPosition =
				cameraHolder.localPosition;

			crouchingCameraPosition =
				standingCameraPosition +
				Vector3.up * crouchingCameraOffset;
		}
	}

	private void Update()
	{
		HandleCrouchInput();
		HandleWalkInput();
		HandleSprintInput();

		HandleJumpAndGravity();
		HandleMovement();
		HandleCrouchTransition();
	}

	private void HandleWalkInput()
	{
		if (toggleWalk)
		{
			if (Input.GetKeyDown(walkKey))
			{
				walkRequested =
					!walkRequested;
			}
		}
		else
		{
			walkRequested =
				Input.GetKey(walkKey);
		}

		if (!walkRequested)
		{
			return;
		}

		sprintRequested = false;

		ExitCrouchForMovementMode();
	}

	private void HandleSprintInput()
	{
		if (toggleSprint)
		{
			if (Input.GetKeyDown(sprintKey))
			{
				sprintRequested =
					!sprintRequested;
			}
		}
		else
		{
			sprintRequested =
				Input.GetKey(sprintKey);
		}

		if (!sprintRequested)
		{
			return;
		}

		walkRequested = false;

		ExitCrouchForMovementMode();
	}

	private void HandleCrouchInput()
	{
		if (crouchBlockedUntilRelease)
		{
			isCrouching = false;

			if (!Input.GetKey(crouchKey))
			{
				crouchBlockedUntilRelease = false;
			}

			return;
		}

		if (toggleCrouch)
		{
			if (Input.GetKeyDown(crouchKey))
			{
				isCrouching =
					!isCrouching;
			}
		}
		else
		{
			isCrouching =
				Input.GetKey(crouchKey);
		}

		if (isCrouching)
		{
			walkRequested = false;
			sprintRequested = false;
		}
	}

	private void ExitCrouchForMovementMode()
	{
		if (!isCrouching)
		{
			return;
		}

		isCrouching = false;

		/*
		 * With hold-to-crouch enabled, prevent the crouch key
		 * from immediately putting the player back into crouch.
		 */
		if (!toggleCrouch &&
			Input.GetKey(crouchKey))
		{
			crouchBlockedUntilRelease = true;
		}
	}

	private void HandleMovement()
	{
		float horizontal =
			Input.GetAxisRaw("Horizontal");

		float vertical =
			Input.GetAxisRaw("Vertical");

		Vector3 inputDirection =
			transform.right * horizontal +
			transform.forward * vertical;

		inputDirection =
			Vector3.ClampMagnitude(
				inputDirection,
				1f);

		bool hasMovementInput =
			inputDirection.sqrMagnitude > 0.001f;

		bool canSprint =
			sprintRequested &&
			!isCrouching &&
			vertical > 0f &&
			hasMovementInput;

		isSprinting =
			canSprint;

		isWalking =
			walkRequested &&
			!isCrouching &&
			!isSprinting &&
			hasMovementInput;

		float currentSpeed;

		if (isCrouching)
		{
			currentSpeed =
				crouchSpeed;
		}
		else if (isSprinting)
		{
			currentSpeed =
				sprintSpeed;
		}
		else if (isWalking)
		{
			currentSpeed =
				walkSpeed;
		}
		else
		{
			currentSpeed =
				runSpeed;
		}

		Vector3 movement =
			inputDirection * currentSpeed;

		movement.y =
			verticalVelocity.y;

		characterController.Move(
			movement * Time.deltaTime);
	}

	private void HandleJumpAndGravity()
	{
		bool isGrounded =
			characterController.isGrounded;

		if (isGrounded &&
			verticalVelocity.y < 0f)
		{
			verticalVelocity.y =
				groundedForce;
		}

		if (isGrounded &&
			!isCrouching &&
			Input.GetKeyDown(jumpKey))
		{
			float downwardGravity =
				Mathf.Min(
					gravity,
					-0.01f);

			verticalVelocity.y =
				Mathf.Sqrt(
					jumpHeight *
					-2f *
					downwardGravity);
		}

		verticalVelocity.y +=
			gravity * Time.deltaTime;

		verticalVelocity.y =
			Mathf.Max(
				verticalVelocity.y,
				terminalVelocity);
	}

	private void HandleCrouchTransition()
	{
		float targetHeight =
			isCrouching
				? crouchingHeight
				: standingHeight;

		Vector3 targetCenter =
			isCrouching
				? crouchingCenter
				: standingCenter;

		characterController.height =
			Mathf.Lerp(
				characterController.height,
				targetHeight,
				crouchTransitionSpeed *
				Time.deltaTime);

		characterController.center =
			Vector3.Lerp(
				characterController.center,
				targetCenter,
				crouchTransitionSpeed *
					Time.deltaTime);

		if (cameraHolder == null)
		{
			return;
		}

		Vector3 targetCameraPosition =
			isCrouching
				? crouchingCameraPosition
				: standingCameraPosition;

		cameraHolder.localPosition =
			Vector3.Lerp(
				cameraHolder.localPosition,
				targetCameraPosition,
				crouchTransitionSpeed *
					Time.deltaTime);
	}
}