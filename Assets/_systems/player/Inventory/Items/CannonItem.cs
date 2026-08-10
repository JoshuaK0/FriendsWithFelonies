using UnityEngine;

public sealed class CannonItem : HotbarHeldItem
{
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private PlacementPreview preview;
    [SerializeField, Min(0f)] private float range = 5f;
    [SerializeField] private float surfaceOffset = 0.05f;
    [SerializeField, Range(-1f, 1f)] private float minimumUpwardDot = 0.9f;
    [SerializeField] private LayerMask surfaceMask = ~0;

    private CannonItemNetworked networkedCounterpart;

    protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedCannon() : null;
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

        bool validFloor = Vector3.Dot(hit.normal, Vector3.up) >= minimumUpwardDot;
        Vector3 forward = Vector3.ProjectOnPlane(rayOrigin.forward, hit.normal);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(rayOrigin.up, hit.normal);

        Quaternion rotation = Quaternion.LookRotation(forward.normalized, hit.normal);
        Vector3 position = hit.point + hit.normal * surfaceOffset;

        preview.SetVisible(true);
        preview.SetPose(position, rotation);
        bool clear = preview.EvaluateClear(validFloor);

        if (clear && Input.GetMouseButtonDown(0))
        {
            networkedCounterpart.RequestPlaceCannon(position, rotation);
            Inventory?.ConsumeOneConfirmed(ItemId);
        }
    }

    protected override void OnUnequipped()
    {
        preview?.SetVisible(false);
    }
}
