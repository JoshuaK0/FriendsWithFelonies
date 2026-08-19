// PlayerMovement version: sprint gate delay + smoothing + stance clearance + crouch-to-run + air control
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private float walkSpeed = 2.5f;
	[SerializeField] private float runSpeed = 5f;
	[SerializeField] private float sprintSpeed = 8f;
	[SerializeField] private float crouchSpeed = 2.5f;

	[Header("Movement Smoothing")]
	[Tooltip("How quickly horizontal movement accelerates toward the requested velocity.")]
	[SerializeField, Min(0f)] private float movementAcceleration = 30f;

	[Tooltip("How quickly horizontal movement slows when there is no movement input.")]
	[SerializeField, Min(0f)] private float movementDeceleration = 40f;

	[Tooltip("Multiplier applied to horizontal acceleration/deceleration while airborne. 0 = no air control, 1 = full ground control.")]
	[SerializeField, Min(0f)] private float airControlMultiplier = 0.35f;

	[Header("Walking")]
	[SerializeField] private KeyCode walkKey = KeyCode.LeftAlt;
	[SerializeField] private bool toggleWalk;

	[Header("Sprint")]
	[SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
	[SerializeField] private bool toggleSprint;
	[SerializeField] private float minimumSprintForwardVelocity = 1f;

	[Tooltip("How long the player must remain below the sprint speed gate before sprint is cancelled.")]
	[SerializeField, Min(0f)] private float sprintLowSpeedExitDelay = 0.25f;

	[Header("Sprint FOV")]
	[SerializeField] private Camera playerCamera;
	[SerializeField] private float normalFov = 60f;
	[SerializeField] private float sprintFov = 70f;
	[SerializeField] private float fovTransitionSpeed = 8f;

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

	[Header("Ladders")]
	[SerializeField] private KeyCode ladderJumpKey = KeyCode.Space;

	[Tooltip("Additional upward velocity when jumping from a ladder.")]
	[SerializeField]
	private float ladderJumpUpwardVelocity = 5f;

	[Tooltip("Velocity applied in the direction the player is looking.")]
	[SerializeField]
	private float ladderJumpLookVelocity = 4f;

	[Tooltip("How quickly ladder jump horizontal momentum disappears.")]
	[SerializeField]
	private float ladderJumpHorizontalDeceleration = 8f;

	[Tooltip("Small distance used when stepping off the edge of a ladder.")]
	[SerializeField]
	private float ladderExitOffset = 0.15f;

	[Header("Ladder Clearance")]
	[SerializeField]
	private LayerMask ladderExitObstructionMask = ~0;

	[SerializeField]
	private float clearanceCheckInset = 0.02f;

	[SerializeField]
	private float platformClearance = 0.02f;

	private CharacterController characterController;

	private Vector3 verticalVelocity;

	/*
	 * Player-controlled horizontal velocity.
	 * This is smoothed toward the requested movement velocity
	 * instead of snapping instantly to full speed.
	 */
	private Vector3 smoothedHorizontalVelocity;

	private Vector3 ladderJumpHorizontalVelocity;

	private Vector3 standingCenter;
	private Vector3 crouchingCenter;

	private Vector3 standingCameraPosition;
	private Vector3 crouchingCameraPosition;

	private bool walkRequested;
	private bool sprintRequested;

	private bool isWalking;
	private bool isSprinting;
	private bool isCrouching;

	private bool forcedCrouch;

	private bool crouchBlockedUntilRelease;

	/*
	 * When Space is used to leave crouch, hold-to-walk should not
	 * immediately force the player back into walking until the key
	 * has been released.
	 */
	private bool walkBlockedUntilRelease;

	/*
	 * True only on the frame Space successfully changes crouch to run.
	 * This prevents the same Space press from also causing a jump.
	 */
	private bool crouchRunTransitionThisFrame;

	/*
	 * When hold-to-sprint is enabled and sprint is cancelled
	 * because forward velocity fell too low while decelerating,
	 * require the sprint key to be released before sprint can
	 * be requested again.
	 */
	private bool sprintBlockedUntilRelease;

	/*
	 * Used to determine whether forward velocity
	 * is currently decreasing.
	 */
	private float previousForwardVelocity;

	/*
	 * Time continuously spent below the sprint speed gate after
	 * the gate has been triggered by actual deceleration.
	 */
	private float sprintLowSpeedTimer;

	/*
	 * The low-speed countdown only becomes active after an
	 * already-sprinting player drops below the gate while
	 * decelerating. Once active, remaining below the gate keeps
	 * the timer running even if the speed becomes temporarily stable.
	 */
	private bool sprintLowSpeedTimerActive;

	private Ladder currentLadder;

	private bool isClimbing;

	/*
	 * Prevents immediately grabbing the same ladder again
	 * after deliberately leaving it.
	 */
	private bool ladderReentryBlocked;

	/*
	 * We keep track of whether the player is physically
	 * inside the ladder trigger separately from currentLadder.
	 *
	 * This allows the player to continue climbing upward
	 * through the top platform even after leaving the
	 * trigger's upper edge.
	 */
	private bool insideCurrentLadderTrigger;

	private bool jumpedFromLadderThisFrame;

	/*
	 * The top platform currently being ignored by this
	 * player's CharacterController.
	 */
	private Collider ignoredLadderPlatform;

	public bool IsWalking => isWalking;
	public bool IsSprinting => isSprinting;
	public bool IsCrouching => isCrouching;
	public bool IsClimbing => isClimbing;

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
				Vector3.up *
				crouchingCameraOffset;
		}

		if (playerCamera != null)
		{
			playerCamera.fieldOfView =
				normalFov;
		}
	}

	private void OnDisable()
	{
		RestoreIgnoredLadderPlatform();
	}

	private void Update()
	{
		jumpedFromLadderThisFrame = false;
		crouchRunTransitionThisFrame = false;

		UpdateIgnoredLadderPlatform();

		HandleLadderState();

		if (isClimbing)
		{
			HandleLadderMovement();

			HandleCrouchTransition();
			HandleCameraFov();

			return;
		}

		HandleCrouchInput();
		HandleWalkInput();
		HandleSprintInput();

		HandleJumpAndGravity();
		HandleMovement();

		HandleCrouchTransition();
		HandleCameraFov();
	}

	// --------------------------------------------------
	// WALKING
	// --------------------------------------------------

	private void HandleWalkInput()
	{
		/*
		 * Space leaving crouch always selects running.
		 * For hold-to-walk, require the already-held walk key
		 * to be released before walking can be requested again.
		 */
		if (!toggleWalk &&
			walkBlockedUntilRelease)
		{
			if (!Input.GetKey(walkKey))
			{
				walkBlockedUntilRelease = false;
			}
			else
			{
				walkRequested = false;
				return;
			}
		}

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

	// --------------------------------------------------
	// SPRINTING
	// --------------------------------------------------

	private void HandleSprintInput()
	{
		/*
		 * If sprint was automatically cancelled because the
		 * player decelerated below the minimum sprint velocity,
		 * hold-to-sprint requires a release before it can be
		 * requested again.
		 */
		if (!toggleSprint &&
			sprintBlockedUntilRelease)
		{
			if (!Input.GetKey(sprintKey))
			{
				sprintBlockedUntilRelease = false;
			}
			else
			{
				sprintRequested = false;
				return;
			}
		}

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

		/*
		 * Sprinting requires the standing capsule.
		 * If there is not enough headroom, remain crouched
		 * and do not begin sprinting.
		 */
		if (isCrouching &&
			!CanStand())
		{
			if (toggleSprint)
			{
				sprintRequested = false;
			}

			return;
		}

		walkRequested = false;

		ExitCrouchForMovementMode();
	}

	// --------------------------------------------------
	// CROUCHING
	// --------------------------------------------------

	private void HandleCrouchInput()
	{
		/*
		 * If a ladder exit forced us into a crouch,
		 * remain crouched until standing space becomes
		 * available.
		 */
		if (forcedCrouch)
		{
			if (!CanStand())
			{
				isCrouching = true;
				return;
			}

			forcedCrouch = false;
		}

		/*
		 * SPACE WHILE CROUCHED -> RUN
		 *
		 * This is a stance/movement-mode transition, not a jump.
		 * Only stand when the complete standing capsule fits.
		 */
		if (isCrouching &&
			Input.GetKeyDown(jumpKey))
		{
			if (CanStand())
			{
				isCrouching = false;
				forcedCrouch = false;

				// Explicitly choose running rather than walking or sprinting.
				walkRequested = false;
				sprintRequested = false;

				crouchRunTransitionThisFrame = true;

				/*
				 * If a hold-style movement modifier is already down,
				 * require it to be released before it can override run.
				 */
				if (!toggleWalk &&
					Input.GetKey(walkKey))
				{
					walkBlockedUntilRelease = true;
				}

				if (!toggleSprint &&
					Input.GetKey(sprintKey))
				{
					sprintBlockedUntilRelease = true;
				}

				/*
				 * If hold-to-crouch is still held, do not immediately
				 * crouch again on the next frame.
				 */
				if (!toggleCrouch &&
					Input.GetKey(crouchKey))
				{
					crouchBlockedUntilRelease = true;
				}
			}

			/*
			 * Consume this Space press even when standing clearance
			 * is blocked, so crouching under a ceiling cannot jump.
			 */
			return;
		}

		if (crouchBlockedUntilRelease)
		{
			/*
			 * Even though another movement mode previously
			 * forced us to stand, never keep the standing
			 * stance if the standing capsule no longer fits.
			 */
			if (!CanStand())
			{
				isCrouching = true;
				return;
			}

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
				if (isCrouching)
				{
					/*
					 * Only leave crouch when the complete
					 * standing capsule fits.
					 */
					if (CanStand())
					{
						isCrouching = false;
					}
				}
				else
				{
					isCrouching = true;
				}
			}
		}
		else
		{
			if (Input.GetKey(crouchKey))
			{
				isCrouching = true;
			}
			else
			{
				/*
				 * Releasing crouch only stands the player
				 * when there is enough room.
				 */
				if (CanStand())
				{
					isCrouching = false;
				}
				else
				{
					isCrouching = true;
				}
			}
		}

		if (isCrouching)
		{
			walkRequested = false;
			sprintRequested = false;
		}
	}

	private bool CanStand()
	{
		return CanFitAtPosition(
			transform.position,
			standingHeight,
			standingCenter);
	}

	private void ExitCrouchForMovementMode()
	{
		if (!isCrouching)
		{
			return;
		}

		/*
		 * Walking or sprinting may only force the player
		 * out of crouch when the full standing capsule fits.
		 */
		if (!CanStand())
		{
			return;
		}

		isCrouching = false;
		forcedCrouch = false;

		if (!toggleCrouch &&
			Input.GetKey(crouchKey))
		{
			crouchBlockedUntilRelease = true;
		}
	}

	// --------------------------------------------------
	// NORMAL MOVEMENT
	// --------------------------------------------------

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
			inputDirection.sqrMagnitude >
			0.001f;

		/*
		 * CharacterController.velocity represents the actual
		 * velocity produced by the previous Move call, including
		 * the effect of collisions. Keep using this for sprint
		 * deceleration detection rather than the requested velocity.
		 */
		Vector3 actualHorizontalVelocity =
			characterController.velocity;

		actualHorizontalVelocity.y = 0f;

		float forwardVelocity =
			Vector3.Dot(
				actualHorizontalVelocity,
				transform.forward);

		/*
		 * isSprinting still contains last frame's state here.
		 * Keep it so the low-velocity rule only cancels a
		 * sprint that was already active. It must not prevent
		 * the player from beginning a sprint from low speed.
		 */
		bool wasSprinting =
			isSprinting;

		bool isDecelerating =
			forwardVelocity <
			previousForwardVelocity - 0.01f;

		bool belowSprintSpeedGate =
			forwardVelocity <
			minimumSprintForwardVelocity;

		/*
		 * SPRINT LOW-SPEED EXIT DELAY
		 *
		 * The countdown may only START when:
		 *  - the player was already sprinting,
		 *  - sprint is still requested,
		 *  - forward speed is below the gate, and
		 *  - forward speed is actively decreasing.
		 *
		 * Once started, the player must remain below the speed gate
		 * continuously for sprintLowSpeedExitDelay seconds. Returning
		 * to or above the speed gate immediately resets the countdown.
		 */
		if (!wasSprinting ||
			!sprintRequested ||
			!belowSprintSpeedGate)
		{
			sprintLowSpeedTimer = 0f;
			sprintLowSpeedTimerActive = false;
		}
		else
		{
			if (!sprintLowSpeedTimerActive &&
				isDecelerating)
			{
				sprintLowSpeedTimerActive = true;
				sprintLowSpeedTimer = 0f;
			}

			if (sprintLowSpeedTimerActive)
			{
				sprintLowSpeedTimer +=
					Time.deltaTime;
			}
		}

		bool shouldExitSprintForLowVelocity =
			sprintLowSpeedTimerActive &&
			sprintLowSpeedTimer >=
				sprintLowSpeedExitDelay;

		if (shouldExitSprintForLowVelocity)
		{
			sprintRequested = false;
			sprintLowSpeedTimer = 0f;
			sprintLowSpeedTimerActive = false;

			/*
			 * With hold-to-sprint, require the sprint key to
			 * be released before another sprint can begin.
			 */
			if (!toggleSprint &&
				Input.GetKey(sprintKey))
			{
				sprintBlockedUntilRelease = true;
			}
		}

		/*
		 * Normally sprinting requires forward input. However, once the
		 * player is already sprinting, keep the sprint state alive while
		 * forward input is released so the smoothed velocity can naturally
		 * decelerate through the sprint speed gate.
		 *
		 * Without this, releasing W immediately sets isSprinting false,
		 * which resets the low-speed timer on the following frame.
		 */
		bool maintainingSprintDuringDeceleration =
			wasSprinting &&
			!hasMovementInput;

		bool canSprint =
			sprintRequested &&
			!isCrouching &&
			(
				(vertical > 0f &&
				 hasMovementInput) ||
				maintainingSprintDuringDeceleration
			);

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

		Vector3 targetHorizontalVelocity =
			hasMovementInput
				? inputDirection * currentSpeed
				: Vector3.zero;

		/*
		 * Smooth acceleration and deceleration.
		 *
		 * MoveTowards is used rather than Lerp so these values are
		 * easy to reason about: they are velocity change per second.
		 */
		bool isReducingHorizontalSpeed =
			!hasMovementInput ||
			targetHorizontalVelocity.sqrMagnitude <
				smoothedHorizontalVelocity.sqrMagnitude;

		float smoothingRate =
			isReducingHorizontalSpeed
				? movementDeceleration
				: movementAcceleration;

		/*
		 * AIR CONTROL
		 *
		 * Scale how quickly the player can alter horizontal velocity
		 * while airborne. The target speed itself is unchanged, so this
		 * controls steering/braking rather than simply lowering air speed.
		 *
		 * verticalVelocity > 0 catches the take-off frame before the
		 * CharacterController has reported itself as airborne.
		 */
		bool isAirborne =
			!characterController.isGrounded ||
			verticalVelocity.y > 0f;

		if (isAirborne)
		{
			smoothingRate *=
				airControlMultiplier;
		}

		smoothedHorizontalVelocity =
			Vector3.MoveTowards(
				smoothedHorizontalVelocity,
				targetHorizontalVelocity,
				smoothingRate *
				Time.deltaTime);

		Vector3 movement =
			smoothedHorizontalVelocity;

		/*
		 * Add momentum remaining from a ladder jump separately
		 * so normal movement smoothing does not erase the jump.
		 */
		movement +=
			ladderJumpHorizontalVelocity;

		movement.y =
			verticalVelocity.y;

		characterController.Move(
			movement *
			Time.deltaTime);

		ladderJumpHorizontalVelocity =
			Vector3.MoveTowards(
				ladderJumpHorizontalVelocity,
				Vector3.zero,
				ladderJumpHorizontalDeceleration *
				Time.deltaTime);

		/*
		 * Save the actual forward velocity for next frame's
		 * sprint deceleration comparison.
		 */
		previousForwardVelocity =
			forwardVelocity;
	}

	// --------------------------------------------------
	// JUMP / GRAVITY
	// --------------------------------------------------

	private void HandleJumpAndGravity()
	{
		bool grounded =
			characterController.isGrounded;

		if (grounded &&
			verticalVelocity.y < 0f)
		{
			verticalVelocity.y =
				groundedForce;
		}

		if (grounded &&
			!isCrouching &&
			!jumpedFromLadderThisFrame &&
			!crouchRunTransitionThisFrame &&
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
			gravity *
			Time.deltaTime;

		verticalVelocity.y =
			Mathf.Max(
				verticalVelocity.y,
				terminalVelocity);
	}

	// --------------------------------------------------
	// LADDER STATE
	// --------------------------------------------------

	private void HandleLadderState()
	{
		if (currentLadder == null)
		{
			isClimbing = false;
			ladderReentryBlocked = false;

			return;
		}

		if (isClimbing)
		{
			if (Input.GetKeyDown(
				ladderJumpKey))
			{
				TryJumpFromLadder();
			}

			return;
		}

		if (ladderReentryBlocked)
		{
			return;
		}

		float verticalInput =
			Input.GetAxisRaw("Vertical");

		/*
		 * W/S attaches to the ladder.
		 *
		 * A/D only moves sideways once already attached.
		 */
		if (Mathf.Abs(verticalInput) >
			0.01f)
		{
			EnterLadder();
		}
	}

	private void EnterLadder()
	{
		if (currentLadder == null)
		{
			return;
		}

		isClimbing = true;

		isWalking = false;
		isSprinting = false;

		walkRequested = false;
		sprintRequested = false;

		verticalVelocity =
			Vector3.zero;

		smoothedHorizontalVelocity =
			Vector3.zero;

		ladderJumpHorizontalVelocity =
			Vector3.zero;

		/*
		 * Reset this so leaving a ladder cannot cause
		 * stale pre-ladder velocity to be interpreted
		 * as sprint deceleration.
		 */
		previousForwardVelocity = 0f;

		/*
		 * If standing space is available, use the
		 * standing capsule on the ladder.
		 *
		 * Otherwise retain a crouching capsule.
		 */
		if (CanFitAtPosition(
			transform.position,
			standingHeight,
			standingCenter))
		{
			SetStandingImmediately();
		}
		else
		{
			SetCrouchingImmediately();
		}

		IgnoreLadderPlatform();
	}

	// --------------------------------------------------
	// LADDER MOVEMENT
	// --------------------------------------------------

	private void HandleLadderMovement()
	{
		if (currentLadder == null)
		{
			isClimbing = false;
			return;
		}

		float horizontal =
			Input.GetAxisRaw("Horizontal");

		float vertical =
			Input.GetAxisRaw("Vertical");

		/*
		 * Process A/D and W/S separately.
		 *
		 * This means that if a side exit is blocked,
		 * the player can still continue climbing
		 * vertically.
		 */
		if (Mathf.Abs(horizontal) > 0.01f)
		{
			MoveLadderHorizontal(
				horizontal);
		}

		if (!isClimbing)
		{
			return;
		}

		if (Mathf.Abs(vertical) > 0.01f)
		{
			MoveLadderVertical(
				vertical);
		}

		verticalVelocity =
			Vector3.zero;
	}

	private void MoveLadderHorizontal(float input)
	{
		input = -input;

		Vector3 delta =
			currentLadder.Right *
			input *
			currentLadder.SideMoveSpeed *
			Time.deltaTime;

		Vector3 proposedPosition =
			transform.position +
			delta;

		Vector3 proposedCenter =
			GetControllerWorldCenterAt(
				proposedPosition,
				characterController.center);

		if (currentLadder.IsOutsideClimbArea(
			proposedCenter,
			out Vector3 exitDirection))
		{
			float sideExit =
				Vector3.Dot(
					exitDirection,
					currentLadder.Right);

			if (Mathf.Abs(sideExit) > 0.01f)
			{
				Vector3 sideDirection =
					currentLadder.Right *
					Mathf.Sign(sideExit);

				Vector3 exitPosition =
					proposedPosition +
					sideDirection *
					ladderExitOffset;

				TryExitLadder(exitPosition);

				return;
			}
		}

		characterController.Move(delta);
	}

	private void MoveLadderVertical(
		float input)
	{
		Vector3 delta =
			currentLadder.Up *
			input *
			currentLadder.ClimbSpeed *
			Time.deltaTime;

		Vector3 proposedPosition =
			transform.position +
			delta;

		Vector3 proposedCenter =
			GetControllerWorldCenterAt(
				proposedPosition,
				characterController.center);

		if (currentLadder.IsOutsideClimbArea(
			proposedCenter,
			out Vector3 exitDirection))
		{
			float verticalExit =
				Vector3.Dot(
					exitDirection,
					currentLadder.Up);

			/*
			 * TOP OF LADDER
			 */
			if (verticalExit > 0.01f)
			{
				if (currentLadder.TopPlatform != null)
				{
					/*
					 * A solid platform exists directly
					 * above the ladder.
					 *
					 * Its collision is currently being
					 * ignored for this player.
					 *
					 * Check whether there will actually
					 * be enough room to emerge onto it.
					 */
					if (!PrepareTopPlatformExit())
					{
						return;
					}

					characterController.Move(
						delta);

					if (IsFullyAboveTopPlatform())
					{
						CompleteTopPlatformExit();
					}

					return;
				}

				/*
				 * No platform assigned.
				 * Treat the top like a normal ladder edge.
				 */
				Vector3 exitPosition =
					proposedPosition +
					currentLadder.Up *
					ladderExitOffset;

				TryExitLadder(
					exitPosition);

				return;
			}

			/*
			 * BOTTOM OF LADDER
			 */
			if (verticalExit < -0.01f)
			{
				Vector3 exitPosition =
					proposedPosition -
					currentLadder.Up *
					ladderExitOffset;

				TryExitLadder(
					exitPosition);

				return;
			}
		}

		characterController.Move(
			delta);
	}

	// --------------------------------------------------
	// TOP PLATFORM
	// --------------------------------------------------

	private bool PrepareTopPlatformExit()
	{
		if (currentLadder == null ||
			currentLadder.TopPlatform == null)
		{
			return true;
		}

		Collider platform =
			currentLadder.TopPlatform;

		float platformTop =
			platform.bounds.max.y;

		/*
		 * Work out where the player would need to be
		 * for their feet to rest just above the
		 * platform.
		 */
		Vector3 standingExitPosition =
			GetRootPositionForBottomHeight(
				transform.position,
				platformTop +
				platformClearance,
				standingHeight,
				standingCenter);

		/*
		 * Prefer leaving the ladder standing.
		 */
		if (CanFitAtPosition(
			standingExitPosition,
			standingHeight,
			standingCenter))
		{
			if (isCrouching)
			{
				SetStandingImmediately();
			}

			forcedCrouch = false;

			return true;
		}

		Vector3 crouchingExitPosition =
			GetRootPositionForBottomHeight(
				transform.position,
				platformTop +
					platformClearance,
				crouchingHeight,
				crouchingCenter);

		/*
		 * Standing doesn't fit.
		 * Try crouching.
		 */
		if (CanFitAtPosition(
			crouchingExitPosition,
			crouchingHeight,
			crouchingCenter))
		{
			SetCrouchingImmediately();

			forcedCrouch = true;

			return true;
		}

		/*
		 * Neither capsule fits above the platform.
		 *
		 * Don't allow the player to climb through.
		 */
		return false;
	}

	private bool IsFullyAboveTopPlatform()
	{
		if (currentLadder == null ||
			currentLadder.TopPlatform == null)
		{
			return false;
		}

		float playerBottom =
			characterController.bounds.min.y;

		float platformTop =
			currentLadder.TopPlatform
				.bounds.max.y;

		return
			playerBottom >=
			platformTop +
			platformClearance;
	}

	private void CompleteTopPlatformExit()
	{
		isClimbing = false;

		ladderReentryBlocked = true;

		/*
		 * Give gravity a tiny downward amount so the
		 * controller immediately settles onto the
		 * platform once collision is restored.
		 */
		verticalVelocity.y =
			groundedForce;

		previousForwardVelocity = 0f;

		/*
		 * If we've already physically left the trigger,
		 * there is no reason to keep the ladder reference.
		 */
		if (!insideCurrentLadderTrigger)
		{
			currentLadder = null;
			ladderReentryBlocked = false;
		}
	}

	private void IgnoreLadderPlatform()
	{
		if (currentLadder == null)
		{
			return;
		}

		Collider platform =
			currentLadder.TopPlatform;

		if (platform == null)
		{
			return;
		}

		/*
		 * Restore any previous platform first.
		 */
		if (ignoredLadderPlatform != null &&
			ignoredLadderPlatform != platform)
		{
			RestoreIgnoredLadderPlatform();
		}

		Physics.IgnoreCollision(
			characterController,
			platform,
			true);

		ignoredLadderPlatform =
			platform;
	}

	private void UpdateIgnoredLadderPlatform()
	{
		if (ignoredLadderPlatform == null)
		{
			return;
		}

		/*
		 * While actively climbing, keep the platform
		 * ignored even before we've reached it.
		 */
		if (isClimbing)
		{
			return;
		}

		Bounds playerBounds =
			characterController.bounds;

		Bounds platformBounds =
			ignoredLadderPlatform.bounds;

		bool completelyAbove =
			playerBounds.min.y >
			platformBounds.max.y +
			platformClearance;

		bool noLongerIntersecting =
			!playerBounds.Intersects(
				platformBounds);

		/*
		 * Restore collision once we're safely above
		 * the platform or have moved away from it.
		 */
		if (completelyAbove ||
			noLongerIntersecting)
		{
			RestoreIgnoredLadderPlatform();
		}
	}

	private void RestoreIgnoredLadderPlatform()
	{
		if (ignoredLadderPlatform == null ||
			characterController == null)
		{
			ignoredLadderPlatform = null;
			return;
		}

		Physics.IgnoreCollision(
			characterController,
			ignoredLadderPlatform,
			false);

		ignoredLadderPlatform = null;
	}

	// --------------------------------------------------
	// LADDER JUMP
	// --------------------------------------------------

	private void TryJumpFromLadder()
	{
		if (currentLadder == null)
		{
			return;
		}

		Vector3 lookDirection =
			playerCamera != null
				? playerCamera.transform.forward
				: transform.forward;

		lookDirection.Normalize();

		/*
		 * Combine the camera's look direction with
		 * a separate guaranteed upward velocity.
		 */
		Vector3 jumpVelocity =
			lookDirection *
			ladderJumpLookVelocity;

		jumpVelocity +=
			Vector3.up *
			ladderJumpUpwardVelocity;

		Vector3 jumpDirection =
			jumpVelocity.normalized;

		Vector3 exitPosition =
			transform.position +
			jumpDirection *
			ladderExitOffset;

		/*
		 * Jumping is still an exit, so make sure
		 * there is enough room for either standing
		 * or crouching.
		 */
		if (!TryExitLadder(
			exitPosition))
		{
			return;
		}

		jumpedFromLadderThisFrame =
			true;

		verticalVelocity.y =
			jumpVelocity.y;

		ladderJumpHorizontalVelocity =
			Vector3.ProjectOnPlane(
				jumpVelocity,
				Vector3.up);

		previousForwardVelocity = 0f;
	}

	// --------------------------------------------------
	// GENERAL LADDER EXIT
	// --------------------------------------------------

	private bool TryExitLadder(
		Vector3 exitPosition)
	{
		/*
		 * Try standing first.
		 */
		if (CanFitAtPosition(
			exitPosition,
			standingHeight,
			standingCenter))
		{
			SetStandingImmediately();

			forcedCrouch = false;

			CompleteLadderExit(
				exitPosition);

			return true;
		}

		/*
		 * Standing is blocked.
		 * Try crouching.
		 */
		if (CanFitAtPosition(
			exitPosition,
			crouchingHeight,
			crouchingCenter))
		{
			SetCrouchingImmediately();

			forcedCrouch = true;

			CompleteLadderExit(
				exitPosition);

			return true;
		}

		/*
		 * No room at all.
		 *
		 * Remain attached to the ladder.
		 */
		return false;
	}

	private void CompleteLadderExit(
		Vector3 exitPosition)
	{
		isClimbing = false;

		ladderReentryBlocked = true;

		verticalVelocity =
			Vector3.zero;

		previousForwardVelocity = 0f;

		Vector3 displacement =
			exitPosition -
			transform.position;

		characterController.Move(
			displacement);

		if (!insideCurrentLadderTrigger)
		{
			currentLadder = null;
			ladderReentryBlocked = false;
		}
	}

	// --------------------------------------------------
	// CLEARANCE CHECKING
	// --------------------------------------------------

	private bool CanFitAtPosition(
		Vector3 rootPosition,
		float height,
		Vector3 center)
	{
		GetCapsuleAtPosition(
			rootPosition,
			height,
			center,
			out Vector3 pointA,
			out Vector3 pointB,
			out float radius);

		Collider[] overlaps =
			Physics.OverlapCapsule(
				pointA,
				pointB,
				radius,
				ladderExitObstructionMask,
				QueryTriggerInteraction.Ignore);

		foreach (Collider overlap in overlaps)
		{
			if (overlap == null)
			{
				continue;
			}

			/*
			 * Ignore the player's own controller.
			 */
			if (overlap ==
				characterController)
			{
				continue;
			}

			/*
			 * Ignore anything parented beneath the
			 * player object.
			 */
			if (overlap.transform ==
				transform ||
				overlap.transform.IsChildOf(
					transform))
			{
				continue;
			}

			/*
			 * The ladder's top platform is deliberately
			 * ignored while climbing through it.
			 */
			if (overlap ==
				ignoredLadderPlatform)
			{
				continue;
			}

			return false;
		}

		return true;
	}

	private void GetCapsuleAtPosition(
		Vector3 rootPosition,
		float height,
		Vector3 center,
		out Vector3 pointA,
		out Vector3 pointB,
		out float radius)
	{
		Vector3 scale =
			transform.lossyScale;

		float horizontalScale =
			Mathf.Max(
				Mathf.Abs(scale.x),
				Mathf.Abs(scale.z));

		float verticalScale =
			Mathf.Abs(scale.y);

		float originalRadius =
			characterController.radius *
			horizontalScale;

		radius =
			Mathf.Max(
				0.01f,
				originalRadius -
				clearanceCheckInset);

		float worldHeight =
			height *
			verticalScale;

		Vector3 scaledCenter =
			Vector3.Scale(
				center,
				scale);

		Vector3 worldCenter =
			rootPosition +
			transform.rotation *
			scaledCenter;

		/*
		 * Use the original radius here.
		 *
		 * Since the check radius itself is slightly
		 * smaller, this shrinks both the top and
		 * bottom of the clearance capsule by
		 * clearanceCheckInset.
		 *
		 * This prevents the floor we're standing on
		 * from being incorrectly treated as an
		 * obstruction.
		 */
		float halfSegment =
			Mathf.Max(
				0f,
				worldHeight * 0.5f -
				originalRadius);

		Vector3 offset =
			transform.up *
			halfSegment;

		pointA =
			worldCenter +
			offset;

		pointB =
			worldCenter -
			offset;
	}

	private Vector3 GetControllerWorldCenterAt(
		Vector3 rootPosition,
		Vector3 center)
	{
		Vector3 scaledCenter =
			Vector3.Scale(
				center,
				transform.lossyScale);

		return
			rootPosition +
			transform.rotation *
			scaledCenter;
	}

	private Vector3 GetRootPositionForBottomHeight(
		Vector3 sourcePosition,
		float desiredBottomHeight,
		float height,
		Vector3 center)
	{
		Vector3 scale =
			transform.lossyScale;

		float worldHeight =
			height *
			Mathf.Abs(scale.y);

		Vector3 scaledCenter =
			Vector3.Scale(
				center,
				scale);

		float worldCenterOffsetY =
			(
				transform.rotation *
				scaledCenter
			).y;

		Vector3 result =
			sourcePosition;

		result.y =
			desiredBottomHeight +
			worldHeight * 0.5f -
			worldCenterOffsetY;

		return result;
	}

	// --------------------------------------------------
	// IMMEDIATE STANCE CHANGES
	// --------------------------------------------------

	private void SetStandingImmediately()
	{
		isCrouching = false;

		characterController.height =
			standingHeight;

		characterController.center =
			standingCenter;
	}

	private void SetCrouchingImmediately()
	{
		isCrouching = true;

		characterController.height =
			crouchingHeight;

		characterController.center =
			crouchingCenter;
	}

	// --------------------------------------------------
	// CROUCH TRANSITION
	// --------------------------------------------------

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

	// --------------------------------------------------
	// CAMERA FOV
	// --------------------------------------------------

	private void HandleCameraFov()
	{
		if (playerCamera == null)
		{
			return;
		}

		float targetFov =
			isSprinting
				? sprintFov
				: normalFov;

		playerCamera.fieldOfView =
			Mathf.Lerp(
				playerCamera.fieldOfView,
				targetFov,
				fovTransitionSpeed *
					Time.deltaTime);
	}

	// --------------------------------------------------
	// LADDER TRIGGERS
	// --------------------------------------------------

	private void OnTriggerEnter(
		Collider other)
	{
		if (!other.TryGetComponent(
			out Ladder ladder))
		{
			return;
		}

		/*
		 * Don't replace an actively climbed ladder
		 * with another overlapping ladder trigger.
		 */
		if (isClimbing &&
			currentLadder != null &&
			currentLadder != ladder)
		{
			return;
		}

		currentLadder =
			ladder;

		insideCurrentLadderTrigger =
			true;
	}

	private void OnTriggerExit(
		Collider other)
	{
		if (!other.TryGetComponent(
			out Ladder ladder))
		{
			return;
		}

		if (ladder != currentLadder)
		{
			return;
		}

		insideCurrentLadderTrigger =
			false;

		/*
		 * If we're still climbing, retain the ladder
		 * reference.
		 *
		 * This is important when climbing upward
		 * through the top platform.
		 */
		if (isClimbing)
		{
			return;
		}

		currentLadder =
			null;

		ladderReentryBlocked =
			false;
	}
}