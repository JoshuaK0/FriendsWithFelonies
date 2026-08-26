using UnityEngine;

public sealed class LootItem : HotbarHeldItem
{
	[Header("Throwing")]
	[SerializeField] private GameObject viewmodel;
	private Transform muzzle;
	[SerializeField] private Vector3 throwOffset;

	[SerializeField, Min(0f)]
	private float fullThrowStrength = 12f;

	[SerializeField, Min(0f)]
	private float softThrowStrength = 5f;

	private LootItemNetworked networkedCounterpart;

	void Start()
	{
		muzzle = MyClient.Instance.PlayerManager
		.LocalPlayerController
		.GetComponent<CharControllerServiceLocator>()
		.muzzle;
	}
	protected override void OnContextInitialized()
	{
		networkedCounterpart = ItemServices != null
			? ItemServices.GetNetworkedLoot()
			: null;
	}

	protected override void OnEquipped()
	{
		if (viewmodel != null)
			viewmodel.SetActive(true);
	}

	protected override void OnEquippedUpdate()
	{
		if (Input.GetMouseButtonDown(0))
			Throw(fullThrowStrength);

		if (Input.GetMouseButtonDown(1))
			Throw(softThrowStrength);
	}

	protected override void OnUnequipped()
	{
		if (viewmodel != null)
			viewmodel.SetActive(false);
	}

	private void Throw(float strength)
	{
		if (muzzle == null ||
			networkedCounterpart == null ||
			Inventory == null)
		{
			return;
		}

		HotbarSlot slot = Inventory.GetSelectedSlot();

		if (slot == null ||
			slot.itemId != ItemId ||
			slot.count <= 0)
		{
			return;
		}

		Vector3 spawnPosition =
			muzzle.position +
			transform.TransformVector(throwOffset);

		Vector3 velocity =
			muzzle.forward * strength;

		networkedCounterpart.RequestThrowLoot(
			spawnPosition,
			muzzle.rotation,
			velocity);

		Inventory.ConsumeOneConfirmed(ItemId);
	}
}