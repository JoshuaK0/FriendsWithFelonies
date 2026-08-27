public interface IUsableItem
{
    void OnEquip();
    void OnUnequip();
}

/// <summary>
/// Cleans up inventory-wide state when the owning inventory is released,
/// rather than merely switching to another item.
/// </summary>
public interface IInventoryReleaseHandler
{
	void OnInventoryReleased();
}
