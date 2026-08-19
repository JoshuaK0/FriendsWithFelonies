using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Ladder : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private float climbSpeed = 3f;
	[SerializeField] private float sideMoveSpeed = 2.5f;

	[Header("Top Platform")]
	[SerializeField] private Collider topPlatform;

	private BoxCollider ladderVolume;

	public float ClimbSpeed => climbSpeed;
	public float SideMoveSpeed => sideMoveSpeed;

	public Vector3 Up => transform.up;
	public Vector3 Right => transform.right;

	public Collider TopPlatform => topPlatform;

	public Bounds LocalBounds =>
		new Bounds(
			ladderVolume.center,
			ladderVolume.size);

	private void Awake()
	{
		ladderVolume =
			GetComponent<BoxCollider>();
	}

	private void Reset()
	{
		BoxCollider box =
			GetComponent<BoxCollider>();

		box.isTrigger = true;
	}

	private void OnValidate()
	{
		BoxCollider box =
			GetComponent<BoxCollider>();

		if (box != null)
		{
			box.isTrigger = true;
		}
	}

	public bool IsOutsideClimbArea(
		Vector3 worldPosition,
		out Vector3 exitDirection)
	{
		Vector3 localPosition =
			transform.InverseTransformPoint(
				worldPosition);

		Bounds bounds =
			LocalBounds;

		exitDirection =
			Vector3.zero;

		if (localPosition.x < bounds.min.x)
		{
			exitDirection -= Right;
		}
		else if (localPosition.x > bounds.max.x)
		{
			exitDirection += Right;
		}

		if (localPosition.y < bounds.min.y)
		{
			exitDirection -= Up;
		}
		else if (localPosition.y > bounds.max.y)
		{
			exitDirection += Up;
		}

		return
			exitDirection.sqrMagnitude >
			0.001f;
	}
}