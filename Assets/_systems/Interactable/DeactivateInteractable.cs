using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;

public class DeactivateInteractable :
	NetworkBehaviour,
	IInteractable
{
	[Header("Interaction")]
	[SerializeField, Min(0f)] private float interactionDuration;
	[SerializeField] private bool useDirectRaycast;

	[SerializeField] List<GameObject> despawnObjects;
	[SerializeField] List<GameObject> disableObjects;

	[SerializeField] bool reenableOnNewRound;

	[SerializeField] TeamType teamType;

	public float InteractionDuration => interactionDuration;
	public bool UseDirectRaycast => useDirectRaycast;

	public bool CanInteract(GameObject interactor, out string reason)
    {
		if(teamType != TeamType.Any)
		{
			if(MyClient.Instance.CurrentTeamType != teamType)
			{
				reason = "Team type is incorrect. Requires:" + teamType + ". But current type is:" + MyClient.Instance.CurrentTeamType.ToString();

				return false;
			}
		}
		reason = string.Empty;
		return true;
    }

	public override void OnStartServer()
	{
		base.OnStartServer();
		if(reenableOnNewRound)
		{
			GameFlowManager.Instance.OnRoundSetupStarted += EnableServer;
		}
	}

	public void Interact(GameObject interactor)
    {
		DespawnServe();
		DisableServer();
	}
	[ServerRpc(RequireOwnership = false)]
	void DespawnServe()
	{
		foreach (GameObject obj in despawnObjects)
		{
			Despawn(obj);
		}
	}

	[ServerRpc (RequireOwnership = false)]
	void DisableServer()
	{
		DisableClient();
	}

	[ObserversRpc]
	void DisableClient()
	{
		foreach (GameObject obj in disableObjects)
		{
			obj.SetActive(false);
		}
	}

	[ServerRpc(RequireOwnership = false)]
	void EnableServer(int round)
	{
		EnableClient();
	}

	[ObserversRpc]
	void EnableClient()
	{
		foreach (GameObject obj in disableObjects)
		{
			obj.SetActive(true);
		}
	}
}
