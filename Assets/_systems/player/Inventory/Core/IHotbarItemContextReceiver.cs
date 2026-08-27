/// <summary>
/// Receives owner and item context from NetHotbarInventory during its eager
/// startup initialization pass, before the held-item container is activated.
/// </summary>
public interface IHotbarItemContextReceiver
{
    void InitializeHotbarItem(NetHotbarInventory inventory, int itemId);
}
