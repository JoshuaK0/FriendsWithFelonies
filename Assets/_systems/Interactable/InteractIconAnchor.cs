using UnityEngine;

/// <summary>
/// Optional component used to override an interactable's icon position.
/// Add it to the same GameObject as the component implementing IInteractable.
/// </summary>
public sealed class InteractIconAnchor : MonoBehaviour
{
	[SerializeField] private Transform anchor;

	public Transform Anchor =>
		anchor != null
			? anchor
			: transform;
}
