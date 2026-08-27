using UnityEngine;

public sealed class PlayerInteractor : MonoBehaviour
{
	[Header("Input")]
	[SerializeField] private KeyCode interactKey = KeyCode.E;

	[Header("Search")]
	[SerializeField] private Camera cam;
	[SerializeField, Min(0f)] private float maxInteractionDistance = 3f;
	[SerializeField, Range(1f, 180f)] private float maxAngle = 25f;
	[SerializeField] private LayerMask interactableMask = ~0;

	[Header("Line of Sight")]
	[SerializeField] private LayerMask losBlockMask = ~0;

	[Header("UI")]
	[SerializeField] private InteractIconUI iconUI;

	[Header("Performance")]
	[SerializeField, Min(4)] private int maxColliders = 32;

	[Header("Debug")]
	[SerializeField] private bool drawDetectionRays;

	private Collider[] colliderBuffer;

	private IInteractable current;
	private Transform currentAnchor;
	private bool currentUsesDirectRaycast;
	private bool canInteract;
	private string cannotInteractReason = string.Empty;

	private IInteractable holdTarget;
	private float holdTime;
	private bool isHolding;
	private bool waitForKeyRelease;

	public IInteractable CurrentInteractable => current;
	public bool CanInteract => current != null && canInteract;
	public string CannotInteractReason => cannotInteractReason;

	private void Awake()
	{
		if (cam == null)
			cam = Camera.main;

		colliderBuffer = new Collider[Mathf.Max(4, maxColliders)];
	}

	private void Update()
	{
		IInteractable previous = current;

		FindCurrentTarget();

		if (!ReferenceEquals(previous, current))
			CancelHold();

		UpdateAvailability();
		UpdateInteractionInput();
		UpdateIcon();
	}

	private void OnDisable()
	{
		CancelHold();
		iconUI?.Hide();
	}

	private void FindCurrentTarget()
	{
		current = null;
		currentAnchor = null;
		currentUsesDirectRaycast = false;

		if (cam == null)
			return;

		// An exact mouse ray wins over the wider cone search.
		if (TryFindDirectRaycastTarget(out IInteractable directTarget))
		{
			current = directTarget;
			currentUsesDirectRaycast = true;
			return;
		}

		TryFindConeTarget(out current, out currentAnchor);
	}

	private bool TryFindDirectRaycastTarget(out IInteractable interactable)
	{
		interactable = null;

		Ray ray = cam.ScreenPointToRay(Input.mousePosition);
		int rayMask = interactableMask.value | losBlockMask.value;

		if (drawDetectionRays)
			Debug.DrawRay(ray.origin, ray.direction * maxInteractionDistance, Color.blue);

		if (!Physics.Raycast(
			ray,
			out RaycastHit hit,
			maxInteractionDistance,
			rayMask,
			QueryTriggerInteraction.Collide))
		{
			return false;
		}

		// The first hit can be an LOS blocker. Only colliders on the
		// interactable mask are allowed to become an interaction target.
		if (!LayerIsInMask(hit.collider.gameObject.layer, interactableMask))
			return false;

		IInteractable hitInteractable =
			hit.collider.GetComponentInParent<IInteractable>();

		if (hitInteractable == null || !hitInteractable.UseDirectRaycast)
			return false;

		interactable = hitInteractable;
		return true;
	}

	private bool TryFindConeTarget(
		out IInteractable bestInteractable,
		out Transform bestAnchor)
	{
		bestInteractable = null;
		bestAnchor = null;

		Vector3 origin = cam.transform.position;
		Vector3 forward = cam.transform.forward;

		int count = Physics.OverlapSphereNonAlloc(
			origin,
			maxInteractionDistance,
			colliderBuffer,
			interactableMask,
			QueryTriggerInteraction.Collide);

		float bestScore = float.PositiveInfinity;

		for (int i = 0; i < count; i++)
		{
			Collider candidateCollider = colliderBuffer[i];

			if (candidateCollider == null)
				continue;

			IInteractable candidate =
				candidateCollider.GetComponentInParent<IInteractable>();

			if (candidate == null || candidate.UseDirectRaycast)
				continue;

			Transform anchor = GetIconAnchor(
				candidate,
				candidateCollider.transform);
			Vector3 targetPosition =
				anchor != null
					? anchor.position
					: candidateCollider.transform.position;

			Vector3 toTarget = targetPosition - origin;
			float distance = toTarget.magnitude;

			if (distance <= 0.0001f || distance > maxInteractionDistance)
				continue;

			Vector3 direction = toTarget / distance;
			float angle = Vector3.Angle(forward, direction);

			if (angle > maxAngle)
				continue;

			if (!HasLineOfSight(origin, direction, distance, candidate))
				continue;

			// Looking directly at something matters more than a small
			// difference in distance.
			float score = angle * 10f + distance;

			if (score >= bestScore)
				continue;

			bestScore = score;
			bestInteractable = candidate;
			bestAnchor = anchor != null
				? anchor
				: candidateCollider.transform;
		}

		return bestInteractable != null;
	}

