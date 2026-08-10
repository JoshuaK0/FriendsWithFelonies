using Evo.UI;
using UnityEngine;

public class PlayerBankCounter : MonoBehaviour
{
	[SerializeField] private PlayerBank playerBank;
	[SerializeField] private Counter counter;

	private void Start()
	{
		if (playerBank == null || counter == null)
			return;

		playerBank.OnMoneyChanged += HandleMoneyChanged;

		counter.SetValueInstant(playerBank.Money);
	}

	private void OnDestroy()
	{
		if (playerBank != null)
			playerBank.OnMoneyChanged -= HandleMoneyChanged;
	}

	private void HandleMoneyChanged(int currentMoney)
	{
		counter.SetValue(currentMoney);
	}
}