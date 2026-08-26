using UnityEngine;

public sealed class SmokeBombItem : HotbarHeldItem
{
    [SerializeField] private GameObject viewmodel;
    [SerializeField] private Transform muzzle;

    private SmokeBombItemNetworked networkedCounterpart;

	void Start()
	{
		muzzle = MyClient.Instance.PlayerManager
		.LocalPlayerController
		.GetComponent<CharControllerServiceLocator>()
		.muzzle;
	}

	protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null ? ItemServices.GetNetworkedSmokeBomb() : null;
    }

    protected override void OnEquipped()
    {
        if (viewmodel != null)
            viewmodel.SetActive(true);
    }

    protected override void OnEquippedUpdate()
    {
        if (!Input.GetMouseButtonDown(0) || muzzle == null || networkedCounterpart == null)
            return;

        networkedCounterpart.RequestThrowSmokeBomb(
            muzzle.position,
            muzzle.rotation,
            muzzle.forward);

        Inventory?.ConsumeOneConfirmed(ItemId);
    }

    protected override void OnUnequipped()
    {
        if (viewmodel != null)
            viewmodel.SetActive(false);
    }
}
