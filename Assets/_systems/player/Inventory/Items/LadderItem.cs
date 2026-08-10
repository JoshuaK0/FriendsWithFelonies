using UnityEngine;

public sealed class LadderItem : HotbarHeldItem
{
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private PlacementPreview preview;
    [SerializeField, Min(0f)] private float range = 5f;
    [SerializeField] private float surfaceOffset = 0.05f;
    [SerializeField, Range(-1f, 1f)] private float maximumUpwardDot = 0.65f;
    [SerializeField] private LayerMask surfaceMask = ~0;

    private LadderItemNetworked networkedCounterpart;

    protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedLadder() : null;
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

        if (Vector3.Dot(hit.normal, Vector3.up) > maximumUpwardDot)
        {
            preview.SetVisible(false);
            return;
        }

        preview.SetVisible(true);
        Quaternion rotation = Quaternion.LookRotation(hit.normal, Vector3.up);
        Vector3 position = hit.point + hit.normal * surfaceOffset;
        preview.SetPose(position, rotation);

        if (preview.EvaluateClear() && Input.GetMouseButtonDown(0))
        {
            networkedCounterpart.RequestPlaceLadder(position, rotation);
            Inventory?.ConsumeOneConfirmed(ItemId);
        }
    }

    protected override void OnUnequipped()
    {
        preview?.SetVisible(false);
    }
}
