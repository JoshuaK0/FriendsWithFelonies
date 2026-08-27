using UnityEngine;

public interface IInteractable
{
	/// <summary>
	/// Seconds the interact key must be held. Zero completes on key press.
	/// </summary>
	float InteractionDuration { get; }

	/// <summary>
	/// True uses a ray from the mouse position. False uses the cone search.
	/// </summary>
	bool UseDirectRaycast { get; }

	bool CanInteract(GameObject interactor, out string reason);

	void Interact(GameObject interactor);
}
