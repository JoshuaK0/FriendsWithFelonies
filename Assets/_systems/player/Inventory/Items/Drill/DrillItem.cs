using UnityEngine;

public sealed class DrillItem : HotbarHeldItem
{
	[Header("Viewmodel")]
	[SerializeField] private GameObject viewmodel;

	[Header("Placement")]
	[SerializeField] private Transform rayOrigin;
	[SerializeField] private PlacementPreview preview;

	[SerializeField, Min(0f)]
	private float maxPlaceDistance = 5f;

	[SerializeField, Min(0f)]
	private float wallOffset = 0.05f;

	[SerializeField]
	private LayerMask hitLayers = ~0;

	private DrillItemNetworked networkedCounterpart;

	protected override void OnContextInitialized()
	{
		if (CharacterServices != null)
			rayOrigin = CharacterServices.muzzle;

		networkedCounterpart =
			ItemServices != null
				? ItemServices.GetNetworkedDrill()
				: null;
	}

	protected override void OnEquipped()
	{
		if (viewmodel != null)
			viewmodel.SetActive(true);

		preview?.SetVisible(true);
	}

	protected override void OnEquippedUpdate()
	{
		if (rayOrigin == null ||
			preview == null ||
			networkedCounterpart == null)
		{
			return;
		}

		if (!Physics.Raycast(
				rayOrigin.position,
				rayOrigin.forward,
				out RaycastHit hit,
				maxPlaceDistance,
				hitLayers,
				QueryTriggerInteraction.Ignore))
		{
			preview.SetVisible(false);
			return;
		}

		Vector3 position =
			hit.point +
			hit.normal * wallOffset;

		Quaternion rotation =
			Quaternion.LookRotation(
				hit.normal,
				Vector3.up);

		preview.SetVisible(true);
		preview.SetPose(position, rotation);

		if (!preview.EvaluateClear())
			return;

		if (!Input.GetMouseButtonDown(0))
			return;

		networkedCounterpart.RequestPlaceDrill(
			position,
			hit.normal);

		Inventory?.ConsumeOneConfirmed(ItemId);
	}

	protected override void OnUnequipped()
	{
		if (viewmodel != null)
			viewmodel.SetActive(false);

		preview?.SetVisible(false);
	}
}
