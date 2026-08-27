using UnityEngine;

/// <summary>
/// Owner-local tripwire placement viewmodel. Network spawning and inventory
/// consumption are forwarded through the player-owned TripWireItemNetworked.
/// </summary>
public sealed class TripWireItem : HotbarHeldItem
{
    private Transform rayOrigin;
    [SerializeField] private PlacementPreview preview;
    [SerializeField, Min(0f)] private float range = 5f;
    [SerializeField] private float surfaceOffset = 0.02f;
    [SerializeField, Range(-1f, 1f)] private float maximumUpwardDot = 0.65f;
    [SerializeField] private LayerMask surfaceMask = ~0;

    private TripWireItemNetworked networkedCounterpart;

	protected override void OnContextInitialized()
    {
		if (CharacterServices != null)
			rayOrigin = CharacterServices.muzzle;

        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedTripWire() : null;
    }

    protected override void OnEquipped()
    {
        preview?.SetVisible(true);
    }

    protected override void OnEquippedUpdate()
    {
        if (rayOrigin == null || preview == null || networkedCounterpart == null)
            return;

        if (!Physics.Raycast(
                rayOrigin.position,
                rayOrigin.forward,
                out RaycastHit hit,
                range,
                surfaceMask,
                QueryTriggerInteraction.Ignore))
        {
            preview.SetVisible(false);
            return;
        }

        bool validSurface = Vector3.Dot(hit.normal, Vector3.up) <= maximumUpwardDot;
        Quaternion rotation = Quaternion.LookRotation(hit.normal, Vector3.up);
        Vector3 position = hit.point + hit.normal * surfaceOffset;

        preview.SetVisible(true);
        preview.SetPose(position, rotation);

        if (preview.EvaluateClear(validSurface) && Input.GetMouseButtonDown(0))
        {
            networkedCounterpart.RequestPlaceTripWire(position, rotation);
            Inventory?.ConsumeOneConfirmed(ItemId);
        }
    }

    protected override void OnUnequipped()
    {
        preview?.SetVisible(false);
    }
}
