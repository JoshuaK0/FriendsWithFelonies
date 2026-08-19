using UnityEngine;

public class TripwireLaser : MonoBehaviour
{
    [SerializeField] bool updateContinuously = false;
    [SerializeField] float maxRange;
    [SerializeField] Transform wireObject;
    [SerializeField] LayerMask layerMask;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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
