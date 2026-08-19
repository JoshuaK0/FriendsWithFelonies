using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Events;

public class VentDoorInteractable : NetworkBehaviour, IInteractable
{
	[Header("Interaction")]
	[SerializeField] private Transform iconAnchor;
	[SerializeField] private UnityEvent onInteract;

	[Header("Vent Objects")]
	[SerializeField] private GameObject closedObject;
	[SerializeField] private GameObject openObject;

	[Header("Initial State")]
	[SerializeField] private bool startsOpen = false;

	[Header("Animation")]
	[SerializeField, Min(0.01f)]
	private float scaleDuration = 0.2f;

	[SerializeField]
	private AnimationCurve scaleCurve =
		AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	private readonly SyncVar<bool> isOpen = new();

	private Coroutine transitionCoroutine;

	public Transform IconAnchor =>
		iconAnchor != null ? iconAnchor : transform;

	private void Awake()
	{
		isOpen.OnChange += OnOpenStateChanged;

		// Set initial visuals immediately.
		ApplyStateInstant(startsOpen);
	}

	private void OnDestroy()
	{
		isOpen.OnChange -= OnOpenStateChanged;
	}

	public override void OnStartServer()
	{
		base.OnStartServer();

		isOpen.Value = startsOpen;
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		// Late joiners should immediately see the correct state
		// instead of playing the transition.
		ApplyStateInstant(isOpen.Value);
	}

	public bool CanInteract(
		GameObject interactor,
		out string reason)
	{
		reason = string.Empty;
		return true;
	}

	public void Interact(GameObject interactor)
	{
		onInteract?.Invoke();

		if (!IsSpawned)
			return;

		if (IsServerStarted)
		{
			ToggleVent();
		}
		else
		{
			ToggleVentServerRpc();
		}
	}

	[ServerRpc(RequireOwnership = false)]
	private void ToggleVentServerRpc()
	{
		ToggleVent();
	}

	[Server]
	private void ToggleVent()
	{
		isOpen.Value = !isOpen.Value;
	}

	private void OnOpenStateChanged(
		bool previous,
		bool next,
		bool asServer)
	{
		// On a host, SyncVar callbacks can occur once for the
		// server side and once for the client side.
		// Only animate the client-side callback.
		if (asServer && IsClientStarted)
			return;

		PlayTransition(next);
	}

	private void PlayTransition(bool opening)
	{
		if (transitionCoroutine != null)
			StopCoroutine(transitionCoroutine);

		transitionCoroutine = StartCoroutine(
			TransitionRoutine(opening));
	}

	private IEnumerator TransitionRoutine(bool opening)
	{
		GameObject outgoingObject =
			opening ? closedObject : openObject;

		GameObject incomingObject =
			opening ? openObject : closedObject;

		// --------------------------------------------------
		// 1. SCALE OUT CURRENT OBJECT
		// --------------------------------------------------

		if (outgoingObject != null)
		{
			outgoingObject.SetActive(true);

			yield return ScaleObject(
				outgoingObject.transform,
				outgoingObject.transform.localScale,
				Vector3.zero);

			outgoingObject.transform.localScale = Vector3.zero;
			outgoingObject.SetActive(false);
		}

		// --------------------------------------------------
		// 2. SCALE IN NEW OBJECT
		// --------------------------------------------------

		if (incomingObject != null)
		{
			incomingObject.transform.localScale = Vector3.zero;
			incomingObject.SetActive(true);

			yield return ScaleObject(
				incomingObject.transform,
				Vector3.zero,
				Vector3.one);

			incomingObject.transform.localScale = Vector3.one;
		}

		transitionCoroutine = null;
	}

	private IEnumerator ScaleObject(
		Transform target,
		Vector3 from,
		Vector3 to)
	{
		float elapsed = 0f;

		while (elapsed < scaleDuration)
		{
			elapsed += Time.deltaTime;

			float t = Mathf.Clamp01(
				elapsed / scaleDuration);

			float curvedT = scaleCurve.Evaluate(t);

			target.localScale = Vector3.LerpUnclamped(
				from,
				to,
				curvedT);

			yield return null;
		}

		target.localScale = to;
	}

	private void ApplyStateInstant(bool open)
	{
		if (transitionCoroutine != null)
		{
			StopCoroutine(transitionCoroutine);
			transitionCoroutine = null;
		}

		if (closedObject != null)
		{
			closedObject.transform.localScale =
				open ? Vector3.zero : Vector3.one;

			closedObject.SetActive(!open);
		}

		if (openObject != null)
		{
			openObject.transform.localScale =
				open ? Vector3.one : Vector3.zero;

			openObject.SetActive(open);
		}
	}
}