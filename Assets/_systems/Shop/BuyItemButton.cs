using System;
using TMPro;
using UnityEngine;
using Evo.UI;

public class BuyItemButton : MonoBehaviour
{
	[SerializeField] private TMP_Text costText;
	[SerializeField] private Button button;

	private ItemDefinition item;
	private int itemId;

	private PlayerBank playerBank;
	private NetHotbarPickup hotbarPickup;

	private Action<int, ItemDefinition> purchaseCallback;

	public void Initialise(
		int itemId,
		ItemDefinition item,
		PlayerBank playerBank,
		NetHotbarPickup hotbarPickup,
		Action<int, ItemDefinition> purchaseCallback = null)
	{
		this.itemId = itemId;
		this.item = item;
		this.playerBank = playerBank;
		this.hotbarPickup = hotbarPickup;
		this.purchaseCallback = purchaseCallback;

		button.SetText(item.DisplayName);

		if (costText != null)
			costText.text = $"${item.Cost}";

		button.onClick.RemoveListener(Buy);
		button.onClick.AddListener(Buy);

		playerBank.OnMoneyChanged -= HandleMoneyChanged;
		playerBank.OnMoneyChanged += HandleMoneyChanged;

		RefreshButton();
	}

	private void OnDestroy()
	{
		if (button != null)
			button.onClick.RemoveListener(Buy);

		if (playerBank != null)
			playerBank.OnMoneyChanged -= HandleMoneyChanged;
	}

	private void HandleMoneyChanged(int currentMoney)
	{
		RefreshButton();
	}

	private void RefreshButton()
	{
		if (item == null ||
			playerBank == null ||
			button == null)
			return;

		button.SetInteractable(
			playerBank.CanAfford(item.Cost));
	}

	private void Buy()
	{
		if (item == null ||
			playerBank == null ||
			hotbarPickup == null)
			return;

		if (!playerBank.TrySpend(item.Cost))
			return;

		hotbarPickup.RequestGiveItem(
			itemId,
			item.PurchaseAmount);

		purchaseCallback?.Invoke(
			itemId,
			item);
	}
}