using UnityEngine;

public interface IInteractable
{
	Transform IconAnchor { get; }

	bool CanInteract(GameObject interactor, out string reason);

	void Interact(GameObject interactor);
}