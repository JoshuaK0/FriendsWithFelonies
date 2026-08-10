using System;
using UnityEngine;

public class PlayerBank : MonoBehaviour
{
	[SerializeField, Min(0)]
	private int startingMoney;

	private int money;

	public int Money => money;

	public event Action<int> OnMoneyChanged;

	private void Awake()
	{
		money = startingMoney;
	}

	public bool CanAfford(int amount)
	{
		return amount >= 0 && money >= amount;
	}

	public bool TrySpend(int amount)
	{
		if (!CanAfford(amount))
			return false;

		money -= amount;

		OnMoneyChanged?.Invoke(money);

		return true;
	}

	public void AddMoney(int amount)
	{
		if (amount <= 0)
			return;

		money += amount;

		OnMoneyChanged?.Invoke(money);
	}

	public void SetMoney(int amount)
	{
		money = Mathf.Max(0, amount);

		OnMoneyChanged?.Invoke(money);
	}
}
