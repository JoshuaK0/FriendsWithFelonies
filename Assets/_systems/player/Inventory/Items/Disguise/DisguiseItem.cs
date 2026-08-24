using UnityEngine;

public sealed class DisguiseItem : HotbarHeldItem
{
	private DisguiseItemNetworked networkedCounterpart;

	protected override void OnContextInitialized()
	{
		networkedCounterpart =
			ItemServices != null
				? ItemServices.GetNetworkedDisguise()
				: null;
	}

	protected override void OnEquippedUpdate()
	{
		// Right click = choose/change disguise.
		if (Input.GetMouseButtonDown(0))
		{
			networkedCounterpart?.RequestRandomDisguise();
			return;
		}

		// Left click = revert to normal character.
		if (Input.GetMouseButtonDown(1))
		{
			networkedCounterpart?.RequestRevertDisguise();
		}
	}
}