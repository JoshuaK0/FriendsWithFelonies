using FishNet.Object;
using UnityEngine;
using UnityEngine.Events;

public class NetWorldItem :
    NetworkBehaviour,
    IInteractable
{
    [Header("Item Data")]
    [SerializeField] private ItemDefinition itemDefinition;

    [Header("Interaction")]
    [SerializeField, Min(0f)] private float interactionDuration;
    [SerializeField] private bool useDirectRaycast;
    [SerializeField] private UnityEvent onPickupRequested;

    [Header("Physics")]
    [SerializeField] private Rigidbody itemRigidbody;

    public ItemDefinition ItemDefinition => itemDefinition;

    public string LookupName =>
        itemDefinition != null
            ? itemDefinition.LookupName
            : string.Empty;

    public virtual float InteractionDuration => interactionDuration;
    public virtual bool UseDirectRaycast => useDirectRaycast;

    private void Reset()
    {
        itemRigidbody = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Resolves this world item's runtime integer ID through the supplied registry.
    /// The world prefab itself only needs a reference to its ItemDefinition.
    /// </summary>
    public int GetItemId(ItemRegistry registry)
    {
        if (registry == null || itemDefinition == null)
            return -1;

        return registry.GetItemId(itemDefinition);
    }

    public virtual void Interact(GameObject interactor)
    {
        if (interactor == null)
            return;

        NetHotbarPickup pickup =
            MyClient.Instance.PlayerManager.LocalPlayerController
                .GetComponent<PlayerCharacter>()
                .GetServiceLocator()
                .NetHotbarPickup;

        if (pickup == null)
            return;

        if (!pickup.RequestPickup(this))
            return;

        onPickupRequested?.Invoke();
        OnPickupRequested(interactor);
    }

    /// <summary>
    /// Called after RequestPickup successfully accepts the pickup request.
    /// Override this for item-specific pickup behaviour.
    /// </summary>
    protected virtual void OnPickupRequested(GameObject interactor)
    {
    }

    public void EnablePhysics(bool enabled)
    {
        if (itemRigidbody == null)
            return;

        itemRigidbody.isKinematic = !enabled;
        itemRigidbody.detectCollisions = enabled;
    }

    public virtual bool CanInteract(GameObject interactor, out string reason)
    {
        if (itemDefinition == null)
        {
            reason = "Item definition is not assigned.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
