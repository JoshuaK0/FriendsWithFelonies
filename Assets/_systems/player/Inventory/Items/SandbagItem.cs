using UnityEngine;

public sealed class SandbagItem : HotbarHeldItem
{
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private PlacementPreview preview;
    [SerializeField, Min(0f)] private float range = 5f;
    [SerializeField] private float surfaceOffset = 0.02f;
    [SerializeField, Range(-1f, 1f)] private float minimumUpDot = 0.8f;
    [SerializeField] private LayerMask surfaceMask = ~0;

    private SandbagItemNetworked networkedCounterpart;

    protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedSandbag() : null;
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

        preview.SetVisible(true);
        bool validSurface = Vector3.Dot(hit.normal, Vector3.up) >= minimumUpDot;
        Vector3 forward = Vector3.ProjectOnPlane(rayOrigin.forward, hit.normal).normalized;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(rayOrigin.up, hit.normal).normalized;

        Quaternion rotation = Quaternion.LookRotation(forward, hit.normal);
        Vector3 position = hit.point + hit.normal * surfaceOffset;
        preview.SetPose(position, rotation);

        if (preview.EvaluateClear(validSurface) && Input.GetMouseButtonDown(0))
        {
            networkedCounterpart.RequestPlaceSandbag(position, rotation);
            Inventory?.ConsumeOneConfirmed(ItemId);
        }
    }

    protected override void OnUnequipped()
    {
        preview?.SetVisible(false);
    }
}
