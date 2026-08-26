using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;

public class DeactivateInteractable : NetworkBehaviour, IInteractable
{
	[Header("Interaction")]
	[SerializeField] private Transform iconAnchor;

	[SerializeField] List<GameObject> despawnObjects;
	public Transform IconAnchor =>
		iconAnchor != null ? iconAnchor : transform;
	public bool CanInteract(GameObject interactor, out string reason)
    {
		reason = string.Empty;
		return true;
    }

    public void Interact(GameObject interactor)
    {
        foreach(GameObject obj in despawnObjects)
		{
			Despawn(obj);
		}
    }
}