	private bool HasLineOfSight(
		Vector3 origin,
		Vector3 direction,
		float distance,
		IInteractable candidate)
	{
		if (drawDetectionRays)
			Debug.DrawRay(origin, direction * distance, Color.cyan);

		if (!Physics.Raycast(
			origin,
			direction,
			out RaycastHit hit,
			distance,
			losBlockMask,
			QueryTriggerInteraction.Collide))
		{
			return true;
		}

		IInteractable hitInteractable =
			hit.collider.GetComponentInParent<IInteractable>();

		return ReferenceEquals(hitInteractable, candidate);
	}

	private void UpdateAvailability()
	{
		canInteract = false;
		cannotInteractReason = string.Empty;

		if (current == null)
			return;

		canInteract = current.CanInteract(
			gameObject,
			out cannotInteractReason);

		if (canInteract)
			cannotInteractReason = string.Empty;
	}

	private void UpdateInteractionInput()
	{
		if (Input.GetKeyUp(interactKey))
		{
			waitForKeyRelease = false;
			CancelHold();
		}

		if (current == null || !canInteract)
		{
			CancelHold();
			return;
		}

		if (waitForKeyRelease)
			return;

		float duration = Mathf.Max(0f, current.InteractionDuration);

		if (duration <= 0f)
		{
			if (Input.GetKeyDown(interactKey))
				CompleteInteraction();

			return;
		}

		if (Input.GetKeyDown(interactKey))
		{
			holdTarget = current;
			holdTime = 0f;
			isHolding = true;
		}

		if (!isHolding || !Input.GetKey(interactKey))
			return;

		if (!ReferenceEquals(holdTarget, current))
		{
			CancelHold();
			return;
		}

		holdTime += Time.deltaTime;

		if (holdTime >= duration)
			CompleteInteraction();
	}

	private void CompleteInteraction()
	{
		IInteractable target = current;
		string reason = string.Empty;

		CancelHold();
		waitForKeyRelease = true;

		// Recheck at completion because the interaction may have become
		// invalid while the key was being held.
		if (target == null ||
			!target.CanInteract(gameObject, out reason))
		{
			cannotInteractReason = reason ?? string.Empty;
			canInteract = false;
			return;
		}

		target.Interact(gameObject);
	}

	private void UpdateIcon()
	{
		if (iconUI == null)
			return;

		if (current == null)
		{
			iconUI.Hide();
			return;
		}

		if (currentUsesDirectRaycast)
			iconUI.ShowCentered();
		else
			iconUI.ShowWorld(currentAnchor, cam);

		float duration = Mathf.Max(0f, current.InteractionDuration);
		bool showProgress = isHolding && duration > 0f;
		float progress = showProgress ? holdTime / duration : 0f;

		iconUI.SetHoldProgress(progress, showProgress);
	}

	private void CancelHold()
	{
		holdTarget = null;
		holdTime = 0f;
		isHolding = false;

		iconUI?.SetHoldProgress(0f, false);
	}

	public bool CanInteractWith(IInteractable interactable)
	{
		return ReferenceEquals(current, interactable) && canInteract;
	}

	private static bool LayerIsInMask(int layer, LayerMask mask)
	{
		return (mask.value & (1 << layer)) != 0;
	}

	private static Transform GetIconAnchor(
		IInteractable interactable,
		Transform fallback)
	{
		Component interactableComponent = interactable as Component;

		if (interactableComponent == null)
			return fallback;

		InteractIconAnchor customAnchor =
			interactableComponent.GetComponent<InteractIconAnchor>();

		return customAnchor != null
			? customAnchor.Anchor
			: interactableComponent.transform;
	}
}
