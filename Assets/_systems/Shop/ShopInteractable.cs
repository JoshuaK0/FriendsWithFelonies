using System.Collections;
using UnityEngine;
using Evo.UI;

public class ShopInteractable : MonoBehaviour, IInteractable
{
	[SerializeField, Min(0f)] private float interactionDuration;
	[SerializeField] private bool useDirectRaycast;
	public float InteractionDuration => interactionDuration;
	public bool UseDirectRaycast => useDirectRaycast;

	[SerializeField] bool onlyDuringSetUp;

	public bool CanInteract(GameObject interactor, out string reason)
	{
		if (
			GameFlowManager.Instance != null &&
			GameFlowManager.Instance.RoundPhase !=
				RoundFlowPhase.Setup && onlyDuringSetUp)
		{
			reason = "Items can only be bought during set up";
			return false;
		}
		reason = string.Empty;
		return true;
	}

	public void Interact(GameObject interactor)
	{
		ShopWindowToggle.Instance.OpenShop();
	}
}