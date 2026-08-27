using UnityEngine;

public class TripwireLaser :
    MonoBehaviour,
    IHotbarItemContextReceiver
{
    [SerializeField] bool updateContinuously = false;
    [SerializeField] float maxRange;
    [SerializeField] Transform wireObject;
    [SerializeField] LayerMask layerMask;
    private bool isInitialized;

    public void InitializeHotbarItem(
        NetHotbarInventory inventory,
        int itemId)
    {
		EnsureInitialized();
	}

	private void Start()
	{
		// TripwireLaser is also used by the spawned world prop, which is not
		// initialized by a hotbar inventory.
		EnsureInitialized();
	}

	private void EnsureInitialized()
	{
		if (isInitialized)
			return;

		isInitialized = true;
		UpdatePosition();
	}

	// Update is called once per frame
	void Update()
    {
        if(updateContinuously)
		{
			UpdatePosition();
		}
    }

    void UpdatePosition()
    {
		float range = maxRange;
		RaycastHit hit;
		if (Physics.Raycast(transform.position, transform.forward, out hit, maxRange, layerMask, QueryTriggerInteraction.Ignore))
		{
			range = hit.distance;
		}
		wireObject.localPosition = new Vector3(0, 0, range / 2);
		wireObject.localScale = new Vector3(1, 1, range);
	}
}
