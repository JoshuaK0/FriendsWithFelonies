using UnityEngine;
using UnityEngine.Events;

public class BuyItemButtonCreator : MonoBehaviour
{
	[Header("Items")]
	[SerializeField] private ItemRegistry itemRegistry;

	[Header("Player")]
	[SerializeField] private PlayerBank playerBank;
	 private NetHotbarPickup hotbarPickup;

	[Header("UI")]
	[SerializeField] private Transform buttonParent;
	[SerializeField] private BuyItemButton buttonPrefab;

	[Header("Events")]
	[SerializeField]
	private ItemPurchasedEvent onItemPurchased;

	[System.Serializable]
	public class ItemPurchasedEvent :
		UnityEvent<int, ItemDefinition>
	{
	}

	private void Start()
	{
		MyClient.Instance.PlayerManager.OnLocalPlayerSpawned += CreateButtons;
	}

	public void CreateButtons(GameObject player)
	{
		ClearButtons();

		if (itemRegistry == null)
		{
			Debug.LogError(
				"BuyItemButtonCreator: ItemRegistry is missing.",
				this);

			return;
		}

		if (playerBank == null)
		{
			Debug.LogError(
				"BuyItemButtonCreator: PlayerBank is missing.",
				this);

			return;
		}

		hotbarPickup = MyClient.Instance.PlayerManager.LocalPlayerController.GetComponent<PlayerCharacter>().GetServiceLocator().NetHotbarPickup;

		if (hotbarPickup == null)
		{
			Debug.LogError(
				"BuyItemButtonCreator: NetHotbarPickup is missing.",
				this);

			return;
		}

		if (buttonPrefab == null)
		{
			Debug.LogError(
				"BuyItemButtonCreator: Button prefab is missing.",
				this);

			return;
		}

		for (int i = 0; i < itemRegistry.Count; i++)
		{
			ItemDefinition item =
				itemRegistry.GetItem(i);

			if (item == null)
				continue;

			if (!item.IsPurchasable)
				continue;

			BuyItemButton button =
				Instantiate(
					buttonPrefab,
					buttonParent);

			button.Initialise(
				i,
				item,
				playerBank,
				hotbarPickup,
				HandleItemPurchased);
		}
	}

	private void HandleItemPurchased(
		int itemId,
		ItemDefinition item)
	{
		onItemPurchased?.Invoke(
			itemId,
			item);
	}

	public void ClearButtons()
	{
		if (buttonParent == null)
			return;

		for (int i = buttonParent.childCount - 1;
			 i >= 0;
			 i--)
		{
			Destroy(
				buttonParent.GetChild(i).gameObject);
		}
	}
}