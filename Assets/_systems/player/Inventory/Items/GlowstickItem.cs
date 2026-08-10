using UnityEngine;

public sealed class GlowstickItem : HotbarHeldItem
{
    [SerializeField] private GameObject viewmodel;
    [SerializeField] private Transform muzzle;

    private GlowstickItemNetworked networkedCounterpart;

    protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedGlowstick() : null;
    }

    protected override void OnEquipped()
    {
        if (viewmodel != null)
            viewmodel.SetActive(true);
    }

    protected override void OnEquippedUpdate()
    {
        if (!Input.GetMouseButtonDown(0) || muzzle == null || networkedCounterpart == null)
            return;

        networkedCounterpart.RequestThrowGlowstick(
            muzzle.position,
            muzzle.rotation,
            muzzle.forward);

        Inventory?.ConsumeOneConfirmed(ItemId);
    }

    protected override void OnUnequipped()
    {
        if (viewmodel != null)
            viewmodel.SetActive(false);
    }
}
