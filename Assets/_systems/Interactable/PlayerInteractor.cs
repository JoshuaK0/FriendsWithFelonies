using UnityEngine;

public sealed class PlayerInteractor : MonoBehaviour
{
	[Header("Search")]
	[SerializeField] private Camera cam;
	[SerializeField] private float maxInteractionDistance = 3f;

	[SerializeField, Range(1f, 180f)]
	private float maxAngle = 25f;

	[SerializeField]
	private LayerMask interactableMask = ~0;

	[Header("Line of Sight")]
	[SerializeField]
	private LayerMask losBlockMask = ~0;

	[Header("UI")]
	[SerializeField]
	private InteractIconUI iconUI;

	[Header("Performance")]
	[SerializeField]
	private int maxColliders = 32;

	private Collider[] _buffer;

	private IInteractable _current;

	private bool _canInteract;
	private string _cannotInteractReason = string.Empty;

	public IInteractable CurrentInteractable =>
		_current;

	public bool CanInteract =>
		_current != null && _canInteract;

	public string CannotInteractReason =>
		_cannotInteractReason;

	private void Awake()
	{
		if (cam == null)
			cam = Camera.main;

		_buffer =
			new Collider[Mathf.Max(4, maxColliders)];
	}

	private void Update()
	{
		UpdateTarget();

		if (_current == null || !Input.GetKeyDown(KeyCode.E))
			return;

		if (!_current.CanInteract(gameObject, out string reason))
		{
			Debug.Log(
				string.IsNullOrEmpty(reason)
					? "Cannot interact."
					: reason,
				gameObject);

			return;
		}

		_current.Interact(gameObject);
	}

	private void UpdateTarget()
	{
		_current = null;
		_canInteract = false;
		_cannotInteractReason = string.Empty;

		if (cam == null || iconUI == null)
			return;

		Vector3 origin =
			cam.transform.position;

		Vector3 forward =
			cam.transform.forward;

		int count = Physics.OverlapSphereNonAlloc(
			origin,
			maxInteractionDistance,
			_buffer,
			interactableMask,
			QueryTriggerInteraction.Collide);

		float bestScore =
			float.PositiveInfinity;

		Transform bestAnchor = null;

		IInteractable bestInteractable = null;

		for (int i = 0; i < count; i++)
		{
			Collider col = _buffer[i];

			if (col == null)
				continue;

			IInteractable interactable =
				col.GetComponentInParent<IInteractable>();

			if (interactable == null)
				continue;

			Transform anchor =
				interactable.IconAnchor != null
					? interactable.IconAnchor
					: col.transform;

			Vector3 to =
				anchor.position - origin;

			float dist =
				to.magnitude;

			if (dist <= 0.0001f ||
				dist > maxInteractionDistance)
			{
				continue;
			}

			Vector3 dir =
				to / dist;

			float angle =
				Vector3.Angle(forward, dir);

			if (angle > maxAngle)
				continue;

			// Check line of sight.
			// Trigger colliders are included.
			if (Physics.Raycast(
				origin,
				dir,
				out RaycastHit hit,
				dist,
				losBlockMask,
				QueryTriggerInteraction.Collide))
			{
				IInteractable hitInteractable =
					hit.collider
						.GetComponentInParent<IInteractable>();

				// Something blocked the ray unless
				// what we hit belongs to the same
				// interactable.
				if (hitInteractable != interactable)
					continue;
			}

			float score =
				angle * 10f + dist;

			if (score < bestScore)
			{
				bestScore = score;
				bestAnchor = anchor;
				bestInteractable = interactable;
			}
		}

		if (bestInteractable == null)
		{
			iconUI.Hide();
			return;
		}

		_current = bestInteractable;

		_canInteract =
			_current.CanInteract(
				gameObject,
				out _cannotInteractReason);

		// Successful interactions should never
		// expose an old/blocking reason.
		if (_canInteract)
			_cannotInteractReason = string.Empty;

		iconUI.Show(bestAnchor, cam);
	}

	public bool CanInteractWith(
		IInteractable interactable)
	{
		return
			_current == interactable &&
			_canInteract;
	}
}