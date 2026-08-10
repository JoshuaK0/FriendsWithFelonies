using FishNet.Object;
using UnityEngine;

public sealed class LootNetWorldItem : NetWorldItem
{
	public override bool CanInteract(
		GameObject interactor,
		out string reason)
	{
		reason = string.Empty;

		if (interactor == null)
			return false;

		if (PlayerTeams.Instance == null)
			return false;

		NetworkObject networkObject =
			interactor.GetComponentInParent<NetworkObject>();

		if (networkObject == null)
			return false;

		TeamType teamType =
			PlayerTeams.Instance.GetPlayerTeamType(
				networkObject.OwnerId);

		if (teamType != TeamType.Robber)
		{
			reason = "Only robbers can pick up loot.";
			return false;
		}

		return true;
	}

	protected override void OnPickupRequested(
		GameObject interactor)
	{
		base.OnPickupRequested(interactor);

		if (LockdownManager.Instance == null)
			return;

		LockdownManager.Instance.RequestLockdownServerRpc();
	}
}