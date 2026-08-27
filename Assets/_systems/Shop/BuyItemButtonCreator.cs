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

	[Header("References")]
	[SerializeField]
	ShopWindowToggle shopWindowToggle;

	private bool isShopOpen;

	[System.Serializable]
	public class ItemPurchasedEvent :
		UnityEvent<int, ItemDefinition>
	{
	}

	private void Awake()
	{
		if (shopWindowToggle != null)
		{
			shopWindowToggle.OnShopOpened += HandleShopOpened;
			shopWindowToggle.OnShopClosed += HandleShopClosed;
		}

		PlayerTeams.OnTeamDataChanged += HandleTeamDataChanged;
	}

	private void OnDestroy()
	{
		if (shopWindowToggle != null)
		{
			shopWindowToggle.OnShopOpened -= HandleShopOpened;
			shopWindowToggle.OnShopClosed -= HandleShopClosed;
		}

		PlayerTeams.OnTeamDataChanged -= HandleTeamDataChanged;
	}

	private void HandleShopOpened()
	{
		isShopOpen = true;
		CreateButtons();
	}

	private void HandleShopClosed()
	{
		isShopOpen = false;
		ClearButtons();
	}

	private void HandleTeamDataChanged()
	{
		if (isShopOpen)
			CreateButtons();
	}

	public void CreateButtons()
	{
		ClearButtons();

		if (!TryGetLocalTeamType(out TeamType teamType))
			return;

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

			if (!item.IsAvailableInShopFor(teamType))
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

	private static bool TryGetLocalTeamType(out TeamType teamType)
	{
		teamType = TeamType.Spectator;

		if (MyClient.Instance == null ||
			PlayerTeams.Instance == null)
		{
			return false;
		}

		teamType =
			PlayerTeams.Instance.GetPlayerTeamType(
				MyClient.Instance.Owner.ClientId);

		return teamType == TeamType.Cop ||
			teamType == TeamType.Robber;
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
