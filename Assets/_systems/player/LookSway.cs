using UnityEngine;

public class LookSway : MonoBehaviour
{
	public float amount = 0.6f;
	public float maxOffset = 0.08f;
	public float smooth = 12f;
	public bool invertX;
	public bool invertY;

	Vector3 startLocalPos;
	Vector3 velocity;

	void Awake()
	{
		startLocalPos = transform.localPosition;
	}

	void Update()
	{
		float mx = Input.GetAxisRaw("Mouse X");
		float my = Input.GetAxisRaw("Mouse Y");

		if (invertX) mx = -mx;
		if (invertY) my = -my;

		Vector3 targetOffset = new Vector3(-mx, -my, 0f) * amount;

		if (targetOffset.x > maxOffset) targetOffset.x = maxOffset;
		if (targetOffset.x < -maxOffset) targetOffset.x = -maxOffset;
		if (targetOffset.y > maxOffset) targetOffset.y = maxOffset;
		if (targetOffset.y < -maxOffset) targetOffset.y = -maxOffset;

		Vector3 targetPos = startLocalPos + targetOffset;

		transform.localPosition = Vector3.SmoothDamp(
			transform.localPosition,
			targetPos,
			ref velocity,
			1f / smooth
		);
	}
}