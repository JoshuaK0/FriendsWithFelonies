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

	[Header("Debug")]
	[SerializeField]
	private bool debugLogs = true;

	[SerializeField]
	private bool debugDrawRays = true;

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
		DebugLog("Awake started.");

		if (cam == null)
		{
			cam = Camera.main;

			DebugLog(
				cam != null
					? $"Camera was null. Found Camera.main: {cam.name}"
					: "Camera was null and Camera.main could not be found.");
		}
		else
		{
			DebugLog($"Using assigned camera: {cam.name}");
		}

		_buffer =
			new Collider[Mathf.Max(4, maxColliders)];

		DebugLog(
			$"Collider buffer created. Size: {_buffer.Length}");
	}

	private void Update()
	{
		UpdateTarget();

		if (!Input.GetKeyDown(KeyCode.E))
			return;

		DebugLog("E pressed.");

		if (_current == null)
		{
			DebugLog(
				"E pressed but there is no current interactable.");

			return;
		}

		DebugLog(
			$"Attempting interaction with: {GetInteractableName(_current)}");

		if (!_current.CanInteract(
			gameObject,
			out string reason))
		{
			Debug.Log(
				string.IsNullOrEmpty(reason)
					? "[PlayerInteractor] Cannot interact. No reason supplied."
					: $"[PlayerInteractor] Cannot interact: {reason}",
				gameObject);

			return;
		}

		DebugLog(
			$"CanInteract returned TRUE. Calling Interact() on {GetInteractableName(_current)}");

		_current.Interact(gameObject);
	}

	private void UpdateTarget()
	{
		DebugLog("");
		DebugLog("========== UPDATE TARGET ==========");

		_current = null;
		_canInteract = false;
		_cannotInteractReason = string.Empty;

		if (cam == null)
		{
			DebugLog(
				"ABORT: Camera is null.");

			return;
		}

		if (iconUI == null)
		{
			DebugLog(
				"ABORT: InteractIconUI is null.");

			return;
		}

		Vector3 origin =
			cam.transform.position;

		Vector3 forward =
			cam.transform.forward;

		DebugLog(
			$"Camera origin: {origin}");

		DebugLog(
			$"Camera forward: {forward}");

		DebugLog(
			$"Max distance: {maxInteractionDistance}");

		DebugLog(
			$"Max angle: {maxAngle}");

		int count = Physics.OverlapSphereNonAlloc(
			origin,
			maxInteractionDistance,
			_buffer,
			interactableMask,
			QueryTriggerInteraction.Collide);

		DebugLog(
			$"OverlapSphere found {count} collider(s).");

		if (count >= _buffer.Length)
		{
			Debug.LogWarning(
				$"[PlayerInteractor] OverlapSphere filled the entire buffer ({_buffer.Length}). " +
				"Some colliders may have been missed. Increase maxColliders.",
				gameObject);
		}

		float bestScore =
			float.PositiveInfinity;

		Transform bestAnchor = null;

		IInteractable bestInteractable = null;

		for (int i = 0; i < count; i++)
		{
			DebugLog(
				$"---------- Candidate {i + 1}/{count} ----------");

			Collider col = _buffer[i];

			if (col == null)
			{
				DebugLog(
					"REJECTED: Collider is null.");

				continue;
			}

			DebugLog(
				$"Collider: {col.name}");

			DebugLog(
				$"GameObject: {col.gameObject.name}");

			DebugLog(
				$"Layer: {LayerMask.LayerToName(col.gameObject.layer)} ({col.gameObject.layer})");

			DebugLog(
				$"Is Trigger: {col.isTrigger}");

			DebugLog(
				$"Enabled: {col.enabled}");

			DebugLog(
				$"Bounds center: {col.bounds.center}");

			DebugLog(
				$"Bounds size: {col.bounds.size}");

			IInteractable interactable =
				col.GetComponentInParent<IInteractable>();

			if (interactable == null)
			{
				DebugLog(
					"REJECTED: No IInteractable found on collider or parents.");

				continue;
			}

			DebugLog(
				$"IInteractable found: {GetInteractableName(interactable)}");

			Transform anchor =
				interactable.IconAnchor != null
					? interactable.IconAnchor
					: col.transform;

			DebugLog(
				interactable.IconAnchor != null
					? $"Using IconAnchor: {anchor.name}"
					: $"IconAnchor is null. Using collider transform: {anchor.name}");

			DebugLog(
				$"Anchor position: {anchor.position}");

			Vector3 to =
				anchor.position - origin;

			float dist =
				to.magnitude;

			DebugLog(
				$"Vector to anchor: {to}");

			DebugLog(
				$"Distance to anchor: {dist:F4}");

			if (dist <= 0.0001f)
			{
				DebugLog(
					"REJECTED: Distance is <= 0.0001. " +
					"Camera may effectively be at the anchor position.");

				continue;
			}

			if (dist > maxInteractionDistance)
			{
				DebugLog(
					$"REJECTED: Distance {dist:F4} exceeds max interaction distance {maxInteractionDistance:F4}.");

				continue;
			}

			Vector3 dir =
				to / dist;

			DebugLog(
				$"Direction to anchor: {dir}");

			float dot =
				Vector3.Dot(forward.normalized, dir);

			float angle =
				Vector3.Angle(forward, dir);

			DebugLog(
				$"Dot product: {dot:F4}");

			DebugLog(
				$"Angle: {angle:F2} degrees");

			if (angle > maxAngle)
			{
				DebugLog(
					$"REJECTED: Angle {angle:F2} exceeds max angle {maxAngle:F2}.");

				if (debugDrawRays)
					Debug.DrawRay(
						origin,
						dir * dist,
						Color.yellow);

				continue;
			}

			DebugLog(
				"PASSED angle check.");

			if (debugDrawRays)
			{
				Debug.DrawRay(
					origin,
					dir * dist,
					Color.cyan);
			}

			bool hitSomething =
				Physics.Raycast(
					origin,
					dir,
					out RaycastHit hit,
					dist,
					losBlockMask,
					QueryTriggerInteraction.Collide);

			DebugLog(
				hitSomething
					? "LOS raycast HIT something."
					: "LOS raycast hit nothing.");

			if (hitSomething)
			{
				DebugLog(
					$"LOS hit collider: {hit.collider.name}");

				DebugLog(
					$"LOS hit GameObject: {hit.collider.gameObject.name}");

				DebugLog(
					$"LOS hit layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)} ({hit.collider.gameObject.layer})");

				DebugLog(
					$"LOS hit trigger: {hit.collider.isTrigger}");

				DebugLog(
					$"LOS hit distance: {hit.distance:F4}");

				DebugLog(
					$"LOS hit point: {hit.point}");

				DebugLog(
					$"LOS hit normal: {hit.normal}");

				IInteractable hitInteractable =
					hit.collider
						.GetComponentInParent<IInteractable>();

				if (hitInteractable == null)
				{
					DebugLog(
						"LOS hit object has NO IInteractable.");
				}
				else
				{
					DebugLog(
						$"LOS hit interactable: {GetInteractableName(hitInteractable)}");
				}

				if (hitInteractable != interactable)
				{
					DebugLog(
						$"REJECTED: Line of sight blocked by " +
						$"'{hit.collider.name}'.");

					if (debugDrawRays)
					{
						Debug.DrawLine(
							origin,
							hit.point,
							Color.red);
					}

					continue;
				}

				DebugLog(
					"LOS ray hit the SAME interactable. Allowing candidate.");

				if (debugDrawRays)
				{
					Debug.DrawLine(
						origin,
						hit.point,
						Color.green);
				}
			}

			float score =
				angle * 10f + dist;

			DebugLog(
				$"Candidate score: {score:F4}");

			DebugLog(
				$"Current best score: {bestScore:F4}");

			if (score < bestScore)
			{
				DebugLog(
					$"NEW BEST candidate: {GetInteractableName(interactable)}");

				bestScore = score;
				bestAnchor = anchor;
				bestInteractable = interactable;
			}
			else
			{
				DebugLog(
					"Candidate valid, but score is worse than current best.");
			}
		}

		DebugLog(
			"---------- SEARCH COMPLETE ----------");

		if (bestInteractable == null)
		{
			DebugLog(
				"RESULT: No valid interactable found.");

			iconUI.Hide();

			return;
		}

		DebugLog(
			$"RESULT: Selected interactable: {GetInteractableName(bestInteractable)}");

		DebugLog(
			$"Selected score: {bestScore:F4}");

		_current =
			bestInteractable;

		_canInteract =
			_current.CanInteract(
				gameObject,
				out _cannotInteractReason);

		DebugLog(
			$"CanInteract returned: {_canInteract}");

		if (!_canInteract)
		{
			DebugLog(
				string.IsNullOrEmpty(_cannotInteractReason)
					? "CanInteract returned FALSE with no reason."
					: $"Cannot interact reason: {_cannotInteractReason}");
		}

		if (_canInteract)
		{
			_cannotInteractReason =
				string.Empty;

			DebugLog(
				"Interactable is READY for interaction.");
		}

		if (bestAnchor == null)
		{
			DebugLog(
				"WARNING: bestAnchor is null.");
		}
		else
		{
			DebugLog(
				$"Showing interaction icon at anchor: {bestAnchor.name}");

			iconUI.Show(
				bestAnchor,
				cam);
		}
	}

	public bool CanInteractWith(
		IInteractable interactable)
	{
		bool result =
			_current == interactable &&
			_canInteract;

		if (debugLogs)
		{
			Debug.Log(
				$"[PlayerInteractor] CanInteractWith(" +
				$"{GetInteractableName(interactable)}) = {result} | " +
				$"Current: {GetInteractableName(_current)} | " +
				$"CanInteract: {_canInteract}",
				gameObject);
		}

		return result;
	}

	private void DebugLog(string message)
	{
		if (!debugLogs)
			return;

		Debug.Log(
			$"[PlayerInteractor] {message}",
			gameObject);
	}

	private string GetInteractableName(
		IInteractable interactable)
	{
		if (interactable == null)
			return "NULL";

		if (interactable is Component component)
		{
			return
				$"{component.gameObject.name} " +
				$"({component.GetType().Name})";
		}

		return interactable.GetType().Name;
	}
}