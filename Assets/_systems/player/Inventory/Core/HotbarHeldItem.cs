using UnityEngine;

/// <summary>
/// Base class for owner-local held-item prefabs. Held prefabs are ordinary
/// GameObjects. Networked actions are forwarded through the player-owned
/// ItemServiceLocator to the item-specific NetworkBehaviour counterpart.
/// </summary>
public abstract class HotbarHeldItem : MonoBehaviour, IUsableItem, IHotbarItemContextReceiver
{
    protected NetHotbarInventory Inventory { get; private set; }
    protected HotbarItemRuntimeStateStore RuntimeState { get; private set; }
    protected ItemServiceLocator ItemServices { get; private set; }
    protected int ItemId { get; private set; } = -1;
    protected bool IsEquipped { get; private set; }

    public void InitializeHotbarItem(NetHotbarInventory inventory, int itemId)
    {
        Inventory = inventory;
        ItemServices = inventory != null ? inventory.ItemServices : null;
        ItemId = itemId;

        if (inventory != null)
        {
            RuntimeState = inventory.GetComponent<HotbarItemRuntimeStateStore>();
            if (RuntimeState == null)
                RuntimeState = inventory.gameObject.AddComponent<HotbarItemRuntimeStateStore>();
        }

        OnContextInitialized();
    }

    public void OnEquip()
    {
        IsEquipped = true;
        OnEquipped();
    }

    public void OnUnequip()
    {
        if (!IsEquipped)
            return;

        IsEquipped = false;
        OnUnequipped();
    }

    protected virtual void Update()
    {
        if (IsEquipped)
            OnEquippedUpdate();
    }

    protected virtual void OnContextInitialized() { }
    protected virtual void OnEquipped() { }
    protected virtual void OnEquippedUpdate() { }
    protected virtual void OnUnequipped() { }
}
