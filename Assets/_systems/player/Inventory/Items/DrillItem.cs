using UnityEngine;

public sealed class DrillItem : HotbarHeldItem
{
    [SerializeField] private GameObject viewmodel;
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField, Min(0f)] private float rayDistance = 5f;
    [SerializeField] private float surfaceOffset = 0.01f;

    private DrillItemNetworked networkedCounterpart;

    protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedDrill() : null;
    }

    protected override void OnEquipped()
    {
        if (viewmodel != null)
            viewmodel.SetActive(true);
    }

    protected override void OnEquippedUpdate()
    {
        if (!Input.GetMouseButtonDown(0) || rayOrigin == null || networkedCounterpart == null)
            return;

        if (!Physics.Raycast(
                rayOrigin.position,
                rayOrigin.forward,
                out RaycastHit hit,
                rayDistance,
                hitLayers,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        networkedCounterpart.RequestPlaceDrill(
            hit.point + hit.normal * surfaceOffset,
            hit.normal);

        Inventory?.ConsumeOneConfirmed(ItemId);
    }

    protected override void OnUnequipped()
    {
        if (viewmodel != null)
            viewmodel.SetActive(false);
    }
}
