using UnityEngine;

public sealed class UVFlashlightItem : HotbarHeldItem
{
    [SerializeField] private GameObject localUVFlashlightObject;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip toggleClip;
    [SerializeField] private bool startsEnabled;

    private UVFlashlightItemNetworked networkedCounterpart;
    private bool enabledState;

    protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedUVFlashlight() : null;
        enabledState = startsEnabled;
    }

    protected override void OnEquipped()
    {
        ApplyLocalState();
        networkedCounterpart?.RequestSetUVFlashlight(enabledState);
    }

    protected override void OnEquippedUpdate()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        enabledState = !enabledState;
        ApplyLocalState();

        if (audioSource != null && toggleClip != null)
            audioSource.PlayOneShot(toggleClip);

        networkedCounterpart?.RequestSetUVFlashlight(enabledState);
    }

    protected override void OnUnequipped()
    {
        ApplyLocalState();
        networkedCounterpart?.RequestSetUVFlashlight(enabledState);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // Switching slots preserves the UV light. Only destroy-time cleanup
        // clears the networked counterpart.
        networkedCounterpart?.RequestSetUVFlashlight(false);
    }

    private void ApplyLocalState()
    {
        if (localUVFlashlightObject != null)
            localUVFlashlightObject.SetActive(enabledState);
    }
}
