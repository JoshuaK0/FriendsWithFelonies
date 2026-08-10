using UnityEngine;
using UnityEngine.Events;

public class BaseInteractable : MonoBehaviour, IInteractable
{
	[Header("Icon")]
	[SerializeField] private Transform iconAnchor;

	[Header("Events")]
	[SerializeField] private UnityEvent onInteract;

	public Transform IconAnchor =>
		iconAnchor != null ? iconAnchor : transform;

	public virtual bool CanInteract(
		GameObject interactor,
		out string reason)
	{
		reason = string.Empty;
		return true;
	}

	public virtual void Interact(GameObject interactor)
	{
		onInteract?.Invoke();
	}
}